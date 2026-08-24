using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>사냥 시작 편성 선택 바가 내비와 안 겹친다. QA_NO면 옛 yMax=640 겹침(§16).</summary>
    public static class HuntPickHudSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Hunt Pick Hud Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(HuntPickHud.EnvShow);
            string noNav = Environment.GetEnvironmentVariable(HuntPickHud.EnvNoNav);
            Environment.SetEnvironmentVariable(HuntPickHud.EnvShow, null);
            Environment.SetEnvironmentVariable(HuntPickHud.EnvNoNav, null);
            HuntPickHud.ResetForTest();

            var body = new Rect(36f, HubHeader.SlimBodyTop, 1208f,
                720f - HubHeader.SlimBodyTop - UiPages.NavReserve);
            var content = HuntPickHud.Content(body);
            var actions = HuntPickHud.Actions(body);
            float navTop = HuntPickHud.NavPlateTop();
            Check(content.yMax <= navTop - HuntPickHud.NavGap + 0.01f,
                $"편성 본문 아랫변 {content.yMax:0} ≤ 내비-간격 {navTop - HuntPickHud.NavGap:0}");
            Check(actions.yMax <= navTop - HuntPickHud.NavGap + 0.01f,
                $"선택 바 아랫변 {actions.yMax:0} ≤ 내비-간격 {navTop - HuntPickHud.NavGap:0}");
            Check(navTop - actions.yMax >= 10f,
                $"선택 바-내비 간격 {navTop - actions.yMax:0} ≥ 10 (전폭 금테가 내비와 한 덩어리가 되지 않게)");
            Check(Mathf.Approximately(actions.height, HuntPickHud.ActionH),
                $"선택 바 높이 {actions.height:0} = {HuntPickHud.ActionH:0}");
            Check(Mathf.Approximately(content.x, body.x) && Mathf.Approximately(content.width, body.width),
                "본문 가로는 그대로");
            Check(HuntPickHud.Line().Contains("내비"),
                $"줄에 내비 (실제 {HuntPickHud.Line()})");
            Check(HuntPickHud.Line().Contains("위"),
                $"줄 (실제 {HuntPickHud.Line()})");

            Environment.SetEnvironmentVariable(HuntPickHud.EnvNoNav, "1");
            Check(HuntPickHud.NavBlocked, "QA_NO_HUNT_PICK_NAV면 차단");
            var oldNav = HuntPickHud.Actions(body);
            Check(oldNav.yMax > HuntPickHud.NavPlateTop() - 1f,
                $"차단 아랫변 {oldNav.yMax:0} 이 내비와 겹친다");
            Check(HuntPickHud.Line().Contains("겹친다"),
                $"차단 줄 (실제 {HuntPickHud.Line()})");
            Environment.SetEnvironmentVariable(HuntPickHud.EnvNoNav, null);

            Environment.SetEnvironmentVariable(HuntPickHud.EnvShow, "1");
            HuntPickHud.SeedQaIfRequested();
            Check(HuntPickHud.ShowQa, "시드 켜짐");
            Check(HuntPickHud.Line().Contains("내비 위"), "시드 줄");
            Environment.SetEnvironmentVariable(HuntPickHud.EnvShow, null);
            HuntPickHud.ResetForTest();
            HuntStart.ResetForTest();

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string screen = File.ReadAllText(Path.Combine(runtime, "FieldScreen.cs"));
            Check(screen.Contains("HuntPickHud.Content"),
                "편성 본문이 Content를 읽는다 (page.yMax 붙이기 금지)");
            Check(screen.Contains("HuntPickHud.Actions"),
                "선택 바가 Actions를 읽는다 (page.yMax-168 금지)");
            Check(screen.Contains("HuntPickHud.Line"), "자막이 Line을 읽는다");
            Check(screen.Contains("HuntPickHud.SeedQaIfRequested"), "시드를 읽는다");
            string hud = File.ReadAllText(Path.Combine(runtime, "HuntPickHud.cs"));
            Check(hud.Contains("NavPlateTop"),
                "선택 바가 NavPlateTop을 읽는다 (body.yMax 붙이기 금지)");
            Check(hud.Contains("NavGap"),
                "선택 바가 NavGap을 읽는다");

            Environment.SetEnvironmentVariable(HuntPickHud.EnvShow, show);
            Environment.SetEnvironmentVariable(HuntPickHud.EnvNoNav, noNav);
            if (_fail == 0) Debug.Log("[HuntPickHudSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[HuntPickHudSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[HuntPickHudSelfCheck] FAIL {_fail}건");
        }
    }
}
