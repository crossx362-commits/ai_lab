using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §18-2 골드 총유입 대비 소각 목표. <see cref="BalanceConfig.소각목표"/>의 런타임 소비처.
    /// 에셋 기본 45~55가 authored돼 있으면서도 grep 소비처가 0곳이었다.
    /// QA_NO면 옛 45~55·줄 없음. 표시 줄 + 하한/상한. W3Party 무접촉.
    /// </summary>
    public static class BurnTarget
    {
        public const string EnvShow = "QA_BURN_TARGET";
        public const string EnvNo = "QA_NO_BURN_TARGET";
        public static readonly Vector2 Default = new Vector2(45f, 55f);

        /// <summary>SelfCheck가 필드 소비를 증명할 때만.</summary>
        public static BalanceConfig ForceConfig;

        static Vector2 _cached = new Vector2(-1f, -1f);
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

        /// <summary>§18-2 앵커. 에셋 기본값 45~55. 차단이면 옛 하드코드 45~55.</summary>
        public static Vector2 Range()
        {
            if (Blocked) return Default;
            if (ForceConfig != null)
                return ClampRaw(ForceConfig.소각목표);
            if (_cached.x > 0f) return _cached;
            _cached = Read();
            return _cached;
        }

        public static float Low() => Range().x;
        public static float High() => Range().y;

        static Vector2 Read()
        {
            Vector2 raw = Default;
            try
            {
                BalanceConfig cfg = ForceConfig;
                bool owned = false;
                if (cfg == null)
                {
                    cfg = ScriptableObject.CreateInstance<BalanceConfig>();
                    owned = cfg != null;
                }
                if (cfg != null)
                    raw = cfg.소각목표;
                if (owned && cfg != null)
                    UnityEngine.Object.DestroyImmediate(cfg);
            }
            catch
            {
                raw = Default;
            }
            return ClampRaw(raw);
        }

        static Vector2 ClampRaw(Vector2 raw)
        {
            float lo = raw.x;
            float hi = raw.y;
            if (lo > hi)
            {
                float t = lo;
                lo = hi;
                hi = t;
            }
            if (lo <= 0f || hi <= 0f) return Default;
            if (hi > 100f) hi = 100f;
            if (lo > hi) return Default;
            return new Vector2(lo, hi);
        }

        /// <summary>캐릭터 속성 탭. QA_NO면 빈 문자열(옛 화면 = 소각 줄 없음).</summary>
        public static string Line()
        {
            if (Blocked) return "";
            Vector2 r = Range();
            return $"소각 목표 {Fmt(r.x)}~{Fmt(r.y)}%(§18-2)";
        }

        static string Fmt(float v) => v.ToString("0.##");

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
            _cached = new Vector2(-1f, -1f);
            _qaSeeded = false;
        }
    }
}
