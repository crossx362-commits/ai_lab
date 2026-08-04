"""예원 자동 복구 — 경고 대신 스스로 고친다 (오너 지시 2026-08-04).

오너 지시: "경고 메세지는 알아서 해결하고 텔레그램으로 보내지 말라고 했자너."
신선도 감사가 무갱신 에이전트를 발견하면 사람을 부르지 말고 직접 1회 실행해 깨우고,
**깨우지 못한 것만** 텔레그램으로 보낸다.

여기서 굳히는 것:
  1. 무갱신 에이전트를 실제로 깨운다(등록된 스크립트를 실행한다).
  2. 자동 복구한 것은 텔레그램에 안 나간다 — 이 단언이 깨지면 오너 지시 위반이다.
  3. 못 깨운 것은 반드시 나간다 — 조용해지려다 진짜 장애를 삼키면 안 된다.
  4. AGENT_SCRIPTS의 경로가 전부 실재한다. 2026-07-12에 존재하지 않는 경로로
     Popen을 3시간 넘게 성공처럼 돌린 사고가 있었다(Popen 성공 ≠ 스크립트 실행).
  5. 쿨다운 안에는 다시 깨우지 않는다(재점화 루프 방지).
"""
import importlib.util
import sys
import time
import unittest
from pathlib import Path
from unittest import mock

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_TEAM_ROOT))

_PATH = AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools" / "petnna_fleet_health.py"


def _load(tmp: Path):
    spec = importlib.util.spec_from_file_location("fh_autofix", _PATH)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    mod.PROJECT_ROOT = tmp
    mod.STATE_PATH = tmp / "state.json"
    return mod


class AgentScriptRegistryTests(unittest.TestCase):
    def test_every_registered_script_exists(self):
        mod = _load(AI_TEAM_ROOT)
        self.assertEqual(set(mod.AGENT_SCRIPTS), {lbl for lbl, *_ in mod.AGENTS},
                         "AGENTS와 AGENT_SCRIPTS의 라벨이 어긋난다 — 한쪽만 고치면 복구가 조용히 빠진다")
        for label, (rel, flags) in mod.AGENT_SCRIPTS.items():
            path = AI_TEAM_ROOT / "skills" / rel
            self.assertTrue(path.is_file(), f"{label}: 등록된 스크립트가 없다 → {rel}")
            src = path.read_text(encoding="utf-8")
            for flag in flags:
                self.assertIn(f'"{flag}"', src,
                              f"{label}: {flag} 인자를 그 스크립트가 받지 않는다")


class StaleRemediationTests(unittest.TestCase):
    def setUp(self):
        import tempfile
        self._tmp = tempfile.TemporaryDirectory()
        self.tmp = Path(self._tmp.name)
        self.mod = _load(self.tmp)
        # 단일 기계 가드는 통과시킨다(이 테스트의 관심사가 아니다).
        p = mock.patch("_shared.process.petnna_single_machine_guard", return_value=False)
        p.start(); self.addCleanup(p.stop)

    def tearDown(self):
        self._tmp.cleanup()

    def test_kicks_stale_agent(self):
        with mock.patch.object(self.mod.subprocess, "Popen") as popen:
            kicked, unfixable = self.mod.remediate_stale_agents(["봄이(QA)"])
        self.assertEqual(kicked, ["봄이(QA)"])
        self.assertEqual(unfixable, [])
        argv = popen.call_args[0][0]
        self.assertTrue(str(argv[1]).endswith("petnna_qa_patrol.py"), argv)
        self.assertIn("--once", argv)

    def test_missing_script_is_reported_not_launched(self):
        self.mod.AGENT_SCRIPTS = dict(self.mod.AGENT_SCRIPTS,
                                      **{"봄이(QA)": ("없는폴더/없는파일.py", ["--once"])})
        with mock.patch.object(self.mod.subprocess, "Popen") as popen:
            kicked, unfixable = self.mod.remediate_stale_agents(["봄이(QA)"])
        popen.assert_not_called()  # 경로가 틀렸는데 띄우면 성공처럼 보인다(2026-07-12 사고)
        self.assertEqual(kicked, [])
        self.assertTrue(unfixable and "스크립트 없음" in unfixable[0])

    def test_cooldown_blocks_reignition(self):
        with mock.patch.object(self.mod.subprocess, "Popen"):
            self.mod.remediate_stale_agents(["봄이(QA)"])
            kicked, unfixable = self.mod.remediate_stale_agents(["봄이(QA)"])
        self.assertEqual(kicked, [], "쿨다운 안인데 또 깨웠다")
        self.assertTrue(unfixable, "쿨다운으로 못 깨운 건 사람에게 보고돼야 한다")

    def test_non_primary_machine_never_launches(self):
        with mock.patch("_shared.process.petnna_single_machine_guard", return_value=True), \
             mock.patch.object(self.mod.subprocess, "Popen") as popen:
            kicked, unfixable = self.mod.remediate_stale_agents(["봄이(QA)"])
        popen.assert_not_called()
        self.assertEqual(kicked, [])
        self.assertEqual(unfixable, ["봄이(QA)"])


