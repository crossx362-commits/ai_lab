using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>하단 도크 탭 클릭. 아이콘·라벨 뒤 GUI.Button이면 FAIL.</summary>
    public static class GameNavDockSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Game Nav Dock Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string src = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/GameScreen.cs"));
            int at = src.IndexOf("void BottomBar()", StringComparison.Ordinal);
            int atlas = src.IndexOf("void DrawAtlasButton", StringComparison.Ordinal);
            Check(at >= 0 && atlas > at, "BottomBar가 있다");
            string barSrc = (at >= 0 && atlas > at) ? src.Substring(at, atlas - at) : "";
            Check(barSrc.IndexOf("EventType.MouseDown", StringComparison.Ordinal) >= 0
                  && barSrc.IndexOf("r.Contains", StringComparison.Ordinal) >= 0
                  && barSrc.IndexOf("GameFlow.Go(scene)", StringComparison.Ordinal) >= 0,
                "하단 도크 클릭은 MouseDown으로 씬을 연다");
            Check(barSrc.IndexOf("GUI.Button", StringComparison.Ordinal) < 0,
                "하단 도크가 GUI.Button(none)을 안 쓴다");
            Check(barSrc.IndexOf("NavIcon", StringComparison.Ordinal) >= 0
                  && barSrc.IndexOf("_navLabel", StringComparison.Ordinal) >= 0,
                "하단 도크가 아이콘·라벨을 그린다");

            if (_fail == 0) Debug.Log("[GameNavDockSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[GameNavDockSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException(
                $"[GameNavDockSelfCheck] FAIL {_fail}건");
        }
    }
}
