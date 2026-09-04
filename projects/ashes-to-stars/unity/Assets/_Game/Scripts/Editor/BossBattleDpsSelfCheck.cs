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
                liveGo = new GameObject("BossBattleDpsSelfCheck_Live");
                var liveParty = liveGo.AddComponent<global::W3Party>();
                liveParty.GameMode = true;
                liveParty.ApplyGameParty();
                var live = liveGo.AddComponent<BossBattle>();
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
                blockedGo = new GameObject("BossBattleDpsSelfCheck_Blocked");
                var blockedParty = blockedGo.AddComponent<global::W3Party>();
                blockedParty.GameMode = true;
                blockedParty.ApplyGameParty();
                var blocked = blockedGo.AddComponent<BossBattle>();
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
