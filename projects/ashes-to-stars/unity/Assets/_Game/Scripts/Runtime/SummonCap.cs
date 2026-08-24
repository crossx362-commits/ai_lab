using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §10-9 소환수 상한. <see cref="BalanceConfig.소환수상한"/>의 런타임 소비처.
    /// 에셋 기본 50이 authored돼 있으면서도 grep 소비처가 0곳이었다 —
    /// 보스 분리 소환이 요청 수를 그대로 넘겼다.
    /// QA_NO면 옛 무제한(클램프 없음·상한 줄 없음). 표시 줄 + 보스 소환 클램프.
    /// W3Party 무접촉.
    /// </summary>
    public static class SummonCap
    {
        public const string EnvShow = "QA_SUMMON_CAP";
        public const string EnvNo = "QA_NO_SUMMON_CAP";
        public const int Default = 50;

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

        /// <summary>§10-9 앵커. 에셋 기본값 50. 차단이면 옛 무제한이라 Limit는 안 쓴다.</summary>
        public static int Limit()
        {
            if (Blocked) return int.MaxValue;
            int raw = Default;
            try
            {
                BalanceConfig cfg = ForceConfig;
                bool owned = false;
                if (cfg == null)
                {
                    cfg = ScriptableObject.CreateInstance<BalanceConfig>();
                    owned = cfg != null;
                }
                if (cfg != null && cfg.소환수상한 > 0)
                    raw = cfg.소환수상한;
                if (owned && cfg != null)
                    UnityEngine.Object.DestroyImmediate(cfg);
            }
            catch
            {
                raw = Default;
            }
            return raw < 1 ? 1 : raw;
        }

        /// <summary>요청 수를 상한으로 자른다. 차단이면 요청 그대로(옛 무제한).</summary>
        public static int Clamp(int requested)
        {
            if (requested < 0) requested = 0;
            if (Blocked) return requested;
            int cap = Limit();
            return requested > cap ? cap : requested;
        }

        /// <summary>캐릭터 속성 탭. QA_NO면 빈 문자열(옛 화면 = 상한 줄 없음).</summary>
        public static string Line()
        {
            if (Blocked) return "";
            return $"소환수 상한 {Limit()}(§10-9)";
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
            _qaSeeded = false;
        }
    }
}
