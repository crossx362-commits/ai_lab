"""예원 워치독의 유휴 감지 → 즉시 디스패치 회귀 테스트 — 2026-07-11.

오너 지적("왜 다 노냐 계속 일하라고"): 미오(주 1회)·테오(하루 1회)는 백로그에 배정된
과제가 있어도 자기 정기 슬롯까지 최대 며칠(미오는 최대 6일) 기다렸다. 예원 워치독이
5분 주기로 백로그를 직접 보고, 대상 담당자(미오·테오·백호)에게 배정된 '대기' 과제가
있으면 슬롯과 무관하게 즉시 스크립트를 깨운다. 수리는 자체 주기가 이미 짧아 제외.
"""
import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools" / "harness_monitor.py"


def load():
    spec = importlib.util.spec_from_file_location("harness_monitor_under_test", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class IdleBacklogDispatchTests(unittest.TestCase):
    def setUp(self):
        self.hm = load()
        self.tmp = Path(tempfile.mkdtemp()) / "backlog.json"
        # dispatch_idle_backlog_work()는 ROOT_DIR/output/bot_logs에 실행 로그를 남긴다 —
        # 패치 안 하면 테스트가 실제 운영 로그 파일에 흔적을 남긴다(2026-07-11 리뷰 중
        # 실제로 발생 확인: 테스트가 output/bot_logs/mio_design_review.out.log를 오염시켰다).
        self.fake_root = Path(tempfile.mkdtemp())
        (self.fake_root / "output" / "bot_logs").mkdir(parents=True)
        self._root_patch = mock.patch.object(self.hm, "ROOT_DIR", str(self.fake_root))
        self._root_patch.start()
        self.addCleanup(self._root_patch.stop)

    def _write(self, items):
        self.tmp.write_text(json.dumps({"items": items}, ensure_ascii=False), encoding="utf-8")

    def test_only_dispatch_target_owners_with_waiting_status(self):
        self._write([
            {"owner": "미오", "status": "대기"},
            {"owner": "테오", "status": "대기"},
            {"owner": "수리", "status": "대기"},   # 자체 주기로 제외 대상
            {"owner": "나무", "status": "보류"},   # 상태가 대기 아님
            {"owner": "백호", "status": "완료"},   # 상태가 대기 아님
        ])
        with mock.patch.object(self.hm, "BACKLOG_PATH", str(self.tmp)):
            self.assertEqual(self.hm._pending_backlog_owners(), {"미오", "테오"})

    def test_missing_backlog_file_is_not_fatal(self):
        with mock.patch.object(self.hm, "BACKLOG_PATH", str(self.tmp.parent / "없음.json")):
            self.assertEqual(self.hm._pending_backlog_owners(), set())

    def test_dispatch_spawns_process_per_pending_owner(self):
        self._write([{"owner": "미오", "status": "대기"}, {"owner": "테오", "status": "대기"}])
        calls = []

        def fake_popen(cmd, **kw):
            calls.append(cmd)
            return mock.Mock()

        with mock.patch.object(self.hm, "BACKLOG_PATH", str(self.tmp)), \
             mock.patch.object(self.hm.subprocess, "Popen", side_effect=fake_popen), \
             mock.patch.object(self.hm, "_bots_off", return_value=False):
            dispatched = self.hm.dispatch_idle_backlog_work({})
            self.assertEqual(set(dispatched), {"미오", "테오"})
            self.assertEqual(len(calls), 2)

    def test_cooldown_prevents_repeat_dispatch(self):
        self._write([{"owner": "미오", "status": "대기"}])
        with mock.patch.object(self.hm, "BACKLOG_PATH", str(self.tmp)), \
             mock.patch.object(self.hm.subprocess, "Popen", return_value=mock.Mock()), \
             mock.patch.object(self.hm, "_bots_off", return_value=False):
            state = {}
            first = self.hm.dispatch_idle_backlog_work(state)
            self.assertEqual(first, ["미오"])
            second = self.hm.dispatch_idle_backlog_work(state)
            self.assertEqual(second, [], "쿨다운 내 재디스패치는 없어야 한다")

    def test_bots_off_flag_suppresses_dispatch(self):
        self._write([{"owner": "미오", "status": "대기"}])
        with mock.patch.object(self.hm, "BACKLOG_PATH", str(self.tmp)), \
             mock.patch.object(self.hm, "_bots_off", return_value=True):
            self.assertEqual(self.hm.dispatch_idle_backlog_work({}), [])

    def test_dispatch_output_is_logged_not_discarded(self):
        """2026-07-11 리뷰 중 발견 — DEVNULL로 버리면 자동 디스패치 실행이 실패해도
        흔적이 안 남는다. 정상 데몬과 같은 bot_logs 파일에 이어 쓰여야 한다."""
        self._write([{"owner": "미오", "status": "대기"}])
        with mock.patch.object(self.hm, "BACKLOG_PATH", str(self.tmp)), \
             mock.patch.object(self.hm.subprocess, "Popen", return_value=mock.Mock()), \
             mock.patch.object(self.hm, "_bots_off", return_value=False):
            self.hm.dispatch_idle_backlog_work({})
            out_log = self.fake_root / "output" / "bot_logs" / "mio_design_review.out.log"
            self.assertTrue(out_log.exists(), "디스패치 마커가 로그 파일에 남아야 한다")
            self.assertIn("예원 유휴감지 디스패치", out_log.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
