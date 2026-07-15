"""예원 워치독 자동 pull(ff-only) 회귀 테스트 — 2026-07-15 신설.

"push만 하면 운영기에 적용"의 마지막 수동 구멍(pull 자체)을 워치독 auto_pull()이
메운다. 이 테스트는 그 안전 경계를 굳힌다: master가 아니면/봇 원격종료면/뒤처짐이
없으면 절대 손대지 않고, ff-only 실패(로컬 분기·충돌)는 변경 없이 보고만 한다.
"""
import importlib.util
import sys
import unittest
from pathlib import Path
from unittest import mock

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_TEAM_ROOT))

HM_PATH = AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools" / "harness_monitor.py"


def load(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


hm = load("harness_monitor_under_test", HM_PATH)


def _git_out_stub(branch="master", behind="2"):
    def fn(*args):
        if args[:2] == ("rev-parse", "--abbrev-ref"):
            return branch
        if args[0] == "rev-list":
            return behind
        return ""
    return fn


class AutoPullTests(unittest.TestCase):

    def test_bots_off_does_nothing(self):
        with mock.patch.object(hm, "_bots_off", return_value=True), \
             mock.patch.object(hm.subprocess, "run") as run:
            self.assertIsNone(hm.auto_pull())
            run.assert_not_called()

    def test_non_master_checkout_untouched(self):
        with mock.patch.object(hm, "_bots_off", return_value=False), \
             mock.patch.object(hm, "_git_out", side_effect=_git_out_stub(branch="feat/wip")), \
             mock.patch.object(hm.subprocess, "run") as run:
            self.assertIsNone(hm.auto_pull())
            run.assert_not_called()

    def test_up_to_date_no_merge(self):
        with mock.patch.object(hm, "_bots_off", return_value=False), \
             mock.patch.object(hm, "_git_out", side_effect=_git_out_stub(behind="0")), \
             mock.patch.object(hm.subprocess, "run") as run:
            run.return_value = mock.Mock(returncode=0, stdout="", stderr="")
            self.assertIsNone(hm.auto_pull())
            self.assertEqual(run.call_count, 1, "fetch 1회만 — merge를 부르면 안 된다")
            self.assertIn("fetch", run.call_args_list[0].args[0])

    def test_behind_fast_forwards(self):
        with mock.patch.object(hm, "_bots_off", return_value=False), \
             mock.patch.object(hm, "_git_out", side_effect=_git_out_stub(behind="3")), \
             mock.patch.object(hm.subprocess, "run") as run:
            run.return_value = mock.Mock(returncode=0, stdout="", stderr="")
            note = hm.auto_pull()
            self.assertIn("자동 반영", note)
            merge_cmd = run.call_args_list[1].args[0]
            self.assertIn("--ff-only", merge_cmd, "ff-only 없는 병합은 로컬 분기를 머지커밋으로 오염시킨다")

    def test_ff_failure_reports_without_changing(self):
        def run_side(cmd, **kw):
            if "merge" in cmd:
                return mock.Mock(returncode=1, stdout="", stderr="fatal: Not possible to fast-forward")
            return mock.Mock(returncode=0, stdout="", stderr="")
        with mock.patch.object(hm, "_bots_off", return_value=False), \
             mock.patch.object(hm, "_git_out", side_effect=_git_out_stub(behind="2")), \
             mock.patch.object(hm.subprocess, "run", side_effect=run_side):
            note = hm.auto_pull()
            self.assertIn("불가", note, "조용히 뒤처지는 침묵 실패가 되면 안 된다 — 보고 필요")


if __name__ == "__main__":
    unittest.main()
