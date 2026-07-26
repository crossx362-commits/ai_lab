"""수리 diff 게이트 — JS 수정 시 index.html의 `?v=` 캐시버전 갱신 강제 (2026-07-25).

이 저장소는 모든 JS를 `<script src="js/x.js?v=N">`로 싣는다. JS만 고치고 N을 안
올리면 브라우저가 캐시된 옛 파일을 계속 써서 **배포해도 기능이 안 보인다**.

발견 경위: PR대기 브랜치 2개(나무_20260722110259_4 건강 목표 진척 링,
나무_20260725110420_0 급여 음식 검색기)를 사람이 검토하다 **둘 다** 이 상태인 걸
발견했다. 수리의 게이트는 경로·크기·시크릿만 보고 캐시버전은 안 봤다 — 그대로
병합됐다면 두 기능 모두 조용히 안 보였을 것이다.

가짜 저장소를 실제로 만들어 검증한다(모킹하면 `git show`·경로 계산 같은 진짜
실패 지점을 못 잡는다 — 2026-07-12 유휴 디스패치 경로 오류의 교훈).
"""
import importlib.util
import subprocess
import tempfile
import unittest
from pathlib import Path

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = AI_TEAM_ROOT / "skills" / "수리_개발자" / "tools" / "petnna_dev_engine.py"

_INDEX = """<!doctype html>
<script defer src="js/album.js?v={album}"></script>
<script defer src="js/other.js?v=7"></script>
{extra}
"""


