using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>GameScreen.DrawCard 클릭. 슬라이스 뒤 GUI.Button이면 FAIL.</summary>
    public static class GameCardSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Game Card Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string src = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/GameScreen.cs"));
            int at = src.IndexOf("protected bool DrawCard", StringComparison.Ordinal);
            int choice = src.IndexOf("protected bool DrawChoice", StringComparison.Ordinal);
            Check(at >= 0 && choice > at, "DrawCard가 있다");
            string cardSrc = (at >= 0 && choice > at) ? src.Substring(at, choice - at) : "";
            Check(cardSrc.IndexOf("EventType.MouseDown", StringComparison.Ordinal) >= 0
                  && cardSrc.IndexOf("card.Contains", StringComparison.Ordinal) >= 0,
                "카드 클릭은 MouseDown이다");
            Check(cardSrc.IndexOf("GUI.Button", StringComparison.Ordinal) < 0,
                "DrawCard가 GUI.Button(none)을 안 쓴다");
            Check(cardSrc.IndexOf("DrawSliced", StringComparison.Ordinal) >= 0
                  && cardSrc.IndexOf("LabelFit", StringComparison.Ordinal) >= 0,
                "카드는 슬라이스·라벨을 그린다");

            if (_fail == 0) Debug.Log("[GameCardSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[GameCardSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException(
                $"[GameCardSelfCheck] FAIL {_fail}건");
        }
    }
}
