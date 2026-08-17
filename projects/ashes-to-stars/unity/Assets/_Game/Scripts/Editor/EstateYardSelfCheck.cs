using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 영지 마을이 화면을 채우고 집 프랍을 읽는다. QA_NO면 옛 88 상한(§16).
    /// </summary>
    public static class EstateYardSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Estate Yard Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string no = Environment.GetEnvironmentVariable(EstateYard.EnvNo);
            string noPan = Environment.GetEnvironmentVariable(EstateYard.EnvNoPan);
            Environment.SetEnvironmentVariable(EstateYard.EnvNo, null);
            Environment.SetEnvironmentVariable(EstateYard.EnvNoPan, null);
            EstateYard.ResetForTest();

            var body = new Rect(GameScreen.BodyPadX, GameScreen.BodyTop,
                1280f - GameScreen.BodyPadX * 2f,
                720f - GameScreen.BodyTop - UiPages.NavReserve);
            var yard = EstateYard.VillageRect(body);
            float tw = EstateYard.TileW(yard);
            float diamond = tw * EstateGrid.Size;
            Check(yard.height >= 500f, $"전면 높이 {yard.height:0}");
            Check(tw > 100f, $"칸 {tw:0.0} > 100 (옛 상한 88)");
            Check(diamond >= 900f, $"마름모 폭 {diamond:0}");
            Check(EstateYard.Line().Contains("끌어 본다"),
                $"줄 (실제 {EstateYard.Line()})");

            Check(EstateYard.PropOf(EstateGrid.Cell.Keep) == EstateBuildings.Keep, "본성=전용");
            Check(EstateYard.PropOf(EstateGrid.Cell.Warehouse) == EstateBuildings.Warehouse, "창고=전용");
            Check(EstateYard.PropOf(EstateGrid.Cell.Mine) == EstateBuildings.Mine, "광산=전용");
            Check(EstateYard.PropOf(EstateGrid.Cell.Smith) == EstateBuildings.PropOf(EstateGrid.Cell.Smith),
                "대장간=EstateBuildings");
            Check(EstateYard.PropOf(EstateGrid.Cell.Wall) == "village_fence_0", "성벽=울타리");
            Check(Resources.Load<Texture2D>("props/" + EstateYard.PropOf(EstateGrid.Cell.Keep)) != null,
                "본성 프랍이 Resources에 있다");
            Check(Resources.Load<Texture2D>("props/" + EstateYard.PropOf(EstateGrid.Cell.Mine)) != null,
                "광산 프랍이 Resources에 있다");
            Check(Resources.Load<Texture2D>("props/" + EstateYard.PropOf(EstateGrid.Cell.Wall)) != null,
                "성벽 프랍이 Resources에 있다");

            Environment.SetEnvironmentVariable(EstateYard.EnvNo, "1");
            var page = UiPages.AfterTabs(body);
            var oldYard = EstateYard.VillageRect(page);
            float oldTw = EstateYard.TileW(oldYard);
            Check(oldTw <= EstateYard.OldTileCap, $"차단 칸 {oldTw:0.0} ≤ 88");
            Check(oldYard.height < yard.height, $"차단 높이 {oldYard.height:0} < 전면 {yard.height:0}");
            Check(EstateYard.Line().Contains("빈 칸에 놓는다"),
                $"차단 줄 (실제 {EstateYard.Line()})");
            Environment.SetEnvironmentVariable(EstateYard.EnvNo, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string estate = File.ReadAllText(Path.Combine(runtime, "EstateScreen.cs"));
            string yardSrc = File.ReadAllText(Path.Combine(runtime, "EstateYard.cs"));
            Check(estate.Contains("EstateYard.VillageRect"), "영지가 VillageRect를 읽는다");
            Check(estate.Contains("EstateYard.PropOf") || yardSrc.Contains("PropTex(PropOf"),
                "마을이 집 프랍을 그린다");
            Check(estate.Contains("DrawVillage(r)") && estate.Contains("FillBlocked"),
                "마을 탭이 본문을 채운다");
            Check(estate.Contains("EstateYard.SeedQaIfRequested"), "영지가 끌어 보기 시드를 읽는다");

            Environment.SetEnvironmentVariable(EstateYard.EnvNo, no);
            Environment.SetEnvironmentVariable(EstateYard.EnvNoPan, noPan);
            EstateYard.ResetForTest();
            if (_fail == 0) Debug.Log("[EstateYardSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[EstateYardSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[EstateYardSelfCheck] FAIL {_fail}건");
        }
    }
}
