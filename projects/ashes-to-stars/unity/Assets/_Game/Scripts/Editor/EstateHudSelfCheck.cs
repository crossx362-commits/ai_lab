using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>영지 마을 HUD가 마름모를 덜 가린다. QA_NO면 옛 전폭 카드(§16).</summary>
    public static class EstateHudSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Estate Hud Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(EstateHud.EnvShow);
            string no = Environment.GetEnvironmentVariable(EstateHud.EnvNo);
            Environment.SetEnvironmentVariable(EstateHud.EnvShow, null);
            Environment.SetEnvironmentVariable(EstateHud.EnvNo, null);
            EstateHud.ResetForTest();

            int n = EstateDefense.All.Length;
            var bar = new Rect(36f, 596f, 1208f, EstateHud.SlimPaletteH);
            Check(Mathf.Approximately(EstateHud.OverlayH(false), EstateHud.SlimPaletteH),
                $"선택 없음 겹침 {EstateHud.OverlayH(false):0} = 팔레트 {EstateHud.SlimPaletteH:0}");
            Check(Mathf.Approximately(EstateHud.OverlayH(true),
                    EstateHud.SlimPaletteH + EstateHud.SlimInspectH),
                $"선택 겹침 {EstateHud.OverlayH(true):0} = {EstateHud.SlimPaletteH + EstateHud.SlimInspectH:0}");
            Check(EstateHud.OverlayH(false) < 80f, "선택 없음 겹침 < 80 (옛 154)");
            Check(EstateHud.OverlayH(true) < 100f, "선택 겹침 < 100 (옛 154)");
            Check(!EstateHud.ShowInspectBar(false), "선택 없으면 안내 막대 없음");
            Check(EstateHud.ShowInspectBar(true), "선택하면 안내");
            Check(EstateHud.Line().Contains("가리지 않는다"),
                $"줄 (실제 {EstateHud.Line()})");
            Check(EstateHud.Line().Contains("금테 칩"),
                $"줄에 칩 (실제 {EstateHud.Line()})");

            var body = new Rect(36f, 52f, 1208f, 720f - 52f - UiPages.NavReserve);
            var chip = EstateHud.ChipRect(body);
            Check(Mathf.Approximately(chip.height, EstateHud.ChipH),
                $"칩 높이 {chip.height:0} = {EstateHud.ChipH:0}");
            Check(chip.width <= EstateHud.ChipW + 0.01f,
                $"칩 폭 {chip.width:0} ≤ {EstateHud.ChipW:0}");
            Check(chip.y >= body.y + UiPages.TabH - 0.01f,
                $"칩 y {chip.y:0} ≥ 탭 아래 {body.y + UiPages.TabH:0}");
            Check(chip.x >= body.x - 0.01f && chip.xMax <= body.xMax + 0.01f,
                $"칩 x {chip.x:0}~{chip.xMax:0} 본문 안");

            var slim = EstateHud.PaletteTiles(bar, n);
            Check(slim.Length == n, $"도크 {n}칸");
            Check(slim[0].width <= EstateHud.TileW + 0.01f,
                $"도크 칸 폭 {slim[0].width:0.0} ≤ {EstateHud.TileW}");
            float used = EstateHud.PaletteUsedW(bar, n);
            Check(used < 400f && used < bar.width * 0.40f,
                $"도크 폭 {used:0} < 전폭의 40%");
            Check(slim[0].x > bar.x + 200f, "도크는 가운데");

            var pal = EstateHud.PaletteBar(body);
            Check(pal.yMax < EstateHud.NavPlateTop(),
                $"팔레트 아랫변 {pal.yMax:0} < 내비 {EstateHud.NavPlateTop():0}");
            Check(chip.yMax < pal.y - 0.01f,
                $"칩 바닥 {chip.yMax:0} < 팔레트 {pal.y:0} — 침략 줄이 방어 도크와 안 겹친다");

            Environment.SetEnvironmentVariable(EstateHud.EnvNo, "1");
            Check(EstateHud.Blocked, "QA_NO면 차단");
            Check(Mathf.Approximately(EstateHud.OverlayH(false),
                    EstateHud.OldInspectH + EstateHud.OldPaletteH),
                $"차단 겹침 {EstateHud.OverlayH(false):0} = 옛 154");
            Check(EstateHud.ShowInspectBar(false), "차단하면 안내가 항상 있다");
            var old = EstateHud.PaletteTiles(bar, n);
            Check(old[0].width > 200f, $"차단 칸 {old[0].width:0} 전폭 카드");
            Check(EstateHud.Line().Contains("가린다"),
                $"차단 줄 (실제 {EstateHud.Line()})");
            var oldChip = EstateHud.ChipRect(body);
            Check(oldChip.height <= EstateHud.OldChipH + 0.01f,
                $"차단 칩 {oldChip.height:0} ≤ 옛 {EstateHud.OldChipH:0} — 옛 Hint 높이");
            Environment.SetEnvironmentVariable(EstateHud.EnvNo, null);

            Environment.SetEnvironmentVariable(EstateHud.EnvShow, "1");
            EstateHud.SeedQaIfRequested();
            Check(EstateHud.ShowQa, "시드 켜짐");
            Check(EstateHud.Line().Contains("가리지 않는다"), "시드 줄");
            Environment.SetEnvironmentVariable(EstateHud.EnvShow, null);
            EstateHud.ResetForTest();

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string estate = File.ReadAllText(Path.Combine(runtime, "EstateScreen.cs"));
            Check(estate.Contains("EstateHud.OverlayH") || estate.Contains("EstateHud.PaletteH")
                  || estate.Contains("EstateHud.PaletteBar"),
                "영지가 Overlay/PaletteH를 읽는다");
            Check(estate.Contains("EstateHud.PaletteBar"), "팔레트가 PaletteBar를 읽는다");
            Check(estate.Contains("EstateHud.PaletteTiles"), "팔레트가 PaletteTiles를 읽는다");
            Check(estate.Contains("EstateHud.ShowInspectBar"), "안내가 ShowInspectBar를 읽는다");
            Check(estate.Contains("EstateHud.Line"), "자막이 Line을 읽는다");
            int dv = estate.IndexOf("void DrawVillage", StringComparison.Ordinal);
            int dvEnd = estate.IndexOf("void HandleBuildingDrag", dv, StringComparison.Ordinal);
            Check(dv >= 0 && dvEnd > dv, "DrawVillage 블록을 찾는다");
            string block = dv >= 0 && dvEnd > dv ? estate.Substring(dv, dvEnd - dv) : "";
            Check(block.Contains("EstateHud.ChipRect"),
                "마을 침략 줄이 ChipRect를 읽는다 (옛 인라인 Hint 22px 금지)");
            Check(block.Contains("InfoAt(chip"),
                "새 길은 InfoAt 금테 — Hint면 글씨가 마을에 묻힌다");
            Check(block.Contains("EstateHud.Blocked") && block.Contains("Hint(chip"),
                "QA_NO면 옛 Hint 경로");

            Environment.SetEnvironmentVariable(EstateHud.EnvShow, show);
            Environment.SetEnvironmentVariable(EstateHud.EnvNo, no);
            if (_fail == 0) Debug.Log("[EstateHudSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[EstateHudSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[EstateHudSelfCheck] FAIL {_fail}건");
        }
    }
}
