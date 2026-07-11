"""Unified process utilities - mutex lock + duplicate guard."""
import hashlib
import json
import os
import sys
import time
from pathlib import Path

# ==================== MUTEX LOCK (Windows) ====================

if sys.platform == "win32":
    try:
        import win32event
        import win32api
        import winerror
        _has_win32 = True
    except ImportError:
        _has_win32 = False

if sys.platform == "win32" and _has_win32:
    class ProcessLock:
        """Windows named mutex for preventing duplicate processes."""

        def __init__(self, name: str):
            self.name = f"Global\\{name}"
            self.mutex = None

        def __enter__(self):
            self.mutex = win32event.CreateMutex(None, False, self.name)
            if win32api.GetLastError() == winerror.ERROR_ALREADY_EXISTS:
                print(f"❌ [{self.name}] Already running. Exiting.")
                sys.exit(0)
            print(f"✅ [{self.name}] Lock acquired")
            return self

        def __exit__(self, *args):
            if self.mutex:
                win32api.CloseHandle(self.mutex)
                print(f"🔓 [{self.name}] Lock released")

if sys.platform != "win32" or not _has_win32:
    # Fallback: file-based locking (macOS/Linux or Windows without pywin32)
    try:
        import fcntl
        _has_fcntl = True
    except ImportError:
        _has_fcntl = False

