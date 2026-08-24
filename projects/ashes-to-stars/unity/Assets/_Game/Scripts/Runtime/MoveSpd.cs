using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §18-11 플레이어 이동 속도. <see cref="BalanceConfig.플레이어이동속도"/>의 런타임 소비처.
    /// 에셋 기본 4.2가 authored돼 있으면서도 grep 소비처가 0곳이었다 —
    /// W2Arena.MoveSpeed·W3Party.PlayerSpeed가 4.2를 하드코딩했다.
    /// QA_NO면 옛 4.2·줄 없음. 표시 줄 + 읽기. W3Party·W2 손맛은 안 건드린다.
    /// </summary>
    public static class MoveSpd
    {
        public const string EnvShow = "QA_MOVE_SPD";
        public const string EnvNo = "QA_NO_MOVE_SPD";
        public const float Default = 4.2f;

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

        /// <summary>§18-11 앵커. 에셋 기본값 4.2. 차단이면 옛 하드코드 4.2.</summary>
        public static float Units()
        {
            if (Blocked) return Default;
            if (ForceConfig != null)
                return ClampRaw(ForceConfig.플레이어이동속도);
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
                if (cfg != null && cfg.플레이어이동속도 > 0f)
                    raw = cfg.플레이어이동속도;
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

        /// <summary>캐릭터 속성 탭. QA_NO면 빈 문자열(옛 화면 = 이동 줄 없음).</summary>
        public static string Line()
        {
            if (Blocked) return "";
            return $"이동 {Fmt(Units())}(§18-11)";
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
            _cached = -1f;
            _qaSeeded = false;
        }
    }
}
