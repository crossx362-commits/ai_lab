# -*- coding: utf-8 -*-
import sys
import os
import time

sys.path.insert(0, r"d:\ai_lab\projects\ai-team")

from _shared.process_lock import acquire_lock, release_lock

if acquire_lock("test"):
    print(f"[PID {os.getpid()}] 락 획득 성공! 5초 대기...")
    time.sleep(5)
    print(f"[PID {os.getpid()}] 종료")
    release_lock("test")
else:
    print(f"[PID {os.getpid()}] 락 실패 - 이미 실행 중")
