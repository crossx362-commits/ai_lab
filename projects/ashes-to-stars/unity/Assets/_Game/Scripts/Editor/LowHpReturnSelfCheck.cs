using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>필드 저체력 귀환 — 30% 발동·3초 이탈·비필드 거부·토글·저장.</summary>
    public static class LowHpReturnSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Low HP Return Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string old = Environment.GetEnvironmentVariable("QA_NO_LOW_HP_RETURN");
            Environment.SetEnvironmentVariable("QA_NO_LOW_HP_RETURN", null);

            LowHpReturn.ResetForTest();
            GameState.ResetAll();
            Check(LowHpReturn.Enabled, "기본은 켜짐(§4)");
            Check(LowHpReturn.ShouldWatch(GameFlow.BattleKind.잡몹웨이브, GameFlow.Field),
                "필드 잡몹 사냥만 본다");
            Check(!LowHpReturn.ShouldWatch(GameFlow.BattleKind.보스, GameFlow.Tower),
                "탑 보스는 안 본다");
            Check(!LowHpReturn.ShouldWatch(GameFlow.BattleKind.던전, GameFlow.Dungeon),
                "던전은 안 본다");
            Check(!LowHpReturn.ShouldWatch(GameFlow.BattleKind.침략, GameFlow.WorldMap),
                "침략은 안 본다");
            Check(!LowHpReturn.ShouldWatch(GameFlow.BattleKind.잡몹웨이브, GameFlow.Tower),
                "탑 잡몹웨이브는 안 본다");

            bool watch = true;
            Check(LowHpReturn.Tick(0.1f, 0.31f, watch) == LowHpReturn.Phase.Idle,
                "31%는 발동하지 않는다");
            Check(LowHpReturn.Tick(0.1f, LowHpReturn.Threshold, watch) == LowHpReturn.Phase.Leaving,
                "30%에서 이탈이 시작된다");
            Check(LowHpReturn.Leaving && LowHpReturn.Remaining > 2.8f,
                "시작 직후 남은 시간은 약 3초");
            Check(LowHpReturn.Tick(2.8f, 0.05f, watch) == LowHpReturn.Phase.Leaving,
                "2.9초까지는 아직 판에 있다");
            Check(LowHpReturn.Tick(0.2f, 0.05f, watch) == LowHpReturn.Phase.Left,
                "3초가 끝나면 Left");
            Check(!LowHpReturn.Leaving, "Left 뒤 캐스트가 비어 있다");

            LowHpReturn.ResetForTest();
            Check(LowHpReturn.Tick(0.1f, 0.10f, watch) == LowHpReturn.Phase.Leaving,
                "재발동");
            Check(LowHpReturn.Tick(0.1f, 0.10f, watch) == LowHpReturn.Phase.Leaving,
                "피격해도 취소되지 않는다(§18-14)");
            Check(LowHpReturn.Tick(3f, 0.80f, watch) == LowHpReturn.Phase.Left,
                "힐로 비율이 올라도 이미 시작한 이탈은 끝낸다");

            LowHpReturn.ResetForTest();
            Check(LowHpReturn.Tick(1f, 0.01f, false) == LowHpReturn.Phase.NotField,
                "필드가 아니면 1%여도 안 빠진다");

            LowHpReturn.Enabled = false;
            Check(!LowHpReturn.Enabled, "끄면 Enabled=false");
            Check(LowHpReturn.Tick(1f, 0.01f, watch) == LowHpReturn.Phase.Disabled,
                "끄면 1%여도 안 빠진다");
            LowHpReturn.Enabled = true;
            Check(LowHpReturn.Tick(0f, 0.01f, watch) == LowHpReturn.Phase.Leaving,
                "다시 켜면 발동한다");

            LowHpReturn.ResetForTest();
            LowHpReturn.Enabled = false;
            LowHpReturn.ForgetInMemoryForTest();
            Check(!LowHpReturn.Enabled, "끔이 저장에서 되살아난다");

            Environment.SetEnvironmentVariable("QA_NO_LOW_HP_RETURN", "1");
            LowHpReturn.ResetForTest();
            Check(!LowHpReturn.Enabled, "QA_NO_LOW_HP_RETURN=1이면 꺼짐");
            Check(LowHpReturn.Tick(1f, 0.01f, watch) == LowHpReturn.Phase.Disabled,
                "강제 끔이면 발동하지 않는다");
            Environment.SetEnvironmentVariable("QA_NO_LOW_HP_RETURN", old);

            LowHpReturn.ResetForTest();
            _ = nameof(LowHpReturn.Tick);
            _ = nameof(LowHpReturn.ShouldWatch);
            _ = nameof(LowHpReturn.Enabled);
            _ = nameof(global::W3Party.ActivePartyLowestHpRatio);

            if (_fail == 0) Debug.Log("[LowHpReturnSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[LowHpReturnSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[LowHpReturnSelfCheck] FAIL {_fail}건");
        }
    }
}
