#!/usr/bin/env python3
"""예원 - 하네스 & 시스템 전체 관리"""
import os, sys, json, subprocess
from datetime import datetime
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "..", ".."))
from _shared.env import load_env
from _shared.notify import send
from _shared.llm import text as llm

load_env()

class HarnessManager:
    """하네스 & 시스템 관리자"""

    def __init__(self):
        self.harness_dir = Path(__file__).parent.parent.parent / "harness"
        self.ai_team = Path(__file__).parent.parent.parent
        self.reports_dir = self.ai_team.parent.parent / "reports" / "harness"
        self.reports_dir.mkdir(parents=True, exist_ok=True)

    def run_harness(self):
        """하네스 실행"""
        result = subprocess.run(
            [sys.executable, str(self.harness_dir / "check_all.py")],
            capture_output=True,
            text=True
        )
        return result.stdout

    def analyze_structure(self):
        """폴더 구조 분석"""
        issues = []

        # _shared 확인
        shared = self.ai_team / "_shared"
        required_modules = ["env.py", "llm.py", "notify.py", "process.py", "utils.py"]
        for mod in required_modules:
            if not (shared / mod).exists():
                issues.append(f"Missing: _shared/{mod}")

        # 중복 파일 체크
        old_modules = ["env_loader.py", "ollama_client.py", "telegram_notifier.py"]
        for mod in old_modules:
            if (shared / mod).exists():
                issues.append(f"Old module: _shared/{mod}")

        # 빈 __pycache__ 체크
        for pycache in self.ai_team.rglob("__pycache__"):
            issues.append(f"Cache: {pycache.relative_to(self.ai_team)}")

        return issues

    def analyze_logic(self):
        """봇 로직 분석"""
        bots = {
            "데이브 코인": "skills/데이브_주식/tools/upbit_auto_trader.py",
            "데이브 주식": "skills/데이브_주식/tools/stock_auto_trader.py",
            "레오 코인": "skills/레오_트레이더/tools/leo_aggressive_trader.py",
            "현빈 코인": "skills/현빈_전략가/tools/crypto_market_intelligence.py",
            "현빈 주식": "skills/현빈_전략가/tools/stock_market_intelligence.py",
        }

        analysis = {}
        for name, rel_path in bots.items():
            path = self.ai_team / rel_path
            if path.exists():
                lines = len(path.read_text(encoding="utf-8", errors="ignore").splitlines())
                analysis[name] = {"lines": lines, "status": "✅"}
            else:
                analysis[name] = {"status": "❌ Missing"}

        return analysis

    def optimize_suggestions(self, harness_output, structure_issues, logic_analysis):
        """LLM 최적화 제안"""
        prompt = f"""AI Team 시스템 분석 및 개선 제안.

=== 하네스 출력 ===
{harness_output}

=== 구조 이슈 ===
{json.dumps(structure_issues, indent=2, ensure_ascii=False)}

=== 봇 로직 현황 ===
{json.dumps(logic_analysis, indent=2, ensure_ascii=False)}

CEO 관점에서:
1. 즉시 수정 필요한 이슈 (1-3개)
2. 성능 개선 제안 (1-2개)
3. 다음 우선순위 작업

각 항목당 1줄, 총 6줄 이내로 간결하게."""

        try:
            result = llm(prompt, max_tokens=300, temperature=0.3, lm_first=True)
            return result
        except:
            return "LLM 분석 불가"

    def generate_report(self):
        """전체 리포트 생성"""
        print("🔍 [예원] 시스템 전체 분석 중...")

        # 1. 하네스 실행
        harness = self.run_harness()

        # 2. 구조 분석
        structure = self.analyze_structure()

        # 3. 로직 분석
        logic = self.analyze_logic()

        # 4. LLM 제안
        suggestions = self.optimize_suggestions(harness, structure, logic)

        # 리포트 생성
        report = {
            "timestamp": datetime.now().isoformat(),
            "harness_output": harness,
            "structure_issues": structure,
            "bot_analysis": logic,
            "suggestions": suggestions
        }

        # 저장
        report_path = self.reports_dir / f"yewon_analysis_{datetime.now().strftime('%Y%m%d_%H%M')}.json"
        report_path.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")

        print(f"✅ 리포트: {report_path.name}")

        # 요약
        summary = f"""📊 [예원 CEO] 시스템 분석 완료

구조 이슈: {len(structure)}개
봇 상태: {sum(1 for b in logic.values() if '✅' in str(b))} / {len(logic)}

{suggestions}"""

        print(summary)
        send(summary)

        return report

    def cleanup(self):
        """자동 정리"""
        cleaned = []

        # __pycache__ 삭제
        for pycache in self.ai_team.rglob("__pycache__"):
            try:
                import shutil
                shutil.rmtree(pycache)
                cleaned.append(str(pycache.relative_to(self.ai_team)))
            except:
                pass

        if cleaned:
            print(f"🧹 정리: {len(cleaned)}개")

        return cleaned

def main():
    """메인"""
    manager = HarnessManager()

    # 정리
    manager.cleanup()

    # 분석
    manager.generate_report()

if __name__ == "__main__":
    main()
