# -*- coding: utf-8 -*-
"""
중복 프로세스 자동 정리 스크립트.

역할별로 같은 봇/데몬이 여러 개 떠 있을 때 가장 오래된 프로세스 1개만
남기고 나머지를 종료한다. 전체 Python 프로세스를 한꺼번에 죽이지 않는다.
"""
import io
import json
import os
import subprocess
import sys
import time
from dataclasses import dataclass


if sys.platform == "win32" and hasattr(sys.stdout, "buffer"):
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding="utf-8", errors="replace")

_here = os.path.dirname(os.path.abspath(__file__))
AI_TEAM_ROOT = os.path.abspath(os.path.join(_here, ".."))
WORKSPACE_ROOT = os.path.abspath(os.path.join(AI_TEAM_ROOT, "..", ".."))
sys.path.insert(0, AI_TEAM_ROOT)

from _shared.env import load_env
from _shared.notify import send

load_env()

CREATE_NO_WINDOW = subprocess.CREATE_NO_WINDOW if sys.platform == "win32" else 0


@dataclass(frozen=True)
class ProcessRule:
    name: str
    markers: tuple[str, ...]


class ProcessInfo:
    def __init__(self, pid: int, name: str, cmdline: str, create_time=0, psutil_proc=None):
        self.pid = pid
        self._psutil_proc = psutil_proc
        self.info = {
            "pid": pid,
            "name": name,
            "cmdline": cmdline.split() if isinstance(cmdline, str) else (cmdline or []),
            "create_time": create_time or 0,
        }

    def terminate(self):
        if self._psutil_proc:
            return self._psutil_proc.terminate()
        subprocess.run(
            ["powershell", "-NoProfile", "-Command", f"Stop-Process -Id {int(self.pid)} -ErrorAction Stop"],
            capture_output=True,
            text=True,
            timeout=10,
            check=True,
            creationflags=CREATE_NO_WINDOW,
        )

    def kill(self):
        if self._psutil_proc:
            return self._psutil_proc.kill()
        subprocess.run(
            ["powershell", "-NoProfile", "-Command", f"Stop-Process -Id {int(self.pid)} -Force -ErrorAction Stop"],
            capture_output=True,
            text=True,
            timeout=10,
            check=True,
            creationflags=CREATE_NO_WINDOW,
        )

    def wait(self, timeout=5):
        if self._psutil_proc:
            return self._psutil_proc.wait(timeout=timeout)
        end = time.time() + timeout
        while time.time() < end:
            result = subprocess.run(
                ["powershell", "-NoProfile", "-Command", f"Get-Process -Id {int(self.pid)} -ErrorAction SilentlyContinue"],
                capture_output=True,
                text=True,
                timeout=5,
                creationflags=CREATE_NO_WINDOW,
            )
            if not result.stdout.strip():
                return None
            time.sleep(0.2)
        raise TimeoutError(f"PID {self.pid} did not exit")


RULES = [
    ProcessRule("영숙 텔레그램", ("telegram_receiver.py", "run_youngsuk_daemon.py")),
    ProcessRule("트레이딩 팀 런처", ("start_trading_team.py",)),
    ProcessRule("현빈 시장정보", ("crypto_market_intelligence.py",)),
    ProcessRule("데이브 트레이더", ("upbit_auto_trader.py", "run_trader_daemon.py dave")),
    ProcessRule("레오 트레이더", ("leo_aggressive_trader.py", "run_trader_daemon.py leo")),
    ProcessRule("프로세스 모니터", ("monitor_processes.py",)),
]


def _norm(text: str) -> str:
    return " ".join(str(text or "").replace("\\", "/").lower().split())


def _load_psutil():
    try:
        import psutil
        return psutil
    except Exception as exc:
        print(f"❌ psutil 로드 실패: {exc}")
        return None


def iter_python_processes_powershell():
    command = (
        "Get-CimInstance Win32_Process | "
        "Where-Object { $_.Name -match '^python' } | "
        "Select-Object ProcessId,Name,CommandLine,CreationDate | ConvertTo-Json -Compress"
    )
    try:
        result = subprocess.run(
            ["powershell", "-NoProfile", "-Command", command],
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=20,
            creationflags=CREATE_NO_WINDOW,
        )
        if result.returncode != 0:
            print(f"❌ PowerShell 프로세스 조회 실패: {result.stderr.strip()}")
            return []
        raw = result.stdout.strip()
        if not raw:
            return []
        data = json.loads(raw)
        if isinstance(data, dict):
            data = [data]

        procs = []
        current_pid = os.getpid()
        root_marker = _norm(WORKSPACE_ROOT)
        for item in data:
            pid = int(item.get("ProcessId") or 0)
            if not pid or pid == current_pid:
                continue
            cmdline = item.get("CommandLine") or ""
            cmd = _norm(cmdline)
            if root_marker not in cmd and "telegram_receiver.py" not in cmd:
                continue
            procs.append(ProcessInfo(pid, item.get("Name") or "python", cmdline, item.get("CreationDate") or 0))
        return procs
    except Exception as exc:
        print(f"❌ PowerShell fallback 실패: {exc}")
        return []


