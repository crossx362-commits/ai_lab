using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 경매장 안내 막대. 클래시·킹덤처럼 배경이 보이고 안내는 가장자리만 쓴다.
    /// 옛 길은 Info 2줄이 본문 전폭을 덮었다. QA_NO면 그 옛 전폭.
    /// EstateScreen 경매장이 읽는다.
    /// </summary>
    public static class AuctionHud
    {
        public const string EnvShow = "QA_AUCTION_HUD";
        public const string EnvNo = "QA_NO_AUCTION_HUD";
        public const float OldBarH = 64f;
        public const float OldGap = 12f;
        public const float SlimH = 36f;
        public const float SlimGap = 8f;
        public const float SlimW = 580f;
        public const float LotPad = 8f;

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

        public static float BarH => Blocked ? OldBarH : SlimH;

        public static float Gap => Blocked ? OldGap : SlimGap;

        public static float BarW(Rect body) => Blocked ? body.width + 24f : SlimW;

        public static float OverlayH(int lines)
        {
            if (lines < 1) return 0f;
            return lines * BarH + (lines - 1) * Gap;
        }

        public static string Line() => Blocked
            ? "안내 막대가 경매를 가린다"
            : "HUD는 경매 배경을 가리지 않는다(§16)";

        public static string StatusLine()
        {
            long copper = GameState.Wallet.Copper;
            string money = copper >= Economy.COPPER_PER_GOLD
                ? $"{copper / Economy.COPPER_PER_GOLD}골드"
                : Economy.FormatCurrency(copper);
            return $"{money} · {AuctionState.MineCount}/{AuctionState.MaxMine}";
        }

        /// <summary>막히면 전폭 Info와 같은 칸, 아니면 왼쪽 슬림 도크.</summary>
        public static Rect BarRect(Rect r, int index)
        {
            float h = BarH;
            float w = BarW(r);
            float x = Blocked ? r.x - 12f : r.x;
            return new Rect(x, r.y + index * (h + Gap), w, h);
        }

        public static Rect LotsBody(Rect r, int infoLines)
        {
            float top = OverlayH(infoLines);
            if (top <= 0f) return r;
            float y = r.y + top + LotPad;
            float h = r.yMax - y;
            if (h < 40f) h = 40f;
            return new Rect(r.x, y, r.width, h);
        }

        public static void SeedQaIfRequested()
        {
            if (!ShowQa) return;
            if (Blocked) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            RacePrefs.Set(RaceId.인간);
            GameState.SetTowerFloorForTest(EstateScreen.AuctionUnlockFloor);
            AuctionState.SetOpenedAtForTest(
                DateTimeOffset.UtcNow.ToUnixTimeSeconds() - AuctionState.BuyLockSeconds - 1);
            if (GameState.Wallet.Copper < 50_000)
                GameState.Grant(50_000);
            StarterSecond.ResetForTest();
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
        }
    }
}
