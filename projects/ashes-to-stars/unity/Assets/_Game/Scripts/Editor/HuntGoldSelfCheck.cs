using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>필드 사냥 골드 — T1 1시간 = 1골드. QA_NO면 0(§18-1).</summary>
    public static class HuntGoldSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Hunt Gold Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(Economy.EnvShowHuntGold);
            string no = Environment.GetEnvironmentVariable(Economy.EnvNoHuntGold);
            Environment.SetEnvironmentVariable(Economy.EnvShowHuntGold, null);
            Environment.SetEnvironmentVariable(Economy.EnvNoHuntGold, null);

            GameState.ResetAll();
            SoftCap.ResetForTest();
            GameState.SetTowerFloorForTest(1);
            GameState.TrySelectTier(0);

            Check(Economy.HuntGoldHourSeconds == 3600, "1시간은 3600초");
            Check(Economy.WaveHuntGold(0, 0f) == 0, "0초는 골드 0");
            Check(Economy.WaveHuntGold(0, 3600f) == 10_000,
                $"T1 3600초 = 10000 (실제 {Economy.WaveHuntGold(0, 3600f)})");
            Check(Economy.WaveHuntGold(0, 240f) == 666,
                $"T1 240초 = 666 (실제 {Economy.WaveHuntGold(0, 240f)})");
            Check(Economy.WaveHuntGold(1, 3600f) == 16_000,
                $"T2 3600초 = 16000 (실제 {Economy.WaveHuntGold(1, 3600f)})");
            Check(Economy.WaveHuntGold(0, 1f) >= 1, "1초도 최소 1쿠퍼");

            long earned = GameState.Earn(Economy.WaveHuntGold(0, 3600f));
            Check(earned == 10_000, $"Earn 3600초 = 10000 (실제 {earned})");
            Check(GameState.Wallet.Copper == 10_000,
                $"지갑 1골드 (실제 {GameState.Wallet.Copper})");
            Check(Economy.HuntGoldLine(earned).Contains("1골드"),
                $"줄 1골드 (실제 {Economy.HuntGoldLine(earned)})");
            Check(Economy.HuntGoldHourLine().Contains("1골드"),
                $"시간당 1골드 (실제 {Economy.HuntGoldHourLine()})");

            GameState.ForgetInMemoryForTest();
            Check(GameState.Wallet.Copper == 10_000, "재기동 뒤에도 1골드");

            GameState.ResetAll();
            SoftCap.ResetForTest();
            GameState.SetTowerFloorForTest(1);
            Check(GameState.Earn(Economy.WaveHuntGold(0, 3600f)) == 10_000, "문턱 아래라 소프트캡 안 탐");

            Environment.SetEnvironmentVariable(Economy.EnvNoHuntGold, "1");
            Check(Economy.HuntGoldBlocked, "QA_NO면 차단");
            Check(Economy.WaveHuntGold(0, 3600f) == 0, "차단하면 3600초도 0");
            GameState.ResetAll();
            SoftCap.ResetForTest();
            Check(GameState.Earn(Economy.WaveHuntGold(0, 3600f)) == 0
                  && GameState.Wallet.Copper == 0,
                "차단하면 지갑 0");
            Check(Economy.HuntGoldHourLine().Contains("없음"),
                $"차단 문구 없음 (실제 {Economy.HuntGoldHourLine()})");
            Environment.SetEnvironmentVariable(Economy.EnvNoHuntGold, null);

            Check(Economy.WaveHuntGold(0, 3600f) == 10_000, "차단을 풀면 다시 10000");

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string battle = File.ReadAllText(Path.Combine(runtime, "BattleScreen.cs"));
            string field = File.ReadAllText(Path.Combine(runtime, "FieldScreen.cs"));
            string result = File.ReadAllText(Path.Combine(runtime, "ResultScreen.cs"));
            Check(battle.Contains("WaveHuntGold") && battle.Contains("Earn(huntGold)"),
                "필드 생존이 WaveHuntGold를 Earn한다");
            Check(field.Contains("HuntGoldHourLine"), "필드 자막이 시간당을 읽는다");
            Check(result.Contains("HuntGoldLine") && result.Contains("SeedHuntGoldRewardQaIfRequested"),
                "결과가 필드 골드 줄을 읽는다");
            Check(!battle.Contains("WaveHuntGold") || battle.IndexOf("ReturnTo == GameFlow.Field", StringComparison.Ordinal) >= 0,
                "필드 정산 안에 골드가 있다");

            Environment.SetEnvironmentVariable(Economy.EnvShowHuntGold, "1");
            GameState.ResetAll();
            SoftCap.ResetForTest();
            Economy.SeedHuntGoldQaIfRequested();
            Check(GameState.Tier == 0, $"QA 시드는 T1 (실제 {GameState.Tier})");
            BattleScreen.SeedHuntGoldRewardQaIfRequested();
            var reward = BattleScreen._GetLastReward();
            Check(reward != null && reward.Survived && reward.GoldReward == 10_000,
                $"QA 시드 보상 10000 (실제 {reward?.GoldReward})");
            Check(GameFlow.LastBattleSummary != null
                  && GameFlow.LastBattleSummary.Contains("1골드"),
                $"QA 요약 1골드 (실제 {GameFlow.LastBattleSummary})");
            Environment.SetEnvironmentVariable(Economy.EnvShowHuntGold, show);
            Environment.SetEnvironmentVariable(Economy.EnvNoHuntGold, no);

            _ = nameof(Economy.WaveHuntGold);
            _ = nameof(Economy.HuntGoldHourLine);
            _ = nameof(BattleScreen.SeedHuntGoldRewardQaIfRequested);

            GameState.ResetAll();
            SoftCap.ResetForTest();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();

            if (_fail == 0) Debug.Log("[HuntGoldSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[HuntGoldSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[HuntGoldSelfCheck] FAIL {_fail}건");
        }
    }
}