def iter_python_processes():
    psutil = _load_psutil()
    if not psutil:
        return iter_python_processes_powershell()

    current_pid = os.getpid()
    procs = []
    for proc in psutil.process_iter(["pid", "name", "cmdline", "create_time"]):
        try:
            if proc.info["pid"] == current_pid:
                continue
            name = _norm(proc.info.get("name") or "")
            cmdline = " ".join(proc.info.get("cmdline") or [])
            cmd = _norm(cmdline)
            if not name.startswith("python"):
                continue
            if _norm(WORKSPACE_ROOT) not in cmd and "telegram_receiver.py" not in cmd:
                continue
            procs.append(ProcessInfo(
                proc.info["pid"],
                proc.info.get("name") or "python",
                cmdline,
                proc.info.get("create_time") or 0,
                psutil_proc=proc,
            ))
        except (psutil.NoSuchProcess, psutil.AccessDenied):
            continue
        except Exception as exc:
            print(f"⚠️ 프로세스 조회 실패: {exc}")
    return procs


def match_rule(proc, rule: ProcessRule) -> bool:
    try:
        cmd = _norm(" ".join(proc.info.get("cmdline") or []))
    except Exception:
        return False
    return any(_norm(marker) in cmd for marker in rule.markers)


def describe_proc(proc) -> str:
    try:
        cmd = " ".join(proc.info.get("cmdline") or [])
        if len(cmd) > 120:
            cmd = cmd[:117] + "..."
        return f"PID {proc.pid}: {cmd}"
    except Exception:
        return f"PID {getattr(proc, 'pid', '?')}"


def terminate_process(proc, timeout=5) -> bool:
    psutil = _load_psutil()
    try:
        print(f"  종료 시도: {describe_proc(proc)}")
        proc.terminate()
        try:
            proc.wait(timeout=timeout)
        except Exception as wait_exc:
            if psutil and wait_exc.__class__.__name__ != "TimeoutExpired":
                raise
            print(f"  강제 종료: PID {proc.pid}")
            proc.kill()
            proc.wait(timeout=timeout)
        return True
    except Exception as exc:
        if exc.__class__.__name__ == "NoSuchProcess":
            return True
        if exc.__class__.__name__ == "AccessDenied":
            print(f"  ❌ 권한 부족: PID {proc.pid} ({exc})")
            return False
        print(f"  ❌ 종료 실패: PID {getattr(proc, 'pid', '?')} ({exc})")
        return False


def cleanup_duplicates(dry_run=False):
    processes = iter_python_processes()
    print(f"감시 대상 Python 프로세스: {len(processes)}개")

    removed = []
    failed = []
    seen_pids = set()

    for rule in RULES:
        matches = [proc for proc in processes if proc.pid not in seen_pids and match_rule(proc, rule)]
        if not matches:
            continue

        matches.sort(key=lambda p: p.info.get("create_time") or 0)
        keeper = matches[0]
        seen_pids.add(keeper.pid)
        print(f"\n[{rule.name}] 유지: {describe_proc(keeper)}")

        extras = matches[1:]
        if not extras:
            continue

        print(f"[{rule.name}] 중복 {len(extras)}개 감지")
        for proc in extras:
            seen_pids.add(proc.pid)
            if dry_run:
                print(f"  DRY-RUN 종료 대상: {describe_proc(proc)}")
                removed.append((rule.name, proc.pid, "dry-run"))
                continue
            if terminate_process(proc):
                removed.append((rule.name, proc.pid, "terminated"))
            else:
                failed.append((rule.name, proc.pid))

    return removed, failed


def main():
    dry_run = "--dry-run" in sys.argv[1:]

    print("=" * 60)
    print("중복 프로세스 자동 정리")
    print("=" * 60)

    removed, failed = cleanup_duplicates(dry_run=dry_run)

    if removed:
        lines = [f"• {name}: PID {pid} ({status})" for name, pid, status in removed]
        msg = "✅ 중복 프로세스 정리 완료\n" + "\n".join(lines)
        print("\n" + msg)
        if not dry_run:
            send(msg)
    else:
        print("\n✅ 정리할 중복 프로세스 없음")

    if failed:
        lines = [f"• {name}: PID {pid}" for name, pid in failed]
        msg = "⚠️ 일부 중복 프로세스 정리 실패\n" + "\n".join(lines)
        print("\n" + msg)
        if not dry_run:
            send(msg)
        return 1

    time.sleep(1)
    print("\n" + "=" * 60)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
