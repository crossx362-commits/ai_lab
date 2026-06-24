#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
코다리: 에이전트 시스템 자동 모니터링 & 오류 수정
- 5분마다 모든 에이전트 상태 체크
- 오류 발견 시 자동 재시작 + 텔레그램 알림
- 로그 분석 & 에러 패턴 수집
"""
import os
import sys
import time
import subprocess
from datetime import datetime
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

SCRIPT_DIR = Path(__file__).resolve().parent
AI_TEAM_ROOT = SCRIPT_DIR.parents[2]
WORKSPACE_ROOT = AI_TEAM_ROOT.parents[1]
sys.path.insert(0, str(AI_TEAM_ROOT))

from _shared.env import load_env
from _shared.notify import send
from _shared.process import ProcessLock

load_env(str(WORKSPACE_ROOT))


class AgentHealthMonitor:
    """에이전트 헬스 체크 & 자동 복구"""

    AGENTS = {
        "leo": {
            "name": "레오",
            "script": "projects/ai-team/skills/레오_트레이더/tools/leo_aggressive_trader.py",
            "log": "output/trading_logs/leo_daemon.out.log",
            "launchd": "com.ailab.leo",
            "critical": True,
            "max_restart": 3,
        },
        "dave": {
            "name": "데이브",
            "script": "projects/ai-team/skills/데이브_주식/tools/upbit_auto_trader.py",
            "log": "output/trading_logs/dave_daemon.out.log",
            "launchd": "com.ailab.dave",
            "critical": True,
            "max_restart": 3,
        },
        "signal": {
            "name": "시그널",
            "script": "projects/ai-team/skills/시그널_분석가/tools/market_signal.py",
            "log": "output/trading_logs/signal_daemon.out.log",
            "launchd": "com.ailab.signal",
            "critical": True,
            "max_restart": 3,
        },
        "youngsuk": {
            "name": "영숙",
            "script": "projects/ai-team/skills/영숙_비서/tools/telegram_receiver.py",
            "log": "output/trading_logs/youngsuk_daemon.out.log",
            "launchd": "com.ailab.youngsuk",
            "critical": True,
            "max_restart": 3,
        },
    }

    ERROR_PATTERNS = [
        "Traceback",
        "Exception",
        "Error:",
        "Failed",
        "Timeout",
        "Connection refused",
        "API key",
        "Permission denied",
    ]

    def __init__(self):
        self.restart_count = {agent: 0 for agent in self.AGENTS}
        self.last_check = {}
        self.error_history = []

    def is_process_running(self, script_name: str) -> bool:
        """프로세스 실행 여부 확인"""
        try:
            if os.name == "nt":  # Windows
                result = subprocess.run(
                    ["powershell", "-Command",
                     f"Get-CimInstance Win32_Process | Where-Object {{$_.CommandLine -like '*{script_name}*'}} | Select-Object ProcessId"],
                    capture_output=True,
                    text=True,
                    timeout=10
                )
                return "ProcessId" in result.stdout and len(result.stdout.strip().split("\n")) > 2
            else:  # Unix/macOS
                result = subprocess.run(
                    ["pgrep", "-f", script_name],
                    capture_output=True,
                    text=True,
                    timeout=10
                )
                return bool(result.stdout.strip())
        except Exception as e:
            print(f"[코다리] 프로세스 체크 실패: {e}")
            return False

    def check_log_errors(self, log_path: Path, tail_lines: int = 50) -> list:
        """로그에서 에러 패턴 검색"""
        if not log_path.exists():
            return []

        errors = []
        try:
            with open(log_path, "r", encoding="utf-8", errors="ignore") as f:
                lines = f.readlines()
                recent_lines = lines[-tail_lines:] if len(lines) > tail_lines else lines

                for i, line in enumerate(recent_lines):
                    for pattern in self.ERROR_PATTERNS:
                        if pattern in line:
                            context = "".join(recent_lines[max(0, i-2):min(len(recent_lines), i+3)])
                            errors.append({
                                "pattern": pattern,
                                "line": line.strip(),
                                "context": context,
                                "timestamp": datetime.now().isoformat()
                            })
                            break
        except Exception as e:
            print(f"[코다리] 로그 읽기 실패 {log_path}: {e}")

        return errors

    def check_telegram_connection(self) -> bool:
        """텔레그램 연결 상태 확인 (메시지 발송 없이 API 상태만 조회)"""
        try:
            bot_token = os.getenv("TELEGRAM_BOT_TOKEN")
            if not bot_token:
                return False
            import urllib.request
            import json
            url = f"https://api.telegram.org/bot{bot_token}/getMe"
            with urllib.request.urlopen(url, timeout=5) as response:
                data = json.loads(response.read())
                return bool(data.get("ok"))
        except Exception as e:
            print(f"[코다리] 텔레그램 체크 실패: {e}")
            return False

    def check_log_activity(self, log_path: Path, max_age_minutes: int = 30) -> bool:
        """로그 파일 최근 활동 확인"""
        if not log_path.exists():
            return False

        try:
            import time
            mtime = log_path.stat().st_mtime
            age_minutes = (time.time() - mtime) / 60
            return age_minutes < max_age_minutes
        except Exception:
            return False

    def restart_agent(self, agent_id: str, config: dict) -> bool:
        """에이전트 재시작 — launchd kickstart 우선 사용"""
        print(f"[코다리] 🔄 {config['name']} 재시작 중...")

        try:
            # macOS: launchctl kickstart로 launchd가 직접 재시작 (올바른 환경/launcher 사용)
            launchd_label = config.get("launchd")
            if os.name != "nt" and launchd_label:
                import pwd
                uid = pwd.getpwnam(os.environ.get("USER", "junholee")).pw_uid
                result = subprocess.run(
                    ["launchctl", "kickstart", "-k", f"gui/{uid}/{launchd_label}"],
                    capture_output=True, text=True, timeout=15
                )
                time.sleep(4)
                if self.is_process_running(config["script"]):
                    print(f"[코다리] ✅ {config['name']} launchctl 재시작 성공")
                    self.restart_count[agent_id] += 1
                    return True
                else:
                    print(f"[코다리] ⚠️ {config['name']} launchctl 실패, subprocess 시도...")

            # 폴백: 직접 subprocess (Windows 또는 launchd 없는 경우)
            script_path = WORKSPACE_ROOT / config["script"]
            if not script_path.exists():
                print(f"[코다리] ❌ {config['name']} 스크립트 없음: {script_path}")
                return False

            if os.name == "nt":
                subprocess.Popen(
                    ["python", str(script_path)],
                    cwd=str(WORKSPACE_ROOT),
                    creationflags=subprocess.CREATE_NO_WINDOW
                )
            else:
                subprocess.Popen(
                    [sys.executable, str(script_path)],
                    cwd=str(WORKSPACE_ROOT),
                    stdout=subprocess.DEVNULL,
                    stderr=subprocess.DEVNULL
                )

            time.sleep(3)
            if self.is_process_running(config["script"]):
                print(f"[코다리] ✅ {config['name']} subprocess 재시작 성공")
                self.restart_count[agent_id] += 1
                return True
            else:
                print(f"[코다리] ❌ {config['name']} 재시작 실패")
                return False

        except Exception as e:
            print(f"[코다리] ❌ {config['name']} 재시작 에러: {e}")
            return False

    def check_agent(self, agent_id: str, config: dict) -> dict:
        """에이전트 상태 체크"""
        status = {
            "agent": agent_id,
            "name": config["name"],
            "running": False,
            "errors": [],
            "action": None,
        }

        # 프로세스 체크
        status["running"] = self.is_process_running(config["script"])

        # 로그 활동 체크 (30분 이상 업데이트 없으면 문제)
        log_path = WORKSPACE_ROOT / config["log"]
        log_active = self.check_log_activity(log_path, max_age_minutes=30)

        # 로그 에러 체크
        errors = self.check_log_errors(log_path)
        status["errors"] = errors

        # 복구 액션
        if not status["running"]:
            # 프로세스 다운
            if self.restart_count[agent_id] < config["max_restart"]:
                print(f"[코다리] {config['name']} 다운 감지 - 자동 재시작")
                if self.restart_agent(agent_id, config):
                    status["action"] = "restarted"
                else:
                    status["action"] = "restart_failed"
            else:
                status["action"] = "max_restart_reached"

        elif not log_active and agent_id != "youngsuk":
            # 프로세스는 살아있지만 로그가 오래됨 (영숙 제외)
            print(f"[코다리] {config['name']} 로그 활동 없음 - 재시작")
            if self.restart_agent(agent_id, config):
                status["action"] = "restarted_inactive"

        elif errors and len(errors) > 3:
            # 에러가 많으면 재시작
            print(f"[코다리] {config['name']} 다수 에러 감지 - 재시작")
            if self.restart_agent(agent_id, config):
                status["action"] = "restarted_errors"

        return status

    def run_health_check(self):
        """전체 에이전트 헬스 체크 및 자동 복구"""
        print(f"\n{'='*60}")
        print(f"[코다리] 에이전트 헬스 체크: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
        print(f"{'='*60}\n")

        results = []
        actions_taken = []

        for agent_id, config in self.AGENTS.items():
            status = self.check_agent(agent_id, config)
            results.append(status)

            # 상태 출력
            running_icon = "✅" if status["running"] else "❌"
            error_count = len(status["errors"])
            error_icon = f"⚠️ {error_count}" if error_count > 0 else "🟢"

            print(f"{running_icon} {config['name']:8s} | 에러: {error_icon} | 액션: {status['action'] or 'none'}")

            if status["action"]:
                actions_taken.append(f"{config['name']}: {status['action']}")

        # 텔레그램 연결 체크 (5분마다 1회)
        if len(self.error_history) % 5 == 0:
            if not self.check_telegram_connection():
                print(f"[코다리] ⚠️ 텔레그램 연결 실패")

        print(f"\n{'='*60}\n")

        # 액션 요약 알림 — 문제 해결 후 요약 하나만 전송
        if actions_taken:
            action_labels = {
                "restarted": "재시작 완료",
                "restarted_inactive": "비활성→복구 완료",
                "restarted_errors": "에러감지→재시작 완료",
                "restart_failed": "⚠️ 재시작 실패",
                "max_restart_reached": "🚨 최대 재시작 초과",
            }
            lines = []
            for r in results:
                if r["action"]:
                    label = action_labels.get(r["action"], r["action"])
                    lines.append(f"• {r['name']}: {label}")
            summary = "✅ [코다리] 자동 복구 완료\n" + "\n".join(lines)
            print(f"[코다리] 자동 복구 수행:\n{summary}")
            send(summary)

        return results

    def check_trading_issues(self):
        """트레이딩 문제 자동 체크 및 수정"""
        try:
            sys.path.insert(0, str(AI_TEAM_ROOT / "skills/코다리_개발자/tools"))
            from auto_fix_trading_issues import check_and_fix_positions

            issues, actions = check_and_fix_positions()
            if issues > 0 or actions > 0:
                print(f"[코다리] 트레이딩 문제: {issues}개 감지, {actions}개 수정")
        except Exception as e:
            print(f"[코다리] 트레이딩 체크 실패: {e}")

    def start_monitoring(self, interval: int = 300):
        """모니터링 데몬 시작 (기본 5분 간격)"""
        print(f"[코다리] 에이전트 모니터링 시작 (간격: {interval}초)")
        send("👀 [코다리] 자동 모니터링 시작")

        iteration = 0
        while True:
            try:
                self.run_health_check()

                # 10분마다 트레이딩 문제 체크 (iteration % 2 == 0)
                if iteration % 2 == 0:
                    self.check_trading_issues()

                iteration += 1
                time.sleep(interval)
            except KeyboardInterrupt:
                print("\n[코다리] 모니터링 종료")
                send("👋 [코다리] 모니터링 종료")
                break
            except Exception as e:
                print(f"[코다리] 모니터링 에러: {e}")
                time.sleep(60)


def main():
    """메인 실행"""
    # 중복 실행 방지
    with ProcessLock("kodari_monitor"):
        monitor = AgentHealthMonitor()

        # 단발 체크 (--once 옵션)
        if len(sys.argv) > 1 and sys.argv[1] == "--once":
            monitor.run_health_check()
        else:
            # 데몬 모드
            monitor.start_monitoring(interval=300)  # 5분


if __name__ == "__main__":
    main()
