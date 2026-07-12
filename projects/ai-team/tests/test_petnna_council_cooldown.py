"""예원 긴급회의 쿨다운 순서 회귀 테스트 — 2026-07-12.

발견 경위: convene()가 _recent_meeting(topic)에서 ProcessLock("petnna_council")을
얻기 *전에* 24h 쿨다운을 이미 기록하고 있었다. 회의 A가 진행 중일 때 다른 안건
회의 B가 트리거되면, B는 자기 topic을 먼저 기록한 뒤 ProcessLock 충돌로
sys.exit(0)해 — 회의는 한 번도 안 열렸는데 그 안건이 24시간 재소집 금지 상태가
됐다. _is_recent_meeting()(순수 조회)과 _mark_meeting()(기록)으로 분리해, 기록은
락을 실제로 잡은 뒤에만 하도록 고쳤다.
"""
import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools" / "petnna_council.py"


def load():
    spec = importlib.util.spec_from_file_location("council_cooldown_under_test", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class CooldownOrderingTests(unittest.TestCase):
    def setUp(self):
        self.council = load()
        self.tmp_dir = Path(tempfile.mkdtemp())
        self.state_patch = mock.patch.object(self.council, "STATE", self.tmp_dir / "state.json")
        self.out_patch = mock.patch.object(self.council, "OUT_DIR", self.tmp_dir)
        self.state_patch.start()
        self.out_patch.start()
        self.addCleanup(self.state_patch.stop)
        self.addCleanup(self.out_patch.stop)

    def test_is_recent_meeting_is_read_only(self):
        """핵심 회귀: 조회만 해서는(아직 회의를 시작 안 했으면) 쿨다운이 기록되면
        안 된다 — 예전 _recent_meeting()은 조회 시점에 이미 기록해버렸다."""
        topic = "테스트 안건"
        self.assertFalse(self.council._is_recent_meeting(topic))
        self.assertFalse(self.council._is_recent_meeting(topic),
                         "조회만으로 쿨다운이 기록되면 안 된다(부작용 없어야 함)")

    def test_mark_meeting_then_is_recent_returns_true_within_cooldown(self):
        topic = "테스트 안건"
        self.assertFalse(self.council._is_recent_meeting(topic))
        self.council._mark_meeting(topic)
        self.assertTrue(self.council._is_recent_meeting(topic),
                        "실제로 마킹한 뒤에는 쿨다운 내로 판정돼야 한다")

    def test_lock_conflict_never_marks_cooldown(self):
        """convene()가 ProcessLock 충돌로 즉시 종료되는 경로를 재현 — 회의가 실제로
        시작되지 않았다면 쿨다운이 기록되면 안 된다."""
        topic = "동시 트리거 안건"

        class FakeConflictLock:
            def __enter__(self):
                raise SystemExit(0)  # ProcessLock 충돌 시 실제 동작과 동일

            def __exit__(self, *a):
                return False

        with mock.patch.object(self.council, "ProcessLock", return_value=FakeConflictLock()):
            with self.assertRaises(SystemExit):
                self.council.convene(topic, "", "P1")
        self.assertFalse(self.council._is_recent_meeting(topic),
                         "락 충돌로 회의가 안 열렸으면 쿨다운도 기록되면 안 된다")

    def test_different_topics_tracked_independently(self):
        self.council._mark_meeting("안건 A")
        self.assertTrue(self.council._is_recent_meeting("안건 A"))
        self.assertFalse(self.council._is_recent_meeting("안건 B"))


if __name__ == "__main__":
    unittest.main()