class SilencePolicyTests(unittest.TestCase):
    """자동으로 고친 것은 텔레그램에 안 나간다 — 오너 지시의 핵심."""

    def setUp(self):
        import tempfile
        self._tmp = tempfile.TemporaryDirectory()
        self.tmp = Path(self._tmp.name)
        self.mod = _load(self.tmp)
        (self.tmp / "projects" / "petnna").mkdir(parents=True, exist_ok=True)
        (self.tmp / "projects" / "petnna" / "index.html").write_text("", encoding="utf-8")
        for name, ret in [("migration_gap", []), ("deploy_drift", (0, [])),
                          ("remediate_stale_branches", []),
                          ("remediate_backlog_ghosts", ([], []))]:
            p = mock.patch.object(self.mod, name, return_value=ret)
            p.start(); self.addCleanup(p.stop)
        self.sent = []
        p = mock.patch.object(self.mod, "send", side_effect=lambda *a, **k: self.sent.append(a[0]))
        p.start(); self.addCleanup(p.stop)

    def tearDown(self):
        self._tmp.cleanup()

    def test_auto_recovered_stale_sends_nothing(self):
        with mock.patch.object(self.mod, "remediate_stale_agents", return_value=(["봄이(QA)"], [])):
            n = self.mod.audit(do_send=True)
        self.assertEqual(self.sent, [], "자동 복구한 무갱신을 텔레그램으로 보냈다 — 오너 지시 위반")
        self.assertEqual(n, 0)

    def test_unrecoverable_stale_still_alerts(self):
        with mock.patch.object(self.mod, "remediate_stale_agents",
                               return_value=([], ["봄이(QA): 스크립트 없음"])):
            n = self.mod.audit(do_send=True)
        self.assertEqual(len(self.sent), 1, "못 고친 장애까지 삼켰다 — 조용해지려다 눈이 멀면 안 된다")
        self.assertIn("봄이(QA)", self.sent[0])
        self.assertEqual(n, 1)

    def test_auto_applied_migration_sends_nothing(self):
        with mock.patch.object(self.mod, "migration_gap", side_effect=[[("t", "a.sql")], []]), \
             mock.patch.object(self.mod, "apply_pending_migrations", return_value=(["a.sql"], [])), \
             mock.patch.dict("os.environ", {"SUPABASE_ACCESS_TOKEN": "x"}), \
             mock.patch.object(self.mod, "remediate_stale_agents", return_value=([], [])):
            self.mod.audit(do_send=True)
        self.assertEqual(self.sent, [], "자동 적용된 마이그레이션을 알렸다 — 고친 건 보고 대상이 아니다")

    def test_self_healed_deploy_drift_sends_nothing(self):
        with mock.patch.object(self.mod, "deploy_drift", return_value=(2, ["js/a.js"])), \
             mock.patch.object(self.mod, "remediate_deploy_drift",
                               return_value=("fixed", "재배포 트리거함")), \
             mock.patch.object(self.mod, "remediate_stale_agents", return_value=([], [])):
            self.mod.audit(do_send=True)
        self.assertEqual(self.sent, [], "예원이 재배포로 해결한 표류를 알렸다")

    def test_blocked_deploy_drift_alerts_with_reason(self):
        with mock.patch.object(self.mod, "deploy_drift", return_value=(2, ["js/a.js"])), \
             mock.patch.object(self.mod, "remediate_deploy_drift",
                               return_value=("blocked", "직전 배포 실패: schema error")), \
             mock.patch.object(self.mod, "remediate_stale_agents", return_value=([], [])):
            self.mod.audit(do_send=True)
        self.assertEqual(len(self.sent), 1)
        self.assertIn("schema error", self.sent[0], "사유 없는 경보는 사람이 또 조사해야 한다")


if __name__ == "__main__":
    unittest.main()
