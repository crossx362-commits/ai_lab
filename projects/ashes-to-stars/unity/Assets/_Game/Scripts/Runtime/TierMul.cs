using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §18-1 티어당 수익 배율. <see cref="BalanceConfig.티어배율"/>의 런타임 소비처.
    /// 에셋 기본 1.6이 authored돼 있으면서도 grep 소비처가 0곳이었다 —
    /// <c>Economy.TierRevenueMultiplier</c>가 ×1.6 거듭제곱 표를 하드코딩했다.
    /// QA_NO면 옛 표. 표시 줄 + T2 골드. W3Party 무접촉.
    /// </summary>
    public static class TierMul
    {
        public const string EnvShow = "QA_TIER_MUL";
        public const string EnvNo = "QA_NO_TIER_MUL";
        public const float Default = 1.6f;
        public const int Tiers = 10;

        /// <summary>QA_NO·기본 1.6이 쓰는 옛 하드코드 표. 끝자리까지 그대로.</summary>
        public static readonly float[] Legacy =
        {
            1.0f,
            1.6f,
            2.56f,
            4.096f,
            6.5536f,
            10.48576f,
            16.777216f,
            26.8435456f,
            42.94967296f,
            68.71947674f
        };

        /// <summary>SelfCheck가 필드 소비를 증명할 때만.</summary>
        public static BalanceConfig ForceConfig;

        static float _cached = -1f;
        static float[] _cachedTable;
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

        /// <summary>§18-1 앵커. 에셋 기본값 1.6. 차단이면 옛 하드코드 1.6.</summary>
        public static float Factor()
        {
            if (Blocked) return Default;
            if (ForceConfig != null)
                return ClampRaw(ForceConfig.티어배율);
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
                if (cfg != null && cfg.티어배율 > 0f)
                    raw = cfg.티어배율;
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

        /// <summary>
        /// T1=1, Tn = Factor^(n-1). 차단이거나 배율이 기본 1.6이면 옛 표를 그대로 쓴다
        /// (기존 T2=16000 쿠퍼 검정이 소수점 오차로 깨지지 않게).
        /// </summary>
        public static float[] Table()
        {
            if (Blocked) return Legacy;
            float f = Factor();
            if (_cachedTable != null && _cachedTable.Length == Tiers
                && Mathf.Approximately(_cachedTable[1], f))
                return _cachedTable;
            if (Mathf.Approximately(f, Default))
            {
                _cachedTable = Legacy;
                return _cachedTable;
            }
            var t = new float[Tiers];
            float v = 1f;
            for (int i = 0; i < Tiers; i++)
            {
                t[i] = v;
                v *= f;
            }
            _cachedTable = t;
            return _cachedTable;
        }

        /// <summary>캐릭터 속성 탭. QA_NO면 빈 문자열(옛 화면 = 배율 줄 없음).</summary>
        public static string Line()
        {
            if (Blocked) return "";
            return $"티어당 ×{Factor():0.##}(§18-1)";
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
            _cachedTable = null;
            _qaSeeded = false;
        }
    }
}
