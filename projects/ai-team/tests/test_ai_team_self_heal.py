"""ai-team 코드 자기수정 회귀 테스트 — 2026-08-08.

오너 지시: "코드 자기수정은 문제가 생길경우 나에게 보고하지 않고 알아서 수정하는거
... 그래 그렇게해." petnna 앱과 달리 ai-team 코드에는 다층 검증이 없다 — 여기가
안전망 자체이기 때문이다. 그래서 안전선 두 개를 굳힌다: ①안전망 파일 접촉 시
검토로 보낸다 ②projects/petnna(수리 전용) 접촉 시 검토로 보낸다.

이 테스트는 실제 git worktree·클로드 CLI를 쓴다(모킹하면 git 명령·경로 계산 같은
진짜 실패 지점을 못 잡는다 — 2026-07-12 유휴 디스패치 사고의 교훈). run_claude만
가짜로 주입해 헤드리스 API 호출을 피한다.
"""
import importlib.util
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
if str(AI_TEAM_ROOT) not in sys.path:
    sys.path.insert(0, str(AI_TEAM_ROOT))
SCRIPT = AI_TEAM_ROOT / "harness" / "ai_team_self_heal.py"


def load():
    spec = importlib.util.spec_from_file_location("self_heal_under_test", SCRIPT)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def _git(args, cwd):
    return subprocess.run(["git", *args], cwd=cwd, capture_output=True, text=True)


def _fake_repo():
    """수정 대상이 될 최소 가짜 저장소 — 실패하는 테스트 하나를 심어둔다."""
    repo = Path(tempfile.mkdtemp())
    tests = repo / "projects" / "ai-team" / "tests"
    tests.mkdir(parents=True)
    (repo / "projects" / "ai-team" / "buggy.py").write_text(
        "def add(a, b):\n    return a - b   # 버그\n", encoding="utf-8")
    (tests / "test_buggy.py").write_text(
        "import sys, importlib.util\nfrom pathlib import Path\n"
        "spec = importlib.util.spec_from_file_location('b', "
        "Path(__file__).resolve().parents[1] / 'buggy.py')\n"
        "b = importlib.util.module_from_spec(spec); spec.loader.exec_module(b)\n"
        "import unittest\n"
        "class T(unittest.TestCase):\n"
        "    def test_add(self): self.assertEqual(b.add(2, 3), 5)\n",
        encoding="utf-8")
    _git(["init", "-q", "-b", "master"], repo)
    _git(["config", "user.email", "t@t"], repo)
    _git(["config", "user.name", "t"], repo)
    _git(["add", "-A"], repo)
    _git(["commit", "-qm", "base"], repo)
    return repo


class ScopeChecksTests(unittest.TestCase):
    """순수 함수라 격리 없이 바로 검증 — 이게 안전선의 핵심 판정 로직이다."""

    def setUp(self):
        self.mod = load()

    def test_safety_net_file_is_flagged(self):
        hit = self.mod.touches_safety_net(["projects/ai-team/_shared/backlog.py"])
        self.assertEqual(hit, ["projects/ai-team/_shared/backlog.py"])

    def test_ordinary_ai_team_file_is_not_flagged(self):
        self.assertEqual(self.mod.touches_safety_net(
            ["projects/ai-team/skills/나무_기획/tools/petnna_product_manager.py"]), [])

    def test_petnna_app_code_is_out_of_scope(self):
        """수리 전용 영역이다 — 자기수정이 여길 건드리면 안 된다."""
        self.assertEqual(self.mod.out_of_scope(["projects/petnna/js/app.js"]),
                         ["projects/petnna/js/app.js"])

    def test_ai_team_file_is_in_scope(self):
        self.assertEqual(self.mod.out_of_scope(["projects/ai-team/skills/x.py"]), [])


