using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §18-12 동시 건설 슬롯. 원장 수치 그대로: 2개(본성 라인 1 + 그 외 1).
    /// 본성(Keep)은 전용 슬롯이라 언제나 착공 가능하고, 그 외 핵심 건물은
    /// 「그 외 슬롯」(= Cap-1 = 1)을 공유한다 — 하나가 공사 중이면 다른 건물은 못 짓는다.
    /// 소비처: EstateBuild.WhyCannotUpgrade(비-본성)이 OtherSlots()로 착공을 막는다.
    /// QA_NO_BUILD_SLOTS면 옛 동작(칸마다 병렬 공사 OK).
    /// 수치 튜닝(§21-3) 방어: 0 이하 값은 원장 기본(2)으로 폴백한다.
    /// </summary>
    public static class BuildSlots
    {
        public const string EnvNo = "QA_NO_BUILD_SLOTS";
        public const int DefaultCap = 2;

        /// <summary>SelfCheck가 필드 소비를 증명할 때만.</summary>
        public static BalanceConfig ForceConfig;

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>§18-12 동시 건설 슬롯 상한(본성 라인 포함, ≥1).</summary>
        public static int Cap()
        {
            int cap = DefaultCap;
            try
            {
                BalanceConfig cfg = ForceConfig;
                bool owned = false;
                if (cfg == null)
                {
                    cfg = ScriptableObject.CreateInstance<BalanceConfig>();
                    owned = cfg != null;
                }
                if (cfg != null && cfg.동시건설슬롯 > 0)
                    cap = cfg.동시건설슬롯;
                if (owned && cfg != null)
                    UnityEngine.Object.DestroyImmediate(cfg);
            }
            catch
            {
                cap = DefaultCap;
            }
            return cap < 1 ? DefaultCap : cap;
        }

        /// <summary>본성 전용 슬롯을 뺀 「그 외」 동시 공사 상한(≥0).</summary>
        public static int OtherSlots()
        {
            int o = Cap() - 1;
            return o < 0 ? 0 : o;
        }

        public static void ResetForTest()
        {
            ForceConfig = null;
        }
    }
}
