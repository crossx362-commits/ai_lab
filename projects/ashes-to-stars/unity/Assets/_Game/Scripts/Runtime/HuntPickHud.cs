using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 필드 사냥 시작 편성 HUD. 하단 스타트/취소가 본문 yMax=640에 붙어
    /// 내비 플레이트(636)와 4px 겹친다(실측 1280×720, hunt_start_shots/qa_go:Field.png).
    /// QA_NO_HUNT_PICK_NAV면 그 옛 겹침. FieldScreen 편성 페이지가 읽는다.
    /// </summary>
    public static class HuntPickHud
    {
        public const string EnvShow = "QA_HUNT_PICK";
        public const string EnvNoNav = "QA_NO_HUNT_PICK_NAV";
        /// <summary>DrawChoice와 같은 두 장 높이. 본문 yMax에 붙이면 금테가 내비에 먹힌다.</summary>
        public const float ActionH = 168f;
        /// <summary>
        /// 전폭 선택 카드는 좌우가 내비 옆으로 빠진다.
        /// 2px면 금테가 내비 윗변에 붙어 한 덩어리로 읽힌다(필드·스타일과 동형).
        /// </summary>
        public const float NavGap = 12f;

        static bool _qaSeeded;

        public static bool NavBlocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNoNav);
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

        public static string Line() => NavBlocked
            ? "선택 바가 내비와 겹친다"
            : "선택 바는 내비 위(§16)";

        /// <summary>내비 플레이트 윗변. 선택 바 아랫변이 이보다 아래면 금테가 먹힌다(§16).</summary>
        public static float NavPlateTop(float screenH = 720f) =>
            UiPages.NavPlateTop(GameFlow.BottomBar.Length, 1280f, screenH);

        /// <summary>
        /// 명부·선택 바 본문. 막히면 page.yMax에 붙어 하단 금테가 내비에 먹힌다
        /// (실측 1280×720, 선택 바 yMax 640 · 플레이트 636). 새 길은 필드·스타일과 같이
        /// 내비 플레이트 위에 둔다.
        /// </summary>
        public static Rect Content(Rect page, float screenH = 720f)
        {
            if (NavBlocked) return page;
            float yMax = Mathf.Min(page.yMax, NavPlateTop(screenH) - NavGap);
            return new Rect(page.x, page.y, page.width, Mathf.Max(40f, yMax - page.y));
        }

        /// <summary>하단 스타트/취소. Content 아랫변에 붙인다 — page.yMax에 붙이면 내비에 먹힌다.</summary>
        public static Rect Actions(Rect page, float screenH = 720f)
        {
            var box = Content(page, screenH);
            float h = Mathf.Min(ActionH, box.height);
            return new Rect(box.x, box.yMax - h, box.width, h);
        }

        public static void SeedQaIfRequested()
        {
            if (!ShowQa) return;
            if (NavBlocked) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            _ = LifeSystem.GetCharacters();
            _ = PartyState.Slots;
            HuntStart.BeginPick();
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
        }
    }
}
