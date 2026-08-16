using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>전직 재료 드랍은 RaceDef.전직재료배율을 읽는다. 인간 +15% · 나머지 100%(§3·§18-9).</summary>
    public static class RaceAdvMatSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Race Adv Mat Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(Economy.EnvShowAdvMat);
            string no = Environment.GetEnvironmentVariable(Economy.EnvNoAdvMat);
            string dropNo = Environment.GetEnvironmentVariable(Economy.EnvNoDrop);
            RaceId oldRace = RacePrefs.Get();
            float oldForce = Economy.ForceRaceAdvMatMul;
            float oldDrop = Economy.ForceRaceDropMul;
            Environment.SetEnvironmentVariable(Economy.EnvShowAdvMat, null);
            Environment.SetEnvironmentVariable(Economy.EnvNoAdvMat, null);
            Environment.SetEnvironmentVariable(Economy.EnvNoDrop, null);
            Economy.ForceRaceAdvMatMul = 0f;
            Economy.ForceRaceDropMul = 0f;

            RacePrefs.Set(RaceId.인간);
            Check(Economy.RaceAdvMatPercent() == Economy.HumanAdvMatPercent,
                $"인간 전직 재료 115 (실제 {Economy.RaceAdvMatPercent()})");
            Check(Math.Abs(Economy.ApplyAdvMatRate(0.35f) - 0.4025f) < 1e-6,
                $"인간 던전 보스 40.25% (실제 {Economy.ApplyAdvMatRate(0.35f)})");
            Check(Math.Abs(Economy.ApplyDropRate(0.50f) - 0.50f) < 1e-6,
                $"인간 가죽은 그대로 50% (실제 {Economy.ApplyDropRate(0.50f)})");
            Check(Economy.RaceAdvMatLine().Contains("+15%"),
                $"인간 문구 +15% (실제 {Economy.RaceAdvMatLine()})");

            RacePrefs.Set(RaceId.엘프);
            Check(Economy.RaceAdvMatPercent() == Economy.OtherAdvMatPercent
                    && Math.Abs(Economy.ApplyAdvMatRate(0.35f) - 0.35f) < 1e-6,
                $"엘프도 기준값 ({Economy.ApplyAdvMatRate(0.35f)})");

            RacePrefs.Set(RaceId.드워프);
            Check(Economy.RaceAdvMatPercent() == Economy.OtherAdvMatPercent,
                $"드워프도 100 (실제 {Economy.RaceAdvMatPercent()})");

            RacePrefs.Set(RaceId.수인);
            Check(Economy.RaceAdvMatPercent() == Economy.OtherAdvMatPercent,
                $"수인 전직 재료 배율은 100 (실제 {Economy.RaceAdvMatPercent()})");
            Check(Math.Abs(Economy.ApplyDropRate(0.35f) - 0.4025f) < 1e-6,
                $"수인은 드랍률로만 35→40.25 (실제 {Economy.ApplyDropRate(0.35f)})");

            int humanHits = CountMaterial(RaceId.인간, 2000u);
            int elfHits = CountMaterial(RaceId.엘프, 2000u);
            Check(humanHits > elfHits,
                $"같은 시드에서 인간이 더 맞는다 (인간 {humanHits} / 엘프 {elfHits})");
            Check(humanHits * 100 >= elfHits * 108,
                $"인간이 적어도 +8% (인간 {humanHits} / 엘프 {elfHits})");

            RacePrefs.Set(RaceId.인간);
            Environment.SetEnvironmentVariable(Economy.EnvNoAdvMat, "1");
            Check(Economy.RaceAdvMatPercent() == Economy.OtherAdvMatPercent,
                "QA_NO_RACE_ADV이면 인간도 100");
            Check(Math.Abs(Economy.ApplyAdvMatRate(0.35f) - 0.35f) < 1e-6,
                "차단하면 재료 35%");
            Environment.SetEnvironmentVariable(Economy.EnvNoAdvMat, null);

            RacePrefs.Set(RaceId.인간);
            Check(Economy.RaceAdvMatPercent() == Economy.HumanAdvMatPercent,
                "재기동 뒤에도 인간 115");

            Environment.SetEnvironmentVariable(Economy.EnvShowAdvMat, "1");
            BattleScreen.SeedRaceAdvMatRewardQaIfRequested();
            var reward = BattleScreen._GetLastReward();
            Check(RacePrefs.Get() == RaceId.인간, "시드는 인간을 고른다");
            Check(reward != null && reward.Survived, "시드 보상은 승리다");
            Check(reward.DroppedItems != null
                  && reward.DroppedItems.Contains(Economy.LifeItem.AdvancementMaterial),
                "시드 전직 재료 1개");
            Check(Economy.RaceAdvMatLine().Contains("+15%"), "시드 화면 문구 +15%");
            Check(!string.IsNullOrEmpty(GameFlow.LastBattleSummary)
                  && GameFlow.LastBattleSummary.Contains("+15%"),
                $"시드 요약 +15% (실제 {GameFlow.LastBattleSummary})");
            Environment.SetEnvironmentVariable(Economy.EnvShowAdvMat, null);

            _ = nameof(Economy.RaceAdvMatPercent);
            _ = nameof(Economy.ApplyAdvMatRate);
            _ = nameof(Economy.RaceAdvMatLine);
            _ = nameof(BattleScreen.SeedRaceAdvMatRewardQaIfRequested);
            _ = nameof(RaceDef.전직재료배율);

            Environment.SetEnvironmentVariable(Economy.EnvShowAdvMat, show);
            Environment.SetEnvironmentVariable(Economy.EnvNoAdvMat, no);
            Environment.SetEnvironmentVariable(Economy.EnvNoDrop, dropNo);
            Economy.ForceRaceAdvMatMul = oldForce;
            Economy.ForceRaceDropMul = oldDrop;
            RacePrefs.Set(oldRace);

            if (_fail > 0)
            {
                Debug.LogError("[RaceAdvMatSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("RaceAdvMatSelfCheck FAIL " + _fail);
            }
            Debug.Log("[RaceAdvMatSelfCheck] PASS\n" + _log);
        }

        static int CountMaterial(RaceId race, uint n)
        {
            RacePrefs.Set(race);
            int hits = 0;
            for (uint s = 1; s <= n; s++)
            {
                var rng = Rng.Stream(s, 0, SeedChannel.Drop);
                foreach (var d in Economy.RollBattleDrops(Economy.DropSource.FieldDungeonBoss, 1, ref rng))
                    if (d == Economy.LifeItem.AdvancementMaterial) hits++;
            }
            return hits;
        }
    }
}