def load():
    spec = importlib.util.spec_from_file_location("suri_under_test", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def _git(args, cwd):
    return subprocess.run(["git", *args], cwd=cwd, capture_output=True, text=True)


class CacheVersionGateTests(unittest.TestCase):
    def setUp(self):
        self.suri = load()
        self.repo = Path(tempfile.mkdtemp())
        pet = self.repo / "projects" / "petnna" / "js"
        pet.mkdir(parents=True)
        (self.repo / "projects/petnna/index.html").write_text(
            _INDEX.format(album=161, extra=""), encoding="utf-8")
        (pet / "album.js").write_text("// v1\n", encoding="utf-8")
        _git(["init", "-q", "-b", "master"], self.repo)
        _git(["config", "user.email", "t@t"], self.repo)
        _git(["config", "user.name", "t"], self.repo)
        _git(["add", "-A"], self.repo)
        _git(["commit", "-qm", "base"], self.repo)

    def _branch(self):
        """diff_gate는 merge-base(master, HEAD)와 비교하므로 반드시 브랜치에서 작업한다."""
        _git(["checkout", "-q", "-b", "work"], self.repo)

    def _commit_change(self, *, bump: bool, extra_js: str = ""):
        self._branch()
        pet = self.repo / "projects" / "petnna" / "js"
        (pet / "album.js").write_text("// v1\n// 기능 추가\n", encoding="utf-8")
        (self.repo / "projects/petnna/index.html").write_text(
            _INDEX.format(album=162 if bump else 161, extra=extra_js), encoding="utf-8")
        _git(["add", "-A"], self.repo)
        _git(["commit", "-qm", "change"], self.repo)

    def test_js_changed_without_version_bump_is_rejected(self):
        self._commit_change(bump=False)
        ok, msg, _ = self.suri.diff_gate(self.repo)
        self.assertFalse(ok, "캐시버전을 안 올렸는데 게이트를 통과함")
        self.assertIn("캐시버전", msg)
        self.assertIn("js/album.js", msg)

    def test_js_changed_with_version_bump_passes(self):
        self._commit_change(bump=True)
        ok, msg, _ = self.suri.diff_gate(self.repo)
        self.assertTrue(ok, f"캐시버전을 올렸는데도 거부됨: {msg}")

    def test_new_js_file_is_not_flagged(self):
        """새로 추가된 js는 script 태그 자체가 신규라 '미갱신' 대상이 아니다."""
        self._branch()
        pet = self.repo / "projects" / "petnna" / "js"
        (pet / "brand-new.js").write_text("// new\n", encoding="utf-8")
        (self.repo / "projects/petnna/index.html").write_text(
            _INDEX.format(album=161,
                          extra='<script defer src="js/brand-new.js?v=1"></script>'),
            encoding="utf-8")
        _git(["add", "-A"], self.repo)
        _git(["commit", "-qm", "add new js"], self.repo)
        ok, msg, _ = self.suri.diff_gate(self.repo)
        self.assertTrue(ok, f"신규 js가 캐시버전 미갱신으로 오탐됨: {msg}")

    def test_non_js_change_is_not_flagged(self):
        self._branch()
        (self.repo / "projects/petnna/README.md").write_text("문서\n", encoding="utf-8")
        _git(["add", "-A"], self.repo)
        _git(["commit", "-qm", "docs"], self.repo)
        ok, msg, _ = self.suri.diff_gate(self.repo)
        self.assertTrue(ok, f"js가 아닌 변경이 캐시버전으로 거부됨: {msg}")


class AutobumpCacheVersionTests(unittest.TestCase):
    """엔진의 기계적 캐시버전 보정 — 게이트만으로는 부족한 이유.

    프롬프트로 "?v= 를 올려라"라고 지시해도 LLM이 잊으면 게이트가 거부하고, 그 브랜치는
    PR대기로만 쌓여 자동 병합 루프가 무력화된다(PR대기 상한 5에 걸리면 신규 착수도 멈춤).
    버전 +1은 판단 여지가 없는 순수 기계 작업이라 엔진이 커밋 직전에 직접 채운다.
    """

    def setUp(self):
        self.suri = load()
        self.repo = Path(tempfile.mkdtemp())
        pet = self.repo / "projects" / "petnna" / "js"
        pet.mkdir(parents=True)
        (self.repo / "projects/petnna/index.html").write_text(
            _INDEX.format(album=161, extra=""), encoding="utf-8")
        (pet / "album.js").write_text("// v1\n", encoding="utf-8")
        _git(["init", "-q", "-b", "master"], self.repo)
        _git(["config", "user.email", "t@t"], self.repo)
        _git(["config", "user.name", "t"], self.repo)
        _git(["add", "-A"], self.repo)
        _git(["commit", "-qm", "base"], self.repo)

    def _index(self):
        return (self.repo / "projects/petnna/index.html").read_text(encoding="utf-8")

    def test_modified_js_gets_version_bumped(self):
        (self.repo / "projects/petnna/js/album.js").write_text("// 수정\n", encoding="utf-8")
        bumped = self.suri._autobump_cache_versions(self.repo)
        self.assertTrue(bumped, "수정된 js인데 버전이 보정되지 않음")
        self.assertIn("js/album.js?v=162", self._index())

    def test_untouched_js_is_left_alone(self):
        (self.repo / "projects/petnna/js/album.js").write_text("// 수정\n", encoding="utf-8")
        self.suri._autobump_cache_versions(self.repo)
        self.assertIn("js/other.js?v=7", self._index(), "건드리지 않은 js의 버전이 바뀜")

    def test_new_js_is_not_bumped(self):
        """신규 js는 script 태그 생성 판단이 필요해 엔진이 손대지 않는다."""
        (self.repo / "projects/petnna/js/brand-new.js").write_text("// new\n", encoding="utf-8")
        self.assertEqual(self.suri._autobump_cache_versions(self.repo), [])

    def test_no_js_change_is_noop(self):
        (self.repo / "projects/petnna/README.md").write_text("문서\n", encoding="utf-8")
        self.assertEqual(self.suri._autobump_cache_versions(self.repo), [])
        self.assertIn("js/album.js?v=161", self._index())

    def test_already_bumped_is_not_double_bumped(self):
        """구현자가 이미 올렸으면 엔진이 또 올리지 않는다(2026-07-26 175→177 관측)."""
        (self.repo / "projects/petnna/js/album.js").write_text("// 수정\n", encoding="utf-8")
        (self.repo / "projects/petnna/index.html").write_text(
            _INDEX.format(album=162, extra=""), encoding="utf-8")   # 구현자가 161→162
        self.assertEqual(self.suri._autobump_cache_versions(self.repo), [])
        self.assertIn("js/album.js?v=162", self._index(), "이미 올린 버전을 또 올림")

    def test_autobump_then_gate_passes(self):
        """보정 후에는 게이트가 통과해야 한다(둘이 서로 어긋나지 않는지)."""
        _git(["checkout", "-q", "-b", "work"], self.repo)
        (self.repo / "projects/petnna/js/album.js").write_text("// 수정\n", encoding="utf-8")
        self.suri._autobump_cache_versions(self.repo)
        _git(["add", "-A"], self.repo)
        _git(["commit", "-qm", "change"], self.repo)
        ok, msg, _ = self.suri.diff_gate(self.repo)
        self.assertTrue(ok, f"자동 보정 후에도 게이트가 거부함: {msg}")


if __name__ == "__main__":
    unittest.main()
