using System;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    public static class LevelCombatGrowthSelfCheck
    {
        [MenuItem("Ashes to Stars/QA/Level Combat Growth Self Check")]
        public static void Run()
        {
            string old = Environment.GetEnvironmentVariable("QA_NO_LEVEL_GROWTH");
            try
            {
                Environment.SetEnvironmentVariable("QA_NO_LEVEL_GROWTH", null);
                Require(Mathf.Approximately(global::W3Party.LevelStatMultiplier(1), 1.00f), "Lv1은 기준 전투력이어야 한다");
                Require(Mathf.Approximately(global::W3Party.LevelStatMultiplier(50), 1.98f), "Lv50은 1.98배여야 한다");
                Require(Mathf.Approximately(global::W3Party.LevelStatMultiplier(100), 2.98f), "Lv100은 2.98배여야 한다");

                Environment.SetEnvironmentVariable("QA_NO_LEVEL_GROWTH", "1");
                Require(Mathf.Approximately(global::W3Party.LevelStatMultiplier(50), 1.00f),
                    "차단 대조에서 Lv50 계수가 1.00으로 돌아가야 한다");

                Debug.Log("[LevelCombatGrowthSelfCheck] PASS Lv1=1.00 Lv50=1.98 Lv100=2.98 negative=1.00");
            }
            finally
            {
                Environment.SetEnvironmentVariable("QA_NO_LEVEL_GROWTH", old);
            }
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[LevelCombatGrowth] " + message);
        }
    }
}
