"""크래시 복구 — 프로그램이 죽어도 상태가 남고, **재시작만으로 완료가 되지 않는다**(§14).

여기서 지키는 선은 하나다: 회수는 **모르는 것을 모른다고 세우는 일**이다.
죽은 판을 DONE으로 올리는 경로는 이 파일에 없고, 만들어서도 안 된다.

살아있음 판정은 PID 하나로 하지 않는다 — PID는 재사용된다. 그 방향으로 틀리면
(죽은 판을 "살아 있다"로 보면) task가 영원히 RUNNING에 갇히므로, **명령줄까지 확인**한다.
"""

from __future__ import annotations

import os
import signal
import subprocess
import time

from . import db

# 내 소유 프로세스의 표식. 다른 프로그램이 같은 PID를 물려받아도 이것까지 같지는 않다.
OWNER_MARK = "autodev_core.cli"
STALE_SEC = 900.0          # 심장박동이 이만큼 끊기면 의심 대상(죽음 판정은 PID로 한다)
INTERRUPTED = "INTERRUPTED"


def beat(conn, task_id: int) -> None:
    """살아 있다는 흔적. 게이트를 하나 지날 때마다 찍는다."""
    db.update_task(conn, task_id, heartbeat=time.time())


def claim(conn, task_id: int) -> None:
    db.update_task(conn, task_id, owner_pid=os.getpid(), heartbeat=time.time())


def _cmdline(pid: int) -> str | None:
    try:
        p = subprocess.run(["ps", "-p", str(pid), "-o", "command="],
                           capture_output=True, text=True, timeout=10)
    except (OSError, subprocess.SubprocessError):
        return None
    out = p.stdout.strip()
    return out or None


def owner_alive(pid: int | None) -> bool:
    """그 PID가 **아직 이 오케스트레이터인지**까지 본다(PID 재사용 방어)."""
    if not pid:
        return False
    cmd = _cmdline(int(pid))
    return bool(cmd and OWNER_MARK in cmd)


def orphans(conn, stale_sec: float = STALE_SEC) -> list:
    """RUNNING인데 주인이 사라진 task. 주인이 살아 있으면 오래 걸리는 것일 뿐 고아가 아니다."""
    rows = []
    for t in db.list_tasks(conn, 10 ** 6):
        if t["status"] != "RUNNING":
            continue
        if owner_alive(t["owner_pid"]):
            continue
        # 주인 PID가 아예 기록되지 않은 옛 판은 심장박동/시작시각으로 본다.
        if not t["owner_pid"]:
            last = t["heartbeat"] or t["created_at"]
            if time.time() - (last or 0) < stale_sec:
                continue
        rows.append(t)
    return rows


def _kill_leftovers(conn, task_id: int) -> list[str]:
    """고아 task가 남긴 자식(에이전트·Unity)을 정리한다. **DB에 기록된 그룹만** 죽인다 —
    패턴으로 훑어 죽이면 남의 세션을 친다(safety.unity_procs와 같은 원칙)."""
    notes = []
    for p in conn.execute(
            "SELECT * FROM processes WHERE task_id=? AND status='RUNNING'", (task_id,)).fetchall():
        pgid = p["pgid"]
        if pgid:
            try:
                os.killpg(int(pgid), signal.SIGKILL)
                notes.append(f"잔존 프로세스 그룹 {pgid} 정리 ({p['kind']})")
            except OSError:
                pass
        db.update_process(conn, p["id"], status="ORPHANED", ended_at=db.now())
    return notes


def recover(conn, dry_run: bool = False, stale_sec: float = STALE_SEC) -> list[dict]:
    """고아를 INTERRUPTED로 세운다. **DONE·PASS로 올리는 길은 없다.**
    worktree와 브랜치는 지우지 않는다 — 죽은 자리가 곧 증거다."""
    out = []
    for t in orphans(conn, stale_sec):
        item = {"id": t["id"], "goal": t["goal"], "attempts": t["attempts"],
                "worktree": t["worktree"], "owner_pid": t["owner_pid"], "notes": []}
        if not dry_run:
            item["notes"] = _kill_leftovers(conn, t["id"])
            for a in db.list_attempts(conn, t["id"]):
                if a["status"] == "RUNNING":
                    db.update_attempt(conn, a["id"], status="INTERRUPTED",
                                      reason="주인 프로세스가 사라졌다", ended_at=db.now())
            db.update_task(
                conn, t["id"], status=INTERRUPTED, verdict="UNKNOWN",
                reason=f"주인 프로세스(pid {t['owner_pid'] or '미기록'})가 사라져 회수 — "
                       f"검증되지 않았다(재시작이 완료를 만들지 않는다)",
                ended_at=db.now())
        out.append(item)
    return out
