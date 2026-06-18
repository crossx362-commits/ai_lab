#!/usr/bin/env python3
"""데이브/레오/현빈 통합 트레이딩 시스템 시작 (코인 + 주식)"""
import os, sys, subprocess, time

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

_here = os.path.dirname(os.path.abspath(__file__))
AI_TEAM_ROOT = os.path.abspath(os.path.join(_here, ".."))
sys.path.insert(0, AI_TEAM_ROOT)

from _shared.env import load_env
from _shared.notify import send

load_env()

# Windows에서 백그라운드 실행 플래그
CREATE_NO_WINDOW = 0x08000000 if sys.platform == "win32" else 0

def start_bot(script_path: str, name: str):
    """봇 시작"""
    try:
        if sys.platform == "win32":
            # Windows: 백그라운드 실행
            subprocess.Popen(
                [sys.executable, script_path, "--daemon"],
                creationflags=CREATE_NO_WINDOW
            )
        else:
            # macOS/Linux: nohup으로 백그라운드 실행
            subprocess.Popen(
                ["nohup", sys.executable, script_path, "--daemon"],
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
                preexec_fn=os.setpgrp
            )

        print(f"✅ {name} 시작됨")
        return True
    except Exception as e:
        print(f"❌ {name} 시작 실패: {e}")
        return False


def main():
    """메인 함수"""
    print("=" * 60)
    print("🤖 AI Team 통합 트레이딩 시스템 시작")
    print("=" * 60)
    print()

    bots = []

    # === 코인 트레이딩 ===
    print("💰 코인 트레이딩 봇")
    print("-" * 60)

    # 현빈 - 코인 시장 인텔
    bots.append({
        "script": os.path.join(AI_TEAM_ROOT, "skills", "현빈_전략가", "tools", "crypto_market_intelligence.py"),
        "name": "현빈 (코인 시장 인텔)"
    })

    # 데이브 - 코인 보수적 매매
    bots.append({
        "script": os.path.join(AI_TEAM_ROOT, "skills", "데이브_주식", "tools", "upbit_auto_trader.py"),
        "name": "데이브 (코인 보수적)"
    })

    # 레오 - 코인 공격적 매매
    bots.append({
        "script": os.path.join(AI_TEAM_ROOT, "skills", "레오_트레이더", "tools", "leo_aggressive_trader.py"),
        "name": "레오 (코인 공격적)"
    })

    print()

    # === 주식 트레이딩 ===
    print("📈 주식 트레이딩 봇")
    print("-" * 60)

    # 현빈 - 주식 시장 인텔
    bots.append({
        "script": os.path.join(AI_TEAM_ROOT, "skills", "현빈_전략가", "tools", "stock_market_intelligence.py"),
        "name": "현빈 (주식 시장 인텔)"
    })

    # 데이브 - 주식 보수적 매매
    bots.append({
        "script": os.path.join(AI_TEAM_ROOT, "skills", "데이브_주식", "tools", "stock_auto_trader.py"),
        "name": "데이브 (주식 보수적)"
    })

    print()
    print("=" * 60)
    print("🚀 봇 시작 중...")
    print("=" * 60)
    print()

    # 봇 시작
    started = []
    failed = []

    for bot in bots:
        if os.path.exists(bot["script"]):
            if start_bot(bot["script"], bot["name"]):
                started.append(bot["name"])
            else:
                failed.append(bot["name"])
            time.sleep(1)  # 1초 간격
        else:
            print(f"⚠️  {bot['name']} 스크립트 없음: {bot['script']}")
            failed.append(bot["name"])

    print()
    print("=" * 60)
    print("📊 시작 결과")
    print("=" * 60)
    print(f"✅ 성공: {len(started)}개")
    for name in started:
        print(f"   - {name}")

    if failed:
        print(f"\n❌ 실패: {len(failed)}개")
        for name in failed:
            print(f"   - {name}")

    # Telegram 알림
    msg = f"🤖 [AI Team] 트레이딩 시스템 시작\n\n"
    msg += f"✅ 성공: {len(started)}개\n"
    if failed:
        msg += f"❌ 실패: {len(failed)}개\n"
    msg += f"\n코인: 현빈, 데이브, 레오\n"
    msg += f"주식: 현빈, 데이브"

    send(msg)

    print()
    print("=" * 60)
    print("✅ 완료")
    print("=" * 60)


if __name__ == "__main__":
    main()
