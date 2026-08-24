using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>탑 골드부족·마지막목숨·사망동의 선택 바가 내비와 안 겹친다. QA_NO면 옛 yMax=640 겹침(§16).</summary>
    public static class TowerWarnHudSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Tower Warn Hud Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(TowerWarnHud.EnvShow);
            string noNav = Environment.GetEnvironmentVariable(TowerWarnHud.EnvNoNav);
            string gold = Environment.GetEnvironmentVariable(TowerWarnHud.EnvGold);
            string consent = Environment.GetEnvironmentVariable(TowerWarnHud.EnvConsent);
            Environment.SetEnvironmentVariable(TowerWarnHud.EnvShow, null);
            Environment.SetEnvironmentVariable(TowerWarnHud.EnvNoNav, null);
            Environment.SetEnvironmentVariable(TowerWarnHud.EnvGold, null);
            Environment.SetEnvironmentVariable(TowerWarnHud.EnvConsent, null);
            TowerWarnHud.ResetForTest();

            var body = new Rect(36f, HubHeader.SlimBodyTop, 1208f,
                720f - HubHeader.SlimBodyTop - UiPages.NavReserve);
            var content = TowerWarnHud.Content(body);
            var choice = TowerWarnHud.Choice(body);
            float navTop = TowerWarnHud.NavPlateTop();
            Check(content.yMax <= navTop - TowerWarnHud.NavGap + 0.01f,
                $"경고 본문 아랫변 {content.yMax:0} ≤ 내비-간격 {navTop - TowerWarnHud.NavGap:0}");
            Check(choice.yMax <= navTop - TowerWarnHud.NavGap + 0.01f,
                $"선택 바 아랫변 {choice.yMax:0} ≤ 내비-간격 {navTop - TowerWarnHud.NavGap:0}");
            Check(navTop - choice.yMax >= 10f,
                $"선택 바-내비 간격 {navTop - choice.yMax:0} ≥ 10 (전폭 금테가 내비와 한 덩어리가 되지 않게)");
            Check(Mathf.Approximately(choice.height, TowerWarnHud.ActionH),
                $"선택 바 높이 {choice.height:0} = {TowerWarnHud.ActionH:0}");
            Check(Mathf.Approximately(content.x, body.x) && Mathf.Approximately(content.width, body.width),
                "본문 가로는 그대로");
            Check(TowerWarnHud.Line().Contains("내비"),
                $"줄에 내비 (실제 {TowerWarnHud.Line()})");
            Check(TowerWarnHud.Line().Contains("위"),
                $"줄 (실제 {TowerWarnHud.Line()})");

            Environment.SetEnvironmentVariable(TowerWarnHud.EnvNoNav, "1");
            Check(TowerWarnHud.NavBlocked, "QA_NO_TOWER_WARN_NAV면 차단");
            var oldNav = TowerWarnHud.Choice(body);
            Check(oldNav.yMax > TowerWarnHud.NavPlateTop() - 1f,
                $"차단 아랫변 {oldNav.yMax:0} 이 내비와 겹친다");
            Check(TowerWarnHud.Line().Contains("겹친다"),
                $"차단 줄 (실제 {TowerWarnHud.Line()})");
            Environment.SetEnvironmentVariable(TowerWarnHud.EnvShow, "1");
            TowerWarnHud.ResetForTest();
            TowerWarnHud.SeedQaIfRequested();
            Check(TowerWarnHud.QaLifePrompt, "차단이어도 경고 시드는 켠다");
            Environment.SetEnvironmentVariable(TowerWarnHud.EnvShow, null);
            TowerWarnHud.ResetForTest();
            Environment.SetEnvironmentVariable(TowerWarnHud.EnvNoNav, null);

            Environment.SetEnvironmentVariable(TowerWarnHud.EnvShow, "1");
            TowerWarnHud.SeedQaIfRequested();
            Check(TowerWarnHud.ShowQa, "시드 켜짐");
            Check(TowerWarnHud.QaLifePrompt, "기본 시드는 마지막목숨");
            Check(!TowerWarnHud.QaGoldPrompt, "기본 시드는 골드부족 아님");
            Check(!TowerWarnHud.QaConsentPrompt, "기본 시드는 사망동의 아님");
            Check(TowerWarnHud.Line().Contains("내비 위"), "시드 줄");
            TowerWarnHud.ResetForTest();
            Environment.SetEnvironmentVariable(TowerWarnHud.EnvGold, "1");
            TowerWarnHud.SeedQaIfRequested();
            Check(TowerWarnHud.QaGoldPrompt, "QA_TOWER_WARN_GOLD면 골드부족");
            Check(!TowerWarnHud.QaLifePrompt, "골드 시드는 마지막목숨 아님");
            Check(!TowerWarnHud.QaConsentPrompt, "골드 시드는 사망동의 아님");
            TowerWarnHud.ResetForTest();
            Environment.SetEnvironmentVariable(TowerWarnHud.EnvGold, null);
            Environment.SetEnvironmentVariable(TowerWarnHud.EnvConsent, "1");
            TowerWarnHud.SeedQaIfRequested();
            Check(TowerWarnHud.QaConsentPrompt, "QA_TOWER_WARN_CONSENT면 사망동의");
            Check(!TowerWarnHud.QaLifePrompt, "동의 시드는 마지막목숨 아님");
            Check(!TowerWarnHud.QaGoldPrompt, "동의 시드는 골드부족 아님");
            Environment.SetEnvironmentVariable(TowerWarnHud.EnvShow, null);
            Environment.SetEnvironmentVariable(TowerWarnHud.EnvConsent, null);
            TowerWarnHud.ResetForTest();

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string screen = File.ReadAllText(Path.Combine(runtime, "TowerScreen.cs"));
            Check(screen.Contains("TowerWarnHud.Content"),
                "경고 본문이 Content를 읽는다 (page.yMax 붙이기 금지)");
            Check(screen.Contains("TowerWarnHud.Line"), "자막이 Line을 읽는다");
            Check(screen.Contains("TowerWarnHud.SeedQaIfRequested"), "시드를 읽는다");
            Check(screen.Contains("TowerWarnHud.QaGoldPrompt"), "골드부족 시드를 읽는다");
            Check(screen.Contains("TowerWarnHud.QaLifePrompt"), "마지막목숨 시드를 읽는다");
            Check(screen.Contains("TowerWarnHud.QaConsentPrompt"), "사망동의 시드를 읽는다");
            int contentHits = 0;
            int from = 0;
            while (true)
            {
                int at = screen.IndexOf("TowerWarnHud.Content", from, StringComparison.Ordinal);
                if (at < 0) break;
                contentHits++;
                from = at + 1;
            }
            Check(contentHits >= 3,
                $"골드부족·마지막목숨·사망동의 셋 다 Content를 읽는다 ({contentHits}곳)");
            string hud = File.ReadAllText(Path.Combine(runtime, "TowerWarnHud.cs"));
            Check(hud.Contains("NavPlateTop"),
                "선택 바가 NavPlateTop을 읽는다 (body.yMax 붙이기 금지)");
            Check(hud.Contains("NavGap"),
                "선택 바가 NavGap을 읽는다");

            Environment.SetEnvironmentVariable(TowerWarnHud.EnvShow, show);
            Environment.SetEnvironmentVariable(TowerWarnHud.EnvNoNav, noNav);
            Environment.SetEnvironmentVariable(TowerWarnHud.EnvGold, gold);
            Environment.SetEnvironmentVariable(TowerWarnHud.EnvConsent, consent);
            if (_fail == 0) Debug.Log("[TowerWarnHudSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[TowerWarnHudSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[TowerWarnHudSelfCheck] FAIL {_fail}건");
        }
    }
}
