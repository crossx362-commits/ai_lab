using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 허브 본문을 내비 플레이트 위에서 한 곳에서 자른다.
    /// NavReserve=80이면 Body yMax=640인데 플레이트 윗변은 636이라 하단 금테가
    /// 4px 겹친다(실측 1280×720). 화면마다 *Hud.NavGap 12를 복제하면 새 화면은
    /// 또 겹침으로 시작한다. QA_NO면 옛 yMax=640. GameScreen이 읽는다.
    /// </summary>
    public static class BodyNav
    {
        public const string EnvShow = "QA_BODY_NAV";
        public const string EnvNo = "QA_NO_BODY_NAV";
        /// <summary>
        /// 2px면 금테가 내비 윗변에 붙어 한 덩어리로 읽힌다(필드·영지와 동형).
        /// </summary>
        public const float NavGap = 12f;

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

        public static float NavPlateTop(float screenW = 1280f, float screenH = 720f) =>
            UiPages.NavPlateTop(GameFlow.BottomBar.Length, screenW, screenH);

        /// <summary>
        /// 도크 있는 허브 본문. 막히면 body.yMax(640)에 붙어 하단 금테가 내비에 먹힌다.
        /// 도크 없는 화면(전투·결과·타이틀)은 그대로 둔다.
        /// </summary>
        public static Rect Fit(Rect body, bool showBottomBar, float screenW = 1280f, float screenH = 720f)
        {
            if (!showBottomBar || Blocked) return body;
            float yMax = Mathf.Min(body.yMax, NavPlateTop(screenW, screenH) - NavGap);
            return new Rect(body.x, body.y, body.width, Mathf.Max(40f, yMax - body.y));
        }

        public static string Line() => Blocked
            ? "본문이 내비와 겹친다"
            : "본문은 내비 위(§16)";

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
