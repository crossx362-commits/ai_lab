using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 탑 골드부족·마지막목숨·사망동의 경고 HUD. 하단 DrawChoice 두 장이 본문 yMax=640에
    /// 붙어 내비 플레이트(636)와 4px 겹친다(실측 1280×720, 필드 경고와 동형).
    /// QA_NO_TOWER_WARN_NAV면 그 옛 겹침. TowerScreen 경고 페이지가 읽는다.
    /// </summary>
    public static class TowerWarnHud
    {
        public const string EnvShow = "QA_TOWER_WARN";
        public const string EnvNoNav = "QA_NO_TOWER_WARN_NAV";
        public const string EnvGold = "QA_TOWER_WARN_GOLD";
        public const string EnvConsent = "QA_TOWER_WARN_CONSENT";
        /// <summary>DrawChoice와 같은 두 장 높이. 본문 yMax에 붙이면 금테가 내비에 먹힌다.</summary>
        public const float ActionH = 168f;
        /// <summary>
        /// 전폭 선택 카드는 좌우가 내비 옆으로 빠진다.
        /// 2px면 금테가 내비 윗변에 붙어 한 덩어리로 읽힌다(필드·스타일과 동형).
        /// </summary>
        public const float NavGap = 12f;

        static bool _qaSeeded;
        static bool _goldPrompt;
        static bool _lifePrompt;
        static bool _consentPrompt;

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

        public static bool GoldQa
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvGold);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool ConsentQa
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvConsent);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool QaGoldPrompt => _goldPrompt;
        public static bool QaLifePrompt => _lifePrompt;
        public static bool QaConsentPrompt => _consentPrompt;
        public static void AckGold() => _goldPrompt = false;
        public static void AckLife() => _lifePrompt = false;
        public static void AckConsent() => _consentPrompt = false;

        public static string Line() => NavBlocked
            ? "선택 바가 내비와 겹친다"
            : "선택 바는 내비 위(§16)";

        /// <summary>내비 플레이트 윗변. 선택 바 아랫변이 이보다 아래면 금테가 먹힌다(§16).</summary>
        public static float NavPlateTop(float screenH = 720f) =>
            UiPages.NavPlateTop(GameFlow.BottomBar.Length, 1280f, screenH);

        /// <summary>
        /// 경고 본문. 막히면 page.yMax에 붙어 하단 금테가 내비에 먹힌다
        /// (실측 1280×720, 선택 바 yMax 640 · 플레이트 636). 새 길은 필드 경고와 같이
        /// 내비 플레이트 위에 둔다.
        /// </summary>
        public static Rect Content(Rect page, float screenH = 720f)
        {
            if (NavBlocked) return page;
            float yMax = Mathf.Min(page.yMax, NavPlateTop(screenH) - NavGap);
            return new Rect(page.x, page.y, page.width, Mathf.Max(40f, yMax - page.y));
        }

        /// <summary>하단 확인/취소. Content 아랫변에 붙인다 — page.yMax에 붙이면 내비에 먹힌다.</summary>
        public static Rect Choice(Rect page, float screenH = 720f)
        {
            var box = Content(page, screenH);
            float h = Mathf.Min(ActionH, box.height);
            return new Rect(box.x, box.yMax - h, box.width, h);
        }

        public static void SeedQaIfRequested()
        {
            if (!ShowQa) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            // 차단이어도 경고 페이지는 띄운다 — QA_NO는 Content만 옛 yMax에 붙여 겹침을 재현한다.
            if (GoldQa) _goldPrompt = true;
            else if (ConsentQa) _consentPrompt = true;
            else _lifePrompt = true;
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
            _goldPrompt = false;
            _lifePrompt = false;
            _consentPrompt = false;
        }
    }
}
