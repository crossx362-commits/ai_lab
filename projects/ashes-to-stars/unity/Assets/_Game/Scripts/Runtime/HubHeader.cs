using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 허브 제목판. 클래시·킹덤처럼 세계가 보이고 제목은 가장자리만 쓴다.
    /// 옛 길은 88+본문시작 100이 720p에서 들판·마을을 덮었다.
    /// QA_NO면 그 옛 높이. GameScreen이 읽는다.
    /// </summary>
    public static class HubHeader
    {
        public const string EnvShow = "QA_HUB_HEADER";
        public const string EnvNo = "QA_NO_HUB_HEADER";
        public const string EnvNoSubtitleContrast = "QA_NO_HUB_SUBTITLE_CONTRAST";
        public const float ScreenW = 1280f;
        public const float ScreenH = 720f;
        public const float OldH = 88f;
        public const float OldBodyTop = 100f;
        public const float SlimH = 52f;
        public const float SlimBodyTop = 56f;
        public const float OldIcon = 60f;
        public const float SlimIcon = 36f;

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

        public static float H => Blocked ? OldH : SlimH;

        public static float BodyTop => Blocked ? OldBodyTop : SlimBodyTop;

        public static float IconSize => Blocked ? OldIcon : SlimIcon;

        /// <summary>
        /// 슬림 부제는 12px라 옛 Dim 색(0.62/0.65/0.75)이 영지 성벽·필드 안개와 섞였다.
        /// 제목보다 한 단계만 낮은 명도로 올리고 QA_NO면 옛 색을 재현한다.
        /// </summary>
        public static Color SubtitleColor =>
            Environment.GetEnvironmentVariable(EnvNoSubtitleContrast) == "1"
                ? new Color(0.62f, 0.65f, 0.75f)
                : new Color(0.76f, 0.79f, 0.87f);

        public static float OpenH(float navReserve) => ScreenH - BodyTop - navReserve;

        public static string Line() => Blocked
            ? "제목판이 화면을 가린다"
            : "제목판은 화면을 가리지 않는다(§16)";

        public static Rect IconRect()
        {
            float s = IconSize;
            return Blocked
                ? new Rect(18f, 14f, s, s)
                : new Rect(14f, 8f, s, s);
        }

        public static Rect TitleRect(bool atlas)
        {
            if (Blocked)
                return new Rect(atlas ? 90f : 28f, 10f, ScreenW - (atlas ? 120f : 56f), 42f);
            return new Rect(atlas ? 56f : 20f, 4f, ScreenW - (atlas ? 80f : 40f), 24f);
        }

        public static Rect SubtitleRect(bool atlas)
        {
            if (Blocked)
                return new Rect(atlas ? 90f : 28f, 52f, ScreenW - 80f, 30f);
            return new Rect(atlas ? 56f : 20f, 28f, ScreenW - 40f, 20f);
        }

        public static void SeedQaIfRequested()
        {
            if (!ShowQa) return;
            if (Blocked) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
        }
    }
}
