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
            : "현황 도크 부제가 잘리지 않는다(§16)";

        /// <summary>도크 한 칸에 들어가는 짧은 부제. 긴 순자산·압류 줄은 건물 안에서 본다.</summary>
        public const int CaptionMaxRunes = 16;

        public static string AuraCaption()
        {
            int floor = GameState.TowerFloor;
            return $"{floor}층 · 영공 {WorldStar.Sense(floor):0.0}";
        }

        /// <summary>옛 줄은 FormatCurrency 풀표기라 슬림 도크에서 잘렸다.</summary>
        public static string OldKeepCaption()
        {
            if (EstateBuild.KeepBusy)
                return $"Lv{EstateBuild.KeepLevel} · {EstateBuild.RemainingText()}";
            return $"Lv{EstateBuild.KeepLevel} · {Economy.FormatCurrency(EstateBuild.WarehouseCapCopper())}";
        }

        public static string KeepCaption()
        {
            if (EstateBuild.KeepBusy)
                return $"Lv{EstateBuild.KeepLevel} · {EstateBuild.RemainingText()}";
            return $"Lv{EstateBuild.KeepLevel} · {ShortCopper(EstateBuild.WarehouseCapCopper())}";
        }

        public static string WorldCaption() =>
            $"해금 T{GameState.UnlockedTier + 1} · {GameState.TowerFloor}층";

        public static string MineCaption()
        {
            if (EstateMine.Seized) return "생산 압류";
            return Economy.FormatCurrency(EstateMine.CopperPerHourEffective()) + "/h";
        }

        public static string StoreCaption() =>
            $"{ShortCopper(GameState.Wallet.Copper)} / {ShortCopper(EstateBuild.WarehouseCapCopper())}";

        static string ShortCopper(long n)
        {
            if (n >= 10000) return $"{n / 10000}골드";
            if (n >= 100) return $"{n / 100}실버";
            return $"{n}쿠퍼";
        }

        public static bool CaptionFits(string text)
        {
            if (string.IsNullOrEmpty(text)) return true;
            int n = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (!char.IsLowSurrogate(text[i])) n++;
            }
            return n <= CaptionMaxRunes;
        }

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
