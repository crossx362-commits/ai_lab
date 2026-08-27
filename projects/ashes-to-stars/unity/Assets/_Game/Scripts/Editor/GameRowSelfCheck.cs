using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>GameScreen.Row 클릭. DrawAtlasButton 뒤 GUI.Button이면 FAIL.</summary>
    public static class GameRowSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Game Row Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string src = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/GameScreen.cs"));
            int at = src.IndexOf("protected bool Row(", StringComparison.Ordinal);
            int locked = src.IndexOf("protected void Locked(", StringComparison.Ordinal);
            Check(at >= 0 && locked > at, "Row가 있다");
            string rowSrc = (at >= 0 && locked > at) ? src.Substring(at, locked - at) : "";
            Check(rowSrc.IndexOf("EventType.MouseDown", StringComparison.Ordinal) >= 0
                  && rowSrc.IndexOf("br.Contains", StringComparison.Ordinal) >= 0,
                "본문 한 줄 클릭은 MouseDown이다");
            Check(rowSrc.IndexOf("GUI.Button", StringComparison.Ordinal) < 0,
                "Row가 GUI.Button(none)을 안 쓴다");
            Check(rowSrc.IndexOf("DrawAtlasButton", StringComparison.Ordinal) >= 0,
                "Row가 DrawAtlasButton을 그린다");

            if (_fail == 0) Debug.Log("[GameRowSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[GameRowSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException(
                $"[GameRowSelfCheck] FAIL {_fail}건");
        }
    }
}
