#!/usr/bin/env python3
"""데이브/레오/시그널 통합 트레이딩 시스템 시작."""
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
    """봇 시작 (로그 파일로 stdout/stderr 리다이렉트)"""
    try:
        # 로그 디렉토리 (ai_lab/output/trading_logs)
        workspace_root = os.path.abspath(os.path.join(AI_TEAM_ROOT, "..", ".."))
        log_dir = os.path.join(workspace_root, "output", "trading_logs")
        os.makedirs(log_dir, exist_ok=True)

        # 로그 파일명 생성
        bot_slug = name.split()[0].lower()
        slug_map = {"시그널": "signal", "데이브": "dave", "레오": "leo"}
        bot_slug = slug_map.get(name.split()[0], bot_slug)

        # 중복 방지: "시그널 (일일 분석 스케줄러)" → signal_scheduler
        if "스케줄러" in name:
            bot_slug = f"{bot_slug}_scheduler"
        elif "주식" in name:
            bot_slug = f"{bot_slug}_stock"

        out_log = os.path.join(log_dir, f"{bot_slug}_daemon.out.log")
        err_log = os.path.join(log_dir, f"{bot_slug}_daemon.err.log")

        if sys.platform == "win32":
            # Windows: 로그 파일로 리다이렉트 + 백그라운드 실행
            with open(out_log, "a", encoding="utf-8") as fout, \
                 open(err_log, "a", encoding="utf-8") as ferr:
                subprocess.Popen(
                    [sys.executable, "-u", script_path, "--daemon"],
                    stdout=fout,
                    stderr=ferr,
                    creationflags=CREATE_NO_WINDOW,
                    env={**os.environ, "PYTHONUTF8": "1"}
                )
        else:
            # macOS/Linux: nohup으로 백그라운드 실행
            with open(out_log, "a") as fout, open(err_log, "a") as ferr:
                subprocess.Popen(
                    ["nohup", sys.executable, "-u", script_path, "--daemon"],
                    stdout=fout,
                    stderr=ferr,
                    preexec_fn=os.setpgrp
                )

        print(f"✅ {name} 시작됨 → {out_log}")
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

    # 시그널 - 시장 인텔
    bots.append({
        "script": os.path.join(AI_TEAM_ROOT, "skills", "시그널_분석가", "tools", "market_signal.py"),
        "name": "시그널 (시장 인텔)"
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

    # 시그널 - 일일 분석 스케줄러 (매일 새벽 3시)
    bots.append({
        "script": os.path.join(AI_TEAM_ROOT, "skills", "시그널_분석가", "tools", "daily_signal_scheduler.py"),
        "name": "시그널 (일일 분석 스케줄러)"
    })

    print()

    # === 주식 트레이딩 ===
    print("📈 주식 트레이딩 봇")
    print("-" * 60)

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
    msg += f"\n코인: 시그널, 데이브, 레오\n"
    msg += f"주식: 데이브\n"
    msg += f"스케줄: 시그널 일일 분석 (매일 새벽 3시)"

    send(msg)

    print()
    print("=" * 60)
    print("✅ 완료")
    print("=" * 60)


if __name__ == "__main__":
    main()
