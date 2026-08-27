using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// V4 즉시계속 일시정지 오버레이. HUD·도크·Cap·로그 파일과 섞이면 FAIL.
    /// </summary>
    public static class V4ContinueOverlaySelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/V4 Continue Overlay Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(V4ContinueOverlay.EnvShow);
            string go = Environment.GetEnvironmentVariable(V4ContinueOverlay.EnvGo);
            string no = Environment.GetEnvironmentVariable(V4ContinueOverlay.EnvNo);
            Environment.SetEnvironmentVariable(V4ContinueOverlay.EnvShow, null);
            Environment.SetEnvironmentVariable(V4ContinueOverlay.EnvGo, null);
            Environment.SetEnvironmentVariable(V4ContinueOverlay.EnvNo, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string overlaySrc = File.ReadAllText(Path.Combine(runtime, "V4ContinueOverlay.cs"));
            string screenSrc = File.ReadAllText(Path.Combine(runtime, "GameScreen.cs"));

            Check(screenSrc.IndexOf("V4ContinueOverlay.Draw", StringComparison.Ordinal) >= 0,
                "GameScreen.Overlay가 V4ContinueOverlay를 그린다");
            Check(overlaySrc.IndexOf("EventType.MouseDown", StringComparison.Ordinal) >= 0,
                "선택은 MouseDown");
            Check(overlaySrc.IndexOf("GUI.Button", StringComparison.Ordinal) < 0,
                "오버레이에 GUI.Button 없음");
            Check(overlaySrc.IndexOf("YardInspect", StringComparison.Ordinal) < 0
                  && overlaySrc.IndexOf("EstateHud", StringComparison.Ordinal) < 0
                  && overlaySrc.IndexOf("BottomBar", StringComparison.Ordinal) < 0,
                "HUD·도크·YardInspect와 안 섞인다");
            Check(overlaySrc.IndexOf("LifeSystem", StringComparison.Ordinal) < 0
                  && overlaySrc.IndexOf("Memorial", StringComparison.Ordinal) < 0
                  && overlaySrc.IndexOf("V4LoopLog", StringComparison.Ordinal) < 0,
                "LifeSystem·Memorial·V4LoopLog 미접촉");
            Check(overlaySrc.IndexOf("삭제됨", StringComparison.Ordinal) < 0
                  && overlaySrc.IndexOf("FormatCurrency", StringComparison.Ordinal) < 0
                  && overlaySrc.IndexOf("70%", StringComparison.Ordinal) < 0
                  && overlaySrc.IndexOf("24h", StringComparison.Ordinal) < 0,
                "Cap·70%/24h 문자열 없음");
            Check(overlaySrc.IndexOf(V4ContinueOverlay.TitleText, StringComparison.Ordinal) >= 0,
                "제목 바로 이어서");
            Check(overlaySrc.IndexOf(V4ContinueOverlay.ContinueText, StringComparison.Ordinal) >= 0,
                "계속 버튼");

            V4ContinueOverlay.ResetForTest();
            Check(!V4ContinueOverlay.Open, "기본은 닫힘");
            Environment.SetEnvironmentVariable(V4ContinueOverlay.EnvShow, "1");
            V4ContinueOverlay.ResetForTest();
            V4ContinueOverlay.SeedQaIfRequested();
            Check(V4ContinueOverlay.Open, "QA_V4_CONTINUE→열림");
            Environment.SetEnvironmentVariable(V4ContinueOverlay.EnvShow, null);
            Environment.SetEnvironmentVariable(V4ContinueOverlay.EnvGo, "1");
            V4ContinueOverlay.ResetForTest();
            V4ContinueOverlay.SeedQaIfRequested();
            Check(!V4ContinueOverlay.Open, "QA_V4_CONTINUE_GO→닫힘(계속)");
            Check(V4ContinueOverlay.LastChoice == V4ContinueOverlay.ContinueText,
                "GO 시드는 계속");
            Environment.SetEnvironmentVariable(V4ContinueOverlay.EnvNo, "1");
            Environment.SetEnvironmentVariable(V4ContinueOverlay.EnvShow, "1");
            V4ContinueOverlay.ResetForTest();
            V4ContinueOverlay.SeedQaIfRequested();
            Check(!V4ContinueOverlay.Open, "QA_NO면 안 뜬다");

            Environment.SetEnvironmentVariable(V4ContinueOverlay.EnvShow, show);
            Environment.SetEnvironmentVariable(V4ContinueOverlay.EnvGo, go);
            Environment.SetEnvironmentVariable(V4ContinueOverlay.EnvNo, no);
            V4ContinueOverlay.ResetForTest();

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "v4_continue_overlay_selfcheck.log");
            var body = new StringBuilder();
            body.AppendLine(_fail == 0 ? "PASS V4ContinueOverlaySelfCheck" : "FAIL V4ContinueOverlaySelfCheck");
            body.Append(_log);
            File.WriteAllText(path, body.ToString());
            if (_fail == 0) Debug.Log("[V4ContinueOverlaySelfCheck] PASS → " + path);
            else
            {
                Debug.LogError("[V4ContinueOverlaySelfCheck] FAIL " + _fail + " → " + path);
                throw new InvalidOperationException("[V4ContinueOverlaySelfCheck] FAIL " + _fail + "건");
            }
        }
    }
}
