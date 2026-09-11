"""안전장치: 디스크·동시성.

여기 있는 판정은 **한 곳에만** 산다. 같은 판단이 여러 곳에 복사되면 한쪽만 고쳐져 재발한다.
"""

from __future__ import annotations

import errno
import fcntl
import os
import shutil
import time
from contextlib import contextmanager
from pathlib import Path

from .config import STATE_DIR

# Unity worktree 하나는 Library를 통째로 새로 만든다(대상에 따라 0.7~2.4GB).
# 이 기계는 여유가 넉넉하지 않다 — 시작 전에 막는 편이 중간에 터지는 것보다 낫다.
MIN_FREE_GB = 8.0
DEFAULT_UNITY_SLOTS = 2


class SafetyError(RuntimeError):
    pass


# --- STOP ----------------------------------------------------------------
# STOP은 "프로세스를 죽였다"가 아니다. 죽여도 루프가 다음 시도를 시작하면 멈춘 게 아니다
# (2026-09-11 실측: 그룹은 죽었는데 시도 2가 곧바로 떴다). 그래서 **파일 플래그를 먼저 세우고**
# 루프가 매 단계에서 그것을 확인한다. 플래그가 상태고, 프로세스 종료는 그 뒤의 정리다.
STOP_FILE = STATE_DIR / "STOP"


def request_stop(reason: str = "") -> Path:
    STATE_DIR.mkdir(parents=True, exist_ok=True)
    STOP_FILE.write_text(f"{time.time()}\n{reason}\n", encoding="utf-8")
    return STOP_FILE


def stop_requested() -> bool:
    return STOP_FILE.exists()


def clear_stop() -> bool:
    if STOP_FILE.exists():
        STOP_FILE.unlink()
        return True
    return False


def free_gb(path: Path) -> float:
    usage = shutil.disk_usage(str(path))
    return usage.free / (1024 ** 3)


def ensure_disk(path: Path, min_gb: float = MIN_FREE_GB) -> float:
    free = free_gb(path)
    if free < min_gb:
        raise SafetyError(
            f"디스크 여유 부족: {free:.1f}GB < {min_gb:.1f}GB — worktree는 Unity Library를 새로 만든다"
        )
    return free


@contextmanager
def unity_slot(slots: int = DEFAULT_UNITY_SLOTS, timeout: int = 3600, poll: float = 2.0):
    """Unity 배치 동시 실행 수를 제한한다.

    16GB 기계에서 Unity를 무제한으로 띄우면 스왑으로 전부 느려지거나 죽는다.
    파일 락 기반이라 **프로세스가 죽어도 슬롯이 자동 반납**된다(플래그 방식의 함정 회피).
    """
    slot_dir = STATE_DIR / "slots"
    slot_dir.mkdir(parents=True, exist_ok=True)
    deadline = time.time() + timeout
    fh = None
    held = None
    while True:
        for i in range(slots):
            p = slot_dir / f"unity-{i}.lock"
            f = open(p, "w")
            try:
                fcntl.flock(f.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
            except OSError as e:
                f.close()
                if e.errno not in (errno.EAGAIN, errno.EACCES):
                    raise
                continue
            f.write(f"{os.getpid()}\n")
            f.flush()
            fh, held = f, i
            break
        if fh is not None:
            break
        if time.time() > deadline:
            raise SafetyError(f"Unity 슬롯 {slots}개가 {timeout}s 동안 비지 않았다")
        time.sleep(poll)
    try:
        yield held
    finally:
        try:
            fcntl.flock(fh.fileno(), fcntl.LOCK_UN)
        finally:
            fh.close()
