using System;

namespace AshesToStars
{
    /// <summary>
    /// 파티 편성 헤더 부제. 옛 줄은 상한·편성·진형·부활초를 이어 붙여 LabelClip에 잘렸다.
    /// QA_NO면 옛 긴 줄. PartyScreen이 읽는다.
    /// </summary>
    public static class PartyHudCap
    {
        public const string EnvShow = "QA_PARTY_HUD";
        public const string EnvNo = "QA_NO_PARTY_HUD";
        /// <summary>슬림 제목판 부제 한 줄(§16). 헤더는 도크보다 넓지만 네 덩어리면 잘린다.</summary>
        public const int CaptionMaxRunes = 28;

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
                if (Blocked) return false;
                string raw = Environment.GetEnvironmentVariable(EnvShow);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static string Line() => Blocked
            ? "부제가 잘린다"
            : "파티 부제는 한 줄이다(§16)";

        public static int RuneCount(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int n = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (!char.IsLowSurrogate(text[i])) n++;
            }
            return n;
        }

        public static bool CaptionFits(string text) =>
            RuneCount(text) <= CaptionMaxRunes;

        /// <summary>옛 소비처 — 상한·편성·진형·부활초를 한 줄에 이어 붙였다.</summary>
        public static string Old() =>
            $"최대 {PartyState.MaxSlots}인(§9) · 편성 {PartyState.Slots.Count}명 · " +
            $"1번 자리가 탱 자리다(§10-4 진형) · 부활초 {LifeSystem.GetRevivePotions()}/3";

        /// <summary>제목이 파티 편성이라 숫자·탱·부활초만. 28자 이하.</summary>
        public static string Caption()
        {
            if (Blocked) return Old();
            return $"편성 {PartyState.Slots.Count}/{PartyState.MaxSlots} · 1번=탱 · 부활초 {LifeSystem.GetRevivePotions()}/3";
        }

        public static void SeedQaIfRequested()
        {
            if (!ShowQa) return;
            if (Blocked) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            _ = Caption();
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
        }
    }
}
