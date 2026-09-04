using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 월드 티어 선택 첫 슬라이스. 해금/선택 분리·영지 소비처·던전 비용·탑 비용은 해금 유지.
    /// </summary>
    public static class WorldTierSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/World Tier Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;

            string old = Environment.GetEnvironmentVariable("QA_WORLD_TIER");
            Environment.SetEnvironmentVariable("QA_WORLD_TIER", null);

            GameState.ResetAll();
            Check(GameState.TowerFloor == 1 && GameState.UnlockedTier == 0 && GameState.Tier == 0,
                "시작은 해금 T1·세계 T1");

            GameState.SetTowerFloorForTest(21);
            Check(GameState.UnlockedTier == 2, $"21층은 해금 T3 (실제 T{GameState.UnlockedTier + 1})");
            Check(GameState.Tier == 2, "고른 적 없으면 해금 최고가 세계 티어");

            long dungeonHigh = Economy.GetActionCost("DungeonEntry", GameState.Tier);
            long towerHigh = Economy.GetActionCost("TowerNormalFloor", GameState.UnlockedTier);
            Check(dungeonHigh == Economy.GetActionCost("DungeonEntry", 2), "던전 비용이 해금 T3를 따른다");
            Check(GameState.TrySelectTier(0), "해금된 T1로 낮출 수 있다");
            Check(GameState.Tier == 0 && GameState.UnlockedTier == 2,
                "선택 T1 · 해금 T3 — 최고 기록은 그대로");
            long dungeonLow = Economy.GetActionCost("DungeonEntry", GameState.Tier);
            Check(dungeonLow < dungeonHigh && dungeonLow == Economy.GetActionCost("DungeonEntry", 0),
                $"던전 비용이 선택 T1로 내려간다 ({dungeonLow} < {dungeonHigh})");
            Check(Economy.GetActionCost("TowerNormalFloor", GameState.UnlockedTier) == towerHigh,
                "탑 도전 비용은 해금 티어를 유지한다");

            Check(!GameState.TrySelectTier(3), "해금보다 높은 T4는 거부");
            Check(GameState.Tier == 0, "거부하면 선택이 안 바뀐다");
            Check(!GameState.TrySelectTier(-1), "음수 티어는 거부");

            GameState.ForgetInMemoryForTest();
            Check(GameState.Tier == 0 && GameState.UnlockedTier == 2,
                "선택 T1이 저장에서 되살아난다");

            GameState.ClearFloor(30);
            Check(GameState.TowerFloor == 31 && GameState.UnlockedTier == 3,
                "30층 클리어는 해금 T4");
            Check(GameState.Tier == 3, "새 해금은 최고 티어를 기본 선택으로 올린다");

            Environment.SetEnvironmentVariable("QA_WORLD_TIER", "1");
            GameState.ResetAll();
            GameState.SeedWorldTierQaIfRequested();
            Check(GameState.TowerFloor >= 21 && GameState.UnlockedTier == 2 && GameState.Tier == 0,
                "QA_WORLD_TIER=1이면 해금 T3·선택 T1");
            Environment.SetEnvironmentVariable("QA_WORLD_TIER", old);

            _ = nameof(GameState.TrySelectTier);
            _ = nameof(GameState.UnlockedTier);
            _ = nameof(GameState.SeedWorldTierQaIfRequested);

            if (_fail == 0) Debug.Log("[WorldTierSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[WorldTierSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[WorldTierSelfCheck] FAIL {_fail}건");
        }
    }
}
