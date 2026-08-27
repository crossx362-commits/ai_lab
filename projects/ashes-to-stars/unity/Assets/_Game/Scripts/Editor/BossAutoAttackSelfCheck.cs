using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>보스가 제자리에서 파티를 때리는지. BOSS_NO_AUTO=1이면 피해 0.</summary>
    public static class BossAutoAttackSelfCheck
    {
        [MenuItem("Ashes to Stars/QA/Boss Auto Attack Self Check")]
        public static void Run()
        {
            string old = Environment.GetEnvironmentVariable("BOSS_NO_AUTO");
            GameObject go = null;
            try
            {
                Environment.SetEnvironmentVariable("BOSS_NO_AUTO", null);
                go = Build(out var party);
                float hpBefore = global::W3Party.ActivePartyHp;
                Tick(party, 3.4f);
                float lost = hpBefore - global::W3Party.ActivePartyHp;
                int hits = global::W3Party.BossAutoHitsOnActive();
                Require(hits >= 1, $"정상 보스가 한 번은 쳐야 한다: hits={hits}");
                Require(lost > 0f, $"정상 보스가 파티 HP를 깎아야 한다: lost={lost} hits={hits}");

                UnityEngine.Object.DestroyImmediate(go);
                go = null;

                Environment.SetEnvironmentVariable("BOSS_NO_AUTO", "1");
                go = Build(out party);
                hpBefore = global::W3Party.ActivePartyHp;
                Tick(party, 3.4f);
                lost = hpBefore - global::W3Party.ActivePartyHp;
                int blockedHits = global::W3Party.BossAutoHitsOnActive();
                Require(Mathf.Approximately(lost, 0f),
                    $"BOSS_NO_AUTO에서 파티 HP가 줄면 안 된다: lost={lost}");
                Require(blockedHits == 0,
                    $"BOSS_NO_AUTO에서 히트 카운트가 늘면 안 된다: {blockedHits}");

                Debug.Log("[BossAutoAttackSelfCheck] PASS hits>0 hp_down / NO_AUTO hits=0 hp_same");
            }
            finally
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
                Environment.SetEnvironmentVariable("BOSS_NO_AUTO", old);
            }
        }

        static GameObject Build(out global::W3Party party)
        {
            var go = new GameObject("BossAutoAttackSelfCheck");
            go.SetActive(false);
            party = TestAttach.AttachWithAwake<global::W3Party>(go, p => { p.GameMode = true; });
            party.ApplyGameParty();
            var configure = typeof(global::W3Party).GetMethod("ConfigureBossTargets",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(configure != null, "ConfigureBossTargets 없음");
            configure.Invoke(party, new object[]
            {
                new[] { new global::W3Party.BossTarget(0, 9000f, new Vector2(0f, 4f)) }
            });
            return go;
        }

        static void Tick(global::W3Party party, float seconds)
        {
            var tick = typeof(global::W3Party).GetMethod("TickBossAttacks",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(tick != null, "TickBossAttacks 없음");
            const float dt = 0.016f;
            int n = Mathf.CeilToInt(seconds / dt);
            for (int i = 0; i < n; i++) tick.Invoke(party, new object[] { dt });
        }


        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[BossAuto] " + message);
        }
    }
}
