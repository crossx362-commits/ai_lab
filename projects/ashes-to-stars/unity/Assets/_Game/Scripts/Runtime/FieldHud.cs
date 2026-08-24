using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 필드 허브 HUD. 클래시·킹덤·하데스처럼 배경이 보이고 조작은 아래에만 둔다.
    /// 옛 길은 2×3 전폭 카드가 본문 540을 전부 덮었다.
    /// QA_NO면 그 옛 겹침. FieldScreen이 읽는다.
    /// </summary>
    public static class FieldHud
    {
        public const string EnvShow = "QA_FIELD_HUD";
        public const string EnvNo = "QA_NO_FIELD_HUD";
        public const float OldBodyH = 540f;
        public const float DockH = 200f;
        public const float DockGap = 10f;
        public const int DockCols = 3;
        public const int DockRows = 2;
        public const int OldCols = 2;
        public const int OldRows = 3;

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
            ? "카드가 필드를 가린다"
            : "HUD는 필드를 가리지 않는다 — 도크는 내비 위(§16)";

        /// <summary>내비 플레이트 윗변. 도크 아랫변이 이보다 아래면 금테가 먹힌다(§16).</summary>
        public static float NavPlateTop(float screenH = 720f) =>
            UiPages.NavPlateTop(GameFlow.BottomBar.Length, 1280f, screenH);

        /// <summary>
        /// 사냥·던전·레이드·저체력·일정·배회 보스 순서.
        /// 막히면 옛 2×3 전폭, 아니면 아래 3×2 도크.
        /// 새 길은 영지 PaletteBar와 같이 내비 플레이트 위에 둔다 —
        /// body.yMax에 붙이면 하단 금테가 도크에 먹힌다(실측 1280×720, 카드 yMax 640 · 플레이트 636).
        /// </summary>
        public static Rect[] Cards(Rect body, float screenH = 720f)
        {
            if (Blocked)
                return UiPages.Grid(body, OldCols, OldRows, 16f);
            float yMax = Mathf.Min(body.yMax, NavPlateTop(screenH) - 2f);
            var dock = new Rect(body.x, yMax - DockH, body.width, DockH);
            return UiPages.Grid(dock, DockCols, DockRows, DockGap);
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
