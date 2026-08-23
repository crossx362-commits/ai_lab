using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 시간당 수익 소프트캡(§18-14). 티어 기대의 150%까지는 그대로,
    /// 초과분은 획득량 −70%(30%만 남김). 채굴 농장 완화. 서버 검증은 안 연다.
    /// </summary>
    public static class SoftCap
    {
        public const string EnvShow = "QA_SOFT_CAP";
        public const string EnvNo = "QA_NO_SOFT_CAP";
        public const int ThresholdPercent = 150;
        public const int ExcessKeepPercent = 30;
        public const long HourSeconds = 3600;

        const string K_HOUR = "ats.softcap.hour";
        const string K_EARNED = "ats.softcap.earned";

        static bool _loaded;
        static bool _qaSeeded;
        static long _hourStart;
        static long _earned;

        public static Func<long> NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static long HourStart { get { Load(); Rotate(); return _hourStart; } }
        public static long EarnedThisHour { get { Load(); Rotate(); return _earned; } }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            long.TryParse(PlayerPrefs.GetString(K_HOUR, "0"), out _hourStart);
            long.TryParse(PlayerPrefs.GetString(K_EARNED, "0"), out _earned);
            if (_hourStart < 0) _hourStart = 0;
            if (_earned < 0) _earned = 0;
        }

        static void Save()
        {
            PlayerPrefs.SetString(K_HOUR, _hourStart.ToString());
            PlayerPrefs.SetString(K_EARNED, _earned.ToString());
            PlayerPrefs.Save();
        }

        static void Rotate()
        {
            long now = NowUnix();
            if (_hourStart <= 0 || now - _hourStart >= HourSeconds)
            {
                _hourStart = now;
                _earned = 0;
                Save();
            }
        }

        /// <summary>이 티어의 1시간 기대 수익(쿠퍼). T1=10000.</summary>
        public static long ExpectedCopper(int tier)
        {
            var mul = Economy.TierRevenueMultiplier;
            if (mul == null || mul.Length == 0) return Economy.COPPER_PER_GOLD;
            if (tier < 0) tier = 0;
            if (tier >= mul.Length) tier = mul.Length - 1;
            return (long)(mul[tier] * Economy.COPPER_PER_GOLD);
        }

        public static long ExpectedCopper() => ExpectedCopper(GameState.Tier);

        /// <summary>소프트캡이 붙는 문턱. T1=15000.</summary>
        public static long ThresholdCopper(int tier) =>
            ExpectedCopper(tier) * ThresholdPercent / 100;

        public static long ThresholdCopper() => ThresholdCopper(GameState.Tier);

        /// <summary>
        /// 이미 번 금액 <paramref name="already"/> 위에 <paramref name="incoming"/>을 얹는다.
        /// 창을 바꾸지 않는 순수 계산. SelfCheck·네거티브가 이걸 본다.
        /// </summary>
        public static long Preview(long incoming, long already, long threshold)
        {
            if (incoming <= 0) return 0;
            if (already < 0) already = 0;
            if (threshold < 0) threshold = 0;
            if (already + incoming <= threshold) return incoming;
            long room = threshold - already;
            if (room < 0) room = 0;
            long excess = incoming - room;
            return room + excess * ExcessKeepPercent / 100;
        }

        public static long Preview(long incoming, long already) =>
            Preview(incoming, already, ThresholdCopper());

        public static long Preview(long incoming) =>
            Preview(incoming, EarnedThisHour);

        /// <summary>시간창에 반영하고 실제로 줄 금액을 돌려준다. QA_NO면 그대로.</summary>
        public static long Apply(long incoming)
        {
            if (incoming <= 0) return 0;
            if (Blocked) return incoming;
            Load();
            Rotate();
            long keep = Preview(incoming, _earned, ThresholdCopper());
            _earned += keep;
            Save();
            return keep;
        }

        public static string Line()
        {
            if (Blocked) return "시간당 수익 소프트캡 없음";
            return "시간당 수익 소프트캡 150%(§18-14) · 한도 "
                + EstateStatusHud.ShortCopper(ThresholdCopper()) + "/h";
        }

        public static string HourLine()
        {
            if (Blocked) return Line();
            return Line() + " · 이번 시간 " + EstateStatusHud.ShortCopper(EarnedThisHour);
        }

        /// <summary>시각 QA. QA_SOFT_CAP=1이면 T1에서 20000을 넣어 16500이 남게 한다.</summary>
        public static void SeedQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable(EnvShow) != "1") return;
            if (Blocked) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            Load();
            GameState.TrySelectTier(0);
            _hourStart = NowUnix();
            _earned = 0;
            Save();
            GameState.Earn(20_000);
        }

        public static void ResetForTest()
        {
            PlayerPrefs.DeleteKey(K_HOUR);
            PlayerPrefs.DeleteKey(K_EARNED);
            PlayerPrefs.Save();
            NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            _hourStart = 0;
            _earned = 0;
            _qaSeeded = false;
            _loaded = false;
        }

        public static void ForgetInMemoryForTest()
        {
            _hourStart = 0;
            _earned = 0;
            _loaded = false;
        }
    }
}
