"""병합했지만 브랜치를 안 지운 경우도 '완료'로 정리되는가 (2026-08-04 실측 사고).

무슨 일이 있었나: 사람이 수리의 PR대기 브랜치를 검토해 `git merge`로 병합했는데
브랜치를 지우지 않았다. 3분 뒤 예원 PR 리뷰어가 그 브랜치를 집어
`git diff master...branch`가 비었다는 이유로 **"빈 diff(변경 없음)" 반려**를 때렸다.
멀쩡히 병합되고 배포까지 된 작업이 dev_state에 '보류'로, 백로그에도 '보류'로
기록되고 시도 횟수(attempts)만 하나 축났다.

원인: sync_merged_branches가 '브랜치가 사라졌는가'만 봤다. 병합 판정은 diff나
브랜치 존재 여부가 아니라 **조상 관계**로 해야 한다 — 병합된 브랜치의 팁은
언제나 master의 조상이다.

여기서 굳히는 것:
  1. 팁이 master의 조상이면(=병합됨) 브랜치가 남아 있어도 완료 처리하고 지운다.
  2. 병합되지 않은 브랜치는 절대 건드리지 않는다 — 이 단언이 깨지면 검토 대기
     중인 실제 구현이 '완료'로 위장되고 브랜치까지 사라진다.
"""
import importlib.util
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_TEAM_ROOT))

_ENGINE = AI_TEAM_ROOT / "skills" / "수리_개발자" / "tools" / "petnna_dev_engine.py"


def _load_engine(root: Path):
    spec = importlib.util.spec_from_file_location("dev_engine_sync_t", _ENGINE)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    mod.PROJECT_ROOT = root
    mod.BACKLOG = root / "backlog.json"
    mod.STATE = root / "dev_state.json"
    mod._update_backlog = lambda *a, **k: None   # 백로그 경로는 이 테스트의 관심사가 아니다
    mod.save_dev_state = lambda s: None
    return mod


class SyncMergedButUndeletedTests(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        self.git("init", "-q", "-b", "master")
        self.git("config", "user.email", "t@t.t")
        self.git("config", "user.name", "t")
        (self.root / "a.txt").write_text("base")
        self.git("add", "-A"); self.git("commit", "-qm", "base")

        # 병합된 브랜치(팁이 master의 조상)
        self.git("checkout", "-qb", "feat/petnna-merged")
        (self.root / "b.txt").write_text("work")
        self.git("add", "-A"); self.git("commit", "-qm", "work")
        self.git("checkout", "-q", "master")
        self.git("merge", "-q", "--no-edit", "feat/petnna-merged")

        # 병합 안 된 브랜치(검토 대기 중인 진짜 작업)
        self.git("checkout", "-qb", "feat/petnna-open")
        (self.root / "c.txt").write_text("pending")
        self.git("add", "-A"); self.git("commit", "-qm", "pending work")
        self.git("checkout", "-q", "master")

        self.mod = _load_engine(self.root)

    def tearDown(self):
        self._tmp.cleanup()

    def git(self, *args):
        return subprocess.run(["git", *args], cwd=self.root, capture_output=True, text=True)

    def _state(self):
        return {"issues": {
            "task_merged": {"status": "PR대기", "branch": "feat/petnna-merged"},
            "task_open":   {"status": "PR대기", "branch": "feat/petnna-open"},
        }}

    def test_merged_but_undeleted_branch_is_cleared(self):
        st = self._state()
        cleared = self.mod.sync_merged_branches(st)
        self.assertIn("task_merged", cleared,
                      "병합됐는데 브랜치가 남아 있다는 이유로 PR대기에 머물렀다 — "
                      "예원 리뷰어가 '빈 diff'로 오반려한다")
        self.assertEqual(st["issues"]["task_merged"]["status"], "완료")
        self.assertNotIn("feat/petnna-merged", self.git("branch", "--list").stdout,
                         "완료 처리했으면 잔재 브랜치도 지워야 한다")

    def test_unmerged_branch_is_untouched(self):
        st = self._state()
        cleared = self.mod.sync_merged_branches(st)
        self.assertNotIn("task_open", cleared,
                         "병합 안 된 브랜치를 완료로 처리했다 — 검토 대기 중인 구현이 증발한다")
        self.assertEqual(st["issues"]["task_open"]["status"], "PR대기")
        self.assertIn("feat/petnna-open", self.git("branch", "--list").stdout,
                      "병합 안 된 브랜치를 지웠다")

    def test_deleted_branch_still_cleared(self):
        """기존 동작(병합 후 삭제) 회귀 방지."""
        self.git("branch", "-D", "feat/petnna-merged")
        st = self._state()
        cleared = self.mod.sync_merged_branches(st)
        self.assertIn("task_merged", cleared)
        self.assertEqual(st["issues"]["task_merged"]["status"], "완료")


if __name__ == "__main__":
    unittest.main()
