using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>영공 적 디버프를 침략 약탈이 읽는다. 켜면 95% · QA_NO면 100(§14).</summary>
    public static class AuraDebuffSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Aura Debuff Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(WorldStar.EnvShowDebuff);
            string no = Environment.GetEnvironmentVariable(WorldStar.EnvNoDebuff);
            Environment.SetEnvironmentVariable(WorldStar.EnvShowDebuff, null);
            Environment.SetEnvironmentVariable(WorldStar.EnvNoDebuff, null);
            Environment.SetEnvironmentVariable(InvasionState.EnvShow, null);
            Environment.SetEnvironmentVariable(InvasionState.EnvShowLoot, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            InvasionState.ResetForTest();
            DefenseState.ResetForTest();
            EstateDefense.ResetForTest();
            WorldStar.ResetForTest();
            GameState.SetTowerFloorForTest(WorldMapScreen.InvasionUnlockFloor);
            GameState.Grant(100_000);

            Check(!WorldStar.EnemyDebuff && WorldStar.EnemyPercent() == 100,
                "기본은 디버프 꺼짐 100");
            Check(WorldStar.EnemyMul == 1f, "꺼지면 EnemyMul=1");
            Check(WorldStar.ApplyEnemy(1000) == 1000, "꺼지면 1000 유지");
            Check(WorldStar.EnemyLine().Contains("없음"),
                $"꺼짐 문구 (실제 {WorldStar.EnemyLine()})");
            long off = InvasionState.LootCopper();
            Check(off > 0, $"기준 약탈 > 0 (실제 {off})");

            WorldStar.EnemyDebuff = true;
            Check(WorldStar.EnemyPercent() == WorldStar.EnemyDebuffPercent,
                $"켜면 95 (실제 {WorldStar.EnemyPercent()})");
            Check(Mathf.Abs(WorldStar.EnemyMul - WorldStar.EnemyDebuffMul) < 0.001f,
                $"EnemyMul 0.95 (실제 {WorldStar.EnemyMul})");
            Check(WorldStar.ApplyEnemy(1000) == 950, "1000→950");
            long on = InvasionState.LootCopper();
            Check(on == off * WorldStar.EnemyDebuffPercent / 100,
                $"같은 티어 약탈이 95% (꺼짐 {off} / 켜짐 {on})");
            Check(WorldStar.EnemyLine().Contains("−5%"),
                $"켜짐 문구 −5% (실제 {WorldStar.EnemyLine()})");

            Environment.SetEnvironmentVariable(WorldStar.EnvNoDebuff, "1");
            Check(WorldStar.EnemyPercent() == 100, "QA_NO_AURA_DEBUFF면 켜져도 100");
            Check(InvasionState.LootCopper() == off, "차단하면 켜짐=꺼짐");
            Environment.SetEnvironmentVariable(WorldStar.EnvNoDebuff, null);

            Check(InvasionState.LootCopper() == on, "차단을 풀면 다시 95");

            long gold0 = GameState.Wallet.Copper;
            Check(InvasionState.TryBegin(), "출정");
            long loot = InvasionState.Settle(true);
            Check(loot == on, $"정산 약탈이 95% (실제 {loot})");
            Check(GameState.Wallet.Copper == gold0 - InvasionState.SortieCost() + loot
                    || GameState.Wallet.Copper == gold0 + loot,
                "약탈이 지갑에 들어온다");
            InvasionState.ResetForTest();
            DefenseState.ResetForTest();
            EstateDefense.ResetForTest();
            GameState.ResetAll();
            WorldStar.ResetForTest();
            WorldStar.EnemyDebuff = true;
            GameState.SetTowerFloorForTest(WorldMapScreen.InvasionUnlockFloor);
            GameState.Grant(100_000);

            Environment.SetEnvironmentVariable(WorldStar.EnvShowDebuff, "1");
            InvasionState.SeedAuraDebuffQaIfRequested();
            Check(WorldStar.EnemyDebuff, "시드는 적 디버프를 켠다");
            Check(RacePrefs.Get() == RaceId.인간, "시드는 인간을 고른다");
            Check(GameState.TowerFloor >= WorldMapScreen.InvasionUnlockFloor, "시드는 30층");
            Check(!InvasionState.ShieldActive, "시드는 보호막을 안 건다");
            Check(WorldStar.EnemyLine().Contains("−5%"), "시드 화면 문구 −5%");
            Check(WorldMapScreen.InvasionHubLockReason() == null, "시드 침략 카드가 열린다");
            Environment.SetEnvironmentVariable(WorldStar.EnvShowDebuff, null);

            _ = nameof(WorldStar.EnemyMul);
            _ = nameof(WorldStar.ApplyEnemy);
            _ = nameof(WorldStar.EnemyPercent);
            _ = nameof(WorldStar.EnemyLine);
            _ = nameof(WorldStar.SeedAuraDebuffQaIfRequested);
            _ = nameof(InvasionState.SeedAuraDebuffQaIfRequested);

            Environment.SetEnvironmentVariable(WorldStar.EnvShowDebuff, show);
            Environment.SetEnvironmentVariable(WorldStar.EnvNoDebuff, no);
            InvasionState.ResetForTest();
            DefenseState.ResetForTest();
            EstateDefense.ResetForTest();
            WorldStar.ResetForTest();
            GameState.ResetAll();

            if (_fail > 0)
            {
                Debug.LogError("[AuraDebuffSelfCheck] FAIL\n" + _log);
                throw new InvalidOperationException("AuraDebuffSelfCheck FAIL " + _fail);
            }
            Debug.Log("[AuraDebuffSelfCheck] PASS\n" + _log);
        }
    }
}
