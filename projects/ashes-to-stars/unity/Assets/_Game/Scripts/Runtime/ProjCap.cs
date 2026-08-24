using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §10-9 투사체 상한. <see cref="BalanceConfig.투사체상한"/>의 런타임 소비처.
    /// 에셋 기본 200이 authored돼 있으면서도 grep 소비처가 0곳이었다 —
    /// <c>StressTest.ProjectileBudget</c>이 200을 하드코딩했다.
    /// QA_NO면 옛 200. 표시 줄 + W1 풀 크기. W3Party 무접촉.
    /// </summary>
    public static class ProjCap
    {
        public const string EnvShow = "QA_PROJ_CAP";
        public const string EnvNo = "QA_NO_PROJ_CAP";
        public const int Default = 200;

        /// <summary>SelfCheck가 필드 소비를 증명할 때만.</summary>
        public static BalanceConfig ForceConfig;

        static int _cached = -1;
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

        /// <summary>§10-9 앵커. 에셋 기본값 200. 차단이면 옛 하드코드 200. 캐시해서 풀 생성이 SO를 매 프레임 안 만든다.</summary>
        public static int Limit()
        {
            if (Blocked) return Default;
            if (ForceConfig != null)
                return ClampRaw(ForceConfig.투사체상한);
            if (_cached > 0) return _cached;
            _cached = Read();
            return _cached;
        }

        static int Read()
        {
            int raw = Default;
            try
            {
                var cfg = ScriptableObject.CreateInstance<BalanceConfig>();
                if (cfg != null && cfg.투사체상한 > 0)
                    raw = cfg.투사체상한;
                if (cfg != null)
                    UnityEngine.Object.DestroyImmediate(cfg);
            }
            catch
            {
                raw = Default;
            }
            return ClampRaw(raw);
        }

        static int ClampRaw(int raw) => raw < 1 ? 1 : raw;

        /// <summary>요청 수를 상한으로 자른다. 차단이면 옛 200 상한.</summary>
        public static int Clamp(int requested)
        {
            if (requested < 0) requested = 0;
            int cap = Limit();
            return requested > cap ? cap : requested;
        }

        /// <summary>캐릭터 속성 탭. QA_NO면 빈 문자열(옛 화면 = 상한 줄 없음).</summary>
        public static string Line()
        {
            if (Blocked) return "";
            return $"투사체 상한 {Limit()}(§10-9)";
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
            _cached = -1;
            _qaSeeded = false;
        }
    }
}
