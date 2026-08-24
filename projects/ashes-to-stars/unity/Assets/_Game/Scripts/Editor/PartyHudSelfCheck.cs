using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>파티 출전 탭 HUD가 내비와 안 겹친다. QA_NO면 옛 2×2 전폭(§16).</summary>
    public static class PartyHudSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Party Hud Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(PartyHud.EnvShow);
            string no = Environment.GetEnvironmentVariable(PartyHud.EnvNo);
            Environment.SetEnvironmentVariable(PartyHud.EnvShow, null);
            Environment.SetEnvironmentVariable(PartyHud.EnvNo, null);
            PartyHud.ResetForTest();

            var body = new Rect(36f, HubHeader.SlimBodyTop, 1208f,
                720f - HubHeader.SlimBodyTop - UiPages.NavReserve);
            var page = UiPages.AfterTabs(body);
            var slim = PartyHud.Cards(page);
            Check(slim.Length == PartyHud.CardCount,
                $"도크 {PartyHud.CardCount}칸 (실제 {slim.Length})");
            Check(Mathf.Approximately(PartyHud.OverlayH(page), PartyHud.DockH),
                $"겹침 {PartyHud.OverlayH(page):0} = 도크 {PartyHud.DockH:0}");
            Check(PartyHud.OverlayH(page) < 220f, "겹침 < 220 (옛 AfterTabs 전폭)");
            Check(PartyHud.OpenH(page) > 200f,
                $"열린 배경 {PartyHud.OpenH(page):0} > 200");
            Check(slim[0].y > page.y + page.height * 0.40f,
                $"도크 y {slim[0].y:0} 는 아래쪽");
            Check(slim[0].height < 110f,
                $"도크 칸 높이 {slim[0].height:0} < 110");
            Check(slim[0].width > 300f,
                $"도크 칸 폭 {slim[0].width:0} 가로 카드");
            Check(PartyHud.Line().Contains("가리지 않는다"),
                $"줄 (실제 {PartyHud.Line()})");
            Check(PartyHud.Line().Contains("내비"),
                $"줄에 내비 (실제 {PartyHud.Line()})");

            float dockBottom = slim[slim.Length - 1].yMax;
            float navTop = PartyHud.NavPlateTop();
            Check(dockBottom <= navTop - PartyHud.NavGap + 0.01f,
                $"도크 아랫변 {dockBottom:0} ≤ 내비-간격 {navTop - PartyHud.NavGap:0}");
            Check(navTop - dockBottom >= 10f,
                $"도크-내비 간격 {navTop - dockBottom:0} ≥ 10 (전폭 카드가 내비와 한 덩어리가 되지 않게)");

            Environment.SetEnvironmentVariable(PartyHud.EnvNo, "1");
            Check(PartyHud.Blocked, "QA_NO면 차단");
            Check(PartyHud.OverlayH(page) > 400f,
                $"차단 겹침 {PartyHud.OverlayH(page):0} > 400 (옛 AfterTabs)");
            var old = PartyHud.Cards(page);
            Check(old[0].height > 150f, $"차단 칸 {old[0].height:0} 전폭 카드");
            Check(old[0].y < page.y + 20f, "차단하면 본문 위에서 시작");
            Check(old[old.Length - 1].yMax > PartyHud.NavPlateTop() - 1f,
                $"차단 아랫변 {old[old.Length - 1].yMax:0} 이 내비와 겹친다");
            Check(PartyHud.Line().Contains("가린다"),
                $"차단 줄 (실제 {PartyHud.Line()})");
            Environment.SetEnvironmentVariable(PartyHud.EnvNo, null);

            Environment.SetEnvironmentVariable(PartyHud.EnvShow, "1");
            PartyHud.SeedQaIfRequested();
            Check(PartyHud.ShowQa, "시드 켜짐");
            Check(PartyHud.Line().Contains("가리지 않는다"), "시드 줄");
            Environment.SetEnvironmentVariable(PartyHud.EnvShow, null);
            PartyHud.ResetForTest();

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string party = File.ReadAllText(Path.Combine(runtime, "PartyScreen.cs"));
            Check(party.Contains("PartyHud.Cards"), "출전이 Cards를 읽는다");
            Check(party.Contains("PartyHud.SeedQaIfRequested"), "시드를 읽는다");
            Check(!party.Contains("UiPages.Grid(r, 2, 2")
                  && !party.Contains("UiPages.Grid(page, 2, 2"),
                "본문 2×2 전폭 Grid를 안 쓴다");
            Check(party.Contains("PartyHud.Dock") || party.Contains("PartyHud.Cards"),
                "출전이 Dock/Cards를 읽는다 (page.yMax-전폭 금지)");
            string hud = File.ReadAllText(Path.Combine(runtime, "PartyHud.cs"));
            Check(hud.Contains("NavPlateTop"),
                "도크가 NavPlateTop을 읽는다 (page.yMax 붙이기 금지)");

            Environment.SetEnvironmentVariable(PartyHud.EnvShow, show);
            Environment.SetEnvironmentVariable(PartyHud.EnvNo, no);
            if (_fail == 0) Debug.Log("[PartyHudSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[PartyHudSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[PartyHudSelfCheck] FAIL {_fail}건");
        }
    }
}
