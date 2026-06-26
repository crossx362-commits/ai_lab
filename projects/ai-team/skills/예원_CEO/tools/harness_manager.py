#!/usr/bin/env python3
"""예원 - 하네스 및 현재 ai-team 런타임 관리."""
from __future__ import annotations

import json
import os
import shutil
import subprocess
import sys
from datetime import datetime
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

_here = Path(__file__).resolve().parent
AI_TEAM = _here.parents[2]
PROJECT_ROOT = AI_TEAM.parents[1]
sys.path.insert(0, str(AI_TEAM))

from _shared.env import load_env
from _shared.notify import agent_status, send

load_env(str(PROJECT_ROOT))


class HarnessManager:
    """Run the harness and summarize current installed agents."""

    def __init__(self):
        self.harness = AI_TEAM / "harness" / "check_all.py"
        self.reports_dir = PROJECT_ROOT / "reports" / "harness"
        self.reports_dir.mkdir(parents=True, exist_ok=True)

    def run_harness(self) -> str:
        result = subprocess.run(
            [sys.executable, str(self.harness)],
            cwd=str(AI_TEAM),
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=120,
            env={**os.environ, "PYTHONUTF8": "1"},
        )
        return result.stdout or result.stderr or ""

    def analyze_structure(self) -> list[str]:
        issues = []
        for rel in [
            "_shared/env.py",
            "_shared/llm.py",
            "_shared/notify.py",
            "_shared/process.py",
            "skills/소미_분석가/SKILL.md",
            "skills/영숙_비서/SKILL.md",
            "skills/예원_CEO/SKILL.md",
        ]:
            if not (AI_TEAM / rel).exists():
                issues.append(f"Missing: {rel}")
        return issues

    def generate_report(self) -> dict:
        print("[예원] 시스템 분석 중...")
        harness_output = self.run_harness()
        structure_issues = self.analyze_structure()
        runtime = agent_status()

        report = {
            "timestamp": datetime.now().isoformat(timespec="seconds"),
            "harness_output": harness_output,
            "structure_issues": structure_issues,
            "runtime": runtime,
        }
        report_path = self.reports_dir / f"yewon_analysis_{datetime.now().strftime('%Y%m%d_%H%M')}.json"
        report_path.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")

        summary = [
            "[예원 CEO] 시스템 분석 완료",
            f"구조 이슈: {len(structure_issues)}개",
            "런타임: " + ", ".join(f"{k}={v}" for k, v in runtime.items()),
            f"보고서: {report_path}",
        ]
        print("\n".join(summary))
        if structure_issues or "WARN" in harness_output or "FAIL" in harness_output:
            send("\n".join(summary), silent=True)
        return report

    def cleanup(self) -> list[str]:
        cleaned = []
        for pycache in AI_TEAM.rglob("__pycache__"):
            try:
                shutil.rmtree(pycache)
                cleaned.append(str(pycache.relative_to(AI_TEAM)))
            except OSError:
                pass
        if cleaned:
            print(f"정리: {len(cleaned)}개")
        return cleaned


def main():
    manager = HarnessManager()
    manager.cleanup()
    manager.generate_report()


if __name__ == "__main__":
    main()
