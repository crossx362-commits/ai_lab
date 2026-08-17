using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 영지 마을 HUD. 클래시·킹덤처럼 마을이 보이고 조작은 가장자리에만 둔다.
    /// 옛 길은 안내 86 + 팔레트 68 전폭 카드가 마름모 아래를 덮었다.
    /// QA_NO면 그 옛 겹침. EstateScreen 마을 탭이 읽는다.
    /// </summary>
    public static class EstateHud
    {
        public const string EnvShow = "QA_YARD_HUD";
        public const string EnvNo = "QA_NO_YARD_HUD";
        public const float OldInspectH = 86f;
        public const float OldPaletteH = 68f;
        public const float SlimPaletteH = 44f;
        public const float SlimInspectH = 36f;
        public const float TileW = 56f;
        public const float TileGap = 6f;

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

        public static float PaletteH => Blocked ? OldPaletteH : SlimPaletteH;

        public static float InspectH(bool selected) =>
            Blocked ? OldInspectH : (selected ? SlimInspectH : 0f);

        public static float OverlayH(bool selected) => InspectH(selected) + PaletteH;

        public static bool ShowInspectBar(bool selected) => Blocked || selected;

        public static string Line() => Blocked
            ? "안내·팔레트가 마을을 가린다"
            : "HUD는 마을을 가리지 않는다(§16)";

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

            float tw = Mathf.Min(TileW, Mathf.Max(36f, r.height + 8f));
            float th = Mathf.Max(28f, r.height - 4f);
            float used = count * tw + (count - 1) * TileGap;
            float x0 = r.x + (r.width - used) * 0.5f;
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
