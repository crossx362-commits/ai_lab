using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 월드맵 HUD. 클래시·킹덤처럼 별이 보이고 조작은 아래에만 둔다.
    /// 옛 길은 2×2 전폭 카드가 AfterPlate(~456)를 전부 덮었다.
    /// QA_NO면 그 옛 겹침. WorldMapScreen이 읽는다.
    /// </summary>
    public static class WorldMapHud
    {
        public const string EnvShow = "QA_WORLD_HUD";
        public const string EnvNo = "QA_NO_WORLD_HUD";
        public const float OldBodyH = 540f;
        public const float DockH = 200f;
        public const float DockGap = 10f;
        /// <summary>
        /// 전폭 2×2 카드는 좌우가 내비 옆으로 빠진다.
        /// 2px면 금테가 내비 윗변에 붙어 한 덩어리로 읽힌다(실측 2026-08-24 worldmap_hud_nav_shots/before.png).
        /// </summary>
        public const float NavGap = 12f;
        public const int DockCols = 2;
        public const int DockRows = 2;
        public const int OldCols = 2;
        public const int OldRows = 2;
        public const int CardCount = 4;

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

        public static float OverlayH(Rect body) => Blocked
            ? WorldStar.AfterPlate(body).height
            : DockH;

        public static float OpenH(Rect body) =>
            Mathf.Max(0f, body.height - OverlayH(body));

        public static string Line() => Blocked
            ? "카드가 월드맵을 가린다"
            : "HUD는 월드맵을 가리지 않는다 — 도크는 내비 위(§16)";

        /// <summary>내비 플레이트 윗변. 도크 아랫변이 이보다 아래면 금테가 먹힌다(§16).</summary>
        public static float NavPlateTop(float screenH = 720f) =>
            UiPages.NavPlateTop(GameFlow.BottomBar.Length, 1280f, screenH);

        /// <summary>
        /// 새 길 도크 상자. 영지 PaletteBar·필드 도크와 같이 내비 플레이트 위에 둔다 —
        /// body.yMax에 붙이면 하단 금테가 도크에 먹힌다(실측 1280×720, 카드 yMax 640 · 플레이트 636).
        /// </summary>
        public static Rect Dock(Rect body, float screenH = 720f)
        {
            float yMax = Mathf.Min(body.yMax, NavPlateTop(screenH) - NavGap);
            return new Rect(body.x, yMax - DockH, body.width, DockH);
        }

        /// <summary>
        /// 성계 이동 · 침략 · 랭킹 · 수비대 순서.
        /// 막히면 옛 AfterPlate 2×2 전폭, 아니면 아래 2×2 도크.
        /// </summary>
        public static Rect[] Cards(Rect body, float screenH = 720f)
        {
            if (Blocked)
                return UiPages.Grid(WorldStar.AfterPlate(body), OldCols, OldRows, 16f);
            return UiPages.Grid(Dock(body, screenH), DockCols, DockRows, DockGap);
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
