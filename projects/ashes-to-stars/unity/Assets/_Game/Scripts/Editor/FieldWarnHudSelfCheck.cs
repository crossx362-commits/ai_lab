using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>필드 골드부족·마지막목숨 선택 바가 내비와 안 겹친다. QA_NO면 옛 yMax=640 겹침(§16).</summary>
    public static class FieldWarnHudSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Field Warn Hud Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(FieldWarnHud.EnvShow);
            string noNav = Environment.GetEnvironmentVariable(FieldWarnHud.EnvNoNav);
            string gold = Environment.GetEnvironmentVariable(FieldWarnHud.EnvGold);
            Environment.SetEnvironmentVariable(FieldWarnHud.EnvShow, null);
            Environment.SetEnvironmentVariable(FieldWarnHud.EnvNoNav, null);
            Environment.SetEnvironmentVariable(FieldWarnHud.EnvGold, null);
            FieldWarnHud.ResetForTest();

            var body = new Rect(36f, HubHeader.SlimBodyTop, 1208f,
                720f - HubHeader.SlimBodyTop - UiPages.NavReserve);
            var content = FieldWarnHud.Content(body);
            var choice = FieldWarnHud.Choice(body);
            float navTop = FieldWarnHud.NavPlateTop();
            Check(content.yMax <= navTop - FieldWarnHud.NavGap + 0.01f,
                $"경고 본문 아랫변 {content.yMax:0} ≤ 내비-간격 {navTop - FieldWarnHud.NavGap:0}");
            Check(choice.yMax <= navTop - FieldWarnHud.NavGap + 0.01f,
                $"선택 바 아랫변 {choice.yMax:0} ≤ 내비-간격 {navTop - FieldWarnHud.NavGap:0}");
            Check(navTop - choice.yMax >= 10f,
                $"선택 바-내비 간격 {navTop - choice.yMax:0} ≥ 10 (전폭 금테가 내비와 한 덩어리가 되지 않게)");
            Check(Mathf.Approximately(choice.height, FieldWarnHud.ActionH),
                $"선택 바 높이 {choice.height:0} = {FieldWarnHud.ActionH:0}");
            Check(Mathf.Approximately(content.x, body.x) && Mathf.Approximately(content.width, body.width),
                "본문 가로는 그대로");
            Check(FieldWarnHud.Line().Contains("내비"),
                $"줄에 내비 (실제 {FieldWarnHud.Line()})");
            Check(FieldWarnHud.Line().Contains("위"),
                $"줄 (실제 {FieldWarnHud.Line()})");

            Environment.SetEnvironmentVariable(FieldWarnHud.EnvNoNav, "1");
            Check(FieldWarnHud.NavBlocked, "QA_NO_FIELD_WARN_NAV면 차단");
            var oldNav = FieldWarnHud.Choice(body);
            Check(oldNav.yMax > FieldWarnHud.NavPlateTop() - 1f,
                $"차단 아랫변 {oldNav.yMax:0} 이 내비와 겹친다");
            Check(FieldWarnHud.Line().Contains("겹친다"),
                $"차단 줄 (실제 {FieldWarnHud.Line()})");
            Environment.SetEnvironmentVariable(FieldWarnHud.EnvShow, "1");
            FieldWarnHud.ResetForTest();
            FieldWarnHud.SeedQaIfRequested();
            Check(FieldWarnHud.QaLifePrompt, "차단이어도 경고 시드는 켠다");
            Environment.SetEnvironmentVariable(FieldWarnHud.EnvShow, null);
            FieldWarnHud.ResetForTest();
            Environment.SetEnvironmentVariable(FieldWarnHud.EnvNoNav, null);

            Environment.SetEnvironmentVariable(FieldWarnHud.EnvShow, "1");
            FieldWarnHud.SeedQaIfRequested();
            Check(FieldWarnHud.ShowQa, "시드 켜짐");
            Check(FieldWarnHud.QaLifePrompt, "기본 시드는 마지막목숨");
            Check(!FieldWarnHud.QaGoldPrompt, "기본 시드는 골드부족 아님");
            Check(FieldWarnHud.Line().Contains("내비 위"), "시드 줄");
            FieldWarnHud.ResetForTest();
            Environment.SetEnvironmentVariable(FieldWarnHud.EnvGold, "1");
            FieldWarnHud.SeedQaIfRequested();
            Check(FieldWarnHud.QaGoldPrompt, "QA_FIELD_WARN_GOLD면 골드부족");
            Check(!FieldWarnHud.QaLifePrompt, "골드 시드는 마지막목숨 아님");
            Environment.SetEnvironmentVariable(FieldWarnHud.EnvShow, null);
            Environment.SetEnvironmentVariable(FieldWarnHud.EnvGold, null);
            FieldWarnHud.ResetForTest();

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string screen = File.ReadAllText(Path.Combine(runtime, "FieldScreen.cs"));
            Check(screen.Contains("FieldWarnHud.Content"),
                "경고 본문이 Content를 읽는다 (page.yMax 붙이기 금지)");
            Check(screen.Contains("FieldWarnHud.Line"), "자막이 Line을 읽는다");
            Check(screen.Contains("FieldWarnHud.SeedQaIfRequested"), "시드를 읽는다");
            Check(screen.Contains("FieldWarnHud.QaGoldPrompt"), "골드부족 시드를 읽는다");
            Check(screen.Contains("FieldWarnHud.QaLifePrompt"), "마지막목숨 시드를 읽는다");
            int contentHits = 0;
            int from = 0;
            while (true)
            {
                int at = screen.IndexOf("FieldWarnHud.Content", from, StringComparison.Ordinal);
                if (at < 0) break;
                contentHits++;
                from = at + 1;
            }
            Check(contentHits >= 2,
                $"골드부족·마지막목숨 둘 다 Content를 읽는다 ({contentHits}곳)");
            string hud = File.ReadAllText(Path.Combine(runtime, "FieldWarnHud.cs"));
            Check(hud.Contains("NavPlateTop"),
                "선택 바가 NavPlateTop을 읽는다 (body.yMax 붙이기 금지)");
            Check(hud.Contains("NavGap"),
                "선택 바가 NavGap을 읽는다");

            Environment.SetEnvironmentVariable(FieldWarnHud.EnvShow, show);
            Environment.SetEnvironmentVariable(FieldWarnHud.EnvNoNav, noNav);
            Environment.SetEnvironmentVariable(FieldWarnHud.EnvGold, gold);
            if (_fail == 0) Debug.Log("[FieldWarnHudSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[FieldWarnHudSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[FieldWarnHudSelfCheck] FAIL {_fail}건");
        }
    }
}
