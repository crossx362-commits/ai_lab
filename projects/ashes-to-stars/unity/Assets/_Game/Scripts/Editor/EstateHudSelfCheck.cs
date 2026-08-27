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
            string noEdge = Environment.GetEnvironmentVariable(EstateHud.EnvNoEdge);
            string noReadable = Environment.GetEnvironmentVariable(EstateHud.EnvNoReadablePalette);
            string noContrast = Environment.GetEnvironmentVariable(EstateHud.EnvNoPaletteContrast);
            string noBrightLabel = Environment.GetEnvironmentVariable(EstateHud.EnvNoPaletteBrightLabel);
            string noChipCompact = Environment.GetEnvironmentVariable(EstateHud.EnvNoChipCompact);
            string noHeaderPlayerCopy = Environment.GetEnvironmentVariable(EstateScreen.EnvNoHeaderPlayerCopy);
            Environment.SetEnvironmentVariable(EstateHud.EnvShow, null);
            Environment.SetEnvironmentVariable(EstateHud.EnvNo, null);
            Environment.SetEnvironmentVariable(EstateHud.EnvNoEdge, null);
            Environment.SetEnvironmentVariable(EstateHud.EnvNoReadablePalette, null);
            Environment.SetEnvironmentVariable(EstateHud.EnvNoPaletteContrast, null);
            Environment.SetEnvironmentVariable(EstateHud.EnvNoPaletteBrightLabel, null);
            Environment.SetEnvironmentVariable(EstateHud.EnvNoChipCompact, null);
            Environment.SetEnvironmentVariable(EstateScreen.EnvNoHeaderPlayerCopy, null);
            EstateHud.ResetForTest();

            string qaHeader = EstateScreen.PlayerSubtitle(EstateHud.Line());
            Check(!qaHeader.Contains("§") && qaHeader == "침략에 대비해 방어 시설을 배치하고 영지를 확장하세요",
                $"영지 QA 헤더도 플레이어 문구만 표시 (실제 {qaHeader})");
            Check(EstateScreen.PlayerSubtitle("본성 상한(§13-2) · 자동 적용") == "본성 상한 · 자동 적용",
                "영지 하위 화면 헤더의 내부 절 번호도 제거");
            Environment.SetEnvironmentVariable(EstateScreen.EnvNoHeaderPlayerCopy, "1");
            Check(EstateScreen.PlayerSubtitle("HUD는 마을을 가리지 않는다(§16)").Contains("§16"),
                "네거티브는 옛 내부 절 번호 노출");
            Environment.SetEnvironmentVariable(EstateScreen.EnvNoHeaderPlayerCopy, null);

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
            Check(EstateHud.Line().Contains("침략에 대비"),
                $"줄 (실제 {EstateHud.Line()})");
            Check(!EstateHud.Line().Contains("HUD") && !EstateHud.Line().Contains("팔레트"),
                $"플레이어 문구에 개발 용어 없음 (실제 {EstateHud.Line()})");
            Check(EstateHud.PaletteTint(false).a >= 0.90f,
                $"비선택 팔레트 알파 {EstateHud.PaletteTint(false).a:0.00} ≥ 0.90 — 잔디 위 대비");
            Check(Mathf.Approximately(EstateHud.PaletteTint(true).a, 1f),
                "선택 팔레트는 불투명");
            Check(!EstateHud.BrightLabelBlocked, "방어 도크는 밝은 굵은 라벨");
            Environment.SetEnvironmentVariable(EstateHud.EnvNoPaletteBrightLabel, "1");
            Check(EstateHud.BrightLabelBlocked, "네거티브는 옛 흐린 Hint 라벨");
            Environment.SetEnvironmentVariable(EstateHud.EnvNoPaletteBrightLabel, null);
            Environment.SetEnvironmentVariable(EstateHud.EnvNoPaletteContrast, "1");
            Check(Mathf.Approximately(EstateHud.PaletteTint(false).a, 0.72f),
                "네거티브는 옛 흐린 알파 0.72");
            Environment.SetEnvironmentVariable(EstateHud.EnvNoPaletteContrast, null);

            var body = new Rect(36f, 52f, 1208f, 720f - 52f - UiPages.NavReserve);
            var chip = EstateHud.ChipRect(body);
            Check(Mathf.Approximately(chip.height, EstateHud.ChipH),
                $"칩 높이 {chip.height:0} = {EstateHud.ChipH:0}");
            Check(chip.width <= EstateHud.ChipW + 0.01f,
                $"칩 폭 {chip.width:0} ≤ {EstateHud.ChipW:0}");
            Check(chip.xMax <= 460f,
                $"칩 오른쪽 {chip.xMax:0} ≤ 460 — 높은 본성 탑 지붕을 덮지 않는다");
            Check(chip.y >= body.y + UiPages.TabH - 0.01f,
                $"칩 y {chip.y:0} ≥ 탭 아래 {body.y + UiPages.TabH:0}");
            Check(chip.x >= body.x - 0.01f && chip.xMax <= body.xMax + 0.01f,
                $"칩 x {chip.x:0}~{chip.xMax:0} 본문 안");
            Environment.SetEnvironmentVariable(EstateHud.EnvNoChipCompact, "1");
            var oldWideChip = EstateHud.ChipRect(body);
            Check(oldWideChip.width >= EstateHud.OldChipW - 0.01f,
                $"네거티브 칩 폭 {oldWideChip.width:0} = 옛 {EstateHud.OldChipW:0}");
            Check(oldWideChip.xMax > 500f,
                $"네거티브 칩 오른쪽 {oldWideChip.xMax:0} > 500 — 높은 탑 영역을 덮는다");
            Environment.SetEnvironmentVariable(EstateHud.EnvNoChipCompact, null);

            var slim = EstateHud.PaletteTiles(bar, n);
            Check(slim.Length == n, $"도크 {n}칸");
            Check(Mathf.Approximately(slim[0].width, EstateHud.TileW),
                $"읽기 좋은 도크 칸 폭 {slim[0].width:0.0} = {EstateHud.TileW}");
            float used = EstateHud.PaletteUsedW(bar, n);
            Check(used < 480f && used < bar.width * 0.40f,
                $"도크 폭 {used:0} < 480·전폭의 40%");
            Check(Mathf.Abs(slim[0].x - (bar.x + EstateHud.EdgePad)) < 0.01f,
                $"도크 x {slim[0].x:0} = 왼쪽 가장자리 {bar.x + EstateHud.EdgePad:0}");
            Check(slim[n - 1].xMax < bar.center.x - 0.01f,
                $"마지막 칸 {slim[n - 1].xMax:0} < 가운데 {bar.center.x:0} — 오두막과 안 겹친다");
            Check(Mathf.Approximately(slim[0].x, bar.x + EstateHud.EdgePad),
                "팔레트 가장자리 배치는 자막이 아니라 좌표로 검증");
            Environment.SetEnvironmentVariable(EstateHud.EnvNoReadablePalette, "1");
            var cramped = EstateHud.PaletteTiles(bar, n);
            Check(Mathf.Approximately(cramped[0].width, EstateHud.OldTileW),
                $"네거티브 좁은 칸 {cramped[0].width:0} = 옛 {EstateHud.OldTileW:0}");
            Check(EstateHud.PaletteUsedW(bar, n) < used,
                "네거티브는 이름 칸이 더 좁다");
            Environment.SetEnvironmentVariable(EstateHud.EnvNoReadablePalette, null);

            var pal = EstateHud.PaletteBar(body);
            float navTop = EstateHud.NavPlateTop();
            Check(pal.yMax <= navTop - EstateHud.NavGap + 0.01f,
                $"팔레트 아랫변 {pal.yMax:0} ≤ 내비-간격 {navTop - EstateHud.NavGap:0}");
            Check(navTop - pal.yMax >= 10f,
                $"팔레트-내비 간격 {navTop - pal.yMax:0} ≥ 10 (전폭 카드가 내비와 한 덩어리가 되지 않게)");
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
            var oldPal = EstateHud.PaletteBar(body);
            Check(oldPal.yMax > EstateHud.NavPlateTop() - 1f,
                $"차단 아랫변 {oldPal.yMax:0} 이 내비와 겹친다");
            Environment.SetEnvironmentVariable(EstateHud.EnvNo, null);

            Environment.SetEnvironmentVariable(EstateHud.EnvNoEdge, "1");
            Check(EstateHud.EdgeBlocked, "QA_NO_YARD_PALETTE_EDGE면 가운데 차단");
            Check(!EstateHud.Blocked, "가장자리 차단은 전폭 HUD 차단이 아니다");
            var oldEdge = EstateHud.PaletteTiles(bar, n);
            Check(oldEdge[0].x > bar.x + 200f, $"차단 도크 x {oldEdge[0].x:0} 옛 가운데");
            Check(oldEdge[n - 1].xMax > bar.center.x,
                $"차단 마지막 칸 {oldEdge[n - 1].xMax:0} 이 가운데를 넘는다");
            Check(EstateHud.Line().Contains("가운데"),
                $"차단 줄 (실제 {EstateHud.Line()})");
            Check(EstateHud.Line().Contains("겹친다"),
                $"차단 줄에 겹침 (실제 {EstateHud.Line()})");
            Environment.SetEnvironmentVariable(EstateHud.EnvNoEdge, null);

            Environment.SetEnvironmentVariable(EstateHud.EnvShow, "1");
            EstateHud.SeedQaIfRequested();
            Check(EstateHud.ShowQa, "시드 켜짐");
            Check(EstateHud.Line().Contains("침략에 대비"), "시드 줄");
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

            int inspect = estate.IndexOf("void DrawVillageInspect", StringComparison.Ordinal);
            int openHub = estate.IndexOf("void OpenHub", StringComparison.Ordinal);
            Check(inspect >= 0 && openHub > inspect, "DrawVillageInspect가 있다");
            string inspectSrc = (inspect >= 0 && openHub > inspect)
                ? estate.Substring(inspect, openHub - inspect) : "";
            Check(inspectSrc.IndexOf("EventType.MouseDown", StringComparison.Ordinal) >= 0
                  && inspectSrc.IndexOf("btn.Contains", StringComparison.Ordinal) >= 0
                  && inspectSrc.IndexOf("TryPickUp", StringComparison.Ordinal) >= 0,
                "회수 버튼 클릭은 MouseDown으로 거둔다");
            Check(inspectSrc.IndexOf("GUI.Button(btn", StringComparison.Ordinal) < 0,
                "회수 버튼이 GUI.Button(none)을 안 쓴다");

            int layout = estate.IndexOf("void DrawLayout", StringComparison.Ordinal);
            int fillFn = estate.IndexOf("static void FillCell", StringComparison.Ordinal);
            Check(layout >= 0 && fillFn > layout, "DrawLayout가 있다");
            string layoutSrc = (layout >= 0 && fillFn > layout)
                ? estate.Substring(layout, fillFn - layout) : "";
            Check(layoutSrc.IndexOf("EventType.MouseDown", StringComparison.Ordinal) >= 0
                  && layoutSrc.IndexOf("box.Contains", StringComparison.Ordinal) >= 0
                  && layoutSrc.IndexOf("TryPlace", StringComparison.Ordinal) >= 0,
                "격자 칸 클릭은 MouseDown으로 선택/배치한다");
            Check(layoutSrc.IndexOf("GUI.Button(box", StringComparison.Ordinal) < 0,
                "격자 칸이 GUI.Button(none)을 안 쓴다");
            Check(estate.Contains("EstateHud.Line"), "자막이 Line을 읽는다");
            Check(estate.Contains("PlayerSubtitle(_sub switch"),
                "영지의 모든 헤더 자막이 PlayerSubtitle을 거친다");
            int dv = estate.IndexOf("void DrawVillage", StringComparison.Ordinal);
            int dvEnd = estate.IndexOf("void HandleBuildingDrag", dv, StringComparison.Ordinal);
            Check(dv >= 0 && dvEnd > dv, "DrawVillage 블록을 찾는다");
            string block = dv >= 0 && dvEnd > dv ? estate.Substring(dv, dvEnd - dv) : "";
            Check(block.Contains("EstateHud.ChipRect"),
                "마을 침략 줄이 ChipRect를 읽는다 (옛 인라인 Hint 22px 금지)");
            Check(estate.Contains("EstateHud.PaletteTint"),
                "방어 팔레트가 잔디 대비 PaletteTint를 읽는다");
            Check(estate.Contains("EstateHud.BrightLabelBlocked") && estate.Contains("DockLabel(label"),
                "방어 팔레트가 밝은 굵은 도크 라벨을 읽는다");
            Check(block.Contains("InfoAt(chip"),
                "새 길은 InfoAt 금테 — Hint면 글씨가 마을에 묻힌다");
            Check(block.Contains("EstateHud.Blocked") && block.Contains("Hint(chip"),
                "QA_NO면 옛 Hint 경로");
            string hud = File.ReadAllText(Path.Combine(runtime, "EstateHud.cs"));
            Check(hud.Contains("NavPlateTop"),
                "팔레트가 NavPlateTop을 읽는다 (body.yMax 붙이기 금지)");
            Check(hud.Contains("NavGap"),
                "팔레트가 NavGap을 읽는다");
            Check(hud.Contains("EnvNoEdge") && hud.Contains("EdgePad"),
                "팔레트가 가장자리 차단·여백을 읽는다");
            Check(hud.Contains("EdgeBlocked"),
                "타일이 EdgeBlocked를 읽는다 (가운데 붙이기 금지)");

            Environment.SetEnvironmentVariable(EstateHud.EnvShow, show);
            Environment.SetEnvironmentVariable(EstateHud.EnvNo, no);
            Environment.SetEnvironmentVariable(EstateHud.EnvNoEdge, noEdge);
            Environment.SetEnvironmentVariable(EstateHud.EnvNoReadablePalette, noReadable);
            Environment.SetEnvironmentVariable(EstateHud.EnvNoPaletteContrast, noContrast);
            Environment.SetEnvironmentVariable(EstateHud.EnvNoPaletteBrightLabel, noBrightLabel);
            Environment.SetEnvironmentVariable(EstateHud.EnvNoChipCompact, noChipCompact);
            Environment.SetEnvironmentVariable(EstateScreen.EnvNoHeaderPlayerCopy, noHeaderPlayerCopy);
            if (_fail == 0) Debug.Log("[EstateHudSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[EstateHudSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[EstateHudSelfCheck] FAIL {_fail}건");
        }
    }
}
