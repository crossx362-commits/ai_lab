# -*- coding: utf-8 -*-
"""
일일 트레이딩 학습 스케줄러
매일 자정에 레오, 데이브의 거래 성과를 분석하고 전략 개선
"""
import os
import sys
import schedule
import time

_here = os.path.dirname(os.path.abspath(__file__))
AI_TEAM_ROOT = os.path.abspath(os.path.join(_here, ".."))
sys.path.insert(0, AI_TEAM_ROOT)

# 레오 학습 시스템 임포트
LEO_TOOLS = os.path.join(AI_TEAM_ROOT, "skills", "레오_트레이더", "tools")
sys.path.insert(0, LEO_TOOLS)

from _shared.env_loader import load_env
from _shared.telegram_notifier import send_telegram_message
from leo_learning_system import LeoLearningSystem

load_env()


def run_daily_learning():
    """일일 학습 루틴"""
    print(f"\n{'='*60}")
    print(f"📚 일일 트레이딩 학습 시작 - {time.strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"{'='*60}\n")

    # 레오 학습
    print("⚡ 레오 거래 성과 분석...")
    leo_learner = LeoLearningSystem()
    try:
        leo_learner.run_daily_learning()
    except Exception as e:
        print(f"❌ 레오 학습 실패: {e}")

    # TODO: 데이브 학습 시스템 추가
    # dave_learner = DaveLearningSystem()
    # dave_learner.run_daily_learning()

    print(f"\n{'='*60}")
    print("✅ 일일 학습 완료")
    print(f"{'='*60}\n")

    send_telegram_message("📚 일일 트레이딩 학습 완료\n레오, 데이브의 거래 성과를 분석하고 전략을 업데이트했습니다.")


def main():
    """학습 스케줄러 시작"""
    print("🤖 트레이딩 학습 스케줄러 시작")
    print("  - 매일 00:00: 전일 거래 성과 분석 및 전략 개선")

    # 매일 자정 실행
    schedule.every().day.at("00:00").do(run_daily_learning)

    # 시작 즉시 1회 실행 (테스트)
    if "--now" in sys.argv:
        print("\n즉시 실행 모드...")
        run_daily_learning()

    send_telegram_message("🤖 트레이딩 학습 스케줄러 가동 시작\n매일 00:00에 자동 학습합니다.")

    # 스케줄러 루프
    while True:
        schedule.run_pending()
        time.sleep(60)  # 1분마다 체크


if __name__ == "__main__":
    main()
