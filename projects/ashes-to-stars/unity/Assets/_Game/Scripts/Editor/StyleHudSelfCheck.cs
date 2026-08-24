using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>전투 스타일 선택 바가 내비와 안 겹친다. QA_NO면 옛 yMax=640 겹침(§16).</summary>
    public static class StyleHudSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Style Hud Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(StyleHud.EnvShow);
            string noNav = Environment.GetEnvironmentVariable(StyleHud.EnvNoNav);
            Environment.SetEnvironmentVariable(StyleHud.EnvShow, null);
            Environment.SetEnvironmentVariable(StyleHud.EnvNoNav, null);
            StyleHud.ResetForTest();

            var body = new Rect(36f, HubHeader.SlimBodyTop, 1208f,
                720f - HubHeader.SlimBodyTop - UiPages.NavReserve);
            var content = StyleHud.Content(body);
            float navTop = StyleHud.NavPlateTop();
            Check(content.yMax <= navTop - StyleHud.NavGap + 0.01f,
                $"선택 바 아랫변 {content.yMax:0} ≤ 내비-간격 {navTop - StyleHud.NavGap:0}");
            Check(navTop - content.yMax >= 10f,
                $"선택 바-내비 간격 {navTop - content.yMax:0} ≥ 10 (전폭 금테가 내비와 한 덩어리가 되지 않게)");
            Check(Mathf.Approximately(content.x, body.x) && Mathf.Approximately(content.width, body.width),
                "본문 가로는 그대로");
            Check(StyleHud.Line().Contains("내비"),
                $"줄에 내비 (실제 {StyleHud.Line()})");
            Check(StyleHud.Line().Contains("위"),
                $"줄 (실제 {StyleHud.Line()})");

            Environment.SetEnvironmentVariable(StyleHud.EnvNoNav, "1");
            Check(StyleHud.NavBlocked, "QA_NO_STYLE_NAV면 차단");
            var oldNav = StyleHud.Content(body);
            Check(oldNav.yMax > StyleHud.NavPlateTop() - 1f,
                $"차단 아랫변 {oldNav.yMax:0} 이 내비와 겹친다");
            Check(StyleHud.Line().Contains("겹친다"),
                $"차단 줄 (실제 {StyleHud.Line()})");
            Environment.SetEnvironmentVariable(StyleHud.EnvNoNav, null);

            Environment.SetEnvironmentVariable(StyleHud.EnvShow, "1");
            StyleHud.SeedQaIfRequested();
            Check(StyleHud.ShowQa, "시드 켜짐");
            Check(StyleHud.Line().Contains("내비 위"), "시드 줄");
            Environment.SetEnvironmentVariable(StyleHud.EnvShow, null);
            StyleHud.ResetForTest();

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string screen = File.ReadAllText(Path.Combine(runtime, "StyleScreen.cs"));
            Check(screen.Contains("StyleHud.Content"),
                "본문이 Content를 읽는다 (page.yMax 붙이기 금지)");
            Check(screen.Contains("StyleHud.Line"), "자막이 Line을 읽는다");
            Check(screen.Contains("StyleHud.SeedQaIfRequested"), "시드를 읽는다");
            string hud = File.ReadAllText(Path.Combine(runtime, "StyleHud.cs"));
            Check(hud.Contains("NavPlateTop"),
                "선택 바가 NavPlateTop을 읽는다 (body.yMax 붙이기 금지)");
            Check(hud.Contains("NavGap"),
                "선택 바가 NavGap을 읽는다");

            Environment.SetEnvironmentVariable(StyleHud.EnvShow, show);
            Environment.SetEnvironmentVariable(StyleHud.EnvNoNav, noNav);
            if (_fail == 0) Debug.Log("[StyleHudSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[StyleHudSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[StyleHudSelfCheck] FAIL {_fail}건");
        }
    }
}
