"""예원 백로그 정체 해소기 + 그 원인이 된 게이트 오탐 (오너 지시 2026-08-06).

배경: 자동 루프가 3회 실패한 과제를 `보류`로 옮기는데, 그 상태를 아무도 다시 꺼내주지
않는다. 게이트가 잘못돼 반려된 것과 진짜로 못 하는 것이 같은 무덤에 쌓여 67건이 됐고,
그중 절반 이상이 게이트 쪽 문제였다:

  · 시크릿 스캐너가 `password: "비밀번호를 입력해 주세요"`(UI 문구)를 시크릿으로 오판 — 9건
  · 크기 상한 200줄이 기능 과제에 구조적으로 안 맞음(쪼개줄 담당자 없음) — 12건
  · E2E 1회 실패로 반려, 지목된 테스트는 지금 master에서 전부 통과 — 15건

오너 지시: "해결하고 앞으로 그런일 발생하면 예원이가 해결해."

여기서 굳히는 것:
  A. 시크릿 스캐너는 **값이 자격증명처럼 생겼을 때만** 잡는다(한글·공백 문장은 아니다).
  B. 크기 상한은 자동 병합 경로(엄격)와 사람 검토 경로(완화)가 다르다.
  C. 해소기는 게이트 결함으로 막힌 것만 되돌리고, 사람 판단·금지 경로는 절대 안 건드린다.
  D. 되돌릴 때 attempts를 0으로 초기화한다 — 안 하면 수리가 집자마자 다시 탈락한다.
"""
import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_TEAM_ROOT))

_ENGINE = AI_TEAM_ROOT / "skills" / "수리_개발자" / "tools" / "petnna_dev_engine.py"
_UNBLOCK = AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools" / "petnna_backlog_unblocker.py"


