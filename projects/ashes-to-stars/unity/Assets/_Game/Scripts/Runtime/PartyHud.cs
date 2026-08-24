using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 파티 출전 탭 HUD. 필드·월드맵과 같이 조작은 내비 위에만 둔다.
    /// 옛 길은 AfterTabs 2×2 전폭이 본문 yMax=640에 붙어 내비 플레이트(636)와 겹쳤다.
    /// QA_NO면 그 옛 겹침. PartyScreen 출전 페이지가 읽는다.
    /// </summary>
    public static class PartyHud
    {
        public const string EnvShow = "QA_PARTY_SORTIE";
        public const string EnvNo = "QA_NO_PARTY_SORTIE";
        public const float DockH = 200f;
        public const float DockGap = 10f;
        /// <summary>
        /// 전폭 2×2 카드는 좌우가 내비 옆으로 빠진다.
        /// 2px면 금테가 내비 윗변에 붙어 한 덩어리로 읽힌다(필드·월드맵과 동형).
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

        public static float OverlayH(Rect page) => Blocked ? page.height : DockH;

        public static float OpenH(Rect page) =>
            Mathf.Max(0f, page.height - OverlayH(page));

        public static string Line() => Blocked
            ? "카드가 출전 탭을 가린다"
            : "HUD는 출전 탭을 가리지 않는다 — 도크는 내비 위(§16)";

        /// <summary>내비 플레이트 윗변. 도크 아랫변이 이보다 아래면 금테가 먹힌다(§16).</summary>
        public static float NavPlateTop(float screenH = 720f) =>
            UiPages.NavPlateTop(GameFlow.BottomBar.Length, 1280f, screenH);

        /// <summary>
        /// 새 길 도크 상자. 필드·월드맵과 같이 내비 플레이트 위에 둔다 —
        /// page.yMax에 붙이면 하단 금테가 도크에 먹힌다(실측 1280×720, 카드 yMax 640 · 플레이트 636).
        /// </summary>
        public static Rect Dock(Rect page, float screenH = 720f)
        {
            float yMax = Mathf.Min(page.yMax, NavPlateTop(screenH) - NavGap);
            return new Rect(page.x, yMax - DockH, page.width, DockH);
        }

        /// <summary>
        /// 필드 출전 · 전투 스타일 · 편성 수 · 영지로 순서.
        /// 막히면 옛 AfterTabs 2×2 전폭, 아니면 아래 2×2 도크.
        /// </summary>
        public static Rect[] Cards(Rect page, float screenH = 720f)
        {
            if (Blocked)
                return UiPages.Grid(page, OldCols, OldRows, 16f);
            return UiPages.Grid(Dock(page, screenH), DockCols, DockRows, DockGap);
        }

        public static void SeedQaIfRequested()
        {
            if (!ShowQa) return;
            if (Blocked) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            _ = Line();
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
        }
    }
}
