"""유령 과제 탐지 회귀 테스트 — 2026-08-08.

배경: 회의·미오·나무가 "X를 구현하라"고 적재할 때 X가 이미 있는지 확인하지 않는다.
보류 53건 전수 조사에서 5건이 그 경로였고, 그중 3건은 요구한 회귀 테스트까지 이미
존재했다. 사람이 전수 조사를 해야만 발견되던 것을 예원이 매일 기계적으로 잡는다.

여기서 굳히는 것:
  ① 요구한 산출물이 실재하면 후보로 잡는다
  ② **검증·감사 과제는 제외한다** — 기존 산출물을 지목하는 게 정상이라 실재가
     완료 근거가 못 된다(첫 실행에서 실제로 낸 오탐, 네거티브 컨트롤)
  ③ 실재하지 않으면 안 잡는다
  ④ **절대 자동 종결하지 않는다** — "있긴 한데 목표 미달"을 닫으면 진짜 남은 일이
     사라진다(미오_20260716231002_0 placeholder 대비: 5.3:1로 목표 7:1 미달이었다)
"""
import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
if str(AI_TEAM_ROOT) not in sys.path:
    sys.path.insert(0, str(AI_TEAM_ROOT))
SCRIPT = AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools" / "petnna_backlog_unblocker.py"


def load():
    spec = importlib.util.spec_from_file_location("unblocker_under_test", SCRIPT)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


class GhostDetectionTests(unittest.TestCase):
    def setUp(self):
        self.mod = load()
        self.tmp = Path(tempfile.mkdtemp())
        p = mock.patch.object(self.mod, "BACKLOG", self.tmp / "backlog.json")
        p.start(); self.addCleanup(p.stop)

    def _write(self, items):
        self.mod.BACKLOG.write_text(json.dumps({"items": items}, ensure_ascii=False),
                                    encoding="utf-8")

    def test_existing_required_test_is_flagged(self):
        """요구한 회귀 테스트가 실재하면 유령 후보다 — 실제로 겪은 3건의 서명."""
        self._write([{"id": "x", "status": "보류", "title": "게이트 예외 구현",
                      "detail": "tests/test_backlog_ghost_detection.py 회귀 동반"}])
        got = self.mod.ghost_candidates()
        self.assertEqual([g["id"] for g in got], ["x"])
        self.assertIn("요구한 테스트 실재", got[0]["evidence"][0])

    def test_verification_task_is_not_flagged(self):
        """네거티브 컨트롤 — 이 제외가 없으면 첫 실행이 냈던 오탐이 그대로 재현된다."""
        self._write([{"id": "v", "status": "보류",
                      "title": "offline_sync E2E flaky 소지 검증",
                      "detail": "test_backlog_ghost_detection.py가 실 응답에 의존하지 않는지 확인"}])
        self.assertEqual(self.mod.ghost_candidates(), [])

    def test_missing_artifact_is_not_flagged(self):
        self._write([{"id": "n", "status": "보류", "title": "새 기능 구현",
                      "detail": "tests/test_absolutely_no_such_file_xyz.py 회귀 동반"}])
        self.assertEqual(self.mod.ghost_candidates(), [])

    def test_non_held_items_ignored(self):
        self._write([{"id": "d", "status": "대기", "title": "구현",
                      "detail": "tests/test_backlog_ghost_detection.py"}])
        self.assertEqual(self.mod.ghost_candidates(), [])

    def test_detection_never_mutates_backlog(self):
        """보고 전용 — 상태를 건드리면 '목표 미달'까지 조용히 닫힌다."""
        items = [{"id": "x", "status": "보류", "title": "게이트 예외 구현",
                  "detail": "tests/test_backlog_ghost_detection.py 회귀 동반"}]
        self._write(items)
        before = self.mod.BACKLOG.read_text(encoding="utf-8")
        self.mod.ghost_candidates()
        self.assertEqual(self.mod.BACKLOG.read_text(encoding="utf-8"), before,
                         "유령 탐지가 백로그를 수정했다 — 보고 전용이어야 한다")

    def test_llm_failure_does_not_break_detection(self):
        """로컬 모델이 죽어도 본편(정체 해소)이 죽으면 안 된다."""
        cands = [{"id": "x", "title": "t", "evidence": ["e"], "detail": "d"}]
        with mock.patch.dict(sys.modules, {"_shared.llm": None}):
            self.assertEqual(self.mod._llm_second_opinion(cands), {})


if __name__ == "__main__":
    unittest.main()
