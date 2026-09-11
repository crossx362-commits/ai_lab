"""외부 프로세스 관리.

핵심: 자식이 아니라 **프로세스 그룹**을 죽인다. Unity도 codex도 손자 프로세스를 만든다 —
부모만 죽이면 좀비가 남아 다음 판을 오염시킨다(기존 시스템의 최대 실패 지점, §13).
"""

from __future__ import annotations

import os
import signal
import threading
import subprocess
import time
from dataclasses import dataclass
from pathlib import Path

from . import db, logs


@dataclass
class RunResult:
    cmd: list[str]
    exit_code: int | None
    status: str  # EXITED / TIMEOUT / STALLED / KILLED / SPAWN_FAILED
    stdout: str
    stderr: str
    stdout_path: Path
    stderr_path: Path
    duration_s: float

    @property
    def timed_out(self) -> bool:
        return self.status == "TIMEOUT"


def _kill_group(pgid: int) -> None:
    for sig in (signal.SIGTERM, signal.SIGKILL):
        try:
            os.killpg(pgid, sig)
        except ProcessLookupError:
            return
        except PermissionError:
            return
        for _ in range(20):  # 최대 2초 대기
            time.sleep(0.1)
            try:
                os.killpg(pgid, 0)
            except ProcessLookupError:
                return
            except PermissionError:
                return


def run(
    cmd: list[str],
    *,
    cwd: Path,
    timeout: int,
    stdin_text: str | None = None,
    log_prefix: Path,
    conn=None,
    task_id=None,
    attempt_id=None,
    kind: str = "proc",
    env: dict | None = None,
    watch_file: Path | None = None,
    stall_sec: int = 0,
) -> RunResult:
    """프로세스를 독립 프로세스 그룹으로 띄우고, 타임아웃이면 그룹째 종료한다.

    `watch_file`+`stall_sec`을 주면 **진행 없음**도 잡는다. 타임아웃만 보면
    "돌고 있다"와 "멎었다"를 구분할 수 없어, 라이선스 핸드셰이크에서 멎은 Unity를
    10분 내내 기다린 적이 있다(2026-09-11). 로그가 자라지 않으면 그것이 멎은 것이다.
    """
    stdout_path = Path(f"{log_prefix}.stdout.log")
    stderr_path = Path(f"{log_prefix}.stderr.log")
    stdout_path.parent.mkdir(parents=True, exist_ok=True)

    proc_row = None
    if conn is not None:
        proc_row = db.create_process(
            conn,
            task_id=task_id,
            attempt_id=attempt_id,
            kind=kind,
            cmd=cmd,
            cwd=cwd,
            stdout_path=stdout_path,
            stderr_path=stderr_path,
        )

    started = time.time()
    try:
        p = subprocess.Popen(
            cmd,
            cwd=str(cwd),
            stdin=subprocess.PIPE if stdin_text is not None else subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            start_new_session=True,  # 독립 프로세스 그룹 = 그룹 종료가 가능해진다
            env=env,
            text=True,
            encoding="utf-8",
            errors="replace",
        )
    except FileNotFoundError as e:
        # "실행조차 못했다"를 성공과 섞지 않는다.
        res = RunResult(cmd, None, "SPAWN_FAILED", "", str(e), stdout_path, stderr_path, 0.0)
        logs.write(stderr_path, str(e))
        if conn is not None:
            db.update_process(conn, proc_row, status="SPAWN_FAILED", ended_at=db.now())
        return res

    pgid = os.getpgid(p.pid)
    if conn is not None:
        db.update_process(conn, proc_row, pid=p.pid, pgid=pgid)

    status = "EXITED"
    out = err = ""
    if not (watch_file and stall_sec > 0):
        try:
            out, err = p.communicate(input=stdin_text, timeout=timeout)
        except subprocess.TimeoutExpired:
            status = "TIMEOUT"
            _kill_group(pgid)
            try:
                out, err = p.communicate(timeout=10)
            except Exception:
                out, err = "", ""
    else:
        # 파이프 읽기는 스레드에 맡기고, 여기서는 **로그가 자라는지**를 본다.
        box: dict = {}

        def _pump():
            try:
                box["out"], box["err"] = p.communicate(input=stdin_text)
            except Exception as e:                # 읽기가 터져도 감시는 계속돼야 한다
                box["out"], box["err"] = "", str(e)

        th = threading.Thread(target=_pump, daemon=True)
        th.start()
        deadline = started + timeout
        last_size, last_change = -1, time.time()
        while th.is_alive():
            th.join(2.0)
            now = time.time()
            if not th.is_alive():
                break
            if now > deadline:
                status = "TIMEOUT"
                _kill_group(pgid)
                th.join(10)
                break
            try:
                size = watch_file.stat().st_size
            except OSError:
                continue                          # 아직 로그가 안 생겼다 — 멎은 것과 다르다
            if size != last_size:
                last_size, last_change = size, now
            elif last_size > 0 and now - last_change > stall_sec:
                # 로그가 stall_sec 동안 한 글자도 안 늘었다. 남은 타임아웃을 태우지 않는다.
                status = "STALLED"
                _kill_group(pgid)
                th.join(10)
                break
        out, err = box.get("out", ""), box.get("err", "")
    duration = time.time() - started

    logs.write(stdout_path, out or "")
    logs.write(stderr_path, err or "")

    exit_code = p.returncode
    if conn is not None:
        db.update_process(
            conn, proc_row, status=status, exit_code=exit_code, ended_at=db.now()
        )

    return RunResult(
        cmd=cmd,
        exit_code=exit_code,
        status=status,
        stdout=logs.mask(out or ""),
        stderr=logs.mask(err or ""),
        stdout_path=stdout_path,
        stderr_path=stderr_path,
        duration_s=duration,
    )


def stop_all(conn) -> list[str]:
    """DB가 RUNNING으로 알고 있는 프로세스 그룹을 정리한다(STOP 경로, §13)."""
    notes = []
    for row in db.running_processes(conn):
        pgid = row["pgid"]
        if not pgid:
            db.update_process(conn, row["id"], status="UNKNOWN", ended_at=db.now())
            notes.append(f"proc#{row['id']} pgid 없음 → UNKNOWN")
            continue
        try:
            os.killpg(pgid, 0)
        except (ProcessLookupError, PermissionError):
            db.update_process(conn, row["id"], status="EXITED", ended_at=db.now())
            notes.append(f"proc#{row['id']} 이미 종료됨")
            continue
        _kill_group(pgid)
        db.update_process(conn, row["id"], status="KILLED", ended_at=db.now())
        notes.append(f"proc#{row['id']} pgid={pgid} 그룹 종료")
    return notes
