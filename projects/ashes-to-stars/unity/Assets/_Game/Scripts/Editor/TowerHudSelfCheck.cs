using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>탑 허브 HUD가 배경을 덜 가린다. QA_NO면 옛 2×2 전폭(§16).</summary>
    public static class TowerHudSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Tower Hud Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(TowerHud.EnvShow);
            string no = Environment.GetEnvironmentVariable(TowerHud.EnvNo);
            Environment.SetEnvironmentVariable(TowerHud.EnvShow, null);
            Environment.SetEnvironmentVariable(TowerHud.EnvNo, null);
            TowerHud.ResetForTest();

            var body = new Rect(36f, 56f, 1208f, TowerHud.OldBodyH);
            var slim = TowerHud.Cards(body);
            Check(slim.Length == TowerHud.CardCount,
                $"도크 {TowerHud.CardCount}칸 (실제 {slim.Length})");
            Check(Mathf.Approximately(TowerHud.OverlayH(body), TowerHud.DockH),
                $"겹침 {TowerHud.OverlayH(body):0} = 도크 {TowerHud.DockH:0}");
            Check(TowerHud.OverlayH(body) < 220f, "겹침 < 220 (옛 540)");
            Check(TowerHud.OverlayH(body) < body.height * 0.40f,
                $"겹침 {TowerHud.OverlayH(body):0} < 본문 40%");
            Check(TowerHud.OpenH(body) > 300f,
                $"열린 배경 {TowerHud.OpenH(body):0} > 300");
            Check(slim[0].y > body.y + body.height * 0.55f,
                $"도크 y {slim[0].y:0} 는 아래쪽");
            Check(slim[0].height < 110f,
                $"도크 칸 높이 {slim[0].height:0} < 110");
            Check(slim[0].width > 300f,
                $"도크 칸 폭 {slim[0].width:0} 가로 카드");
            Check(TowerHud.Line().Contains("가리지 않는다"),
                $"줄 (실제 {TowerHud.Line()})");

            Environment.SetEnvironmentVariable(TowerHud.EnvNo, "1");
            Check(TowerHud.Blocked, "QA_NO면 차단");
            Check(Mathf.Approximately(TowerHud.OverlayH(body), body.height),
                $"차단 겹침 {TowerHud.OverlayH(body):0} = 옛 540");
            var old = TowerHud.Cards(body);
            Check(old[0].height > 150f, $"차단 칸 {old[0].height:0} 전폭 카드");
            Check(old[0].y < body.y + 20f, "차단하면 본문 위에서 시작");
            Check(TowerHud.Line().Contains("가린다"),
                $"차단 줄 (실제 {TowerHud.Line()})");
            Environment.SetEnvironmentVariable(TowerHud.EnvNo, null);

            Environment.SetEnvironmentVariable(TowerHud.EnvShow, "1");
            TowerHud.SeedQaIfRequested();
            Check(TowerHud.ShowQa, "시드 켜짐");
            Check(TowerHud.Line().Contains("가리지 않는다"), "시드 줄");
            Environment.SetEnvironmentVariable(TowerHud.EnvShow, null);
            TowerHud.ResetForTest();

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string tower = File.ReadAllText(Path.Combine(runtime, "TowerScreen.cs"));
            Check(tower.Contains("TowerHud.Cards"), "탑이 Cards를 읽는다");
            Check(tower.Contains("TowerHud.Line"), "자막이 Line을 읽는다");
            Check(tower.Contains("TowerHud.SeedQaIfRequested"), "시드를 읽는다");

            Check(tower.Contains("ShortCopper(_pendingCost)")
                  && tower.Contains("ShortCopper(GameState.Wallet.Copper)")
                  && tower.IndexOf("FormatCurrency(_pendingCost)") < 0,
                "탑 입장 필요·보유는 ShortCopper만");
            Check(tower.Contains("FormatCurrency(GameState.LoanBorrowable)")
                  && tower.Contains("FormatCurrency(GameState.Debt)"),
                "대출·부채 FormatCurrency 유지(다음 칸)");
            Check(!tower.Contains("UiPages.Grid(r, 2, 2"),
                "옛 2×2 전폭 Grid를 안 쓴다");

            Environment.SetEnvironmentVariable(TowerHud.EnvShow, show);
            Environment.SetEnvironmentVariable(TowerHud.EnvNo, no);
            if (_fail == 0) Debug.Log("[TowerHudSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[TowerHudSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[TowerHudSelfCheck] FAIL {_fail}건");
        }
    }
}
