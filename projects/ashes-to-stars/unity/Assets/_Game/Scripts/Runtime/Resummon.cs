using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §18-14 소환수 재소환. 원장 수치 그대로: 비용 0.5 G/h(티어1 = 50실버) · 쿨다운 30초.
    /// 소환사의 소환수는 본체 공격 사이에 추가 타격을 넣는 소환 슬롯(W3Party 게이지)이라
    /// 개체·죽음이 아직 없다 — 그래서 이번 바퀴는 소비처0 해소 패턴(Reader+속성 탭+QA_NO
    /// 네거티브) 준용으로 수치·문구·차단만 만든다. 개체 도입은 W3Party 접촉이라 별도 심의.
    /// QA_NO_RESUMMON이면 옛 동작(줄 없음). 티어 배율은 원장이 T1 고정만 명시해 미적용(§21-3).
    /// </summary>
    public static class Resummon
    {
        public const string EnvShow = "QA_RESUMMON";
        public const string EnvNo = "QA_NO_RESUMMON";
        public const float DefaultSilver = 50f;
        public const float DefaultCooldown = 30f;

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

        /// <summary>§18-14 재소환 비용(실버, 정수). 차단이면 옛 동작이라 값은 안 쓴다.</summary>
        public static int CostSilver()
        {
            if (Blocked) return (int)DefaultSilver;
            float raw = DefaultSilver;
            try
            {
                BalanceConfig cfg = ForceConfig;
                bool owned = false;
                if (cfg == null)
                {
                    cfg = ScriptableObject.CreateInstance<BalanceConfig>();
                    owned = cfg != null;
                }
                if (cfg != null && cfg.재소환비용실버 > 0f)
                    raw = cfg.재소환비용실버;
                if (owned && cfg != null)
                    UnityEngine.Object.DestroyImmediate(cfg);
            }
            catch
            {
                raw = DefaultSilver;
            }
            return raw < 1f ? (int)DefaultSilver : Mathf.RoundToInt(raw);
        }

        /// <summary>§18-14 재소환 쿨다운(초).</summary>
        public static float CooldownSeconds()
        {
            if (Blocked) return DefaultCooldown;
            float raw = DefaultCooldown;
            try
            {
                BalanceConfig cfg = ForceConfig;
                bool owned = false;
                if (cfg == null)
                {
                    cfg = ScriptableObject.CreateInstance<BalanceConfig>();
                    owned = cfg != null;
                }
                if (cfg != null && cfg.재소환쿨다운초 > 0f)
                    raw = cfg.재소환쿨다운초;
                if (owned && cfg != null)
                    UnityEngine.Object.DestroyImmediate(cfg);
            }
            catch
            {
                raw = DefaultCooldown;
            }
            return raw <= 0f ? DefaultCooldown : raw;
        }

        /// <summary>캐릭터 속성 탭. QA_NO면 빈 문자열(옛 화면 = 재소환 줄 없음).</summary>
        public static string Line()
        {
            if (Blocked) return "";
            return $"소환수 재소환 {CostSilver()}실버 · 쿨다운 {Mathf.RoundToInt(CooldownSeconds())}초(§18-14)";
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
