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
                self.lockfile.unlink(missing_ok=True)
                print(f"🔓 [{self.name}] Lock released")

# Fallback: No locking available
if (sys.platform == "win32" and not _has_win32) or (sys.platform != "win32" and not _has_fcntl):
    class ProcessLock:
        """Dummy lock (no win32 or fcntl available)."""
        def __init__(self, name: str):
            self.name = name
            print(f"⚠️  [{name}] Process lock unavailable (install pywin32)")

        def __enter__(self):
            return self

        def __exit__(self, *args):
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
