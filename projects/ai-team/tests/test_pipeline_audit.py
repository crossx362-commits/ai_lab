"""예원 펫나 파이프라인 딥 로직 감사 회귀 테스트 — 2026-07-11.

오너 지시("문제는 좀 계속 찾아서 고쳐 지식화 자동화해")로 신설된 정기 감사 도구.
읽기 전용(코드 수정 없음) — LLM 발견은 보고서+텔레그램, 고확신 항목만 백로그에
`보류`(사람 검토)로 적재하고 자동 구현 대상에는 절대 넣지 않는다.
"""
import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools" / "petnna_pipeline_audit.py"


def load():
    spec = importlib.util.spec_from_file_location("pipeline_audit_under_test", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class AppendBacklogTests(unittest.TestCase):
    def setUp(self):
        self.mod = load()
        self.tmp = Path(tempfile.mkdtemp()) / "backlog.json"
        self.tmp.write_text(json.dumps({"items": []}), encoding="utf-8")

    def test_findings_are_appended_as_human_review_hold(self):
        findings = [{"title": "가짜 결함", "file": "x.py", "severity": "P1",
                     "evidence": "...", "trigger": "...", "suggested_fix": "..."}]
        with mock.patch.object(self.mod, "BACKLOG", str(self.tmp)):
            added = self.mod._append_backlog(findings)
        self.assertEqual(added, 1)
        data = json.loads(self.tmp.read_text(encoding="utf-8"))
        item = data["items"][0]
        self.assertEqual(item["status"], "보류", "LLM 추정 결함은 사람 검토 없이 자동 대기로 들어가면 안 된다")
        self.assertEqual(item["owner"], "사람")
        self.assertIn("가짜 결함", item["title"])

    def test_no_findings_appends_nothing(self):
        with mock.patch.object(self.mod, "BACKLOG", str(self.tmp)):
            added = self.mod._append_backlog([])
        self.assertEqual(added, 0)

    def test_duplicate_title_not_appended_twice(self):
        findings = [{"title": "중복 결함", "file": "x.py", "severity": "P2",
                     "evidence": "e", "trigger": "t", "suggested_fix": "s"}]
        with mock.patch.object(self.mod, "BACKLOG", str(self.tmp)):
            self.mod._append_backlog(findings)
            added_again = self.mod._append_backlog(findings)
        self.assertEqual(added_again, 0, "이미 적재된 동일 제목은 중복 적재되면 안 된다")


class RunAuditReportTests(unittest.TestCase):
    def setUp(self):
        self.mod = load()
        self.tmp_out = Path(tempfile.mkdtemp())
        self.tmp_backlog = self.tmp_out / "backlog.json"

    def test_infra_failure_does_not_crash_or_pollute_backlog(self):
        with mock.patch.object(self.mod, "OUT_DIR", str(self.tmp_out)), \
             mock.patch.object(self.mod, "BACKLOG", str(self.tmp_backlog)), \
             mock.patch.object(self.mod, "run_claude", return_value=(False, "인프라 실패")), \
             mock.patch.object(self.mod, "send") as fake_send:
            result = self.mod.run_audit(do_send=True)
        self.assertFalse(result["ok"])
        self.assertEqual(result["findings"], [])
        self.assertEqual(result["backlog_added"], 0)
        fake_send.assert_called_once()

    def test_clean_result_reports_no_findings(self):
        with mock.patch.object(self.mod, "OUT_DIR", str(self.tmp_out)), \
             mock.patch.object(self.mod, "BACKLOG", str(self.tmp_backlog)), \
             mock.patch.object(self.mod, "run_claude", return_value=(True, "[]")), \
             mock.patch.object(self.mod, "send") as fake_send:
            result = self.mod.run_audit(do_send=True)
        self.assertTrue(result["ok"])
        self.assertEqual(result["findings"], [])
        self.assertIn("발견 없음", Path(result["report"]).read_text(encoding="utf-8"))
        fake_send.assert_called_once()


if __name__ == "__main__":
    unittest.main()
