using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    public static class BossBattleAoeSelfCheck
    {
        [MenuItem("Ashes to Stars/QA/Boss AoE Self Check")]
        public static void Run()
        {
            string old = Environment.GetEnvironmentVariable("BOSS_NO_AOE");
            GameObject go = null;
            try
            {
                Environment.SetEnvironmentVariable("BOSS_NO_AOE", null);
                go = BuildBattle(out var liveParty, out var liveBoss);
                float hpBefore = global::W3Party.ActivePartyHp;
                StartHealCheck(liveBoss);
                TriggerAndResolveFloorAoe(liveBoss);
                float hpDamage = hpBefore - global::W3Party.ActivePartyHp;
                Require(hpDamage > 0f, $"정상 장판이 파티 HP를 깎아야 한다: {hpDamage}");
                Require(Mathf.Approximately(BossBattle.ActiveHealCheckWindowDamage, hpDamage),
                    $"실제 HP 피해가 힐체크에 그대로 보고돼야 한다: hp={hpDamage}, report={BossBattle.ActiveHealCheckWindowDamage}");

                // 같은 컴포넌트 재진입도 검증한다. 예고 중 장판이 Begin을 넘어가면 차단 판에서도
                // 뒤늦게 터지므로, 새 오브젝트만 쓰는 네거티브로는 상태 누수를 잡을 수 없다.
                TriggerOnly(liveBoss);
                Environment.SetEnvironmentVariable("BOSS_NO_AOE", "1");
                liveBoss.Begin(5, 1);
                hpBefore = global::W3Party.ActivePartyHp;
                StartHealCheck(liveBoss);
                ResolveFloorAoe(liveBoss);
                Require(Mathf.Approximately(hpBefore, global::W3Party.ActivePartyHp),
                    "BOSS_NO_AOE 재진입에서 이전 예고 장판이 남으면 안 된다");
                Require(Mathf.Approximately(BossBattle.ActiveHealCheckWindowDamage, 0f),
                    "BOSS_NO_AOE 재진입에서 이전 장판 피해 보고가 남으면 안 된다");
                UnityEngine.Object.DestroyImmediate(go);
                go = null;

                go = BuildBattle(out var blockedParty, out var blockedBoss);
                hpBefore = global::W3Party.ActivePartyHp;
                StartHealCheck(blockedBoss);
                TriggerAndResolveFloorAoe(blockedBoss);
                hpDamage = hpBefore - global::W3Party.ActivePartyHp;
                Require(Mathf.Approximately(hpDamage, 0f),
                    $"BOSS_NO_AOE에서 파티 HP가 줄면 안 된다: {hpDamage}");
                Require(Mathf.Approximately(BossBattle.ActiveHealCheckWindowDamage, 0f),
                    $"BOSS_NO_AOE에서 힐체크 피해 보고가 생기면 안 된다: {BossBattle.ActiveHealCheckWindowDamage}");

                Debug.Log("[BossBattleAoeSelfCheck] PASS normal_hp_damage>0 report=damage negative_hp_damage=0 report=0");
            }
            finally
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
                Environment.SetEnvironmentVariable("BOSS_NO_AOE", old);
            }
        }

        static GameObject BuildBattle(out global::W3Party party, out BossBattle boss)
        {
            var go = new GameObject("BossBattleAoeSelfCheck");
            go.SetActive(false);
            // 비활성 오브젝트에서는 Unity가 Awake를 예약만 하므로, 실행 QA가 두 컴포넌트를
            // GameMode=true 상태에서 정확히 한 번 초기화한다. 활성화하면 기본 판이 먼저 서서
            // ApplyGameParty 재진입과 섞이므로 이 검증에서는 계속 비활성으로 둔다.
            party = TestAttach.AttachWithAwake<global::W3Party>(go, p => { p.GameMode = true; });
            boss = TestAttach.AttachWithAwake<BossBattle>(go);
            boss.Begin(5, 1);
            return go;
        }

        static void StartHealCheck(BossBattle boss)
        {
            Invoke(boss, "TriggerHealCheck");
        }

        static void TriggerAndResolveFloorAoe(BossBattle boss)
        {
            TriggerOnly(boss);
            ResolveFloorAoe(boss);
        }

        static void TriggerOnly(BossBattle boss) => Invoke(boss, "TriggerFloorAOE", 2);
        static void ResolveFloorAoe(BossBattle boss) => Invoke(boss, "UpdateFloorAOEs", 1.1f);

        static void Invoke(BossBattle boss, string name, params object[] args)
        {
            var method = typeof(BossBattle).GetMethod(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(method != null, $"{name} 경계를 찾지 못했다");
            method.Invoke(boss, args);
        }


        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[BossAoe] " + message);
        }
    }
}
