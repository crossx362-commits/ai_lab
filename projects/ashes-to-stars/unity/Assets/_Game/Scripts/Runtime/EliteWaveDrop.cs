using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §10-2 정예 처치 → 다음 웨이브 드랍 배율.
    /// <see cref="EliteDrop.FieldKills"/>를 읽어 다음 잡몹 웨이브 드랍에 곱한다.
    /// 원장은 「처치해야 다음 웨이브 드랍 배율이 오른다」만 확정, 수치는 미확정.
    /// BeginWave가 웨이브 시작 시점의 FieldKills를 고정하므로 이번 웨이브 킬은
    /// 다음 웨이브부터 먹는다. QA_NO면 옛 ×1. W3Party 무접촉.
    /// </summary>
    public static class EliteWaveDrop
    {
        public const string EnvShow = "QA_ELITE_WAVE_DROP";
        public const string EnvNo = "QA_NO_ELITE_WAVE_DROP";
        public const float DefaultPerKill = 0.25f;
        public const float DefaultCap = 2f;

        /// <summary>SelfCheck가 필드 소비를 증명할 때만.</summary>
        public static BalanceConfig ForceConfig;

        static float _perKill = -1f;
        static float _cap = -1f;
        static int _armed;
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

        /// <summary>필드 정예 1킬당 가산. 에셋 기본 0.25. 차단이어도 숫자는 읽는다(Mul만 옛 1).</summary>
        public static float PerKill()
        {
            if (ForceConfig != null)
                return ClampPer(ForceConfig.정예처치드랍배율);
            if (_perKill > 0f) return _perKill;
            _perKill = ReadPer();
            return _perKill;
        }

        /// <summary>배율 상한. 에셋 기본 2.</summary>
        public static float Cap()
        {
            if (ForceConfig != null)
                return ClampCap(ForceConfig.정예처치드랍상한);
            if (_cap > 0f) return _cap;
            _cap = ReadCap();
            return _cap;
        }

        static float ReadPer()
        {
            float raw = DefaultPerKill;
            try
            {
                var cfg = ScriptableObject.CreateInstance<BalanceConfig>();
                if (cfg != null && cfg.정예처치드랍배율 > 0f)
                    raw = cfg.정예처치드랍배율;
                if (cfg != null)
                    UnityEngine.Object.DestroyImmediate(cfg);
            }
            catch
            {
                raw = DefaultPerKill;
            }
            return ClampPer(raw);
        }

        static float ReadCap()
        {
            float raw = DefaultCap;
            try
            {
                var cfg = ScriptableObject.CreateInstance<BalanceConfig>();
                if (cfg != null && cfg.정예처치드랍상한 > 0f)
                    raw = cfg.정예처치드랍상한;
                if (cfg != null)
                    UnityEngine.Object.DestroyImmediate(cfg);
            }
            catch
            {
                raw = DefaultCap;
            }
            return ClampCap(raw);
        }

        static float ClampPer(float raw) => raw <= 0f ? DefaultPerKill : raw;
        static float ClampCap(float raw) => raw < 1f ? DefaultCap : raw;

        /// <summary>
        /// 다음 웨이브 드랍 배율. BeginWave가 스냅한 이전 웨이브 FieldKills.
        /// 차단이면 옛 ×1.
        /// </summary>
        public static float Mul()
        {
            if (Blocked) return 1f;
            int k = _armed;
            if (k < 0) k = 0;
            float mul = 1f + PerKill() * k;
            float cap = Cap();
            if (mul > cap) mul = cap;
            if (mul < 1f) mul = 1f;
            return mul;
        }

        /// <summary>웨이브 시작 — 지금 FieldKills를 이번 웨이브 배율로 고정(다음 웨이브가 먹는다).</summary>
        public static void BeginWave()
        {
            _armed = EliteDrop.FieldKills;
        }

        /// <summary>필드 정산 줄. QA_NO면 빈 문자열(옛 화면 = 배율 줄 없음).</summary>
        public static string Line()
        {
            if (Blocked) return "";
            return $"정예 처치 다음 웨이브 드랍 ×{Mul():0.##}(§10-2)";
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
            _perKill = -1f;
            _cap = -1f;
            _armed = 0;
            _qaSeeded = false;
        }
    }
}
