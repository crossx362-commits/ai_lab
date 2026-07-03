"""
upload_manager.py — CEO/비서가 호출하는 업로드 자동 관리 도구.

사용법:
  python upload_manager.py --check      # 오늘 현황만 확인
  python upload_manager.py --run        # 누락 파이프라인 자동 실행
  python upload_manager.py              # --run 기본값
"""
import os
import sys
import json
import datetime
import subprocess

_here = os.path.dirname(os.path.abspath(__file__))
_ai_team_root = os.path.abspath(os.path.join(_here, "..", "..", ".."))
sys.path.insert(0, _ai_team_root)
from _shared.notify import send as _send_telegram
from _shared.env import load_env
PROJECT_ROOT = os.path.abspath(os.path.join(_ai_team_root, "..", ".."))

MEM_FILE = os.path.join(PROJECT_ROOT, "reports", "history", "upload_history.json")

# 에이전트별 파이프라인 정의 (자동 실행 비활성화 - 사용자 지시 시에만 실행)
PIPELINES = {}

def _load_env_keys():
    env_path = os.path.join(PROJECT_ROOT, ".env")
    if os.path.exists(env_path):
        with open(env_path, "r", encoding="utf-8") as f:
            for line in f:
                line = line.strip()
                if not line or line.startswith("#"):
                    continue
                if "=" in line:
                    k, v = line.split("=", 1)
                    os.environ[k.strip()] = v.strip().strip('"').strip("'")

_load_env_keys()





def _load_history() -> list:
    if not os.path.exists(MEM_FILE):
        return []
    with open(MEM_FILE, "r", encoding="utf-8") as f:
        return json.load(f)


def _today_agents(history: list) -> set:
    """오늘 이미 published 기록이 있는 에이전트 집합 반환."""
    today = datetime.date.today().isoformat()
    done = set()
    for rec in history:
        if rec.get("status") in ("published", "evaluated"):
            uploaded_at = rec.get("uploaded_at", "")
            if uploaded_at.startswith(today):
                done.add(rec.get("agent"))
    return done


def check_status() -> dict:
    history = _load_history()
    done = _today_agents(history)
    status = {}
    for agent, cfg in PIPELINES.items():
        status[agent] = "done" if agent in done else "pending"
    return status


def run_missing(dry_run=False):
    status = check_status()
    today = datetime.date.today().strftime("%Y-%m-%d")
    lines = [f"📋 <b>[일일 업로드 현황] {today}</b>"]
    ran = []

    for agent, state in status.items():
        if state == "done":
            lines.append(f"✅ {agent}: 오늘 업로드 완료")
        else:
            lines.append(f"⏳ {agent}: 미실행 → 파이프라인 실행 중...")
            if not dry_run:
                cfg = PIPELINES[agent]
                print(f"\n▶ {agent} 파이프라인 실행 중...")
                result = subprocess.run(
                    [sys.executable, cfg["script"]],
                    cwd=cfg["cwd"],
                    capture_output=False,
                )
                if result.returncode == 0:
                    lines[-1] = f"✅ {agent}: 파이프라인 실행 완료"
                    ran.append(agent)
                else:
                    lines[-1] = f"❌ {agent}: 파이프라인 실패 (코드 {result.returncode})"
            else:
                lines[-1] = f"🔍 [dry-run] {agent}: 실행 대상 확인됨"

    summary = "\n".join(lines)
    print("\n" + summary)
    _send_telegram(summary)
    return ran


def main():
    mode = "--check" if "--check" in sys.argv else "--run"
    dry = "--dry-run" in sys.argv

    print("🤖 영숙 업로드 매니저 가동\n")

    if mode == "--check":
        status = check_status()
        today = datetime.date.today().strftime("%Y-%m-%d")
        print(f"📋 [{today}] 업로드 현황:")
        for agent, state in status.items():
            icon = "✅" if state == "done" else "⏳"
            print(f"  {icon} {agent}: {state}")
    else:
        run_missing(dry_run=dry)


if __name__ == "__main__":
    main()
