using System;
using System.Text;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>침략당한 직후 보호막 12시간 = 수비대 회복(§15·§18-13).</summary>
    public static class InvasionShieldSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(InvasionState.EnvShow);
            string no = Environment.GetEnvironmentVariable(InvasionState.EnvNo);
            Environment.SetEnvironmentVariable(InvasionState.EnvShow, null);
            Environment.SetEnvironmentVariable(InvasionState.EnvNo, null);
            Environment.SetEnvironmentVariable("QA_LOAN_OVERDUE", null);

            GameState.ResetAll();
            InvasionState.ResetForTest();

            Check(InvasionState.GuardSeconds == 12L * 3600L, "보호막은 12시간(§15)");
            Check(InvasionState.DefenseRecoverSeconds == InvasionState.GuardSeconds,
                "수비대 회복과 같은 값 — 한쪽만 바꾸지 않는다");
            Check(!InvasionState.ShieldActive, "시작 때 보호막 없음");

            GameState.SetTowerFloorForTest(30);
            GameState.Earn(1_000_000);
            long t0 = 1_700_000_000L;
            InvasionState.NowUnix = () => t0;
            Check(InvasionState.TryBegin(), "첫 출정");
            Check(!InvasionState.ShieldActive, "전투 중에는 아직 보호막이 없다");
            InvasionState.Settle(true);
            Check(InvasionState.ShieldActive, "승패와 무관하게 정산하면 보호막");
            Check(InvasionState.RemainingSeconds() == InvasionState.GuardSeconds,
                $"남은 초 = 12시간 (실제 {InvasionState.RemainingSeconds()})");
            Check(InvasionState.ShieldUntil == t0 + InvasionState.GuardSeconds, "만료 시각");
            Check(!InvasionState.TryBegin(), "보호막 중 출정 거부");
            Check(!GameFlow.TryGoInvasion(), "보호막 중 전투 진입 거부");
            string lockReason = WorldMapScreen.InvasionHubLockReason();
            Check(lockReason != null && lockReason.Contains("보호막") && lockReason.Contains("12시간"),
                $"월드맵이 보호막을 말한다 (실제 {lockReason})");
            Check(InvasionState.ShieldText().Contains("12시간"),
                $"문구 12시간 (실제 {InvasionState.ShieldText()})");

            InvasionState.NowUnix = () => t0 + InvasionState.GuardSeconds - 1;
            Check(InvasionState.ShieldActive && InvasionState.RemainingSeconds() == 1,
                "만료 1초 전은 아직 막힘");
            Check(!InvasionState.TryBegin(), "만료 1초 전 거부");

            InvasionState.NowUnix = () => t0 + InvasionState.GuardSeconds + 1;
            Check(!InvasionState.ShieldActive, "12시간 1초 뒤 보호막 종료");
            Check(WorldMapScreen.InvasionHubLockReason() == null, "만료 뒤 월드맵 잠금 해제");
            Check(InvasionState.TryBegin(), "만료 뒤 재출정");
            InvasionState.Settle(false);
            Check(InvasionState.ShieldActive, "패배 정산도 보호막을 건다");

            InvasionState.ForgetInMemoryForTest();
            InvasionState.NowUnix = () => t0 + InvasionState.GuardSeconds + 1;
            Check(InvasionState.ShieldActive, "재기동 뒤에도 보호막이 남는다");
            Check(InvasionState.RemainingSeconds() == InvasionState.GuardSeconds,
                "재기동 뒤 남은 시간도 12시간");

            GameState.ResetAll();
            InvasionState.ResetForTest();
            GameState.SetTowerFloorForTest(30);
            GameState.Earn(1_000_000);
            InvasionState.NowUnix = () => t0;
            Environment.SetEnvironmentVariable(InvasionState.EnvNo, "1");
            Check(InvasionState.TryBegin(), "QA_NO면 첫 출정");
            InvasionState.Settle(true);
            Check(!InvasionState.ShieldActive, "QA_NO_INVASION_SHIELD면 보호막 없음");
            Check(InvasionState.TryBegin(), "QA_NO면 바로 재출정");
            InvasionState.Settle(false);
            Environment.SetEnvironmentVariable(InvasionState.EnvNo, null);

            GameState.ResetAll();
            InvasionState.ResetForTest();
            Environment.SetEnvironmentVariable(InvasionState.EnvShow, "1");
            InvasionState.NowUnix = () => t0;
            InvasionState.SeedQaIfRequested();
            Check(GameState.TowerFloor >= 30, "시드가 30층을 연다");
            Check(InvasionState.ShieldActive, "QA_INVASION_SHIELD=1이 보호막을 심는다");
            Check(WorldMapScreen.InvasionHubLockReason().Contains("보호막"),
                "시드 화면이 보호막 잠금");
            Environment.SetEnvironmentVariable(InvasionState.EnvShow, null);

            _ = nameof(InvasionState.ArmShield);
            _ = nameof(InvasionState.SeedQaIfRequested);
            _ = nameof(InvasionState.ShieldBlockReason);
            _ = nameof(InvasionState.ShieldActive);
            _ = nameof(WorldMapScreen.InvasionHubLockReason);

            Environment.SetEnvironmentVariable(InvasionState.EnvShow, show);
            Environment.SetEnvironmentVariable(InvasionState.EnvNo, no);
            GameState.ResetAll();
            InvasionState.ResetForTest();

            if (_fail > 0)
            {
                Debug.LogError("[InvasionShieldSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("InvasionShieldSelfCheck FAIL " + _fail);
            }
            Debug.Log("[InvasionShieldSelfCheck] PASS\n" + _log);
        }
    }
}
