"""병합 충돌 시 mid-merge 잔재 원상복구 회귀 테스트 — 2026-07-19 실사고를 굳힌다.

사고: 예원 PR 리뷰어가 feat/petnna-나무_20260717110713_1을 master에 병합하다
index.html 충돌로 실패했는데, `git merge --abort` 없이 반환만 해서 충돌 마커
(<<<<<<< HEAD)가 박힌 작업트리가 그대로 방치됐다. 이후 세션이 그 파일을 편집·
커밋하면서 문서 두 벌이 통째로 섞인 index.html이 배포 라인에 실렸다.

수리: 예원 `_merge()`·수리 엔진 자동병합 실패 분기 둘 다 실패 시 즉시
`git merge --abort`로 원상복구. 이 테스트는 실제 임시 git 저장소에 진짜 충돌을
만들어 예원 `_merge()`가 ①실패를 보고하고 ②MERGE_HEAD를 남기지 않으며
③작업트리를 깨끗하게 되돌리는지 검증한다.
"""
import importlib.util
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
REVIEWER = AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools" / "petnna_pr_reviewer.py"


def load_reviewer():
    sys.path.insert(0, str(AI_TEAM_ROOT / "skills" / "수리_개발자" / "tools"))
    spec = importlib.util.spec_from_file_location("pr_reviewer_under_test", REVIEWER)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def _git(repo: Path, *args) -> subprocess.CompletedProcess:
    return subprocess.run(["git", *args], cwd=str(repo), capture_output=True, text=True)


def make_conflicting_repo() -> Path:
    """master와 feature 브랜치가 같은 줄을 다르게 고쳐 병합이 반드시 충돌하는 저장소."""
    repo = Path(tempfile.mkdtemp()) / "repo"
    repo.mkdir()
    _git(repo, "init", "-b", "master")
    _git(repo, "config", "user.email", "t@t")
    _git(repo, "config", "user.name", "t")
    f = repo / "projects" / "petnna" / "index.html"
    f.parent.mkdir(parents=True)
    f.write_text("base\n", encoding="utf-8")
    _git(repo, "add", "-A")
    _git(repo, "commit", "-m", "base")
    _git(repo, "checkout", "-b", "feature")
    f.write_text("feature side\n", encoding="utf-8")
    _git(repo, "commit", "-am", "feature")
    _git(repo, "checkout", "master")
    f.write_text("master side\n", encoding="utf-8")
    _git(repo, "commit", "-am", "master")
    return repo


class MergeConflictAbortTests(unittest.TestCase):
    def setUp(self):
        self.reviewer = load_reviewer()
        self.repo = make_conflicting_repo()

    def test_conflicted_merge_reports_failure_and_restores_clean_tree(self):
        with mock.patch.object(self.reviewer.eng, "PROJECT_ROOT", self.repo):
            ok, note = self.reviewer._merge("feature")
        self.assertFalse(ok, "충돌 병합이 성공으로 보고되면 안 된다")
        self.assertIn("병합 실패", note)
        self.assertFalse((self.repo / ".git" / "MERGE_HEAD").exists(),
                         "abort가 안 돼 mid-merge 상태(MERGE_HEAD)가 방치됨")
        status = _git(self.repo, "status", "--porcelain").stdout.strip()
        self.assertEqual(status, "", f"작업트리가 원상복구되지 않음: {status!r}")
        content = (self.repo / "projects" / "petnna" / "index.html").read_text(encoding="utf-8")
        self.assertNotIn("<<<<<<<", content, "충돌 마커가 파일에 남아 있음")
        self.assertEqual(content, "master side\n", "파일이 master 원본으로 복구되지 않음")

    def test_clean_merge_still_succeeds(self):
        # 충돌 없는 병합은 기존대로 성공해야 한다(가드가 정상 경로를 깨지 않는지).
        repo = Path(tempfile.mkdtemp()) / "repo"
        repo.mkdir()
        _git(repo, "init", "-b", "master")
        _git(repo, "config", "user.email", "t@t")
        _git(repo, "config", "user.name", "t")
        (repo / "a.txt").write_text("base\n", encoding="utf-8")
        _git(repo, "add", "-A")
        _git(repo, "commit", "-m", "base")
        _git(repo, "checkout", "-b", "feature")
        (repo / "b.txt").write_text("new\n", encoding="utf-8")
        _git(repo, "add", "-A")
        _git(repo, "commit", "-m", "feature")
        _git(repo, "checkout", "master")
        with mock.patch.object(self.reviewer.eng, "PROJECT_ROOT", repo):
            ok, note = self.reviewer._merge("feature")
        self.assertTrue(ok, f"무충돌 병합이 실패함: {note}")


if __name__ == "__main__":
    unittest.main()
