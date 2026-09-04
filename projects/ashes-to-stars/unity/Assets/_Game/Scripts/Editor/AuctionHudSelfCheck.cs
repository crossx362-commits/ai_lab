using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>경매 안내가 전폭을 덮지 않는다. QA_NO면 옛 전폭(§16).</summary>
    public static class AuctionHudSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Auction Hud Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(AuctionHud.EnvShow);
            string no = Environment.GetEnvironmentVariable(AuctionHud.EnvNo);
            Environment.SetEnvironmentVariable(AuctionHud.EnvShow, null);
            Environment.SetEnvironmentVariable(AuctionHud.EnvNo, null);
            AuctionHud.ResetForTest();

            var body = new Rect(36f, 56f, 1208f, 584f);
            var slim = AuctionHud.BarRect(body, 0);
            Check(Mathf.Approximately(AuctionHud.BarH, AuctionHud.SlimH),
                $"높이 {AuctionHud.BarH:0} = 슬림 {AuctionHud.SlimH:0}");
            Check(Mathf.Approximately(AuctionHud.BarW(body), AuctionHud.SlimW),
                $"폭 {AuctionHud.BarW(body):0} = 슬림 {AuctionHud.SlimW:0}");
            Check(AuctionHud.BarW(body) < body.width * 0.50f,
                $"폭 {AuctionHud.BarW(body):0} < 본문 50%");
            Check(AuctionHud.StatusLine().Contains("/") && !AuctionHud.StatusLine().Contains("내 등록"),
                $"상태 줄 짧음 (실제 {AuctionHud.StatusLine()})");
            Check(AuctionHud.OverlayH(2) < 90f,
                $"두 줄 겹침 {AuctionHud.OverlayH(2):0} < 90 (옛 140)");
            Check(AuctionHud.OverlayH(2) < AuctionHud.OldBarH * 2f + AuctionHud.OldGap,
                $"슬림 겹침 {AuctionHud.OverlayH(2):0} < 옛 140");
            Check(slim.width < 600f, $"막대 폭 {slim.width:0} < 600");
            Check(slim.x >= body.x - 0.1f, $"막대 x {slim.x:0} 왼쪽 도크");
            Check(slim.xMax < body.x + body.width * 0.55f,
                $"막대 오른쪽 {slim.xMax:0} 이 본문 절반 안");
            var lots = AuctionHud.LotsBody(body, 2);
            Check(lots.y > slim.yMax, $"롯 y {lots.y:0} > 막대 바닥 {slim.yMax:0}");
            Check(lots.height > 400f, $"롯 높이 {lots.height:0} > 400");
            Check(AuctionHud.Line().Contains("가리지 않는다"),
                $"줄 (실제 {AuctionHud.Line()})");

            Environment.SetEnvironmentVariable(AuctionHud.EnvNo, "1");
            Check(AuctionHud.Blocked, "QA_NO면 차단");
            Check(Mathf.Approximately(AuctionHud.BarH, AuctionHud.OldBarH),
                $"차단 높이 {AuctionHud.BarH:0} = 옛 64");
            Check(AuctionHud.BarW(body) > body.width,
                $"차단 폭 {AuctionHud.BarW(body):0} 전폭");
            Check(Mathf.Approximately(AuctionHud.OverlayH(2),
                    AuctionHud.OldBarH * 2f + AuctionHud.OldGap),
                $"차단 겹침 {AuctionHud.OverlayH(2):0} = 옛 140");
            var old = AuctionHud.BarRect(body, 0);
            Check(old.width > 1200f, $"차단 막대 {old.width:0} 전폭");
            Check(AuctionHud.Line().Contains("가린다"),
                $"차단 줄 (실제 {AuctionHud.Line()})");
            Environment.SetEnvironmentVariable(AuctionHud.EnvNo, null);

            Environment.SetEnvironmentVariable(AuctionHud.EnvShow, "1");
            AuctionHud.SeedQaIfRequested();
            Check(AuctionHud.ShowQa, "시드 켜짐");
            Check(AuctionHud.Line().Contains("가리지 않는다"), "시드 줄");
            Check(GameState.TowerFloor >= EstateScreen.AuctionUnlockFloor,
                $"시드 층 {GameState.TowerFloor} ≥ 30");
            Environment.SetEnvironmentVariable(AuctionHud.EnvShow, null);
            AuctionHud.ResetForTest();

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string estate = File.ReadAllText(Path.Combine(runtime, "EstateScreen.cs"));
            Check(estate.Contains("AuctionHud.BarRect"), "경매장이 BarRect를 읽는다");
            Check(estate.Contains("AuctionHud.LotsBody"), "롯이 LotsBody를 읽는다");
            Check(estate.Contains("AuctionHud.Line"), "자막이 Line을 읽는다");
            Check(estate.Contains("AuctionHud.SeedQaIfRequested"), "시드를 읽는다");

            Environment.SetEnvironmentVariable(AuctionHud.EnvShow, show);
            Environment.SetEnvironmentVariable(AuctionHud.EnvNo, no);
            if (_fail == 0) Debug.Log("[AuctionHudSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[AuctionHudSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[AuctionHudSelfCheck] FAIL {_fail}건");
        }
    }
}
