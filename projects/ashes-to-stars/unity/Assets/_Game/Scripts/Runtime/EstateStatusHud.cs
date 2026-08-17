using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 영지 현황 HUD. 클래시·킹덤처럼 마을이 보이고 조작은 아래에만 둔다.
    /// 옛 길은 영공 80 + 2×2 전폭이 본문을 전부 덮었다.
    /// QA_NO면 그 옛 겹침. EstateScreen 현황 탭이 읽는다.
    /// </summary>
    public static class EstateStatusHud
    {
        public const string EnvShow = "QA_ESTATE_STATUS";
        public const string EnvNo = "QA_NO_ESTATE_STATUS";
        public const float OldAuraH = 80f;
        public const float OldAuraGap = 12f;
        public const float OldBodyH = 540f;
        public const float DockH = 88f;
        public const float DockGap = 8f;
        public const int DockCols = 5;
        public const int DockRows = 1;
        public const int CardCount = 5;

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

        public static float OverlayH(Rect body) => Blocked ? body.height : DockH;

        public static float OpenH(Rect body) =>
            Mathf.Max(0f, body.height - OverlayH(body));

        public static string Line() => Blocked
            ? "카드가 마을을 가린다"
            : "현황은 마을을 가리지 않는다(§16)";

        /// <summary>
        /// 영공·본성·세계·광산·창고 순서.
        /// 막히면 옛 영공 80 + 2×2 전폭, 아니면 아래 3×2 도크의 앞 5칸.
        /// </summary>
        public static Rect[] Cards(Rect body)
        {
            var cards = new Rect[CardCount];
            if (Blocked)
            {
                cards[0] = new Rect(body.x, body.y, body.width, OldAuraH);
                var grid = UiPages.Grid(
                    new Rect(body.x, body.y + OldAuraH + OldAuraGap,
                        body.width, Mathf.Max(40f, body.height - OldAuraH - OldAuraGap)),
                    2, 2, 16f);
                for (int i = 0; i < 4 && i < grid.Length; i++)
                    cards[i + 1] = grid[i];
                return cards;
            }

            var dock = new Rect(body.x, body.yMax - DockH, body.width, DockH);
            var slim = UiPages.Grid(dock, DockCols, DockRows, DockGap);
            for (int i = 0; i < CardCount && i < slim.Length; i++)
                cards[i] = slim[i];
            return cards;
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
