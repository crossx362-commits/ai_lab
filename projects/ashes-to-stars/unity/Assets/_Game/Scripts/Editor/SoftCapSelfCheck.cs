using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>시간당 수익 소프트캡. T1 20000→16500. QA_NO면 그대로(§18-14).</summary>
    public static class SoftCapSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Soft Cap Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(SoftCap.EnvShow);
            string no = Environment.GetEnvironmentVariable(SoftCap.EnvNo);
            Environment.SetEnvironmentVariable(SoftCap.EnvShow, null);
            Environment.SetEnvironmentVariable(SoftCap.EnvNo, null);

            GameState.ResetAll();
            SoftCap.ResetForTest();
            RacePrefs.Set(RaceId.인간);
            GameState.SetTowerFloorForTest(1);

            Check(SoftCap.ExpectedCopper(0) == 10_000, $"T1 기대 10000 (실제 {SoftCap.ExpectedCopper(0)})");
            Check(SoftCap.ThresholdCopper(0) == 15_000, $"T1 문턱 15000 (실제 {SoftCap.ThresholdCopper(0)})");
            Check(SoftCap.Preview(10_000, 0) == 10_000, "10000은 그대로");
            Check(SoftCap.Preview(15_000, 0) == 15_000, "15000까지 그대로");
            Check(SoftCap.Preview(20_000, 0) == 16_500, $"20000→16500 (실제 {SoftCap.Preview(20_000, 0)})");
            Check(SoftCap.Preview(5_000, 10_000) == 5_000, "이미 10000이면 5000은 그대로");
            Check(SoftCap.Preview(5_000, 15_000) == 1_500, "문턱 위 5000은 1500");

            long t10 = SoftCap.ExpectedCopper(9);
            Check(t10 == (long)(Economy.TierRevenueMultiplier[9] * Economy.COPPER_PER_GOLD)
                  && t10 > 10_000, $"T10 기대가 T1보다 크다 (실제 {t10})");
            long t10Cap = SoftCap.ThresholdCopper(9);
            Check(SoftCap.Preview(t10Cap, 0, t10Cap) == t10Cap, "T10 문턱값은 그대로");
            Check(SoftCap.Preview(t10Cap + 10_000, 0, t10Cap) == t10Cap + 3_000,
                "T10 초과 10000은 3000");

            GameState.ResetAll();
            SoftCap.ResetForTest();
            GameState.SetTowerFloorForTest(1);
            long got = GameState.Earn(20_000);
            Check(got == 16_500, $"Earn(20000)=16500 (실제 {got})");
            Check(GameState.Wallet.Copper == 16_500, $"지갑 16500 (실제 {GameState.Wallet.Copper})");
            Check(SoftCap.EarnedThisHour == 16_500, $"시간창 16500 (실제 {SoftCap.EarnedThisHour})");
            Check(SoftCap.Line().Contains("150%"), $"문구 150% (실제 {SoftCap.Line()})");
            Check(SoftCap.Line().Contains("1골드 50실버"),
                $"문구 한도 1골드 50실버 (실제 {SoftCap.Line()})");

            long extra = GameState.Earn(10_000);
            Check(extra == 3_000, $"문턱 위 Earn(10000)=3000 (실제 {extra})");
            Check(GameState.Wallet.Copper == 19_500, $"지갑 19500 (실제 {GameState.Wallet.Copper})");

            GameState.ForgetInMemoryForTest();
            SoftCap.ForgetInMemoryForTest();
            Check(SoftCap.EarnedThisHour == 19_500, "재기동 뒤에도 시간창이 남는다");

            GameState.ResetAll();
            GameState.SetTowerFloorForTest(1);
            long now = 2_000_000_000;
            SoftCap.NowUnix = () => now;
            Check(GameState.Earn(15_000) == 15_000, "새 시간창 15000");
            SoftCap.NowUnix = () => now + SoftCap.HourSeconds;
            Check(GameState.Earn(20_000) == 16_500, "한 시간이 지나면 다시 20000→16500");

            Environment.SetEnvironmentVariable(SoftCap.EnvNo, "1");
            SoftCap.ResetForTest();
            GameState.ResetAll();
            GameState.SetTowerFloorForTest(1);
            Check(SoftCap.Blocked, "QA_NO_SOFT_CAP이면 차단");
            Check(GameState.Earn(20_000) == 20_000, $"차단하면 20000이 그대로 (실제 {GameState.Wallet.Copper})");
            Check(GameState.Wallet.Copper == 20_000, "차단 지갑 20000");
            Check(SoftCap.Line().Contains("없음"), $"차단 문구 없음 (실제 {SoftCap.Line()})");
            Environment.SetEnvironmentVariable(SoftCap.EnvNo, null);

            SoftCap.ResetForTest();
            GameState.ResetAll();
            GameState.SetTowerFloorForTest(1);
            Check(GameState.Earn(20_000) == 16_500, "차단을 풀면 다시 16500");

            long granted = GameState.Grant(50_000);
            Check(granted == 50_000 && GameState.Wallet.Copper == 66_500,
                $"Grant는 소프트캡을 안 탄다 (실제 {granted}, 지갑 {GameState.Wallet.Copper})");

            GameState.ResetAll();
            SoftCap.ResetForTest();
            InvasionState.ResetForTest();
            DefenseState.ResetForTest();
            EstateDefense.ResetForTest();
            WorldStar.ResetForTest();
            GameState.SetTowerFloorForTest(1);
            GameState.Grant(200_000);
            long formula = InvasionState.LootCopper();
            Check(InvasionState.TryBegin(), "출정");
            long settled = InvasionState.Settle(true);
            Check(settled == formula && formula > 0 && formula <= 15_000,
                $"T1 약탈은 문턱 아래라 정산=공식 (공식 {formula}, 정산 {settled})");
            Check(InvasionState.LastLoot == formula, "문턱 아래면 받은 금액=공식");
            InvasionState.ResetForTest();
            GameState.ResetAll();
            SoftCap.ResetForTest();
            GameState.SetTowerFloorForTest(1);
            GameState.Grant(200_000);
            InvasionState.ForceLootBeforeCap = 20_000;
            Check(InvasionState.LootCopper() == 20_000, "강제 약탈 20000");
            Check(InvasionState.TryBegin(), "상한 앞 20000 출정");
            long cappedLoot = InvasionState.Settle(true);
            Check(cappedLoot == 20_000, $"정산 반환은 공식 20000 (실제 {cappedLoot})");
            Check(InvasionState.LastLoot == 16_500,
                $"약탈 20000이 Earn에서 16500 (실제 {InvasionState.LastLoot})");
            InvasionState.ForceLootBeforeCap = 0;

            Environment.SetEnvironmentVariable(SoftCap.EnvShow, "1");
            GameState.ResetAll();
            SoftCap.ResetForTest();
            GameState.SetTowerFloorForTest(21);
            GameState.TrySelectTier(2);
            SoftCap.SeedQaIfRequested();
            Check(GameState.Tier == 0, $"시드는 T1 (실제 T{GameState.Tier + 1})");
            Check(GameState.Wallet.Copper == 16_500, $"시드 지갑 16500 (실제 {GameState.Wallet.Copper})");
            Check(SoftCap.HourLine().Contains("150%"), "시드 문구 150%");
            Check(SoftCap.HourLine().Contains("1골드 65실버"),
                $"시드 이번 시간 1골드 65실버 (실제 {SoftCap.HourLine()})");
            Environment.SetEnvironmentVariable(SoftCap.EnvShow, null);

            _ = nameof(SoftCap.Apply);
            _ = nameof(SoftCap.Preview);
            _ = nameof(SoftCap.Line);
            _ = nameof(SoftCap.SeedQaIfRequested);
            _ = nameof(GameState.Earn);
            _ = nameof(GameState.Grant);

            Environment.SetEnvironmentVariable(SoftCap.EnvShow, show);
            Environment.SetEnvironmentVariable(SoftCap.EnvNo, no);
            SoftCap.ResetForTest();
            InvasionState.ResetForTest();
            DefenseState.ResetForTest();
            EstateDefense.ResetForTest();
            WorldStar.ResetForTest();
            GameState.ResetAll();

            if (_fail > 0)
            {
                Debug.LogError("[SoftCapSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("SoftCapSelfCheck FAIL " + _fail);
            }
            Debug.Log("[SoftCapSelfCheck] PASS\n" + _log);
        }
    }
}
