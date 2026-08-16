using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §22 V3 — 한 판에서 파티 공격→HP→페이즈→장판/소환/힐체크→처치→탑 층 돌파가 닫히는가.
    /// HP·장판·힐 보고 조각은 이미 있다. 이 검사는 그 조각들을 **같은 실행**에서 잇고,
    /// 처치 이벤트가 생산 경계 <see cref="GameFlow.ApplyTowerBossVictory"/> 를 부를 때만
    /// 층이 오르는지를 본다. BOSS_NO_DPS=1이면 층이 그대로여야 한다.
    /// </summary>
    public static class BossBattleRunSelfCheck
    {
        [MenuItem("Ashes to Stars/QA/Boss Run Self Check")]
        public static void Run()
        {
            string oldDps = Environment.GetEnvironmentVariable("BOSS_NO_DPS");
            string oldAoe = Environment.GetEnvironmentVariable("BOSS_NO_AOE");
            string oldSummon = Environment.GetEnvironmentVariable("BOSS_NO_SUMMON");
            int oldFloor = 0;
            var oldKind = GameFlow.Kind;
            int oldBossFloor = GameFlow.BossFloor;
            GameObject liveGo = null;
            GameObject blockedGo = null;
            try
            {
                oldFloor = GameState.TowerFloor;
                Environment.SetEnvironmentVariable("BOSS_NO_DPS", null);
                Environment.SetEnvironmentVariable("BOSS_NO_AOE", null);
                Environment.SetEnvironmentVariable("BOSS_NO_SUMMON", null);

                GameState.SetTowerFloorForTest(5);
                GameFlow.Kind = GameFlow.BattleKind.보스;
                GameFlow.BossFloor = 5;

                liveGo = BuildBattle(out var liveParty, out var liveBoss);
                int defeated = 0;
                int phases = 0;
                liveBoss.OnBossDefeated += _ =>
                {
                    defeated++;
                    GameFlow.ApplyTowerBossVictory(GameFlow.BossFloor);
                };
                liveBoss.OnBossPhaseChange += _ => phases++;

                Require(Mathf.Approximately(BossBattle.ActiveTotalHp, 9000f),
                    $"5층 초기 HP가 9000이어야 한다: {BossBattle.ActiveTotalHp}");
                Require(GameState.TowerFloor == 5, $"시작 층이 5여야 한다: {GameState.TowerFloor}");

                Invoke(liveBoss, "TriggerHealCheck");
                float hpBefore = global::W3Party.ActivePartyHp;
                Invoke(liveBoss, "TriggerFloorAOE", 2);
                Invoke(liveBoss, "UpdateFloorAOEs", 1.1f);
                float aoeDamage = hpBefore - global::W3Party.ActivePartyHp;
                Require(aoeDamage > 0f, $"장판이 파티 HP를 깎아야 한다: {aoeDamage}");
                Require(Mathf.Approximately(BossBattle.ActiveHealCheckWindowDamage, aoeDamage),
                    $"장판 피해가 힐체크에 보고돼야 한다: hp={aoeDamage}, report={BossBattle.ActiveHealCheckWindowDamage}");

                BossBattle.ReportHealingToActive(aoeDamage);
                Invoke(liveBoss, "OnHealCheckFailed");
                Require(defeated == 0, "힐체크 통과가 보스를 처치하면 안 된다");

                Invoke(liveBoss, "TriggerSummonMobs", 5);
                int summoned = global::W3Party.SummonedAliveOnActive();
                Require(summoned > 0, $"소환 쫄이 실판에 있어야 한다: {summoned}");

                DamageFirstW3Target(liveParty, 4500f);
                Require(Mathf.Approximately(BossBattle.ActiveTotalHp, 4500f),
                    $"피해 4500이 HP에 반영돼야 한다: {BossBattle.ActiveTotalHp}");
                Require(phases == 1, $"1/2 HP에서 페이즈가 1회 전환돼야 한다: {phases}");

                DamageFirstW3Target(liveParty, 4500f);
                Require(defeated == 1 && Mathf.Approximately(BossBattle.ActiveTotalHp, 0f),
                    $"HP 0에서 처치가 1회여야 한다: defeated={defeated} hp={BossBattle.ActiveTotalHp}");
                Require(GameState.TowerFloor == 6,
                    $"처치 후 5층→6층이어야 한다: {GameState.TowerFloor}");

                UnityEngine.Object.DestroyImmediate(liveGo);
                liveGo = null;

                Environment.SetEnvironmentVariable("BOSS_NO_DPS", "1");
                GameState.SetTowerFloorForTest(5);
                blockedGo = BuildBattle(out var blockedParty, out var blockedBoss);
                int blockedDefeated = 0;
                blockedBoss.OnBossDefeated += _ =>
                {
                    blockedDefeated++;
                    GameFlow.ApplyTowerBossVictory(GameFlow.BossFloor);
                };
                DamageFirstW3Target(blockedParty, 9000f);
                Require(Mathf.Approximately(BossBattle.ActiveTotalHp, 9000f),
                    $"BOSS_NO_DPS에서 HP가 줄면 안 된다: {BossBattle.ActiveTotalHp}");
                Require(blockedDefeated == 0, "BOSS_NO_DPS에서 처치 이벤트가 뜨면 안 된다");
                Require(GameState.TowerFloor == 5,
                    $"BOSS_NO_DPS에서 층이 오르면 안 된다: {GameState.TowerFloor}");

                Debug.Log("[BossBattleRunSelfCheck] PASS hp=9000→4500→0 phase=1 aoe>0 heal_report=aoe summon>0 floor=5→6 negative_floor=5");
            }
            finally
            {
                if (liveGo != null) UnityEngine.Object.DestroyImmediate(liveGo);
                if (blockedGo != null) UnityEngine.Object.DestroyImmediate(blockedGo);
                Environment.SetEnvironmentVariable("BOSS_NO_DPS", oldDps);
                Environment.SetEnvironmentVariable("BOSS_NO_AOE", oldAoe);
                Environment.SetEnvironmentVariable("BOSS_NO_SUMMON", oldSummon);
                GameFlow.Kind = oldKind;
                GameFlow.BossFloor = oldBossFloor;
                if (oldFloor > 0) GameState.SetTowerFloorForTest(oldFloor);
                else GameState.ResetAll();
            }
        }

        static GameObject BuildBattle(out global::W3Party party, out BossBattle boss)
        {
            // 장판 SelfCheck와 같은 픽스처. 활성 AddComponent는 Awake가 GameMode=false로
            // 먼저 돌고, 그 다음 ApplyGameParty가 NextStyle을 한 번 더 불러 에디터
            // 배치에서 슬롯 Transform이 비는 사고가 난다. 비활성으로 붙인 뒤 GameMode를
            // 켜고 Awake를 한 번만 돌리면 _game과 보스 타깃이 같은 판에 선다.
            var go = new GameObject("BossBattleRunSelfCheck");
            go.SetActive(false);
            party = go.AddComponent<global::W3Party>();
            party.GameMode = true;
            boss = go.AddComponent<BossBattle>();
            Invoke(party, "Awake");
            Invoke(boss, "Awake");
            boss.Begin(5, 1);
            boss.AttachCombatTargets();
            return go;
        }

        static void Invoke(object target, string name)
        {
            var method = target.GetType().GetMethod(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(method != null, $"{target.GetType().Name}.{name} 경계를 찾지 못했다");
            method.Invoke(target, null);
        }

        static void DamageFirstW3Target(global::W3Party party, float amount)
        {
            var method = typeof(global::W3Party).GetMethod("DamageMob",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(method != null, "W3Party.DamageMob 경계를 찾지 못했다");
            method.Invoke(party, new object[] { 0, amount });
        }

        static void Invoke(BossBattle boss, string name, params object[] args)
        {
            var method = typeof(BossBattle).GetMethod(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(method != null, $"{name} 경계를 찾지 못했다");
            method.Invoke(boss, args);
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[BossRun] " + message);
        }
    }
}
