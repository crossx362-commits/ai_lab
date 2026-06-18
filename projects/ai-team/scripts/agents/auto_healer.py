"""
에이전트 자가 수정 시스템 (Auto Healer)
에이전트들의 문제를 자동으로 감지하고 수정

작동 방식:
1. 모든 에이전트 상태 체크
2. 에러 발견 시 자동 수정 시도
3. 수정 불가능하면 텔레그램 알림
"""
import os
import sys
import time
import subprocess
from datetime import datetime

_here = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(_here, "..", ".."))
sys.path.insert(0, PROJECT_ROOT)
sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team"))

from _shared.env_loader import load_env
from _shared.telegram_notifier import send_telegram_message

load_env()

# 에이전트별 헬스 체크 및 자동 수정 설정
AGENTS = {
    "케빈": {
        "health_check": "projects/ai-team/skills/케빈_인프라/tools/petnna_monitor.py health",
        "auto_fix": [
            {
                "error": "GEMINI_API_KEY: EMPTY",
                "fix": "env 파일 확인 및 복원",
                "action": lambda: check_and_fix_env("GEMINI_API_KEY")
            },
            {
                "error": "cffi 백엔드 모듈 누락",
                "fix": "pip install cffi",
                "action": lambda: subprocess.run(["pip", "install", "cffi"], capture_output=True)
            }
        ]
    },
    "현빈": {
        "health_check": "projects/ai-team/skills/현빈_전략가/tools/crypto_market_intelligence.py --status",
        "auto_fix": []
    },
    "데이브": {
        "process_check": "upbit_auto_trader.py",
        "auto_fix": []
    },
    "레오": {
        "process_check": "leo_aggressive_trader.py",
        "auto_fix": []
    }
}

def check_and_fix_env(key: str) -> bool:
    """환경 변수 체크 및 자동 수정"""
    env_path = os.path.join(PROJECT_ROOT, ".env")

    # .env 파일 존재 체크
    if not os.path.exists(env_path):
        print(f"❌ .env 파일 없음, 복원 시도...")

        # .env.encrypted 확인
        encrypted_path = env_path + ".encrypted"
        backup_path = encrypted_path + ".backup"

        if os.path.exists(backup_path):
            os.rename(backup_path, encrypted_path)
            print(f"✅ .env.encrypted 복원 완료")
            return True

        print(f"❌ 복원 실패: 백업 파일 없음")
        return False

    # 키 존재 체크
    with open(env_path, "r", encoding="utf-8") as f:
        for line in f:
            if line.startswith(f"{key}="):
                value = line.split("=", 1)[1].strip().strip('"').strip("'")
                if value:
                    print(f"✅ {key} 정상")
                    return True
                else:
                    print(f"⚠️ {key} 값이 비어있음")
                    return False

    print(f"❌ {key} 키 없음")
    return False

def run_agent_health_check(agent_name: str, config: dict) -> dict:
    """에이전트 헬스 체크 실행"""
    result = {
        "agent": agent_name,
        "status": "unknown",
        "errors": [],
        "timestamp": datetime.now().isoformat()
    }

    # 헬스 체크 명령 실행
    if "health_check" in config:
        cmd = config["health_check"].split()
        script_path = os.path.join(PROJECT_ROOT, cmd[0])

        try:
            proc = subprocess.run(
                ["python", script_path] + cmd[1:],
                capture_output=True,
                text=True,
                timeout=30,
                env={**os.environ, "PYTHONUTF8": "1"}
            )

            output = proc.stdout + proc.stderr

            # 에러 패턴 체크
            for fix_rule in config.get("auto_fix", []):
                if fix_rule["error"] in output:
                    result["errors"].append({
                        "error": fix_rule["error"],
                        "fix": fix_rule["fix"],
                        "fixable": True
                    })

            if "정상" in output or "OK" in output:
                result["status"] = "healthy"
            elif result["errors"]:
                result["status"] = "unhealthy"
            else:
                result["status"] = "unknown"

        except subprocess.TimeoutExpired:
            result["status"] = "timeout"
            result["errors"].append({"error": "헬스 체크 타임아웃", "fixable": False})
        except Exception as e:
            result["status"] = "error"
            result["errors"].append({"error": str(e), "fixable": False})

    return result

