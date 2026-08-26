using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    public static class BossBattleDpsSelfCheck
    {
        [MenuItem("Ashes to Stars/QA/Boss DPS Self Check")]
        public static void Run()
        {
            string old = Environment.GetEnvironmentVariable("BOSS_NO_DPS");
            GameObject liveGo = null;
            GameObject blockedGo = null;
            try
            {
                Environment.SetEnvironmentVariable("BOSS_NO_DPS", null);
                liveGo = BuildParty("BossBattleDpsSelfCheck_Live");
                var liveParty = liveGo.GetComponent<global::W3Party>();
                liveParty.ApplyGameParty();
                var live = AttachWithAwake<BossBattle>(liveGo);
                int defeated = 0;
                int phases = 0;
                live.OnBossDefeated += _ => defeated++;
                live.OnBossPhaseChange += _ => phases++;
                live.Begin(5, 1);
                live.AttachCombatTargets();

                float startHp = BossHp.Hp(5, 90f);
                float half = startHp * 0.5f;
                Require(Mathf.Approximately(BossBattle.ActiveTotalHp, startHp),
                    $"5층 초기 HP가 {startHp:0}이어야 한다: {BossBattle.ActiveTotalHp}");
                DamageFirstW3Target(liveParty, half);
                Require(Mathf.Approximately(BossBattle.ActiveTotalHp, half),
                    $"피해 {half:0}이 HP에 반영돼야 한다: {BossBattle.ActiveTotalHp}");
                Require(phases == 1, $"1/2 HP에서 페이즈가 1회 전환돼야 한다: {phases}");
                DamageFirstW3Target(liveParty, half);
                Require(defeated == 1 && Mathf.Approximately(BossBattle.ActiveTotalHp, 0f),
                    $"HP 0에서 처치 이벤트가 정확히 1회여야 한다: defeated={defeated}");
                UnityEngine.Object.DestroyImmediate(liveGo);
                liveGo = null;

                Environment.SetEnvironmentVariable("BOSS_NO_DPS", "1");
                blockedGo = BuildParty("BossBattleDpsSelfCheck_Blocked");
                var blockedParty = blockedGo.GetComponent<global::W3Party>();
                blockedParty.ApplyGameParty();
                var blocked = AttachWithAwake<BossBattle>(blockedGo);
                blocked.Begin(5, 1);
                blocked.AttachCombatTargets();
                DamageFirstW3Target(blockedParty, startHp);
                Require(Mathf.Approximately(BossBattle.ActiveTotalHp, startHp),
                    $"BOSS_NO_DPS에서 HP가 줄면 안 된다: {BossBattle.ActiveTotalHp}");
                Require(Mathf.Approximately(FirstTargetHp(blockedParty), startHp),
                    "BOSS_NO_DPS에서 W3 보스 타깃 HP도 줄면 안 된다");
                UnityEngine.Object.DestroyImmediate(blockedGo);
                blockedGo = null;

                Debug.Log($"[BossBattleDpsSelfCheck] PASS normal={startHp:0}→{half:0}→0 phase=1 defeated=1 negative={startHp:0}→{startHp:0}");
            }
            finally
            {
                if (liveGo != null) UnityEngine.Object.DestroyImmediate(liveGo);
                if (blockedGo != null) UnityEngine.Object.DestroyImmediate(blockedGo);
                Environment.SetEnvironmentVariable("BOSS_NO_DPS", old);
            }
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[BossDps] " + message);
        }

        /// <summary>
        /// W3Party 검증 판 세우기 — BossAutoAttackSelfCheck.Build와 같은 경계.
        /// 에디터(비플레이) 배치에선 AddComponent가 Awake를 부르지 않아 _slots가 null이고,
        /// ApplyGameParty→NextStyle이 슬롯 접근에서 NRE로 죽었다(전수 실측 2026-08-26).
        /// 비활성 생성 → GameMode 대입 → 수동 Awake(BuildWorld 포함) 순서로 런타임과 같은 상태를 만든다.
        /// </summary>
        static GameObject BuildParty(string name)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            var party = go.AddComponent<global::W3Party>();
            party.GameMode = true;
            var awake = typeof(global::W3Party).GetMethod("Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(awake != null, "W3Party.Awake 경계를 찾지 못했다");
            awake.Invoke(party, null);
            go.SetActive(true);
            return go;
        }

        /// <summary>
        /// 에디터(비플레이) 배치에선 AddComponent가 Awake를 부르지 않는다 — BossBattle.Awake가
        /// activeFloorAOEs 등을 채우므로 Begin 전에 수동으로 띄운다(위 BuildParty와 같은 경계).
        /// </summary>
        static T AttachWithAwake<T>(GameObject go) where T : Component
        {
            var comp = go.AddComponent<T>();
            var awake = typeof(T).GetMethod("Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (awake != null) awake.Invoke(comp, null);
            return comp;
        }

        static void DamageFirstW3Target(global::W3Party party, float amount)
        {
            var method = typeof(global::W3Party).GetMethod("DamageMob",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(method != null, "W3Party.DamageMob 경계를 찾지 못했다");
            method.Invoke(party, new object[] { 0, amount });
        }

        static float FirstTargetHp(global::W3Party party)
        {
            var field = typeof(global::W3Party).GetField("_mHp",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(field != null, "W3Party._mHp 검증 경계를 찾지 못했다");
            return ((float[])field.GetValue(party))[0];
        }
    }
}
