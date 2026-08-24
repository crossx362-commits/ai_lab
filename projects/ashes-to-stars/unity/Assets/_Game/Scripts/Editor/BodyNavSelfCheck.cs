using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>허브 본문이 내비와 안 겹친다. QA_NO면 옛 yMax=640 겹침(§16).</summary>
    public static class BodyNavSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Body Nav Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(BodyNav.EnvShow);
            string no = Environment.GetEnvironmentVariable(BodyNav.EnvNo);
            Environment.SetEnvironmentVariable(BodyNav.EnvShow, null);
            Environment.SetEnvironmentVariable(BodyNav.EnvNo, null);
            BodyNav.ResetForTest();

            var raw = new Rect(GameScreen.BodyPadX, HubHeader.SlimBodyTop,
                1280f - GameScreen.BodyPadX * 2f,
                720f - HubHeader.SlimBodyTop - UiPages.NavReserve);
            Check(Mathf.Approximately(raw.yMax, 640f),
                $"옛 본문 아랫변 {raw.yMax:0} = 640 (NavReserve 80)");

            var fit = BodyNav.Fit(raw, true);
            float navTop = BodyNav.NavPlateTop();
            Check(fit.yMax <= navTop - BodyNav.NavGap + 0.01f,
                $"본문 아랫변 {fit.yMax:0} ≤ 내비-간격 {navTop - BodyNav.NavGap:0}");
            Check(navTop - fit.yMax >= 10f,
                $"본문-내비 간격 {navTop - fit.yMax:0} ≥ 10 (하단 금테가 내비와 한 덩어리가 되지 않게)");
            Check(Mathf.Approximately(fit.x, raw.x) && Mathf.Approximately(fit.width, raw.width),
                "본문 가로는 그대로");
            Check(Mathf.Approximately(fit.y, raw.y), "본문 꼭대기는 그대로");
            Check(BodyNav.Line().Contains("내비"),
                $"줄에 내비 (실제 {BodyNav.Line()})");
            Check(BodyNav.Line().Contains("위"),
                $"줄 (실제 {BodyNav.Line()})");

            var noBar = BodyNav.Fit(raw, false);
            Check(Mathf.Approximately(noBar.yMax, raw.yMax),
                $"도크 없는 화면 아랫변 {noBar.yMax:0} = 옛 {raw.yMax:0}");

            Environment.SetEnvironmentVariable(BodyNav.EnvNo, "1");
            Check(BodyNav.Blocked, "QA_NO_BODY_NAV면 차단");
            var oldNav = BodyNav.Fit(raw, true);
            Check(oldNav.yMax > BodyNav.NavPlateTop() - 1f,
                $"차단 아랫변 {oldNav.yMax:0} 이 내비와 겹친다");
            Check(Mathf.Approximately(oldNav.yMax, raw.yMax),
                $"차단 아랫변 {oldNav.yMax:0} = 옛 640");
            Check(BodyNav.Line().Contains("겹친다"),
                $"차단 줄 (실제 {BodyNav.Line()})");
            Environment.SetEnvironmentVariable(BodyNav.EnvNo, null);

            Environment.SetEnvironmentVariable(BodyNav.EnvShow, "1");
            BodyNav.SeedQaIfRequested();
            Check(BodyNav.ShowQa, "시드 켜짐");
            Check(BodyNav.Line().Contains("내비 위"), "시드 줄");
            Environment.SetEnvironmentVariable(BodyNav.EnvShow, null);
            BodyNav.ResetForTest();

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string screen = File.ReadAllText(Path.Combine(runtime, "GameScreen.cs"));
            Check(screen.Contains("BodyNav.Fit"),
                "본문이 Fit을 읽는다 (REF_H-NavReserve 붙이기 금지)");
            Check(screen.Contains("BodyNav.SeedQaIfRequested"), "시드를 읽는다");
            string hud = File.ReadAllText(Path.Combine(runtime, "BodyNav.cs"));
            Check(hud.Contains("NavPlateTop"),
                "본문이 NavPlateTop을 읽는다 (body.yMax 붙이기 금지)");
            Check(hud.Contains("NavGap"),
                "본문이 NavGap을 읽는다");

            Environment.SetEnvironmentVariable(BodyNav.EnvShow, show);
            Environment.SetEnvironmentVariable(BodyNav.EnvNo, no);
            if (_fail == 0) Debug.Log("[BodyNavSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[BodyNavSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[BodyNavSelfCheck] FAIL {_fail}건");
        }
    }
}
