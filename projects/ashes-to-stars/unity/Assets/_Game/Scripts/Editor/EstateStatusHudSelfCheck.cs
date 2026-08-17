using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>영지 현황 HUD가 마을을 덜 가린다. QA_NO면 옛 2×2 전폭(§16).</summary>
    public static class EstateStatusHudSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Estate Status Hud Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(EstateStatusHud.EnvShow);
            string no = Environment.GetEnvironmentVariable(EstateStatusHud.EnvNo);
            Environment.SetEnvironmentVariable(EstateStatusHud.EnvShow, null);
            Environment.SetEnvironmentVariable(EstateStatusHud.EnvNo, null);
            EstateStatusHud.ResetForTest();

            var body = new Rect(36f, 118f, 1208f, EstateStatusHud.OldBodyH);
            var slim = EstateStatusHud.Cards(body);
            Check(slim.Length == EstateStatusHud.CardCount,
                $"도크 {EstateStatusHud.CardCount}칸 (실제 {slim.Length})");
            Check(Mathf.Approximately(EstateStatusHud.OverlayH(body), EstateStatusHud.DockH),
                $"겹침 {EstateStatusHud.OverlayH(body):0} = 도크 {EstateStatusHud.DockH:0}");
            Check(EstateStatusHud.OverlayH(body) < 100f, "겹침 < 100 (옛 540)");
            Check(EstateStatusHud.OverlayH(body) < body.height * 0.20f,
                $"겹침 {EstateStatusHud.OverlayH(body):0} < 본문 20%");
            Check(EstateStatusHud.OpenH(body) > 400f,
                $"열린 배경 {EstateStatusHud.OpenH(body):0} > 400");
            Check(slim[0].y > body.y + body.height * 0.70f,
                $"도크 y {slim[0].y:0} 는 아래쪽");
            Check(slim[0].height < 90f,
                $"도크 칸 높이 {slim[0].height:0} < 90");
            Check(slim[0].width > 180f,
                $"도크 칸 폭 {slim[0].width:0} 가로 카드");
            Check(EstateStatusHud.Line().Contains("가리지 않는다"),
                $"줄 (실제 {EstateStatusHud.Line()})");

            Environment.SetEnvironmentVariable(EstateStatusHud.EnvNo, "1");
            Check(EstateStatusHud.Blocked, "QA_NO면 차단");
            Check(Mathf.Approximately(EstateStatusHud.OverlayH(body), body.height),
                $"차단 겹침 {EstateStatusHud.OverlayH(body):0} = 옛 540");
            var old = EstateStatusHud.Cards(body);
            Check(old[0].height > 70f && old[0].width > 1000f,
                $"차단 영공 {old[0].width:0}×{old[0].height:0} 전폭");
            Check(old[1].height > 150f, $"차단 칸 {old[1].height:0} 전폭 카드");
            Check(old[0].y < body.y + 4f, "차단하면 본문 위에서 시작");
            Check(EstateStatusHud.Line().Contains("가린다"),
                $"차단 줄 (실제 {EstateStatusHud.Line()})");
            Environment.SetEnvironmentVariable(EstateStatusHud.EnvNo, null);

            Environment.SetEnvironmentVariable(EstateStatusHud.EnvShow, "1");
            EstateStatusHud.SeedQaIfRequested();
            Check(EstateStatusHud.ShowQa, "시드 켜짐");
            Check(EstateStatusHud.Line().Contains("가리지 않는다"), "시드 줄");
            Environment.SetEnvironmentVariable(EstateStatusHud.EnvShow, null);
            EstateStatusHud.ResetForTest();

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string estate = File.ReadAllText(Path.Combine(runtime, "EstateScreen.cs"));
            Check(estate.Contains("EstateStatusHud.Cards"), "영지가 Cards를 읽는다");
            Check(estate.Contains("EstateStatusHud.Line"), "자막이 Line을 읽는다");
            Check(estate.Contains("EstateStatusHud.SeedQaIfRequested"), "시드를 읽는다");
            Check(!estate.Contains("UiPages.Grid(new Rect(r.x, r.y + 92f"),
                "옛 2×2 전폭 Grid를 안 쓴다");

            Environment.SetEnvironmentVariable(EstateStatusHud.EnvShow, show);
            Environment.SetEnvironmentVariable(EstateStatusHud.EnvNo, no);
            if (_fail == 0) Debug.Log("[EstateStatusHudSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[EstateStatusHudSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException(
                $"[EstateStatusHudSelfCheck] FAIL {_fail}건");
        }
    }
}
