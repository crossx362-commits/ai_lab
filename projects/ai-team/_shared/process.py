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