if (sys.platform != "win32" or not _has_win32) and _has_fcntl:
    class ProcessLock:
        """POSIX file-based lock."""

        def __init__(self, name: str):
            self.name = name
            self.lockfile = Path(f"/tmp/{name}.lock")
            self.fd = None

        def __enter__(self):
            self.fd = open(self.lockfile, "w")
            try:
                fcntl.flock(self.fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
                print(f"✅ [{self.name}] Lock acquired")
                return self
            except IOError:
                print(f"❌ [{self.name}] Already running. Exiting.")
                sys.exit(0)

        def __exit__(self, *args):
            if self.fd:
                fcntl.flock(self.fd, fcntl.LOCK_UN)
                self.fd.close()
                # unlink 금지(2026-07-02): 파일을 지우면 다음 인스턴스가 '새 inode'에 flock을 걸어
                # 기존 보유자와 무관하게 락 획득 — kill 경합 시 이중 데몬의 원인(미장소미 2중 실행).
                # 빈 락파일 잔존은 무해.
                print(f"🔓 [{self.name}] Lock released")

# Fallback: File-based locking (no win32 or fcntl)
if (sys.platform == "win32" and not _has_win32) or (sys.platform != "win32" and not _has_fcntl):
    import atexit

    class ProcessLock:
        """File-based lock (fallback when pywin32/fcntl unavailable)."""
        def __init__(self, name: str):
            self.name = name
            lock_dir = Path(os.environ.get("TEMP", "/tmp"))
            self.lockfile = lock_dir / f"{name}.lock"
            self.pid = os.getpid()

        def __enter__(self):
            # Atomic lock check with retry
            for attempt in range(10):
                if not self.lockfile.exists():
                    try:
                        # Atomic create - exclusive mode
                        fd = os.open(str(self.lockfile), os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o644)
                        os.write(fd, str(self.pid).encode())
                        os.close(fd)
                        print(f"✅ [{self.name}] Lock acquired (PID: {self.pid})")
                        return self
                    except FileExistsError:
                        # Another process created it between check and create
                        pass

                # Lock exists - check if process is alive
                try:
                    pid_str = self.lockfile.read_text().strip()
                    old_pid = int(pid_str)

                    # Check if process still running
                    is_running = False
                    if sys.platform == "win32":
                        try:
                            import psutil
                            is_running = psutil.pid_exists(old_pid)
                        except:
                            pass
                    else:
                        try:
                            os.kill(old_pid, 0)
                            is_running = True
                        except OSError:
                            pass

                    if is_running:
                        print(f"❌ [{self.name}] Already running (PID: {old_pid}). Exiting.")
                        sys.exit(0)

                    # Stale lock - remove and retry
                    print(f"🧹 [{self.name}] Removing stale lock (PID: {old_pid})")
                    self.lockfile.unlink(missing_ok=True)
                    time.sleep(0.1)
                except (ValueError, OSError):
                    self.lockfile.unlink(missing_ok=True)
                    time.sleep(0.1)

            print(f"❌ [{self.name}] Failed to acquire lock after 10 attempts. Exiting.")
            sys.exit(1)

            # Ensure cleanup on exit
            atexit.register(self._cleanup)
            return self

        def __exit__(self, *args):
            self._cleanup()

        def _cleanup(self):
            if self.lockfile.exists():
                try:
                    # Only remove if it's our lock
                    if self.lockfile.read_text().strip() == str(self.pid):
                        self.lockfile.unlink()
                        print(f"🔓 [{self.name}] Lock released")
                except Exception:
                    pass


# ==================== ADVISORY LOCK (non-fatal — for work-execution mutual exclusion) ====================

import contextlib


@contextlib.contextmanager
def advisory_lock(name: str):
    """비치명적 상호배제 락 — 충돌 시 `ProcessLock`처럼 `sys.exit`하지 않고 `False`를 yield한다.

    배경(2026-07-11): 펫나 상시 데몬(미오 등)은 `ProcessLock(name)`으로 데몬 전체 수명 동안
    락을 쥐고 있다. 예원 워치독이 유휴 감지로 같은 스크립트를 수동 디스패치하려 하면(직접
    실행, `--daemon` 아님) 그 시점에 데몬이 실제로 일하고 있을 수도, 그냥 자고 있을 수도
    있다 — 이걸 판별하려고 같은 이름의 `ProcessLock`을 쓰면 상시 데몬 쪽이 이미 락을 쥔
    상태라 매번 충돌해 `sys.exit(0)`이 상시 루프 안에서 터져 데몬 자체가 죽는다.

    이 락은 대신 "실제 작업 실행 구간"에만 짧게 잡는다 — 데몬은 주기적으로 깨어나 실제
    review()/generate_test() 등을 부를 때만 이걸로 감싸고, 수동 디스패치도 똑같이 감싼다.
    같은 이름이면 `ProcessLock`과 파일을 공유해 서로를 인식하되, 충돌해도 `yield False`로
    "이번엔 건너뛰어라"라고 알릴 뿐 프로세스를 죽이지 않는다."""
    if sys.platform == "win32" and _has_win32:
        mutex = win32event.CreateMutex(None, False, f"Global\\{name}")
        got = win32api.GetLastError() != winerror.ERROR_ALREADY_EXISTS
        try:
            yield got
        finally:
            win32api.CloseHandle(mutex)
        return
    if _has_fcntl:
        fd = open(Path(f"/tmp/{name}.lock"), "w")
        try:
            fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
            got = True
        except IOError:
            got = False
        try:
            yield got
        finally:
            if got:
                fcntl.flock(fd, fcntl.LOCK_UN)
            fd.close()
        return
    yield True  # 락 메커니즘 자체가 없는 환경 — 기존 폴백과 동일 원칙(그냥 진행)


# ==================== DUPLICATE GUARD (Content Hash) ====================

class DuplicateGuard:
    """Prevent duplicate content uploads using SHA256 hash cache."""

    def __init__(self, cache_file: str = ".upload_cache.json"):
        root = Path(__file__).resolve()
        for _ in range(10):
            if (root / "ENV_MANIFEST.json").exists():
                break
            root = root.parent
        self.cache_path = root / cache_file
        self.cache = self._load()

    def _load(self) -> dict:
        if self.cache_path.exists():
            try:
                return json.loads(self.cache_path.read_text(encoding="utf-8"))
            except Exception:
                return {}
        return {}

    def _save(self):
        self.cache_path.write_text(json.dumps(self.cache, indent=2, ensure_ascii=False), encoding="utf-8")

    def _hash(self, content: str) -> str:
        return hashlib.sha256(content.encode()).hexdigest()[:16]

    def is_duplicate(self, content: str, platform: str = "default") -> bool:
        """Check if content was already uploaded."""
        h = self._hash(content)
        key = f"{platform}:{h}"
        return key in self.cache

    def mark_uploaded(self, content: str, platform: str = "default", meta: dict = None):
        """Mark content as uploaded."""
        h = self._hash(content)
        key = f"{platform}:{h}"
        self.cache[key] = {
            "timestamp": time.time(),
            "length": len(content),
            **(meta or {}),
        }
        self._save()

    def clear_old(self, days: int = 30):
        """Remove entries older than N days."""
        cutoff = time.time() - (days * 86400)
        before = len(self.cache)
        self.cache = {k: v for k, v in self.cache.items() if v.get("timestamp", 0) > cutoff}
        if len(self.cache) < before:
            self._save()
            print(f"  🧹 Cleared {before - len(self.cache)} old entries")


# ==================== 함대 단일 기계 운영 정책 (git 동기화 정책 파일) ====================
# 2차 발견(2026-07-11, 같은 날): 양방향 가드로 고친 PETNNA_AGENTS_ON_WINDOWS 플래그 자체가
# .env.encrypted(기계+계정 파생 키로 암호화 — _shared/env.py _get_key)에 있어, 맥과 Windows가
# 애초에 "같은 값"을 본 적이 없었다(서로의 암호문을 못 읽으므로 각자 평문 폴백/자기 값을 봄).
# 오너 지시("윈도우 아니고 맥이 메인")를 반영하려 해도 암호화 파일은 이 맥에서 Windows가
# 읽을 수 있게 고쳐 쓸 방법이 없다 — 잘못 건드리면 Windows 설정 전체가 깨진다.
# 해법: fleet_machine_policy.json(평문, git 추적)에 "누가 메인인지"를 선언한다. git pull만
# 하면 두 기계가 항상 동일한 값을 보므로 암호화 키 불일치 문제가 구조적으로 사라진다.
#
# 3차 통합(2026-07-11, 같은 날): scripts/fleet_bootstrap.py가 TELEGRAM_POLL_HOST(역시
# .env.encrypted 저장 — 위와 똑같은 결함)로 "이중 가동 방지"를 별도로 구현하고 있었다.
# 그 파일 독스트링엔 이미 "새 개념을 만들지 않아야 두 곳이 어긋나지 않는다"고 적혀 있었는데,
# 이 정책 파일을 만들며 그 원칙을 어긴 셈이었다(오너 지적 — "합칠 수 있는 거 합쳐서 줄이자").
# read_fleet_policy()로 정책 파일 읽기를 한 곳에 모으고, petnna_single_machine_guard()와
# fleet_bootstrap.gates() 양쪽이 이 함수를 공유한다 — TELEGRAM_POLL_HOST/.env.encrypted 경로는
# fleet_bootstrap.py에서 폐기(이 파일로 완전히 대체).
_FLEET_POLICY_PATH = Path(__file__).resolve().parent / "fleet_machine_policy.json"


def write_fleet_policy(policy: dict) -> Path:
    """fleet_machine_policy.json을 갱신한다(전체 덮어쓰기). 반환값은 파일 경로(호출자 로그용)."""
    _FLEET_POLICY_PATH.write_text(json.dumps(policy, ensure_ascii=False, indent=2), encoding="utf-8")
    return _FLEET_POLICY_PATH


def read_fleet_policy() -> dict:
    """fleet_machine_policy.json을 읽는다. 없거나 파싱 실패하면 {}."""
    try:
        return json.loads(_FLEET_POLICY_PATH.read_text(encoding="utf-8"))
    except Exception:
        return {}


def petnna_single_machine_guard(agent_label: str = "펫나 에이전트") -> bool:
    """단일 기계 운영 위반이면 True(호출자는 즉시 return해야 함) + 안내 출력."""
    primary = str(read_fleet_policy().get("primary_platform", "")).strip()

    if primary:
        if sys.platform != primary:
            print(f"{agent_label}는 '{primary}' 전용으로 지정됨(fleet_machine_policy.json) — "
                  f"이 기계({sys.platform})에서 자가 종료(이중 가동 방지)")
            return True
        return False  # 정책 파일이 이 플랫폼을 명시적으로 허용

    # 정책 파일 없음/파싱 실패 — 구형 env 플래그 방식으로 폴백(기계마다 다른 값을 볼 수 있어
    # 완전히 신뢰할 순 없지만, 정책 파일이 아예 없는 과거 상태와의 하위호환용).
    flag = os.getenv("PETNNA_AGENTS_ON_WINDOWS")
    if sys.platform == "win32" and flag != "true":
        print(f"{agent_label}는 맥 전용(이중 가동 방지) — PETNNA_AGENTS_ON_WINDOWS=true로만 해제")
        return True
    if sys.platform != "win32" and flag == "true":
        print(f"{agent_label}는 Windows 전용으로 지정됨(PETNNA_AGENTS_ON_WINDOWS=true) — "
              f"이 기계({sys.platform})에서 자가 종료(이중 가동 방지)")
        return True
    return False