def _load(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


class SecretScannerFalsePositiveTests(unittest.TestCase):
    """A. UI 문구는 시크릿이 아니다 — 진짜 자격증명만 잡는다."""

    @classmethod
    def setUpClass(cls):
        cls.pat = _load(_ENGINE, "eng_secret_t").FORBIDDEN_DIFF

    UI_LINES = [
        '+  password: "비밀번호를 입력해 주세요",',
        '+  password: "Enter your password here",',
        '+  secret: "이 값은 공개되어도 됩니다",',
        '+  token: "인증 토큰이 만료되었습니다",',
        '+  <input type="password" placeholder="비밀번호">',
        "+  el.type = el.type === 'password' ? 'text' : 'password';",
    ]
    SECRET_LINES = [
        '+  const token = "abcdef1234567890";',
        '+  api_key: "sk-live-9f2c8d1e4a7b",',
        '+  password = "hunter2xyz"',
        '+  headers: { Authorization: "Bearer eyJhbGciOiJIUzI1NiJ9" }',
    ]

    def test_ui_copy_is_not_flagged(self):
        for ln in self.UI_LINES:
            self.assertIsNone(self.pat.search(ln),
                              f"UI 문구를 시크릿으로 오판했다 → {ln.strip()[:60]}")

    def test_real_secrets_still_flagged(self):
        for ln in self.SECRET_LINES:
            self.assertIsNotNone(self.pat.search(ln),
                                 f"진짜 하드코딩 시크릿을 놓쳤다 → {ln.strip()[:60]}")


class SizeGateSplitTests(unittest.TestCase):
    """B. 자동 병합 경로는 엄격, 사람 검토 경로는 완화."""

    @classmethod
    def setUpClass(cls):
        cls.eng = _load(_ENGINE, "eng_size_t")

    def test_backlog_limits_are_looser_but_finite(self):
        e = self.eng
        self.assertGreater(e.BACKLOG_MAX_LINES, e.MAX_LINES,
                           "백로그 상한이 자동 병합 상한보다 크지 않다 — 기능 과제가 계속 막힌다")
        self.assertGreater(e.BACKLOG_MAX_FILES, e.MAX_FILES)
        self.assertLess(e.BACKLOG_MAX_LINES, 1000,
                        "상한이 사실상 무한이다 — 리뷰 불가능한 PR이 올라온다")

    def test_diff_gate_accepts_is_backlog(self):
        import inspect
        sig = inspect.signature(self.eng.diff_gate)
        self.assertIn("is_backlog", sig.parameters,
                      "diff_gate가 경로를 구분하지 못한다")

    def test_secret_rejection_names_the_line(self):
        """어느 줄이 걸렸는지 남기지 않으면 3주간 원인 미상으로 반복 반려된다."""
        src = _ENGINE.read_text(encoding="utf-8")
        self.assertIn("매치 키워드", src,
                      "시크릿 반려 사유에 걸린 줄·키워드를 남기지 않는다")


class UnblockerTests(unittest.TestCase):
    """C·D. 되돌릴 것만 되돌리고, 되돌릴 땐 attempts를 초기화한다."""

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.tmp = Path(self._tmp.name)
        self.mod = _load(_UNBLOCK, "unblock_t")
        (self.tmp / "dev").mkdir(parents=True, exist_ok=True)
        (self.tmp / "tests").mkdir(parents=True, exist_ok=True)
        self.mod.BACKLOG = self.tmp / "backlog.json"
        self.mod.DEV_STATE = self.tmp / "dev" / "dev_state.json"
        self.mod.E2E_RESULTS = self.tmp / "tests" / "results.json"
        self.mod.E2E_RESULTS.write_text(json.dumps(
            {"results": {"test_alpha": {"ok": True}, "test_beta": {"ok": True},
                         "test_broken": {"ok": False}}}), encoding="utf-8")

    def tearDown(self):
        self._tmp.cleanup()

    def _seed(self, cases):
        """cases: [(id, title, review_reason, backlog_extra)]"""
        self.mod.BACKLOG.write_text(json.dumps({"items": [
            {"id": i, "title": t, "status": "보류", **(extra or {})}
            for i, t, _, extra in cases]}, ensure_ascii=False), encoding="utf-8")
        self.mod.DEV_STATE.write_text(json.dumps({"issues": {
            i: {"status": "보류", "title": t, "attempts": 3, "review_reason": r}
            for i, t, r, _ in cases}}, ensure_ascii=False), encoding="utf-8")

    def _cat(self, rows, cid):
        return next(r for r in rows if r["id"] == cid)

    def test_classifies_and_marks_releasable(self):
        self._seed([
            ("s1", "비밀번호 토글", "안전 게이트 거부: 추가된 줄에 시크릿/인증 의심 패턴", None),
            ("z1", "큰 기능", "안전 게이트 거부: 변경 과대(파일 4·228줄) — PR 분리 필요", None),
            ("z2", "아주 큰 기능", "안전 게이트 거부: 변경 과대(파일 9·900줄) — PR 분리 필요", None),
            ("e1", "디자인", "E2E 신규 실패: ['test_alpha']", None),
            ("e2", "디자인2", "E2E 신규 실패: ['test_broken']", None),
            ("f1", "API 접촉", "안전 게이트 거부: 금지 경로 접촉: ['projects/petnna/api/x.js']", None),
            ("q1", "품질", "브랜드 컬러가 코랄이 아니라 보라 계열로 구현됨", None),
            ("h1", "오너 보류", "오너 결정(2026-08-02): 당분간 오너 단독 사용", None),
        ])
        rows = self.mod.analyze()["rows"]
        self.assertTrue(self._cat(rows, "s1")["releasable"], "시크릿 오탐을 안 풀었다")
        self.assertTrue(self._cat(rows, "z1")["releasable"], "완화된 상한 안의 과제를 안 풀었다")
        self.assertFalse(self._cat(rows, "z2")["releasable"], "완화 후에도 넘는 과제를 풀었다")
        self.assertTrue(self._cat(rows, "e1")["releasable"], "지금 통과하는 테스트인데 안 풀었다")
        self.assertFalse(self._cat(rows, "e2")["releasable"], "지금도 실패하는 테스트를 풀었다")
        self.assertFalse(self._cat(rows, "f1")["releasable"], "금지 경로를 풀었다 — 안전선 붕괴")
        self.assertFalse(self._cat(rows, "q1")["releasable"], "LLM 품질 판단을 뒤집었다")
        self.assertFalse(self._cat(rows, "h1")["releasable"], "오너 결정을 뒤집었다")

    def test_release_resets_attempts(self):
        self._seed([("s1", "비밀번호 토글",
                     "안전 게이트 거부: 추가된 줄에 시크릿/인증 의심 패턴", None)])
        rows = self.mod.analyze()["rows"]
        freed = self.mod.release(rows, limit=8)
        self.assertEqual([r["id"] for r in freed], ["s1"])
        dev = json.loads(self.mod.DEV_STATE.read_text(encoding="utf-8"))
        self.assertEqual(dev["issues"]["s1"]["status"], "대기")
        self.assertEqual(dev["issues"]["s1"]["attempts"], 0,
                         "attempts를 초기화하지 않으면 수리가 집자마자 MAX_ATTEMPTS에 다시 걸린다")
        bl = json.loads(self.mod.BACKLOG.read_text(encoding="utf-8"))
        self.assertEqual(bl["items"][0]["status"], "대기")

    def test_release_respects_limit(self):
        self._seed([(f"s{i}", f"과제{i}",
                     "안전 게이트 거부: 추가된 줄에 시크릿/인증 의심 패턴", None)
                    for i in range(12)])
        rows = self.mod.analyze()["rows"]
        freed = self.mod.release(rows, limit=3)
        self.assertEqual(len(freed), 3, "상한을 무시하고 한꺼번에 풀면 수리 큐가 묻힌다")

    def test_unknown_e2e_state_is_preserved_not_released(self):
        """테스트 결과를 못 읽으면 '판정 불가'다 — 부재로 읽어 풀어버리면 안 된다."""
        self.mod.E2E_RESULTS = self.tmp / "없는파일.json"
        self._seed([("e1", "디자인", "E2E 신규 실패: ['test_alpha']", None)])
        rows = self.mod.analyze()["rows"]
        self.assertFalse(self._cat(rows, "e1")["releasable"],
                         "테스트 결과 판정 불가인데 해소 가능으로 봤다")


if __name__ == "__main__":
    unittest.main()
