using System;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>선택한 세계 티어가 필드·던전 일반 몬스터 수치와 필드 보상에 함께 적용된다.</summary>
    public static class FieldTierSelfCheck
    {
        [MenuItem("Ashes to Stars/QA/Field Tier Self Check")]
        public static void Run()
        {
            GameState.ResetAll();
            SoftCap.ResetForTest();
            GameState.SetTowerFloorForTest(11);

            Debug.Assert(GameState.TrySelectTier(0), "[FieldTierSelfCheck] T1 선택 실패");
            float t1Enemy = W3Party.EnemyStatMultiplierForTier(GameState.Tier);
            long t1Gold = Economy.WaveHuntGold(GameState.Tier, Economy.HuntGoldHourSeconds);
            DungeonRun.Begin(1u, GameState.Tier, DungeonKind.일반, GameFlow.Field);
            int t1Dungeon = DungeonRun.Plan.Tier;
            DungeonRun.End();

            Debug.Assert(GameState.TrySelectTier(1), "[FieldTierSelfCheck] T2 선택 실패");
            float t2Enemy = W3Party.EnemyStatMultiplierForTier(GameState.Tier);
            long t2Gold = Economy.WaveHuntGold(GameState.Tier, Economy.HuntGoldHourSeconds);
            DungeonRun.Begin(1u, GameState.Tier, DungeonKind.일반, GameFlow.Field);
            int t2Dungeon = DungeonRun.Plan.Tier;
            DungeonRun.End();

            Debug.Assert(Mathf.Approximately(t1Enemy, 1f),
                $"[FieldTierSelfCheck] T1 적 배율은 1이어야 한다 (실제 {t1Enemy})");
            Debug.Assert(t2Enemy > t1Enemy,
                $"[FieldTierSelfCheck] T2 적 스펙이 T1보다 높지 않다 ({t1Enemy} → {t2Enemy})");
            Debug.Assert(t2Gold > t1Gold,
                $"[FieldTierSelfCheck] T2 필드 보상이 T1보다 높지 않다 ({t1Gold} → {t2Gold})");
            Debug.Assert(t1Dungeon == 0 && t2Dungeon == 1,
                $"[FieldTierSelfCheck] 던전 티어가 선택값과 동기화되지 않는다 ({t1Dungeon} → {t2Dungeon})");

            GameState.ResetAll();
            SoftCap.ResetForTest();
            Debug.Log("[FieldTierSelfCheck] PASS");
        }
    }
}
