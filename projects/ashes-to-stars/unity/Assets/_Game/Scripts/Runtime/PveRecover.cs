using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §4·§18-8 PvE 사망 회복. <see cref="BalanceConfig.PvE회복시간"/>의 런타임 소비처.
    /// 에셋 기본 24시간이 authored돼 있으면서도 grep 소비처가 0곳이었다 —
    /// <c>LifeSystem.PveRecoverSeconds</c>가 86400을 하드코딩하고 RaceDef만 읽었다.
    /// QA_NO면 옛 24시간·줄 없음. 표시 줄 + 기본 회복 초. W3Party 무접촉.
    /// 종족 표(인간 18h)는 RaceDef가 이긴다. 이 모듈은 기본(비인간·종족표 차단)만.
    /// </summary>
    public static class PveRecover
    {
        public const string EnvShow = "QA_PVE_RECOVER";
        public const string EnvNo = "QA_NO_PVE_RECOVER";
        public const float DefaultHours = 24f;
        public const long DefaultSeconds = 24L * 3600L;

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

        /// <summary>§18-8 앵커. 에셋 기본값 24시간. 차단이면 옛 하드코드 24.</summary>
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
                if (cfg != null && cfg.PvE회복시간 > 0f)
                    raw = cfg.PvE회복시간;
                if (owned && cfg != null)
                    UnityEngine.Object.DestroyImmediate(cfg);
            }
            catch
            {
                raw = DefaultHours;
            }
            return raw > 0f ? raw : DefaultHours;
        }

        /// <summary>회복 초. LifeSystem이 종족표가 없거나 차단일 때 읽는다.</summary>
        public static long Seconds()
        {
            if (Blocked) return DefaultSeconds;
            float h = Hours();
            long sec = (long)Math.Round(h * 3600.0);
            return sec < 1L ? DefaultSeconds : sec;
        }

        /// <summary>캐릭터 속성 탭. QA_NO면 빈 문자열(옛 화면 = PvE 회복 줄 없음).</summary>
        public static string Line()
        {
            if (Blocked) return "";
            return $"PvE 회복 {Fmt(Hours())}h(§18-8)";
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
