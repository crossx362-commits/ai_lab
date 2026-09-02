using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>세계 티어 변경은 필드 보상표와 새 던전 계획까지 같은 값을 쓴다.</summary>
    public static class FieldTierDropSelfCheck
    {
        [MenuItem("Ashes to Stars/QA/Field Tier Drop Self Check")]
        public static void Run()
        {
            GameState.ResetAll();
            SoftCap.ResetForTest();
            GameState.SetTowerFloorForTest(11); // T2 해금

            Debug.Assert(GameState.TrySelectTier(0), "[FieldTierDropSelfCheck] T1 선택 실패");
            float t1Drop = Economy.DropRateForTier(Economy.DropSource.FieldDungeonBoss,
                Economy.LifeItem.CraftHide, GameState.Tier);
            long t1Gold = Economy.WaveHuntGold(GameState.Tier, Economy.HuntGoldHourSeconds);

            Debug.Assert(GameState.TrySelectTier(1), "[FieldTierDropSelfCheck] T2 선택 실패");
            float t2Drop = Economy.DropRateForTier(Economy.DropSource.FieldDungeonBoss,
                Economy.LifeItem.CraftHide, GameState.Tier);
            Debug.Assert(t2Drop > t1Drop,
                $"[FieldTierDropSelfCheck] T2 드랍률이 T1보다 높지 않다 ({t1Drop} → {t2Drop})");

            Debug.Assert(GameState.TrySelectTier(0), "[FieldTierDropSelfCheck] T1 하향 선택 실패");
            DungeonRun.Begin(1u, GameState.Tier, DungeonKind.일반, GameFlow.Field);
            Debug.Assert(DungeonRun.Plan.Tier == 0,
                $"[FieldTierDropSelfCheck] 하향 티어가 새 던전에 동기화되지 않는다 ({DungeonRun.Plan.Tier})");
            DungeonRun.End();
            Debug.Assert(Mathf.Approximately(W3Party.EnemyStatMultiplierForTier(GameState.Tier), 1f),
                "[FieldTierDropSelfCheck] 하향 뒤 필드 적 스탯이 T1이 아니다");
            Debug.Assert(Economy.WaveHuntGold(GameState.Tier, Economy.HuntGoldHourSeconds) == t1Gold,
                "[FieldTierDropSelfCheck] 하향 뒤 필드 보상이 T1으로 돌아가지 않는다");

            GameState.ResetAll();
            SoftCap.ResetForTest();
            Debug.Log("[FieldTierDropSelfCheck] PASS");
        }
    }
}
