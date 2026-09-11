"""외부 프로세스 관리.

핵심: 자식이 아니라 **프로세스 그룹**을 죽인다. Unity도 codex도 손자 프로세스를 만든다 —
부모만 죽이면 좀비가 남아 다음 판을 오염시킨다(기존 시스템의 최대 실패 지점, §13).
"""

from __future__ import annotations

import os
import signal
import subprocess
import time
from dataclasses import dataclass
from pathlib import Path

from . import db, logs


@dataclass
class RunResult:
    cmd: list[str]
    exit_code: int | None
    status: str  # EXITED / TIMEOUT / KILLED / SPAWN_FAILED
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
) -> RunResult:
    """프로세스를 독립 프로세스 그룹으로 띄우고, 타임아웃이면 그룹째 종료한다."""
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
    try:
        out, err = p.communicate(input=stdin_text, timeout=timeout)
    except subprocess.TimeoutExpired:
        status = "TIMEOUT"
        _kill_group(pgid)
        try:
            out, err = p.communicate(timeout=10)
        except Exception:
            out, err = "", ""
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