def auto_fix_agent(agent_name: str, config: dict, errors: list) -> list:
    """에이전트 자동 수정"""
    fixed = []
    failed = []

    for error in errors:
        if not error.get("fixable"):
            failed.append(error)
            continue

        print(f"🔧 [{agent_name}] 수정 시도: {error['error']}")
        print(f"   방법: {error['fix']}")

        # 자동 수정 액션 찾기
        for fix_rule in config.get("auto_fix", []):
            if fix_rule["error"] == error["error"]:
                try:
                    result = fix_rule["action"]()
                    if result is True or (hasattr(result, 'returncode') and result.returncode == 0):
                        fixed.append(error)
                        print(f"✅ [{agent_name}] 수정 완료: {error['error']}")
                    else:
                        failed.append(error)
                        print(f"❌ [{agent_name}] 수정 실패: {error['error']}")
                except Exception as e:
                    failed.append(error)
                    print(f"❌ [{agent_name}] 수정 오류: {e}")
                break

    return fixed, failed

def run_auto_healer(notify: bool = True) -> dict:
    """전체 에이전트 자동 수정 실행"""
    print("=" * 60)
    print("🏥 에이전트 자가 수정 시스템")
    print("=" * 60)
    print(f"시작 시간: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print()

    results = {
        "total_agents": len(AGENTS),
        "healthy": 0,
        "fixed": 0,
        "failed": 0,
        "details": []
    }

    for agent_name, config in AGENTS.items():
        print(f"🔍 [{agent_name}] 헬스 체크 중...")

        health = run_agent_health_check(agent_name, config)

        if health["status"] == "healthy":
            print(f"✅ [{agent_name}] 정상")
            results["healthy"] += 1
        elif health["errors"]:
            print(f"⚠️ [{agent_name}] 문제 발견: {len(health['errors'])}개")

            # 자동 수정 시도
            fixed, failed = auto_fix_agent(agent_name, config, health["errors"])

            results["fixed"] += len(fixed)
            results["failed"] += len(failed)

            health["fixed_errors"] = fixed
            health["failed_errors"] = failed
        else:
            print(f"❓ [{agent_name}] 상태 불명")

        results["details"].append(health)
        print()

    # 요약
    print("=" * 60)
    print("📊 수정 결과 요약")
    print("=" * 60)
    print(f"전체 에이전트: {results['total_agents']}")
    print(f"정상: {results['healthy']}")
    print(f"수정 완료: {results['fixed']}")
    print(f"수정 실패: {results['failed']}")

    # 텔레그램 알림
    if notify and results["failed"] > 0:
        message = f"""🏥 에이전트 자동 수정 리포트

✅ 정상: {results['healthy']}
🔧 수정 완료: {results['fixed']}
❌ 수정 실패: {results['failed']}

수동 확인 필요한 에이전트:
"""
        for detail in results["details"]:
            if detail.get("failed_errors"):
                message += f"\n• {detail['agent']}: {len(detail['failed_errors'])}개 문제"

        send_telegram_message(message)

    return results

def daemon_mode(interval_minutes: int = 30):
    """데몬 모드: 주기적으로 자동 수정 실행"""
    print(f"🔄 데몬 모드 시작 (체크 간격: {interval_minutes}분)")

    while True:
        try:
            run_auto_healer(notify=True)
        except Exception as e:
            print(f"❌ 오류 발생: {e}")
            send_telegram_message(f"🚨 Auto Healer 오류: {e}")

        print(f"\n⏸️ {interval_minutes}분 대기 중...\n")
        time.sleep(interval_minutes * 60)

if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(description="에이전트 자가 수정 시스템")
    parser.add_argument("--daemon", action="store_true", help="데몬 모드로 실행")
    parser.add_argument("--interval", type=int, default=30, help="체크 간격 (분)")
    parser.add_argument("--no-notify", action="store_true", help="텔레그램 알림 끄기")

    args = parser.parse_args()

    if args.daemon:
        daemon_mode(args.interval)
    else:
        run_auto_healer(notify=not args.no_notify)
