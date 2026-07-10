"""함대 가드레일 회귀 테스트 — 2026-07-10 함대 전멸 사고를 테스트로 굳힌다.

산문 가드레일(CLAUDE.md)은 사람에게만 읽힌다. 자동으로 강제되는 건 테스트뿐이다.
여기 있는 건 전부 실제로 터진 사고다:

1. 게이트 플래그 유실 → 'disabled'로 위장 → 워치독이 재시작 시도조차 안 함
2. start_all_bots가 _KEEP_ON_SHUTDOWN을 기동에서도 건너뜀 → 워치독 자신이 부활 불가
3. 헤드리스 클로드 세션에 시크릿 상속 → 웹 인젝션의 유출 통로
"""
import importlib.util
import sys
import unittest
from pathlib import Path
from unittest import mock

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_TEAM_ROOT))

from _shared import notify  # noqa: E402
from _shared.cc import scrub_secrets  # noqa: E402

CONTROLLER_PATH = (AI_TEAM_ROOT / "skills" / "영숙_비서" / "tools" / "agent_controller.py")


def load_controller():
    spec = importlib.util.spec_from_file_location("agent_controller_under_test", CONTROLLER_PATH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class GateFlagStateTests(unittest.TestCase):
    """플래그 '부재'와 '명시적 OFF'는 다른 상태여야 한다."""

    def _status_with(self, env_value):
        env = {} if env_value is None else {"PETNNA_AGENTS_ON_WINDOWS": env_value}
        with mock.patch.object(notify, "_find_pids", return_value=[]), \
             mock.patch.object(sys, "platform", "win32"), \
             mock.patch.dict("os.environ", env, clear=True):
            return notify.agent_status()

    def test_missing_flag_is_misconfig_not_disabled(self):
        # 사고 재현: .env 재암호화 중 플래그가 사라지면 'disabled'로 위장돼 조용히 죽는다.
        status = self._status_with(None)
        for name in notify._PETNNA_WINDOWS_GATED:
            self.assertEqual(status[name], "misconfig", f"{name}: 플래그 부재는 misconfig여야 한다")

    def test_explicit_off_is_disabled(self):
        status = self._status_with("false")
        for name in notify._PETNNA_WINDOWS_GATED:
            self.assertEqual(status[name], "disabled", f"{name}: 명시적 OFF는 disabled")

    def test_flag_true_but_no_process_is_down(self):
        status = self._status_with("true")
        for name in notify._PETNNA_WINDOWS_GATED:
            self.assertEqual(status[name], "down", f"{name}: 가동 대상인데 프로세스 없으면 down")

    def test_status_report_surfaces_misconfig(self):
        with mock.patch.object(notify, "agent_status", return_value={"bomi_qa": "misconfig"}):
            self.assertIn("PETNNA_AGENTS_ON_WINDOWS", notify.status_report())


class StartAllBotsTests(unittest.TestCase):
    """_KEEP_ON_SHUTDOWN은 '정지 제외'이지 '기동 제외'가 아니다."""

    @staticmethod
    def _started_names(out: str) -> set[str]:
        """'기동(9): 나무, 미오, …' 줄을 파싱한다.

        부분문자열 검사(assertIn)를 쓰면 안 된다 — '영숙스케줄'이 '영숙'을 삼켜서
        수정 전 코드에서도 통과하는 거짓 초록이 나온다(실측 확인).
        """
        for line in out.splitlines():
            if line.startswith("기동("):
                return {n.strip() for n in line.split(":", 1)[1].split(",")}
        return set()

    def test_start_all_includes_watchdog_and_secretary(self):
        ctl = load_controller()
        # 이미 떠 있다고 보고 → start_agent는 호출되지 않지만 목록엔 잡혀야 한다.
        flag = mock.Mock()
        flag.exists.return_value = False  # WindowsPath 메서드는 패치 불가 → 객체째 교체
        with mock.patch.object(ctl, "find_agent_process", return_value=["1"]), \
             mock.patch.object(ctl, "start_agent") as spawn, \
             mock.patch.object(ctl, "BOTS_OFF_FLAG", flag):
            out = ctl.start_all_bots()
        spawn.assert_not_called()
        started = self._started_names(out)
        for name in ctl._KEEP_ON_SHUTDOWN:  # 영숙·예원
            self.assertIn(name, started,
                          f"봇다켜 목록에 {name}이 빠졌다 — 워치독이 부활 못 한다. 실제: {sorted(started)}")

    def test_stop_all_still_spares_them(self):
        # 반대 방향 보호: 정지에서는 여전히 제외되어야 한다(텔레그램 통로·워치독 유지).
        ctl = load_controller()
        self.assertEqual(ctl._KEEP_ON_SHUTDOWN, {"영숙", "예원"})

    def test_every_daemon_key_resolves_to_an_agent(self):
        # 워치독은 notify의 영어 키로 재시작을 건다 — 매핑이 없으면 조용히 실패한다.
        ctl = load_controller()
        for key in notify.CONTINUOUS_DAEMONS:
            self.assertIn(ctl.get_agent_name(key), ctl.AGENTS,
                          f"'{key}'가 AGENTS로 해석되지 않는다 — 자가복구 불능")


class ScrubSecretsTests(unittest.TestCase):
    """헤드리스 세션에 시크릿을 상속시키지 않는다(웹 인젝션 → 유출 차단)."""

    def test_removes_secret_shaped_keys(self):
        out = scrub_secrets({
            "GEMINI_API_KEY": "x", "TELEGRAM_BOT_TOKEN": "x",
            "SUPABASE_ANON_KEY": "x", "DB_PASSWORD": "x", "AWS_SECRET": "x",
        })
        self.assertEqual(out, {})

    def test_preserves_subscription_auth_and_behavior_vars(self):
        env = {"CLAUDE_CODE_OAUTH_TOKEN": "keep", "MAX_THINKING_TOKENS": "keep",
               "PATH": "keep", "SUPABASE_URL": "keep"}
        self.assertEqual(scrub_secrets(env), env)

    def test_anthropic_api_key_still_removed(self):
        # CLAUDE_* 보존 규칙이 ANTHROPIC_API_KEY까지 살려주면 안 된다(구독 OAuth 우회).
        self.assertNotIn("ANTHROPIC_API_KEY", scrub_secrets({"ANTHROPIC_API_KEY": "x"}))


HEARTBEAT_PATH = (AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools" / "fleet_heartbeat.py")


def load_heartbeat():
    spec = importlib.util.spec_from_file_location("fleet_heartbeat_under_test", HEARTBEAT_PATH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class EscalationPolicyTests(unittest.TestCase):
    """HANDBOOK §7 — 기준은 심각도가 아니라 '자동으로 낫는가'다."""

    def _run(self, status, prior_state=None):
        hb = load_heartbeat()
        saved = {}
        # 실제 BOTS_OFF 플래그 파일에 의존하면 안 된다 — 오너가 '봇 다 꺼'를 한 상태에서
        # 테스트를 돌리면 check()가 조기 종료해 5건이 한꺼번에 거짓 실패한다(실제로 겪음).
        no_flag = mock.Mock()
        no_flag.exists.return_value = False
        with mock.patch.object(hb, "BOTS_OFF_FLAG", no_flag), \
             mock.patch.object(hb, "agent_status", return_value=status), \
             mock.patch.object(hb, "send") as send, \
             mock.patch.object(hb, "_revive_watchdog", return_value="ok") as revive, \
             mock.patch.object(hb, "_load_state", return_value=dict(prior_state or {})), \
             mock.patch.object(hb, "_save_state", side_effect=saved.update):
            hb.check()
        return send, revive, saved

    def test_healthy_fleet_is_silent(self):
        send, _, _ = self._run({"youngsuk": "111", "yewon": "222"})
        send.assert_not_called()

    def test_bots_off_flag_suppresses_everything(self):
        # '봇 다 꺼'는 의도된 정지다. 경보도 부활도 하면 안 된다(오너가 기계를 옮길 때).
        hb = load_heartbeat()
        flag = mock.Mock()
        flag.exists.return_value = True
        with mock.patch.object(hb, "BOTS_OFF_FLAG", flag), \
             mock.patch.object(hb, "agent_status") as status, \
             mock.patch.object(hb, "send") as send, \
             mock.patch.object(hb, "_revive_watchdog") as revive:
            self.assertEqual(hb.check(), 0)
        status.assert_not_called()
        send.assert_not_called()
        revive.assert_not_called()

    def test_single_down_waits_one_cycle(self):
        # 워치독이 5분 내 살릴 수 있다 → 첫 감지는 조용히.
        send, _, saved = self._run({"youngsuk": "111", "yewon": "222", "bomi_qa": "down"})
        send.assert_not_called()
        self.assertEqual(saved["down_streak"]["count"], 1)

    def test_single_down_alerts_on_second_cycle(self):
        # 두 번째에도 같은 봇이 죽어 있으면 = 자동복구가 졌다.
        prior = {"down_streak": {"key": "down=bomi_qa|misconfig=", "count": 1}}
        send, _, _ = self._run({"yewon": "222", "bomi_qa": "down"}, prior)
        send.assert_called_once()
        self.assertIn("자동복구 실패", send.call_args[0][0])

    def test_misconfig_alerts_immediately(self):
        # 재시작으로 안 낫는다 → 유예 없이 즉시.
        send, _, _ = self._run({"yewon": "222", "bomi_qa": "misconfig"})
        send.assert_called_once()
        self.assertIn("PETNNA_AGENTS_ON_WINDOWS", send.call_args[0][0])

    def test_three_down_alerts_immediately(self):
        send, _, _ = self._run({"yewon": "222", "bomi_qa": "down",
                                "suri_dev": "down", "teo_test": "down"})
        send.assert_called_once()
        self.assertIn("동시 다운", send.call_args[0][0])

    def test_watchdog_down_alerts_and_revives(self):
        # 복구 책임자가 죽으면 아무도 못 살린다 → 즉시 + 직접 기동.
        send, revive, _ = self._run({"yewon": "down", "youngsuk": "111"})
        revive.assert_called_once()
        send.assert_called_once()


if __name__ == "__main__":
    unittest.main()
