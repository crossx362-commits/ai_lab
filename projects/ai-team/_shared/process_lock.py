"""
process_lock.py — 중복 실행 방지 (Windows Named Mutex / Unix fcntl)

사용법:
    from _shared.process_lock import acquire_lock, release_lock

    if not acquire_lock("my_bot"):
        print("이미 실행 중")
        sys.exit(0)

    try:
        # 봇 로직
        pass
    finally:
        release_lock("my_bot")
"""
import os
import sys
import atexit


_MUTEX_HANDLE = None


def acquire_lock(bot_name: str) -> bool:
    """락 획득 시도. 이미 실행 중이면 False 반환.

    Args:
        bot_name: 봇 이름 (예: "hyunbin", "dave", "leo", "monitor")

    Returns:
        True: 락 획득 성공 (실행 가능)
        False: 이미 다른 프로세스가 실행 중
    """
    global _MUTEX_HANDLE

    if sys.platform == "win32":
        # Windows: Named Mutex 사용 (프로세스 간 동기화)
        import ctypes
        from ctypes import wintypes

        kernel32 = ctypes.windll.kernel32

        # CreateMutexW 함수 정의
        CreateMutexW = kernel32.CreateMutexW
        CreateMutexW.argtypes = [wintypes.LPVOID, wintypes.BOOL, wintypes.LPCWSTR]
        CreateMutexW.restype = wintypes.HANDLE

        GetLastError = kernel32.GetLastError

        ERROR_ALREADY_EXISTS = 183

        # 뮤텍스 이름 (Global 네임스페이스 사용)
        mutex_name = f"Global\\AITeam_{bot_name}_Mutex"

        # 뮤텍스 생성 시도
        mutex = CreateMutexW(None, False, mutex_name)

        if mutex == 0:
            print(f"❌ [{bot_name}] 뮤텍스 생성 실패")
            return False

        # ERROR_ALREADY_EXISTS 확인
        if GetLastError() == ERROR_ALREADY_EXISTS:
            print(f"[{bot_name}] Already running")
            kernel32.CloseHandle(mutex)
            return False

        # 락 획득 성공
        _MUTEX_HANDLE = mutex
        atexit.register(release_lock, bot_name)
        print(f"[{bot_name}] Lock acquired (PID: {os.getpid()})")
        return True

    else:
        # Unix: fcntl 파일 락
        import fcntl

        # 락 파일 경로
        lock_dir = "/tmp/ailab_locks"
        os.makedirs(lock_dir, exist_ok=True)
        lock_file = os.path.join(lock_dir, f"{bot_name}.lock")

        try:
            lock_handle = open(lock_file, 'w')
            fcntl.flock(lock_handle.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
            lock_handle.write(str(os.getpid()))
            lock_handle.flush()

            _MUTEX_HANDLE = lock_handle
            atexit.register(release_lock, bot_name)
            print(f"[{bot_name}] Lock acquired (PID: {os.getpid()})")
            return True

        except IOError:
            print(f"[{bot_name}] Already running")
            try:
                lock_handle.close()
            except:
                pass
            return False
        except Exception as e:
            print(f"[{bot_name}] Lock error: {e}")
            return False


def release_lock(bot_name: str):
    """락 해제 (프로세스 종료 시 자동 호출)"""
    global _MUTEX_HANDLE

    if _MUTEX_HANDLE is None:
        return

    try:
        if sys.platform == "win32":
            import ctypes
            kernel32 = ctypes.windll.kernel32
            kernel32.CloseHandle(_MUTEX_HANDLE)
            print(f"[{bot_name}] Mutex released")
        else:
            _MUTEX_HANDLE.close()
            lock_file = f"/tmp/ailab_locks/{bot_name}.lock"
            if os.path.exists(lock_file):
                os.remove(lock_file)
            print(f"[{bot_name}] Lock released")

        _MUTEX_HANDLE = None

    except Exception as e:
        print(f"[{bot_name}] Release error: {e}")
