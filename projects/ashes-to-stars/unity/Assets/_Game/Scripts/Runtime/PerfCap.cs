using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §10-9 잡몹 상한. <see cref="BalanceConfig.잡몹상한"/>의 런타임 소비처.
    /// 에셋 기본 500이 authored돼 있으면서도 grep 소비처가 0곳이었다 —
    /// <c>DungeonGenerator.MobHardCap</c>이 500을 하드코딩했다(주석만 에셋을 가리킴).
    /// QA_NO면 옛 500. 표시 줄 + 던전 웨이브 클램프. W3Party 무접촉.
    /// </summary>
    public static class PerfCap
    {
        public const string EnvShow = "QA_PERF_CAP";
        public const string EnvNo = "QA_NO_PERF_CAP";
        public const int Default = 500;

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

        /// <summary>§10-9 앵커. 에셋 기본값 500. 차단이면 옛 하드코드 500. 캐시해서 1만 시드 경로가 CreateInstance를 매 웨이브마다 안 돌린다.</summary>
        public static int MobLimit()
        {
            if (Blocked) return Default;
            if (ForceConfig != null)
                return Clamp(ForceConfig.잡몹상한);
            if (_cached > 0) return _cached;
            _cached = ReadMob();
            return _cached;
        }

        static int ReadMob()
        {
            int raw = Default;
            try
            {
                var cfg = ScriptableObject.CreateInstance<BalanceConfig>();
                if (cfg != null && cfg.잡몹상한 > 0)
                    raw = cfg.잡몹상한;
                if (cfg != null)
                    UnityEngine.Object.DestroyImmediate(cfg);
            }
            catch
            {
                raw = Default;
            }
            return Clamp(raw);
        }

        static int Clamp(int raw) => raw < 1 ? 1 : raw;

        /// <summary>캐릭터 속성 탭. QA_NO면 빈 문자열(옛 화면 = 상한 줄 없음).</summary>
        public static string Line()
        {
            if (Blocked) return "";
            return $"잡몹 상한 {MobLimit()}(§10-9)";
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
