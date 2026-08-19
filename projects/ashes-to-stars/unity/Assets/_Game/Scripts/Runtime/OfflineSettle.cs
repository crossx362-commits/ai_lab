using System;

namespace AshesToStars
{
    /// <summary>
    /// 오프라인 정산 감쇠(§18-14 안전장치 · §19 방치 채굴 농장 억제).
    /// 오프라인 경과시간을 정산할 때 8시간까지 100% · 8~12시간 구간 50% ·
    /// 12시간 초과분은 버린다(실효 상한 10시간). 온라인은 매 틱 경과가 작아
    /// 항상 8시간 미만이므로 감쇠 없이 100%로 정산된다 — AFK 온라인은 별개(§19 상시관측).
    /// 소비처는 광산 <see cref="EstateMine"/>.Tick 하나. 경과를 실효 초로 환산한다.
    /// QA_NO_OFFLINE_DECAY면 옛 전 구간 100%(경과 그대로).
    /// </summary>
    public static class OfflineSettle
    {
        public const string EnvNo = "QA_NO_OFFLINE_DECAY";
        public const long FullSeconds = 8L * 3600L;  // 8시간까지 100%
        public const long HalfSeconds = 4L * 3600L;  // 8~12시간 구간
        public const int HalfPercent = 50;           // 그 구간 50%

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>최대 실효 시간(초) = 8h + 4h×50% = 10시간.</summary>
        public static long MaxEffectiveSeconds => FullSeconds + HalfSeconds * HalfPercent / 100;

        /// <summary>
        /// 경과 <paramref name="elapsedSeconds"/>초를 감쇠 곡선으로 환산한 '실효 초'.
        /// 8h까지 그대로, 8~12h는 절반, 12h 초과는 버린다(상한 10h). 온라인 틱(작은 경과)은
        /// 8h 미만이라 그대로 100%다. QA_NO_OFFLINE_DECAY면 경과를 그대로 돌려준다.
        /// </summary>
        public static long EffectiveSeconds(long elapsedSeconds)
        {
            if (elapsedSeconds <= 0) return 0;
            if (Blocked) return elapsedSeconds;
            long full = elapsedSeconds < FullSeconds ? elapsedSeconds : FullSeconds;
            long overFull = elapsedSeconds - full;
            long half = overFull < HalfSeconds ? overFull : HalfSeconds;
            return full + half * HalfPercent / 100;
        }

        public static string Line() =>
            "오프라인 정산 8h 100% · 8~12h 50%(§18-14)";
    }
}
