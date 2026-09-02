using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>필드 자동사냥은 가방 60칸이 차면 보상 없이 즉시 영지로 돌아간다.</summary>
    public static class BagFullReturnSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Bag Full Return Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            GameState.ResetAll();

            Check(!BagFullReturn.ShouldReturn(GameFlow.BattleKind.잡몹웨이브, GameFlow.Field),
                "빈 가방에서는 귀환하지 않는다");
            while (BagSlots.Used() < BagSlots.Cap)
                Equipment.AddUnequippedForTest(Equipment.LeatherArmorRecipe);
            Check(BagFullReturn.ShouldReturn(GameFlow.BattleKind.잡몹웨이브, GameFlow.Field),
                "필드 잡몹 사냥은 가방이 차면 귀환한다");
            Check(!BagFullReturn.ShouldReturn(GameFlow.BattleKind.던전, GameFlow.Dungeon),
                "던전은 가방이 차도 자동 귀환하지 않는다");
            Check(!BagFullReturn.ShouldReturn(GameFlow.BattleKind.잡몹웨이브, GameFlow.Tower),
                "탑 잡몹웨이브는 가방이 차도 자동 귀환하지 않는다");
            string battle = File.ReadAllText("Assets/_Game/Scripts/Runtime/BattleScreen.cs");
            Check(battle.Contains("BagFullReturn.ShouldReturn") && battle.Contains("LeaveForBagFull"),
                "자동사냥 전투가 가방 포화 귀환을 연결한다");

            GameState.ResetAll();
            _ = nameof(BagFullReturn.ShouldReturn);
            if (_fail == 0) Debug.Log("[BagFullReturnSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[BagFullReturnSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[BagFullReturnSelfCheck] FAIL {_fail}건");
        }
    }
}
