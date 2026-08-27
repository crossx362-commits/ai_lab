using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>전투 Overlay 저체력 귀환·긴급 이탈 바. GUI.Box이면 FAIL, DrawMeter면 PASS.</summary>
    public static class BattleLeaveBarSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Battle Leave Bar Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string src = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/BattleScreen.cs"));
            int overlay = src.IndexOf("protected override void Overlay()", StringComparison.Ordinal);
            int next = src.IndexOf("bool DrawHuntBoonPick()", overlay + 1, StringComparison.Ordinal);
            Check(overlay >= 0 && next > overlay, "Overlay가 있다");
            string body = overlay >= 0 && next > overlay ? src.Substring(overlay, next - overlay) : "";
            Check(body.IndexOf("GUI.Box", StringComparison.Ordinal) < 0,
                "Overlay 진행 바가 GUI.Box가 아니다");
            Check(body.IndexOf("UiAtlas.DrawMeter", StringComparison.Ordinal) >= 0,
                "진행 바는 DrawMeter다");
            Check(body.IndexOf("LowHpReturn.Leaving", StringComparison.Ordinal) >= 0
                  && body.IndexOf("EmergencyEscape.Casting", StringComparison.Ordinal) >= 0,
                "귀환·이탈 표시 조건은 그대로다");
            Check(src.IndexOf("void LeaveForLowHp()", StringComparison.Ordinal) >= 0
                  && src.IndexOf("void LeaveByEscape()", StringComparison.Ordinal) >= 0,
                "귀환·이탈 종료 함수는 그대로다");

            if (_fail == 0) Debug.Log("[BattleLeaveBarSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[BattleLeaveBarSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException(
                $"[BattleLeaveBarSelfCheck] FAIL {_fail}건");
        }
    }
}
