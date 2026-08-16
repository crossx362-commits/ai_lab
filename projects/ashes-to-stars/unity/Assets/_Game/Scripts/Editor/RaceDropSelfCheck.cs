using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>보스 드랍은 RaceDef.드랍률배율을 읽는다. 수인 +15% · 나머지 100%(§3·§18-9).</summary>
    public static class RaceDropSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Race Drop Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(Economy.EnvShowDrop);
            string no = Environment.GetEnvironmentVariable(Economy.EnvNoDrop);
            RaceId oldRace = RacePrefs.Get();
            float oldForce = Economy.ForceRaceDropMul;
            Environment.SetEnvironmentVariable(Economy.EnvShowDrop, null);
            Environment.SetEnvironmentVariable(Economy.EnvNoDrop, null);
            Economy.ForceRaceDropMul = 0f;

            RacePrefs.Set(RaceId.인간);
            Check(Economy.RaceDropPercent() == Economy.HumanDropPercent,
                $"인간 드랍 100 (실제 {Economy.RaceDropPercent()})");
            Check(Math.Abs(Economy.ApplyDropRate(0.50f) - 0.50f) < 1e-6,
                $"인간 가죽 50% 유지 (실제 {Economy.ApplyDropRate(0.50f)})");
            Check(Economy.RaceDropLine().Contains("없음"),
                $"인간 문구는 배율 없음 (실제 {Economy.RaceDropLine()})");

            RacePrefs.Set(RaceId.엘프);
            Check(Economy.RaceDropPercent() == Economy.HumanDropPercent
                    && Math.Abs(Economy.ApplyDropRate(0.02f) - 0.02f) < 1e-6,
                $"엘프도 기준값 ({Economy.ApplyDropRate(0.02f)})");

            RacePrefs.Set(RaceId.드워프);
            Check(Economy.RaceDropPercent() == Economy.HumanDropPercent,
                $"드워프도 100 (실제 {Economy.RaceDropPercent()})");

            RacePrefs.Set(RaceId.수인);
            Check(Economy.RaceDropPercent() == Economy.BeastDropPercent,
                $"수인 드랍 115 (실제 {Economy.RaceDropPercent()})");
            Check(Math.Abs(Economy.ApplyDropRate(0.50f) - 0.575f) < 1e-6,
                $"수인 가죽 57.5% (실제 {Economy.ApplyDropRate(0.50f)})");
            Check(Math.Abs(Economy.ApplyDropRate(0.02f) - 0.023f) < 1e-6,
                $"수인 증표 2.3% (실제 {Economy.ApplyDropRate(0.02f)})");
            Check(Economy.RaceDropLine().Contains("+15%"),
                $"수인 문구 +15% (실제 {Economy.RaceDropLine()})");

            int humanHits = CountHide(RaceId.인간, 2000u);
            int beastHits = CountHide(RaceId.수인, 2000u);
            Check(beastHits > humanHits,
                $"같은 시드에서 수인이 더 맞는다 (인간 {humanHits} / 수인 {beastHits})");
            Check(beastHits * 100 >= humanHits * 108,
                $"수인이 적어도 +8% (인간 {humanHits} / 수인 {beastHits})");

            RacePrefs.Set(RaceId.수인);
            Environment.SetEnvironmentVariable(Economy.EnvNoDrop, "1");
            Check(Economy.RaceDropPercent() == Economy.HumanDropPercent,
                "QA_NO_RACE_DROP이면 수인도 100");
            Check(Math.Abs(Economy.ApplyDropRate(0.50f) - 0.50f) < 1e-6,
                "차단하면 가죽 50%");
            Environment.SetEnvironmentVariable(Economy.EnvNoDrop, null);

            RacePrefs.Set(RaceId.수인);
            Check(Economy.RaceDropPercent() == Economy.BeastDropPercent,
                "재기동 뒤에도 수인 115");

            Environment.SetEnvironmentVariable(Economy.EnvShowDrop, "1");
            BattleScreen.SeedRaceDropRewardQaIfRequested();
            var reward = BattleScreen._GetLastReward();
            Check(RacePrefs.Get() == RaceId.수인, "시드는 수인을 고른다");
            Check(reward != null && reward.Survived, "시드 보상은 승리다");
            Check(reward.DroppedItems != null
                  && reward.DroppedItems.Contains(Economy.LifeItem.CraftHide),
                "시드 가죽 1장");
            Check(Economy.RaceDropLine().Contains("+15%"), "시드 화면 문구 +15%");
            Check(!string.IsNullOrEmpty(GameFlow.LastBattleSummary)
                  && GameFlow.LastBattleSummary.Contains("+15%"),
                $"시드 요약 +15% (실제 {GameFlow.LastBattleSummary})");
            Environment.SetEnvironmentVariable(Economy.EnvShowDrop, null);

            _ = nameof(Economy.RaceDropPercent);
            _ = nameof(Economy.ApplyDropRate);
            _ = nameof(Economy.RaceDropLine);
            _ = nameof(BattleScreen.SeedRaceDropRewardQaIfRequested);
            _ = nameof(RaceDef.드랍률배율);

            Environment.SetEnvironmentVariable(Economy.EnvShowDrop, show);
            Environment.SetEnvironmentVariable(Economy.EnvNoDrop, no);
            Economy.ForceRaceDropMul = oldForce;
            RacePrefs.Set(oldRace);

            if (_fail > 0)
            {
                Debug.LogError("[RaceDropSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("RaceDropSelfCheck FAIL " + _fail);
            }
            Debug.Log("[RaceDropSelfCheck] PASS\n" + _log);
        }

        static int CountHide(RaceId race, uint n)
        {
            RacePrefs.Set(race);
            int hits = 0;
            for (uint s = 1; s <= n; s++)
            {
                var rng = Rng.Stream(s, 0, SeedChannel.Drop);
                foreach (var d in Economy.RollBattleDrops(Economy.DropSource.FieldDungeonBoss, 1, ref rng))
                    if (d == Economy.LifeItem.CraftHide) hits++;
            }
            return hits;
        }
    }
}