class AttemptOrchestrationTests(unittest.TestCase):
    """attempt()의 세 안전선(안전망·범위·회귀 재확인)이 실제 워크트리에서 작동하는지."""

    def setUp(self):
        self.mod = load()
        self.repo = _fake_repo()
        self._orig_root = self.mod.PROJECT_ROOT
        self._orig_wt = self.mod.WT_BASE
        self.mod.PROJECT_ROOT = self.repo
        self.mod.WT_BASE = self.repo / "output" / "cache" / "self_heal"

    def tearDown(self):
        self.mod.PROJECT_ROOT = self._orig_root
        self.mod.WT_BASE = self._orig_wt

    def _fake_claude(self, edit_fn):
        """워크트리 경로를 받아 파일을 고치는 흉내를 낸다."""
        def _run(prompt, cwd, timeout=0, allowed_tools="", permission_mode=""):
            edit_fn(Path(cwd))
            return True, "수정함"
        return _run

    def test_no_edit_is_not_healed(self):
        res = self.mod.attempt("test_buggy.T.test_add", run_claude_fn=self._fake_claude(lambda wt: None))
        self.assertFalse(res["healed"])
        self.assertIn("수정 없음", res["reason"])
        self.assertEqual(_git(["worktree", "list"], self.repo).stdout.count("self-heal-"), 0,
                         "실패해도 워크트리를 정리하지 않았다")

    def test_editing_safety_net_file_is_blocked_even_if_it_fixes_bug(self):
        """올바른 수정이어도 안전망 파일이면 자동 병합하지 않는다 — 이게 핵심 안전선이다."""
        safety_file = self.repo / "projects/ai-team/_shared/backlog.py"
        safety_file.parent.mkdir(parents=True, exist_ok=True)

        def edit(wt):
            (wt / "projects/ai-team/buggy.py").write_text(
                "def add(a, b):\n    return a + b\n", encoding="utf-8")
            (wt / "projects/ai-team/_shared").mkdir(parents=True, exist_ok=True)
            (wt / "projects/ai-team/_shared/backlog.py").write_text("# 손댐\n", encoding="utf-8")

        res = self.mod.attempt("test_buggy.T.test_add", run_claude_fn=self._fake_claude(edit))
        self.assertFalse(res["healed"])
        self.assertIn("안전망", res["reason"])
        self.assertIn("backlog.py", res["reason"])

    def test_editing_petnna_app_is_blocked(self):
        def edit(wt):
            (wt / "projects/petnna").mkdir(parents=True, exist_ok=True)
            (wt / "projects/petnna/index.html").write_text("<!-- 손댐 -->", encoding="utf-8")

        res = self.mod.attempt("test_buggy.T.test_add", run_claude_fn=self._fake_claude(edit))
        self.assertFalse(res["healed"])
        self.assertIn("범위 밖", res["reason"])

    def test_fix_that_does_not_pass_suite_is_not_merged(self):
        def edit(wt):
            (wt / "projects/ai-team/buggy.py").write_text(
                "def add(a, b):\n    return a * b  # 여전히 틀림\n", encoding="utf-8")

        res = self.mod.attempt("test_buggy.T.test_add", run_claude_fn=self._fake_claude(edit))
        self.assertFalse(res["healed"])
        self.assertIn("회귀 실패", res["reason"])
        log = _git(["log", "--oneline", "master"], self.repo).stdout
        self.assertNotIn("자기수정", log, "회귀가 안 지나갔는데 master에 병합됐다")

    def test_valid_fix_is_merged_and_committed(self):
        """이게 성공 경로다 — 실제로 버그를 고치면 격리 워크트리 → master 병합까지 끝까지 간다."""
        def edit(wt):
            (wt / "projects/ai-team/buggy.py").write_text(
                "def add(a, b):\n    return a + b\n", encoding="utf-8")

        res = self.mod.attempt("test_buggy.T.test_add", run_claude_fn=self._fake_claude(edit))
        self.assertTrue(res["healed"], res["reason"])
        self.assertTrue(res["committed"])
        content = (self.repo / "projects/ai-team/buggy.py").read_text(encoding="utf-8")
        self.assertIn("a + b", content, "master 작업트리에 고친 내용이 반영되지 않았다")
        self.assertEqual(len(_git(["worktree", "list"], self.repo).stdout.strip().splitlines()), 1,
                         "성공 후에도 워크트리가 안 정리됐다")


if __name__ == "__main__":
    unittest.main()
