using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §4 부활초 소지 상한. <see cref="BalanceConfig.부활초소지상한"/>의 런타임 소비처.
    /// 에셋 기본 3이 authored돼 있으면서도 grep 소비처가 0곳이었다 —
    /// <c>Economy.ItemCapacity</c>가 3을 하드코딩하고 가방·파티 HUD가 그 상수를 그렸다.
    /// QA_NO면 옛 3(하드코드). 표시 줄 + 가방 상한. W3Party 무접촉.
    /// </summary>
    public static class ReviveCap
    {
        public const string EnvShow = "QA_REVIVE_CAP";
        public const string EnvNo = "QA_NO_REVIVE_CAP";
        public const int Default = 3;

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

        /// <summary>§4 앵커. 에셋 기본값 3. 차단이면 옛 하드코드 3.</summary>
        public static int Limit()
        {
            if (Blocked) return Default;
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
                if (cfg != null && cfg.부활초소지상한 > 0)
                    raw = cfg.부활초소지상한;
                if (owned && cfg != null)
                    UnityEngine.Object.DestroyImmediate(cfg);
            }
            catch
            {
                raw = Default;
            }
            return raw < 1 ? 1 : raw;
        }

        /// <summary>캐릭터 속성 탭. QA_NO면 빈 문자열(옛 화면 = 상한 줄 없음).</summary>
        public static string Line()
        {
            if (Blocked) return "";
            return $"부활초 소지 상한 {Limit()}(§4)";
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
