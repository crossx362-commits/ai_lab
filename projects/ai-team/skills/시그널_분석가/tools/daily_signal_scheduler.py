#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""매일 새벽 3시 시그널 유망종목 분석 스케줄러.

백그라운드 데몬으로 실행되며, 매일 새벽 3시에 market_signal.py를 실행하여
주식+코인 유망종목을 분석하고 텔레그램으로 보고합니다.
"""

import os
import sys
import time
import subprocess
from datetime import datetime, timedelta, timezone
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

SCRIPT_DIR = Path(__file__).resolve().parent
AI_TEAM_ROOT = SCRIPT_DIR.parents[2]
WORKSPACE_ROOT = AI_TEAM_ROOT.parents[1]
sys.path.insert(0, str(AI_TEAM_ROOT))

from _shared.env import load_env
from _shared.notify import send
from _shared.process import ProcessLock

load_env(str(WORKSPACE_ROOT))

KST = timezone(timedelta(hours=9))
TARGET_HOUR = 3  # 새벽 3시
TARGET_MINUTE = 0

SIGNAL_SCRIPT = SCRIPT_DIR / "market_signal.py"


def log(message: str) -> None:
    """로그 출력"""
    print(f"[DailySignal] {datetime.now(KST).strftime('%Y-%m-%d %H:%M:%S')} {message}", flush=True)


def get_next_run_time() -> datetime:
    """다음 실행 시간 계산 (매일 새벽 3시)"""
    now = datetime.now(KST)
    next_run = now.replace(hour=TARGET_HOUR, minute=TARGET_MINUTE, second=0, microsecond=0)

    # 이미 오늘 3시가 지났으면 내일 3시
    if next_run <= now:
        next_run += timedelta(days=1)

    return next_run


def run_signal_analysis() -> bool:
    """시그널 분석 실행"""
    log("유망종목 분석 시작...")

    try:
        result = subprocess.run(
            [sys.executable, str(SIGNAL_SCRIPT), "--notify"],
            cwd=str(WORKSPACE_ROOT),
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=300
        )

        if result.returncode == 0:
            log("✅ 분석 완료")
            return True
        else:
            log(f"⚠️ 분석 실패 (exit code: {result.returncode})")
            log(f"stderr: {result.stderr[:500]}")
            return False
    except subprocess.TimeoutExpired:
        log("❌ 분석 타임아웃 (5분 초과)")
        return False
    except Exception as e:
        log(f"❌ 분석 오류: {e}")
        return False


def daemon() -> None:
    """스케줄러 데몬"""
    log("스케줄러 시작 (매일 새벽 3시)")
    send("📅 시그널 일일 분석 스케줄러 시작\n\n매일 새벽 3시에 주식+코인 유망종목 분석 및 보고")

    with ProcessLock("daily_signal_scheduler"):
        while True:
            try:
                next_run = get_next_run_time()
                now = datetime.now(KST)
                wait_seconds = (next_run - now).total_seconds()

                log(f"다음 실행: {next_run.strftime('%Y-%m-%d %H:%M:%S')} (대기: {wait_seconds/3600:.1f}시간)")

                # 대기
                time.sleep(wait_seconds)

                # 실행
                success = run_signal_analysis()

                if success:
                    # 성공 시 텔레그램 알림은 market_signal.py --notify에서 처리
                    pass
                else:
                    send("⚠️ 시그널 일일 분석 실패\n\n로그를 확인해주세요.")

                # 다음 날 3시까지 대기 (실행 직후 바로 다음 날 계산)
                time.sleep(60)  # 1분 대기 후 다시 계산 (중복 실행 방지)

            except KeyboardInterrupt:
                log("스케줄러 중지")
                send("🛑 시그널 일일 분석 스케줄러 중지")
                break
            except Exception as e:
                log(f"⚠️ 오류 발생: {e}")
                send(f"⚠️ 시그널 스케줄러 오류\n\n{str(e)[:200]}")
                time.sleep(600)  # 10분 대기 후 재시도


def main() -> None:
    """메인 엔트리"""
    if "--daemon" in sys.argv:
        daemon()
    elif "--test" in sys.argv:
        # 테스트: 즉시 실행
        log("테스트 모드: 즉시 실행")
        success = run_signal_analysis()
        sys.exit(0 if success else 1)
    else:
        print("사용법:")
        print("  python daily_signal_scheduler.py --daemon   # 스케줄러 실행")
        print("  python daily_signal_scheduler.py --test     # 즉시 실행 (테스트)")
        sys.exit(1)


if __name__ == "__main__":
    main()
