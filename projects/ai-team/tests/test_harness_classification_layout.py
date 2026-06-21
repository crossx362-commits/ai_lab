import contextlib
import importlib.util
import io
import tempfile
import unittest
from pathlib import Path


AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
CHECK_ALL_PATH = AI_TEAM_ROOT / "harness" / "check_all.py"


def load_check_all_module():
    spec = importlib.util.spec_from_file_location("check_all_under_test", CHECK_ALL_PATH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class HarnessClassificationLayoutTests(unittest.TestCase):
    def test_classification_layout_check_exists_and_runs(self):
        module = load_check_all_module()

        self.assertTrue(hasattr(module, "check_classification_layout"))
        status, message = module.check_classification_layout()

        self.assertIn(status, {"OK", "WARN", "FAIL"})
        self.assertIsInstance(message, str)

    def test_main_reports_classification_layout(self):
        module = load_check_all_module()
        output = io.StringIO()

        with tempfile.TemporaryDirectory() as tmp:
            original_root = module.ROOT
            original_checks = {
                "check_env": module.check_env,
                "check_runtime": module.check_runtime,
                "check_schedule": module.check_schedule,
                "check_trading": module.check_trading,
                "check_structure": module.check_structure,
                "check_classification_layout": module.check_classification_layout,
                "check_report_layout": module.check_report_layout,
                "check_root_layout": module.check_root_layout,
            }
            try:
                module.ROOT = Path(tmp)
                module.check_env = lambda: ("OK", "env")
                module.check_runtime = lambda: ("OK", "runtime")
                module.check_schedule = lambda: ("OK", "schedule")
                module.check_trading = lambda: ("OK", "trading")
                module.check_structure = lambda: ("OK", "structure")
                module.check_classification_layout = lambda: ("OK", "classification")
                module.check_report_layout = lambda: ("OK", "reports")
                module.check_root_layout = lambda: ("OK", "root")

                with contextlib.redirect_stdout(output):
                    module.main()
            finally:
                module.ROOT = original_root
                for name, fn in original_checks.items():
                    setattr(module, name, fn)

        self.assertIn("classification_layout", output.getvalue())

    def test_classification_layout_requires_canonical_bot_scripts(self):
        module = load_check_all_module()

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            ai_team = root / "projects" / "ai-team"
            petnna = root / "projects" / "petnna"

            for path in [
                root / "docs" / "setup",
                root / "docs" / "plans",
                root / "docs" / "archive",
                root / "reports" / "research",
                root / "reports" / "status",
                petnna / "docs",
                ai_team / "docs",
                ai_team / "harness",
                ai_team / "_shared",
                ai_team / "scripts" / "agents",
                ai_team / "skills" / "공용스킬",
                ai_team / "skills" / "데이브_주식",
                ai_team / "assets" / "brain-seeds",
            ]:
                path.mkdir(parents=True, exist_ok=True)

            for path in [
                root / "AGENTS.md",
                root / "PROJECT_OVERVIEW.md",
                root / "README.md",
                root / "CLAUDE.md",
                root / "SKILL.md",
                root / "docs" / "REPOSITORY_CLASSIFICATION.md",
                root / "docs" / "TELEGRAM_BOT_README.md",
                ai_team / "README.md",
                ai_team / "scripts" / "README.md",
                ai_team / "skills" / "README.md",
                ai_team / "harness" / "check_all.py",
                ai_team / "skills" / "데이브_주식" / "SKILL.md",
                petnna / "index.html",
            ]:
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text("", encoding="utf-8")

            original_root = module.ROOT
            original_ai_team = module.AI_TEAM
            original_git_tracked = module.git_tracked
            try:
                module.ROOT = root
                module.AI_TEAM = ai_team
                module.git_tracked = lambda: []

                status, message = module.check_classification_layout()
            finally:
                module.ROOT = original_root
                module.AI_TEAM = original_ai_team
                module.git_tracked = original_git_tracked

        self.assertEqual(status, "FAIL")
        self.assertIn("upbit_auto_trader.py", message)

    def test_classification_layout_warns_for_tracked_plaintext_secrets(self):
        module = load_check_all_module()

        original_git_tracked = module.git_tracked
        try:
            module.git_tracked = lambda: [".env", "client_secret.json"]

            status, message = module.check_classification_layout()
        finally:
            module.git_tracked = original_git_tracked

        self.assertEqual(status, "WARN")
        self.assertIn("tracked plaintext secrets", message)

    def test_classification_layout_warns_for_tracked_ignored_files(self):
        module = load_check_all_module()

        original_git_tracked = module.git_tracked
        original_git_tracked_ignored = module.git_tracked_ignored
        try:
            module.git_tracked = lambda: []
            module.git_tracked_ignored = lambda: ["projects/ai-team/skills/케빈_인프라/tools/supabase/.temp/project-ref"]

            status, message = module.check_classification_layout()
        finally:
            module.git_tracked = original_git_tracked
            module.git_tracked_ignored = original_git_tracked_ignored

        self.assertEqual(status, "WARN")
        self.assertIn("tracked ignored files", message)

    def test_classification_layout_warns_for_project_level_reports(self):
        module = load_check_all_module()

        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            ai_team = root / "projects" / "ai-team"
            petnna = root / "projects" / "petnna"

            required_dirs = [
                root / "docs" / "setup",
                root / "docs" / "plans",
                root / "docs" / "archive",
                root / "reports" / "research",
                root / "reports" / "status",
                petnna / "docs",
                petnna / "api",
                petnna / "css",
                petnna / "js" / "templates",
                ai_team / "docs",
                ai_team / "harness",
                ai_team / "_shared",
                ai_team / "scripts" / "agents",
                ai_team / "skills" / "공용스킬",
                ai_team / "assets" / "brain-seeds",
                root / "projects" / "reports" / "harness",
            ]
            for path in required_dirs:
                path.mkdir(parents=True, exist_ok=True)

            required_files = [
                root / "AGENTS.md",
                root / "PROJECT_OVERVIEW.md",
                root / "README.md",
                root / "CLAUDE.md",
                root / "SKILL.md",
                root / "docs" / "REPOSITORY_CLASSIFICATION.md",
                root / "docs" / "TELEGRAM_BOT_README.md",
                ai_team / "README.md",
                ai_team / "scripts" / "README.md",
                ai_team / "scripts" / "check_holdings.py",
                ai_team / "scripts" / "daily_balance_check.py",
                ai_team / "scripts" / "daily_trading_learning.py",
                ai_team / "scripts" / "start_daily_automation.py",
                ai_team / "scripts" / "start_trading_team.py",
                ai_team / "skills" / "README.md",
                ai_team / "harness" / "check_all.py",
                petnna / "index.html",
                petnna / "sw.js",
                petnna / "js" / "app.js",
                petnna / "js" / "state.js",
                petnna / "js" / "settings.js",
                petnna / "api" / "ai-health.js",
                ai_team / "src" / "extension.ts",
            ]
            for agent, tools in {
                "경수_수사관": ["approval_kyungsoo.py", "comment_forensics.py", "content_inspector.py"],
                "데이브_주식": ["upbit_analyzer.py", "upbit_auto_trader.py", "upbit_public.py"],
                "레오_트레이더": ["leo_aggressive_trader.py"],
                "로율_변호사": ["tax_simulator.py"],
                "영숙_비서": ["calendar_manager.py", "posting_scheduler.py", "reports_manager.py", "schedule_manager.py", "telegram_receiver.py", "upload_approval_flow.py"],
                "예원_CEO": ["evaluate_feedback.py", "skill_auditor.py", "upload_manager.py", "yewon_dispatcher.py"],
                "케빈_인프라": ["petnna_monitor.py", "supabase_manager.py", "sync_env_to_vercel.py", "vercel_manager.py"],
                "코다리_개발자": ["agent_health_check.py", "ollama_health_check.py", "web_init.py", "web_preview.py"],
                "티모_디자이너": ["petnna_reviewer.py"],
                "시그널_분석가": ["market_signal.py"],
                "펄스_애널리스트": ["market_pulse.py"],
            }.items():
                required_files.append(ai_team / "skills" / agent / "SKILL.md")
                for tool in tools:
                    required_files.append(ai_team / "skills" / agent / "tools" / tool)

            for path in required_files:
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text("", encoding="utf-8")
            (root / "projects" / "reports" / "harness" / "old.json").write_text("{}", encoding="utf-8")

            original_root = module.ROOT
            original_ai_team = module.AI_TEAM
            original_git_tracked = module.git_tracked
            try:
                module.ROOT = root
                module.AI_TEAM = ai_team
                module.git_tracked = lambda: []

                status, message = module.check_classification_layout()
            finally:
                module.ROOT = original_root
                module.AI_TEAM = original_ai_team
                module.git_tracked = original_git_tracked

        self.assertEqual(status, "WARN")
        self.assertIn("misplaced reports", message)


if __name__ == "__main__":
    unittest.main()
