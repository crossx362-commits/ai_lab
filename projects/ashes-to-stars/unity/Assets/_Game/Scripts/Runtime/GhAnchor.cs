using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §18-1 T1 필드 1시간 수익 앵커. <see cref="BalanceConfig.티어1시간당골드"/>의 런타임 소비처.
    /// 에셋 기본 1골드가 authored돼 있으면서도 grep 소비처가 0곳이었다 —
    /// <c>Economy.WaveHuntGold</c>가 <c>COPPER_PER_GOLD</c>를 1 G/h로 하드코딩했다.
    /// QA_NO면 옛 1골드. 표시 줄 + 필드 시간당 골드. W3Party 무접촉.
    /// </summary>
    public static class GhAnchor
    {
        public const string EnvShow = "QA_GH_ANCHOR";
        public const string EnvNo = "QA_NO_GH_ANCHOR";
        public const float Default = 1f;

        /// <summary>SelfCheck가 필드 소비를 증명할 때만.</summary>
        public static BalanceConfig ForceConfig;

        static float _cached = -1f;
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

        /// <summary>§18-1 앵커. 에셋 기본값 1. 차단이면 옛 하드코드 1. 캐시해서 매 틱 SO를 안 만든다.</summary>
        public static float Hours()
        {
            if (Blocked) return Default;
            if (ForceConfig != null)
                return ClampRaw(ForceConfig.티어1시간당골드);
            if (_cached > 0f) return _cached;
            _cached = Read();
            return _cached;
        }

        static float Read()
        {
            float raw = Default;
            try
            {
                BalanceConfig cfg = ForceConfig;
                bool owned = false;
                if (cfg == null)
                {
                    cfg = ScriptableObject.CreateInstance<BalanceConfig>();
                    owned = cfg != null;
                }
                if (cfg != null && cfg.티어1시간당골드 > 0f)
                    raw = cfg.티어1시간당골드;
                if (owned && cfg != null)
                    UnityEngine.Object.DestroyImmediate(cfg);
            }
            catch
            {
                raw = Default;
            }
            return ClampRaw(raw);
        }

        static float ClampRaw(float raw) => raw > 0f ? raw : Default;

        public static long CopperPerHour()
        {
            long n = (long)(Hours() * Economy.COPPER_PER_GOLD);
            return n < 1 ? 1 : n;
        }

        /// <summary>캐릭터 속성 탭. QA_NO면 빈 문자열(옛 화면 = 앵커 줄 없음).</summary>
        public static string Line()
        {
            if (Blocked) return "";
            return $"G/h 앵커 {EstateStatusHud.ShortCopper(CopperPerHour())}(§18-1)";
        }

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
            _cached = -1f;
            _qaSeeded = false;
        }
    }
}
