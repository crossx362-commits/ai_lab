using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §4·§15 PvP 사망 회복. <see cref="BalanceConfig.PvP회복시간"/>의 런타임 소비처.
    /// 에셋 기본 12시간이 authored돼 있으면서도 grep 소비처가 0곳이었다 —
    /// <c>LifeSystem.PvpRecoverSeconds</c>가 <c>InvasionState.DefenseRecoverSeconds</c>를 하드코딩했다.
    /// QA_NO면 옛 12시간(보호막 상수). 표시 줄 + 회복 초. W3Party 무접촉.
    /// </summary>
    public static class PvpRecover
    {
        public const string EnvShow = "QA_PVP_RECOVER";
        public const string EnvNo = "QA_NO_PVP_RECOVER";
        public const float DefaultHours = 12f;
        public const long DefaultSeconds = 12L * 3600L;

        /// <summary>SelfCheck가 필드 소비를 증명할 때만.</summary>
        public static BalanceConfig ForceConfig;

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

        /// <summary>§4 앵커. 에셋 기본값 12시간. 차단이면 옛 하드코드 12.</summary>
        public static float Hours()
        {
            if (Blocked) return DefaultHours;
            float raw = DefaultHours;
            try
            {
                BalanceConfig cfg = ForceConfig;
                bool owned = false;
                if (cfg == null)
                {
                    cfg = ScriptableObject.CreateInstance<BalanceConfig>();
                    owned = cfg != null;
                }
                if (cfg != null && cfg.PvP회복시간 > 0f)
                    raw = cfg.PvP회복시간;
                if (owned && cfg != null)
                    UnityEngine.Object.DestroyImmediate(cfg);
            }
            catch
            {
                raw = DefaultHours;
            }
            return raw > 0f ? raw : DefaultHours;
        }

        /// <summary>회복 초. LifeSystem·수비 회복이 읽는다.</summary>
        public static long Seconds()
        {
            if (Blocked) return DefaultSeconds;
            float h = Hours();
            long sec = (long)Math.Round(h * 3600.0);
            return sec < 1L ? DefaultSeconds : sec;
        }

        /// <summary>캐릭터 속성 탭. QA_NO면 빈 문자열(옛 화면 = PvP 회복 줄 없음).</summary>
        public static string Line()
        {
            if (Blocked) return "";
            return $"PvP 회복 {Fmt(Hours())}h(§4)";
        }

        static string Fmt(float v) =>
            v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        public static void SeedQaIfRequested()
        {
            if (!ShowQa) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            _ = Line();
        }

        public static void ResetForTest()
        {
            ForceConfig = null;
            _qaSeeded = false;
        }
    }
}
