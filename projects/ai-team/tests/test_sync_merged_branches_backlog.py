"""sync_merged_branches()가 backlog.json 유령 PR대기를 되돌리는지 검증 — 2026-07-16 발견.

사람이 백로그(미오·나무) 과제 브랜치를 수동 병합·삭제하면 dev_state.json은
sync_merged_branches()가 '완료'로 정리하지만, backlog.json은 그대로 'PR대기'로
남아있었다(나무_20260715110905_1 실사례 — 커밋 158c80f9로 이미 병합됐는데
backlog.json엔 영원히 PR대기). 원인: sync_merged_branches가 dev_state만 갱신하고
backlog.json은 안 건드림.
"""
import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = AI_TEAM_ROOT / "skills" / "수리_개발자" / "tools" / "petnna_dev_engine.py"


def load_engine():
    spec = importlib.util.spec_from_file_location("dev_engine_sync_under_test", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class SyncMergedBranchesBacklogMirrorTests(unittest.TestCase):
    def setUp(self):
        self.eng = load_engine()
        self.backlog_path = Path(tempfile.mkdtemp()) / "backlog.json"
        self.backlog_path.write_text(json.dumps({"items": [
            {"id": "나무_1", "title": "제목", "status": "PR대기"},
        ]}, ensure_ascii=False), encoding="utf-8")
        self.enter = mock.patch.object(self.eng, "BACKLOG", self.backlog_path)
        self.enter.start()
        self.addCleanup(self.enter.stop)
        self.addCleanup(lambda: mock.patch.stopall())

    def _fake_git_branch_missing(self, args, cwd):
        return mock.Mock(returncode=1)

    def test_cleared_backlog_branch_mirrors_to_backlog_json(self):
        state = {"issues": {"나무_1": {"status": "PR대기", "branch": "feat/petnna-나무_1"}}}
        with mock.patch.object(self.eng, "_git", side_effect=self._fake_git_branch_missing), \
             mock.patch.object(self.eng, "save_dev_state"):
            cleared = self.eng.sync_merged_branches(state)
        self.assertEqual(cleared, ["나무_1"])
        items = json.loads(self.backlog_path.read_text(encoding="utf-8"))["items"]
        self.assertEqual(items[0]["status"], "완료",
                         "dev_state는 완료로 정리됐는데 backlog.json은 유령 PR대기로 남으면 안 된다")

    def test_qa_issue_fp_with_no_backlog_match_is_harmless(self):
        # QA 이슈(fp가 백로그 id와 무관한 해시)는 backlog.json에 없으므로 조용히 무시돼야 한다.
        state = {"issues": {"qa_hash_abc": {"status": "PR대기", "branch": "hotfix/petnna-qa_hash_abc"}}}
        with mock.patch.object(self.eng, "_git", side_effect=self._fake_git_branch_missing), \
             mock.patch.object(self.eng, "save_dev_state"):
            cleared = self.eng.sync_merged_branches(state)
        self.assertEqual(cleared, ["qa_hash_abc"])
        items = json.loads(self.backlog_path.read_text(encoding="utf-8"))["items"]
        self.assertEqual(items[0]["status"], "PR대기", "무관한 백로그 항목까지 건드리면 안 된다")


if __name__ == "__main__":
    unittest.main()
