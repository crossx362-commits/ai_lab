using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>캐릭터 스크롤 3곳. 기본 스크롤바면 FAIL, DrawVScroll이면 PASS.</summary>
    public static class CharScrollHudSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Char Scroll Hud Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string src = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/CharacterScreen.cs"));
            string atlas = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/UiAtlas.cs"));
            int n = 0;
            int i = 0;
            while ((i = src.IndexOf("GUI.BeginScrollView", i, StringComparison.Ordinal)) >= 0)
            {
                n++;
                i += 10;
            }
            Check(n == 3, $"BeginScrollView 3곳 (실제 {n})");
            Check(src.IndexOf("GUIStyle.none, GUIStyle.none", StringComparison.Ordinal) >= 0,
                "기본 스크롤바 크롬을 끈다");
            Check(src.IndexOf("UiAtlas.DrawVScroll", StringComparison.Ordinal) >= 0,
                "트랙/썸은 DrawVScroll이다");
            Check(atlas.IndexOf("DrawMeter(thumb", StringComparison.Ordinal) >= 0
                  && atlas.IndexOf("DrawSliced(track", StringComparison.Ordinal) >= 0,
                "트랙은 panel, 썸은 DrawMeter다");
            Check(src.IndexOf("InfoFoldLimit = contentH", StringComparison.Ordinal) >= 0
                  && src.IndexOf("InfoFoldLimit = REF_H", StringComparison.Ordinal) >= 0,
                "접힘 한계는 그대로다");

            if (_fail == 0) Debug.Log("[CharScrollHudSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[CharScrollHudSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException(
                $"[CharScrollHudSelfCheck] FAIL {_fail}건");
        }
    }
}
