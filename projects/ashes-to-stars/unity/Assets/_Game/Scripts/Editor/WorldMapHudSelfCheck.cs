using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>월드맵 HUD가 별을 덜 가린다. QA_NO면 옛 2×2 전폭(§16).</summary>
    public static class WorldMapHudSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/World Map Hud Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(WorldMapHud.EnvShow);
            string no = Environment.GetEnvironmentVariable(WorldMapHud.EnvNo);
            Environment.SetEnvironmentVariable(WorldMapHud.EnvShow, null);
            Environment.SetEnvironmentVariable(WorldMapHud.EnvNo, null);
            WorldMapHud.ResetForTest();

            var body = new Rect(36f, 56f, 1208f, WorldMapHud.OldBodyH);
            var slim = WorldMapHud.Cards(body);
            Check(slim.Length == WorldMapHud.CardCount,
                $"도크 {WorldMapHud.CardCount}칸 (실제 {slim.Length})");
            Check(Mathf.Approximately(WorldMapHud.OverlayH(body), WorldMapHud.DockH),
                $"겹침 {WorldMapHud.OverlayH(body):0} = 도크 {WorldMapHud.DockH:0}");
            Check(WorldMapHud.OverlayH(body) < 220f, "겹침 < 220 (옛 456)");
            Check(WorldMapHud.OverlayH(body) < body.height * 0.40f,
                $"겹침 {WorldMapHud.OverlayH(body):0} < 본문 40%");
            Check(WorldMapHud.OpenH(body) > 300f,
                $"열린 배경 {WorldMapHud.OpenH(body):0} > 300");
            Check(slim[0].y > body.y + body.height * 0.55f,
                $"도크 y {slim[0].y:0} 는 아래쪽");
            Check(slim[0].height < 110f,
                $"도크 칸 높이 {slim[0].height:0} < 110");
            Check(slim[0].width > 300f,
                $"도크 칸 폭 {slim[0].width:0} 가로 카드");
            Check(WorldMapHud.Line().Contains("가리지 않는다"),
                $"줄 (실제 {WorldMapHud.Line()})");

            Environment.SetEnvironmentVariable(WorldMapHud.EnvNo, "1");
            Check(WorldMapHud.Blocked, "QA_NO면 차단");
            Check(WorldMapHud.OverlayH(body) > 400f,
                $"차단 겹침 {WorldMapHud.OverlayH(body):0} > 400 (옛 AfterPlate)");
            var old = WorldMapHud.Cards(body);
            Check(old[0].height > 150f, $"차단 칸 {old[0].height:0} 전폭 카드");
            Check(old[0].y < body.y + 100f, "차단하면 본문 위에서 시작");
            Check(WorldMapHud.Line().Contains("가린다"),
                $"차단 줄 (실제 {WorldMapHud.Line()})");
            Environment.SetEnvironmentVariable(WorldMapHud.EnvNo, null);

            Environment.SetEnvironmentVariable(WorldMapHud.EnvShow, "1");
            WorldMapHud.SeedQaIfRequested();
            Check(WorldMapHud.ShowQa, "시드 켜짐");
            Check(WorldMapHud.Line().Contains("가리지 않는다"), "시드 줄");
            Environment.SetEnvironmentVariable(WorldMapHud.EnvShow, null);
            WorldMapHud.ResetForTest();

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string map = File.ReadAllText(Path.Combine(runtime, "WorldMapScreen.cs"));
            Check(map.Contains("WorldMapHud.Cards"), "월드맵이 Cards를 읽는다");
            Check(map.Contains("WorldMapHud.Line"), "자막이 Line을 읽는다");
            Check(map.Contains("WorldMapHud.SeedQaIfRequested"), "시드를 읽는다");
            Check(!map.Contains("UiPages.Grid(WorldStar.AfterPlate"),
                "옛 AfterPlate 2×2 Grid를 안 쓴다");
            Check(!map.Contains("UiPages.Grid(r, 2, 2"),
                "본문 2×2 전폭 Grid를 안 쓴다");

            Environment.SetEnvironmentVariable(WorldMapHud.EnvShow, show);
            Environment.SetEnvironmentVariable(WorldMapHud.EnvNo, no);
            if (_fail == 0) Debug.Log("[WorldMapHudSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[WorldMapHudSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[WorldMapHudSelfCheck] FAIL {_fail}건");
        }
    }
}
