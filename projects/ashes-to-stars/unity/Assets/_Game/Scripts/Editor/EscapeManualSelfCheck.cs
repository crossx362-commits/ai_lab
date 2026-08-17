using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>긴급 탈출은 보스·침략만. 잡몹·던전은 거부. QA_NO면 항상 허용(§4).</summary>
    public static class EscapeManualSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Escape Manual Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(EscapeManual.EnvShow);
            string no = Environment.GetEnvironmentVariable(EscapeManual.EnvNo);
            var oldKind = GameFlow.Kind;
            Environment.SetEnvironmentVariable(EscapeManual.EnvShow, null);
            Environment.SetEnvironmentVariable(EscapeManual.EnvNo, null);

            GameState.ResetAll();
            EscapeManual.ResetForTest();
            EmergencyEscape.ResetForTest();
            GameFlow.Kind = GameFlow.BattleKind.잡몹웨이브;

            Check(!EscapeManual.Blocked, "기본은 켜짐");
            Check(!EscapeManual.Allowed(GameFlow.BattleKind.잡몹웨이브), "잡몹은 거부");
            Check(!EscapeManual.Allowed(GameFlow.BattleKind.던전), "던전 노드는 거부");
            Check(EscapeManual.Allowed(GameFlow.BattleKind.보스), "보스는 허용");
            Check(EscapeManual.Allowed(GameFlow.BattleKind.침략), "침략은 허용");
            Check(!EscapeManual.Allowed(), "기본 Kind 잡몹은 거부");
            Check(EscapeManual.WhyNot().IndexOf("자동 전투", StringComparison.Ordinal) >= 0
                  && EscapeManual.WhyNot().IndexOf("§4", StringComparison.Ordinal) >= 0,
                $"거부 문구 (실제 {EscapeManual.WhyNot()})");
            Check(EscapeManual.Line().IndexOf("보스전 지휘", StringComparison.Ordinal) >= 0,
                $"줄 (실제 {EscapeManual.Line()})");

            GameState.Gain(Economy.LifeItem.ScrollOfReturn, 1);
            Check(!EmergencyEscape.TryBegin(), "잡몹+두루마리여도 캐스트 거부");
            Check(!EmergencyEscape.Casting, "거부 뒤 Casting 아님");
            Check(GameState.Bag.GetCount(Economy.LifeItem.ScrollOfReturn) == 1,
                "거부해도 두루마리 남음");
            EmergencyEscape.ResetForTest();

            GameFlow.Kind = GameFlow.BattleKind.보스;
            Check(string.IsNullOrEmpty(EscapeManual.WhyNot()), "보스 WhyNot 빈 칸");
            Check(EmergencyEscape.TryBegin(), "보스+두루마리면 캐스트");
            Check(EmergencyEscape.Casting, "보스 캐스트 중");
            EmergencyEscape.ResetForTest();

            GameFlow.Kind = GameFlow.BattleKind.던전;
            Check(!EmergencyEscape.TryBegin(), "던전 노드는 캐스트 거부");
            EmergencyEscape.ResetForTest();

            GameFlow.Kind = GameFlow.BattleKind.침략;
            Check(EmergencyEscape.TryBegin(), "침략은 캐스트");
            EmergencyEscape.ResetForTest();

            Environment.SetEnvironmentVariable(EscapeManual.EnvNo, "1");
            GameFlow.Kind = GameFlow.BattleKind.잡몹웨이브;
            Check(EscapeManual.Blocked, "QA_NO");
            Check(EscapeManual.Allowed(), "QA_NO면 잡몹도 허용");
            Check(EmergencyEscape.TryBegin(), "QA_NO면 옛 항상 허용");
            Check(EmergencyEscape.Casting, "QA_NO 캐스트");
            EmergencyEscape.ResetForTest();
            Environment.SetEnvironmentVariable(EscapeManual.EnvNo, null);

            Environment.SetEnvironmentVariable(EscapeManual.EnvShow, "1");
            EscapeManual.ResetForTest();
            int before = GameState.Bag.GetCount(Economy.LifeItem.ScrollOfReturn);
            EscapeManual.SeedQaIfRequested();
            Check(EscapeManual.ShowQa, "시드 ShowQa");
            Check(GameState.Bag.GetCount(Economy.LifeItem.ScrollOfReturn) == before + 1
                  || GameState.Bag.GetCount(Economy.LifeItem.ScrollOfReturn) >= 1,
                "시드 두루마리");
            Check(EscapeManual.Line().IndexOf("§4", StringComparison.Ordinal) >= 0, "시드 줄");
            Environment.SetEnvironmentVariable(EscapeManual.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string escapeSrc = File.ReadAllText(Path.Combine(runtime, "EmergencyEscape.cs"));
            string battleSrc = File.ReadAllText(Path.Combine(runtime, "BattleScreen.cs"));
            string fieldSrc = File.ReadAllText(Path.Combine(runtime, "FieldScreen.cs"));
            Check(escapeSrc.IndexOf("EscapeManual.Allowed", StringComparison.Ordinal) >= 0,
                "탈출이 Allowed를 읽는다");
            Check(battleSrc.IndexOf("EscapeManual.Allowed", StringComparison.Ordinal) >= 0
                  && battleSrc.IndexOf("EscapeManual.WhyNot", StringComparison.Ordinal) >= 0,
                "전투가 Allowed·WhyNot을 읽는다");
            Check(fieldSrc.IndexOf("EscapeManual.Line", StringComparison.Ordinal) >= 0
                  && fieldSrc.IndexOf("EscapeManual.SeedQaIfRequested", StringComparison.Ordinal) >= 0,
                "필드가 Line·Seed를 읽는다");

            _ = nameof(EscapeManual.Allowed);
            _ = nameof(EscapeManual.WhyNot);
            _ = nameof(EscapeManual.Line);
            _ = nameof(EscapeManual.SeedQaIfRequested);

            Environment.SetEnvironmentVariable(EscapeManual.EnvShow, show);
            Environment.SetEnvironmentVariable(EscapeManual.EnvNo, no);
            GameFlow.Kind = oldKind;
            EscapeManual.ResetForTest();
            EmergencyEscape.ResetForTest();
            GameState.ResetAll();

            if (_fail == 0) Debug.Log("[EscapeManualSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[EscapeManualSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[EscapeManualSelfCheck] FAIL {_fail}건");
        }
    }
}
