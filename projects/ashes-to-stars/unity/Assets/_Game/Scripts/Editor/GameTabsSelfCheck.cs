using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>GameScreen.DrawTabs 클릭. 슬라이스 뒤 GUI.Button이면 FAIL.</summary>
    public static class GameTabsSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Game Tabs Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string src = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/GameScreen.cs"));
            int at = src.IndexOf("protected int DrawTabs", StringComparison.Ordinal);
            int card = src.IndexOf("protected bool DrawCard", StringComparison.Ordinal);
            Check(at >= 0 && card > at, "DrawTabs가 있다");
            string tabSrc = (at >= 0 && card > at) ? src.Substring(at, card - at) : "";
            Check(tabSrc.IndexOf("EventType.MouseDown", StringComparison.Ordinal) >= 0
                  && tabSrc.IndexOf("t.Contains", StringComparison.Ordinal) >= 0,
                "탭 클릭은 MouseDown이다");
            Check(tabSrc.IndexOf("GUI.Button", StringComparison.Ordinal) < 0,
                "DrawTabs가 GUI.Button(none)을 안 쓴다");
            Check(tabSrc.IndexOf("DrawSliced", StringComparison.Ordinal) >= 0
                  && tabSrc.IndexOf("LabelClip", StringComparison.Ordinal) >= 0,
                "탭은 슬라이스·라벨을 그린다");

            if (_fail == 0) Debug.Log("[GameTabsSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[GameTabsSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException(
                $"[GameTabsSelfCheck] FAIL {_fail}건");
        }
    }
}
