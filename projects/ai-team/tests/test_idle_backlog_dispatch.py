"""예원 워치독의 유휴 감지 → 즉시 디스패치 회귀 테스트 — 2026-07-11.

오너 지적("왜 다 노냐 계속 일하라고"): 미오(주 1회)·테오(하루 1회)는 백로그에 배정된
과제가 있어도 자기 정기 슬롯까지 최대 며칠(미오는 최대 6일) 기다렸다. 예원 워치독이
5분 주기로 백로그를 직접 보고, 대상 담당자(미오·테오·백호)에게 배정된 '대기' 과제가
있으면 슬롯과 무관하게 즉시 스크립트를 깨운다. 수리는 자체 주기가 이미 짧아 제외.
"""
import importlib.util
import json
import os
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


class DispatchTargetPathsAreReal(unittest.TestCase):
    """실제 파일시스템 경로 검증 — 2026-07-12 실사고 회귀.

    dispatch_idle_backlog_work()의 skills_dir 계산이 한 단계만 올라가(`..`) 실제로는
    skills/예원_CEO/테오_테스트/... 라는 존재하지 않는 경로를 만들고 있었다. 그런데
    다른 모든 테스트는 subprocess.Popen을 모킹해버려서 이 오류를 한 번도 못 잡았다 —
    Popen()은 python3 자체는 성공적으로 띄우므로 예외를 안 던지고, 스크립트 자체가
    없다는 건 그 서브프로세스의 stderr에만 나타난다(디스패치 함수는 이걸 확인 안 함).
    실제로 2026-07-11 밤부터 다음날 낮까지 20분마다 계속 조용히 실패하고 있었다
    (오너가 "지금은 뭐해"로 실제 로그를 직접 보다가 발견). 이 테스트는 Popen을
    모킹하지 않고 DISPATCH_TARGETS의 모든 경로가 실제 파일로 존재하는지 확인한다."""

    def test_all_dispatch_target_scripts_exist_on_disk(self):
        hm = load()
        skills_dir = os.path.join(os.path.dirname(hm.__file__), "..", "..")
        for owner, (rel_script, args, log_name) in hm.DISPATCH_TARGETS.items():
            full = os.path.normpath(os.path.join(skills_dir, rel_script))
            self.assertTrue(os.path.isfile(full),
                            f"{owner} 디스패치 대상 스크립트가 실제로 없다: {full}")


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
        # 기본값: 이 기계가 항상 primary(실제 platform과 일치)라고 가정 — 실제
        # fleet_machine_policy.json 내용·실행 플랫폼에 기존 테스트들이 우연히 좌우되지
        # 않게 한다(2026-07-11 2차 감사가 발견한 single-machine 가드 우회 결함 수정 후 추가).
        self._policy_patch = mock.patch.object(
            self.hm, "read_fleet_policy", return_value={"primary_platform": sys.platform})
        self._policy_patch.start()
        self.addCleanup(self._policy_patch.stop)

    def _write(self, items):
        self.tmp.write_text(json.dumps({"items": items}, ensure_ascii=False), encoding="utf-8")

    def test_only_dispatch_target_owners_with_waiting_status(self):
        self._write([
            {"owner": "미오", "status": "대기", "type": "디자인"},
            {"owner": "테오", "status": "대기", "type": "테스트"},
            {"owner": "수리", "status": "대기"},   # 자체 주기로 제외 대상
            {"owner": "나무", "status": "보류"},   # 상태가 대기 아님
            {"owner": "백호", "status": "완료"},   # 상태가 대기 아님
        ])
        with mock.patch.object(self.hm, "BACKLOG_PATH", str(self.tmp)):
            self.assertEqual(self.hm._pending_backlog_owners(), {"미오", "테오"})

    def test_type_mismatched_assignment_is_never_dispatched(self):
        """자동 파이프라인 감사 도구가 발견(2026-07-11): owner=테오인데 type이 '테스트'가
        아니면 테오의 _backlog_task()가 절대 못 집는다 — 디스패치해봤자 20분마다 영구
        재점화만 하는 좀비. 방어적으로 디스패치 후보에서도 제외해야 한다."""
        self._write([
            {"owner": "테오", "status": "대기", "type": "기획"},   # 불일치 — 제외돼야
            {"owner": "테오", "status": "대기", "type": "테스트"},  # 일치 — 포함돼야
            {"owner": "미오", "status": "대기", "type": "백엔드"},  # 불일치 — 제외돼야
        ])
        with mock.patch.object(self.hm, "BACKLOG_PATH", str(self.tmp)):
            self.assertEqual(self.hm._pending_backlog_owners(), {"테오"})

    def test_empty_type_for_restricted_owner_is_never_dispatched(self):
        """2차 자동 파이프라인 감사가 발견(2026-07-12): owner_type_mismatch()는 type이
        비어있으면 관대하게 통과시키는데(적재 시점용 설계), 그걸 디스패치 필터에도 그대로
        썼더니 owner=테오·type='' 항목이 필터를 통과해 20분마다 영구 재점화될 뻔했다 —
        테오는 정확히 type=='테스트'만 집으므로 type 미지정도 디스패치 시점엔 제외해야
        한다. owner_can_consume()(엄격판)으로 교체해 수정."""
        self._write([
            {"owner": "테오", "status": "대기"},              # type 없음 — 제외돼야
            {"owner": "미오", "status": "대기", "type": ""},   # type 빈 문자열 — 제외돼야
            {"owner": "백호", "status": "대기"},              # 제약 없는 owner — 포함돼야
        ])
        with mock.patch.object(self.hm, "BACKLOG_PATH", str(self.tmp)):
            self.assertEqual(self.hm._pending_backlog_owners(), {"백호"})

    def test_missing_backlog_file_is_not_fatal(self):
        with mock.patch.object(self.hm, "BACKLOG_PATH", str(self.tmp.parent / "없음.json")):
            self.assertEqual(self.hm._pending_backlog_owners(), set())

    def test_dispatch_spawns_process_per_pending_owner(self):
        self._write([{"owner": "미오", "status": "대기", "type": "디자인"}, {"owner": "테오", "status": "대기", "type": "테스트"}])
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
        self._write([{"owner": "미오", "status": "대기", "type": "디자인"}])
        with mock.patch.object(self.hm, "BACKLOG_PATH", str(self.tmp)), \
             mock.patch.object(self.hm.subprocess, "Popen", return_value=mock.Mock()), \
             mock.patch.object(self.hm, "_bots_off", return_value=False):
            state = {}
            first = self.hm.dispatch_idle_backlog_work(state)
            self.assertEqual(first, ["미오"])
            second = self.hm.dispatch_idle_backlog_work(state)
            self.assertEqual(second, [], "쿨다운 내 재디스패치는 없어야 한다")

    def test_bots_off_flag_suppresses_dispatch(self):
        self._write([{"owner": "미오", "status": "대기", "type": "디자인"}])
        with mock.patch.object(self.hm, "BACKLOG_PATH", str(self.tmp)), \
             mock.patch.object(self.hm, "_bots_off", return_value=True):
            self.assertEqual(self.hm.dispatch_idle_backlog_work({}), [])

    def test_non_primary_machine_never_dispatches(self):
        """2차 자동 파이프라인 감사가 발견(2026-07-11): daemon()의 petnna_single_machine_guard는
        각 에이전트 스크립트 안에서만 걸리는데, dispatch_idle_backlog_work는 비데몬 모드로
        직접 실행해 그 가드를 우회한다 — primary가 아닌 기계에서 이중 가동될 수 있었다."""
        self._write([{"owner": "미오", "status": "대기", "type": "디자인"}])
        other_platform = "win32" if sys.platform != "win32" else "darwin"
        with mock.patch.object(self.hm, "BACKLOG_PATH", str(self.tmp)), \
             mock.patch.object(self.hm, "read_fleet_policy",
                              return_value={"primary_platform": other_platform}), \
             mock.patch.object(self.hm.subprocess, "Popen") as fake_popen, \
             mock.patch.object(self.hm, "_bots_off", return_value=False):
            self.assertEqual(self.hm.dispatch_idle_backlog_work({}), [])
            fake_popen.assert_not_called()

    def test_no_policy_file_falls_back_to_dispatching(self):
        """정책파일이 없으면(과거 상태) 기존처럼 디스패치를 막지 않는다(하위호환)."""
        self._write([{"owner": "미오", "status": "대기", "type": "디자인"}])
        with mock.patch.object(self.hm, "BACKLOG_PATH", str(self.tmp)), \
             mock.patch.object(self.hm, "read_fleet_policy", return_value={}), \
             mock.patch.object(self.hm.subprocess, "Popen", return_value=mock.Mock()), \
             mock.patch.object(self.hm, "_bots_off", return_value=False):
            self.assertEqual(self.hm.dispatch_idle_backlog_work({}), ["미오"])

    def test_popen_failure_closes_log_handles_and_alerts_once(self):
        """2차 감사가 발견(2026-07-11): Popen 예외 경로에서 로그 fd가 안 닫혀 누수되고,
        실패가 텔레그램으로 안 올라가 오너가 반복 실패를 알 방법이 없었다."""
        self._write([{"owner": "미오", "status": "대기", "type": "디자인"}])
        opened = []
        real_open = open

        def tracking_open(path, *a, **kw):
            f = real_open(path, *a, **kw)
            opened.append(f)
            return f

        with mock.patch.object(self.hm, "BACKLOG_PATH", str(self.tmp)), \
             mock.patch.object(self.hm.subprocess, "Popen", side_effect=OSError("실행 실패")), \
             mock.patch.object(self.hm, "_bots_off", return_value=False), \
             mock.patch.object(self.hm, "send") as fake_send, \
             mock.patch("builtins.open", side_effect=tracking_open):
            self.hm.dispatch_idle_backlog_work({})
        fake_send.assert_called_once()
        self.assertTrue(opened, "로그 파일이 열렸어야 한다")
        self.assertTrue(all(f.closed for f in opened),
                        "Popen 실패 시에도 열었던 로그 fd는 반드시 닫혀야 한다(누수 방지)")

    def test_dispatch_output_is_logged_not_discarded(self):
        """2026-07-11 리뷰 중 발견 — DEVNULL로 버리면 자동 디스패치 실행이 실패해도
        흔적이 안 남는다. 정상 데몬과 같은 bot_logs 파일에 이어 쓰여야 한다."""
        self._write([{"owner": "미오", "status": "대기", "type": "디자인"}])
        with mock.patch.object(self.hm, "BACKLOG_PATH", str(self.tmp)), \
             mock.patch.object(self.hm.subprocess, "Popen", return_value=mock.Mock()), \
             mock.patch.object(self.hm, "_bots_off", return_value=False):
            self.hm.dispatch_idle_backlog_work({})
            out_log = self.fake_root / "output" / "bot_logs" / "mio_design_review.out.log"
            self.assertTrue(out_log.exists(), "디스패치 마커가 로그 파일에 남아야 한다")
            self.assertIn("예원 유휴감지 디스패치", out_log.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
