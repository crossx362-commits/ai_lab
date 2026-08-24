using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 영지 마을 HUD. 클래시·킹덤처럼 마을이 보이고 조작은 가장자리에만 둔다.
    /// 옛 길은 안내 86 + 팔레트 68 전폭 카드가 마름모 아래를 덮었다.
    /// 팔레트를 NavPlateTop-2에 붙이면 하단 금테가 내비 플레이트(636)와 4px 겹친다
    /// (실측 2026-08-24 tower_hud_nav_shots/before.png와 동형).
    /// QA_NO면 그 옛 겹침. EstateScreen 마을 탭이 읽는다.
    /// 가운데 슬림 타일은 마름모 맨 아래 오두막과 겹친다(실측 estate_hud_nav_shots/after.png).
    /// QA_NO_YARD_PALETTE_EDGE면 그 옛 가운데. 새 길은 왼쪽 가장자리.
    /// </summary>
    public static class EstateHud
    {
        public const string EnvShow = "QA_YARD_HUD";
        public const string EnvNo = "QA_NO_YARD_HUD";
        public const string EnvNoEdge = "QA_NO_YARD_PALETTE_EDGE";
        public const float OldInspectH = 86f;
        public const float OldPaletteH = 68f;
        public const float SlimPaletteH = 44f;
        public const float SlimInspectH = 36f;
        /// <summary>
        /// 가운데 팔레트도 내비 윗변에 2px로 붙이면 한 덩어리로 읽힌다
        /// (필드·월드맵·파티·탑·현황과 동형).
        /// </summary>
        public const float NavGap = 12f;
        // 방어 팔레트 타일 폭. 라벨이 「화살탑 0」처럼 3글자+개수라 폭이 좁으면
        // LabelFit(wordWrap)이 두 줄로 접고 14px 칸이 둘째 줄을 잘라 「탑·개수」가 사라진다
        // (실측 2026-08-20 go:Estate 샷: 화살탑→「화살」, 마법탑→「마법」로 잘림). 아이콘은
        // 정사각이라도 라벨을 담으려면 타일은 세로보다 가로가 넓어야 한다 — 폭을 높이와
        // 분리해 3글자 라벨이 한 줄에 들어가게 한다.
        public const float TileW = 68f;
        public const float TileGap = 6f;
        /// <summary>슬림 팔레트를 본문 왼쪽에서 띄우는 칸. 0이면 금테가 본문 테두리에 붙는다.</summary>
        public const float EdgePad = 8f;

        static bool _qaSeeded;

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool ShowQa
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvShow);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>막히면 슬림 타일이 가운데 — 마름모 아래 오두막과 겹친다.</summary>
        public static bool EdgeBlocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNoEdge);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static float PaletteH => Blocked ? OldPaletteH : SlimPaletteH;

        public static float InspectH(bool selected) =>
            Blocked ? OldInspectH : (selected ? SlimInspectH : 0f);

        public static float OverlayH(bool selected) => InspectH(selected) + PaletteH;

        /// <summary>내비 플레이트 윗변. 팔레트 아랫변이 이보다 아래면 글씨가 가린다(§16).</summary>
        public static float NavPlateTop(float screenH = 720f) =>
            UiPages.NavPlateTop(GameFlow.BottomBar.Length, 1280f, screenH);

        /// <summary>
        /// 막히면 본문 바닥에 붙고, 아니면 필드·현황과 같이 내비 플레이트 위에 둔다 —
        /// NavPlateTop-2에 붙이면 하단 금테가 팔레트에 먹힌다(실측 1280×720, 카드 yMax 640 · 플레이트 636).
        /// </summary>
        public static Rect PaletteBar(Rect body, float screenH = 720f)
        {
            float yMax = body.yMax;
            if (!Blocked)
                yMax = Mathf.Min(body.yMax, NavPlateTop(screenH) - NavGap);
            return new Rect(body.x, yMax - PaletteH, body.width, PaletteH);
        }

        public static bool ShowInspectBar(bool selected) => Blocked || selected;

        /// <summary>
        /// 미선택 침략 줄. 옛 길은 Hint 22px라 마을 그림 위에 글씨만 얹혀
        /// 「침략 북 3칸」이 배경에 묻혔다(실측 2026-08-24, estate_hub_shots).
        /// 새 길은 금테 칩(InfoAt) 36px. QA_NO면 옛 22.
        /// </summary>
        public const float ChipH = 36f;
        public const float ChipW = 520f;
        public const float OldChipH = 22f;

        public static Rect ChipRect(Rect body)
        {
            float h = Blocked ? OldChipH : ChipH;
            float w = Mathf.Min(ChipW, body.width * 0.55f);
            return new Rect(body.x, body.y + UiPages.TabH + 8f, w, h);
        }

        public static string Line()
        {
            if (Blocked) return "안내·팔레트가 마을을 가린다";
            if (EdgeBlocked) return "팔레트가 마름모 아래 가운데와 겹친다";
            return "HUD는 마을을 가리지 않는다 — 침략 줄은 금테 칩 · 팔레트는 왼쪽 가장자리·내비 위(§16)";
        }

        /// <summary>막히면 전폭 카드, 아니면 가운데 아이콘 도크.</summary>
        public static Rect[] PaletteTiles(Rect r, int count)
        {
            if (count < 1) count = 1;
            var tiles = new Rect[count];
            if (Blocked)
            {
                float bw = (r.width - 8f) / count;
                for (int i = 0; i < count; i++)
                    tiles[i] = new Rect(r.x + i * bw, r.y + 4f, bw - 6f, r.height - 6f);
                return tiles;
            }

            // 폭은 라벨 기준(TileW), 높이만 팔레트 바를 따른다 — 둘을 묶으면(옛 min(TileW,height+8))
            // 44px 바에서 폭이 52로 눌려 3글자 라벨이 잘렸다.
            float tw = TileW;
            float th = Mathf.Max(28f, r.height - 4f);
            float used = count * tw + (count - 1) * TileGap;
            // 가운데면 마름모 남단 오두막과 겹친다(실측 estate_hud_nav_shots/after.png).
            // 왼쪽 가장자리는 빈 잔디 위라 건물을 안 가린다. QA_NO_YARD_PALETTE_EDGE면 옛 가운데.
            float x0 = EdgeBlocked
                ? r.x + (r.width - used) * 0.5f
                : r.x + EdgePad;
            for (int i = 0; i < count; i++)
                tiles[i] = new Rect(x0 + i * (tw + TileGap), r.y + 2f, tw, th);
            return tiles;
        }

        public static float PaletteUsedW(Rect r, int count)
        {
            var tiles = PaletteTiles(r, count);
            return tiles[count - 1].xMax - tiles[0].x;
        }

        public static void SeedQaIfRequested()
        {
            if (!ShowQa) return;
            if (Blocked) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            StarterSecond.ResetForTest();
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
        }
    }
}
