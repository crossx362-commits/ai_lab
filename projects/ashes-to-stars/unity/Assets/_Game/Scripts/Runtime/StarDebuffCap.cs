using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §18-13 디버프 중첩 최대 별 수(담합 방지 §14).
    /// <see cref="BalanceConfig.디버프중첩별상한"/>의 런타임 소비처.
    /// 원장 기본 2가 authored인데 WorldStar는 자기 별 0/1만 있어 중첩 모델이 없었다.
    /// 숫자 2를 소비처로 닫기 위해 Cap·Apply를 둔다. 표시 줄 + Min(중첩, 상한).
    /// QA_NO면 옛 동작(상한 줄 없음·Apply는 요청 그대로). W3Party 무접촉.
    /// </summary>
    public static class StarDebuffCap
    {
        public const string EnvShow = "QA_STAR_DEBUFF_CAP";
        public const string EnvNo = "QA_NO_STAR_DEBUFF_CAP";
        public const int Default = 2;

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

        /// <summary>§18-13 앵커. 에셋 기본값 2. 차단이어도 숫자는 읽는다(Apply만 옛 무제한).</summary>
        public static int Cap()
        {
            if (ForceConfig != null)
                return ClampRaw(ForceConfig.디버프중첩별상한);
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
                if (cfg != null && cfg.디버프중첩별상한 > 0)
                    raw = cfg.디버프중첩별상한;
                if (cfg != null)
                    UnityEngine.Object.DestroyImmediate(cfg);
            }
            catch
            {
                raw = Default;
            }
            return ClampRaw(raw);
        }

        static int ClampRaw(int raw) => raw < 1 ? Default : raw;

        /// <summary>중첩 수를 상한으로 자른다. 차단이면 요청 그대로(옛 무제한).</summary>
        public static int Apply(int count)
        {
            if (count < 0) count = 0;
            if (Blocked) return count;
            int cap = Cap();
            return count > cap ? cap : count;
        }

        /// <summary>월드맵 부제. QA_NO면 빈 문자열(옛 화면 = 상한 줄 없음).</summary>
        public static string Line()
        {
            if (Blocked) return "";
            return $"디버프 중첩 최대 {Cap()}개 별(§18-13)";
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
