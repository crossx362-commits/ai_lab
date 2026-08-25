using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>화면별 Body가 공통 하단 내비의 IMGUI 상태를 오염시키지 않는다.</summary>
    public static class BodyGuiStateSelfCheck
    {
        [MenuItem("Ashes to Stars/QA/Body GUI State Self Check")]
        public static void Run()
        {
            string path = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime/GameScreen.cs");
            string source = File.ReadAllText(path);
            int body = source.IndexOf("Body(BodyNav.Fit", StringComparison.Ordinal);
            int bottom = source.IndexOf("if (ShowBottomBar) BottomBar();", StringComparison.Ordinal);
            if (body < 0 || bottom <= body)
                throw new InvalidOperationException("[BodyGuiStateSelfCheck] FAIL Body→BottomBar 순서를 찾지 못함");

            string fence = source.Substring(body, bottom - body);
            var log = new StringBuilder();
            Check(fence.Contains("GUI.matrix = bodyMatrix"), "행렬 복원", log);
            Check(fence.Contains("GUI.color = bodyColor"), "색 복원", log);
            Check(fence.Contains("GUI.contentColor = bodyContentColor"), "글자색 복원", log);
            Check(fence.Contains("GUI.backgroundColor = bodyBackgroundColor"), "배경색 복원", log);
            Check(fence.Contains("GUI.enabled = bodyEnabled"), "활성 상태 복원", log);
            Debug.Log("[BodyGuiStateSelfCheck] PASS\n" + log);
        }

        static void Check(bool ok, string what, StringBuilder log)
        {
            log.AppendLine((ok ? "  PASS  " : "  FAIL  ") + what);
            if (!ok) throw new InvalidOperationException("[BodyGuiStateSelfCheck] FAIL " + what);
        }
    }
}
