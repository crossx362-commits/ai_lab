using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>파티 편성 탭 안내줄이 내비와 안 겹친다. QA_NO면 옛 yMax=640 겹침(§16).</summary>
    public static class PartyFormHudSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Party Form Hud Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(PartyFormHud.EnvShow);
            string noNav = Environment.GetEnvironmentVariable(PartyFormHud.EnvNoNav);
            Environment.SetEnvironmentVariable(PartyFormHud.EnvShow, null);
            Environment.SetEnvironmentVariable(PartyFormHud.EnvNoNav, null);
            PartyFormHud.ResetForTest();

            var body = new Rect(36f, HubHeader.SlimBodyTop, 1208f,
                720f - HubHeader.SlimBodyTop - UiPages.NavReserve);
            var page = UiPages.AfterTabs(body);
            var content = PartyFormHud.Content(page);
            var hint = PartyFormHud.Hint(page);
            float navTop = PartyFormHud.NavPlateTop();
            Check(content.yMax <= navTop - PartyFormHud.NavGap + 0.01f,
                $"편성 본문 아랫변 {content.yMax:0} ≤ 내비-간격 {navTop - PartyFormHud.NavGap:0}");
            Check(hint.yMax <= navTop - PartyFormHud.NavGap + 0.01f,
                $"안내줄 아랫변 {hint.yMax:0} ≤ 내비-간격 {navTop - PartyFormHud.NavGap:0}");
            Check(navTop - hint.yMax >= 10f,
                $"안내줄-내비 간격 {navTop - hint.yMax:0} ≥ 10 (전폭 금테가 내비와 한 덩어리가 되지 않게)");
            Check(Mathf.Approximately(hint.height, PartyFormHud.HintH),
                $"안내줄 높이 {hint.height:0} = {PartyFormHud.HintH:0}");
            Check(Mathf.Approximately(content.x, page.x) && Mathf.Approximately(content.width, page.width),
                "본문 가로는 그대로");
            Check(PartyFormHud.Line().Contains("내비"),
                $"줄에 내비 (실제 {PartyFormHud.Line()})");
            Check(PartyFormHud.Line().Contains("위"),
                $"줄 (실제 {PartyFormHud.Line()})");

            Environment.SetEnvironmentVariable(PartyFormHud.EnvNoNav, "1");
            Check(PartyFormHud.NavBlocked, "QA_NO_PARTY_FORM_NAV면 차단");
            var oldHint = PartyFormHud.Hint(page);
            Check(oldHint.yMax > PartyFormHud.NavPlateTop() - 1f,
                $"차단 아랫변 {oldHint.yMax:0} 이 내비와 겹친다");
            Check(PartyFormHud.Line().Contains("겹친다"),
                $"차단 줄 (실제 {PartyFormHud.Line()})");
            Environment.SetEnvironmentVariable(PartyFormHud.EnvNoNav, null);

            Environment.SetEnvironmentVariable(PartyFormHud.EnvShow, "1");
            PartyFormHud.SeedQaIfRequested();
            Check(PartyFormHud.ShowQa, "시드 켜짐");
            Check(PartyFormHud.Line().Contains("내비 위"), "시드 줄");
            Environment.SetEnvironmentVariable(PartyFormHud.EnvShow, null);
            PartyFormHud.ResetForTest();

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string screen = File.ReadAllText(Path.Combine(runtime, "PartyScreen.cs"));
            Check(screen.Contains("PartyFormHud.Content"),
                "편성 본문이 Content를 읽는다 (page.yMax 붙이기 금지)");
            Check(screen.Contains("PartyFormHud.Hint"),
                "안내줄이 Hint를 읽는다 (page.yMax-RowH 금지)");
            Check(screen.Contains("PartyFormHud.Line"), "자막이 Line을 읽는다");
            Check(screen.Contains("PartyFormHud.SeedQaIfRequested"), "시드를 읽는다");
            string hud = File.ReadAllText(Path.Combine(runtime, "PartyFormHud.cs"));
            Check(hud.Contains("NavPlateTop"),
                "안내줄이 NavPlateTop을 읽는다 (page.yMax 붙이기 금지)");
            Check(hud.Contains("NavGap"),
                "안내줄이 NavGap을 읽는다");

            Environment.SetEnvironmentVariable(PartyFormHud.EnvShow, show);
            Environment.SetEnvironmentVariable(PartyFormHud.EnvNoNav, noNav);
            if (_fail == 0) Debug.Log("[PartyFormHudSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[PartyFormHudSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[PartyFormHudSelfCheck] FAIL {_fail}건");
        }
    }
}
