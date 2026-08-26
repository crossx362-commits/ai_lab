using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §10-2 정예 「처형자」 — 딜(딜러) 역할의 거울(✅ 오너 결정 2026-08-13).
    /// 후열(힐·버퍼)로 곧장 돌진해 명중 시 **폭딜**을 꽂는다(수호자가 탱, 군단장이 버퍼,
    /// 저주술사가 디버퍼의 거울이듯 이쪽은 딜러의 거울이다). 방치하면 힐러가 먼저 터지므로
    /// 「진로를 몸으로 차단(도발)하거나 먼저 처치」가 정답이 된다(RoleTip: "딜 · 진로 차단").
    ///
    /// 전투 소비처는 <see cref="W3Party"/>의 근접 피해 관문(TickMobs — 처형자 명중에 폭딜
    /// 배율을 곱함)과 이동(돌진 spd)이다. 타깃 선택은 후열 저격(PickBackline)을 원거리형과
    /// 공유하고, 탱 도발이 그 안에서 하드 락으로 진로를 끊는다(딜 거울의 정답). QA_NO_ELITE_
    /// EXECUTIONER이면 폭딜 배율이 1로 돌아가 옛 동작(폭딜 없음)이 된다(종별 네거티브 컨트롤).
    ///
    /// 원장(§10-2)은 「후열 돌진 폭딜」만 확정하고 **수치는 미확정**이라 BalanceConfig에
    /// 노출하고 아래 기본값에 「원장 미확정」으로 명시한다. <see cref="DamageMul"/>는
    /// 설정을 안 읽는 순수 함수라 근접 피해 핫패스에서 매 명중 호출해도 싸고,
    /// SelfCheck가 그대로 검증한다.
    /// </summary>
    public static class EliteExecutioner
    {
        public const string EnvNo = "QA_NO_ELITE_EXECUTIONER";

        // 원장 미확정 — §10-2는 「후열 돌진 폭딜」만 확정, 수치는 BalanceConfig에서.
        public const float DefaultBurstMul = 3.0f;   // 후열 명중 폭딜 배율(≥1)
        public const float DefaultRushMul = 0.95f;   // 돌진 이동 배율(플레이어 대비, 정예 중 가장 빠름)

        /// <summary>SelfCheck가 필드 소비를 증명할 때만 주입한다(그 외엔 새 인스턴스에서 읽음).</summary>
        public static BalanceConfig ForceConfig;

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        static float Read(Func<BalanceConfig, float> pick, float dflt)
        {
            float raw = dflt;
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
                {
                    float v = pick(cfg);
                    if (v > 0f) raw = v;
                }
                if (owned && cfg != null)
                    UnityEngine.Object.DestroyImmediate(cfg);
            }
            catch
            {
                raw = dflt;
            }
            return raw;
        }

        /// <summary>후열 명중 폭딜 배율(≥1). 차단이면 1(폭딜 없음).</summary>
        public static float BurstMul() =>
            Blocked ? 1f : Mathf.Max(1f, Read(c => c.처형자폭딜배율, DefaultBurstMul));

        /// <summary>돌진 이동 배율(플레이어 대비). 항상 양수. 폭딜과 달리 차단해도 이동은 그대로.</summary>
        public static float RushMul() => Mathf.Clamp(Read(c => c.처형자돌진속도배율, DefaultRushMul), 0.1f, 3f);

        /// <summary>
        /// 처형자의 근접 명중이 받는 폭딜 배율(순수 함수 — 설정을 안 읽는다). 호출부가 캐시한 수치를 넘긴다.
        /// - 명중원이 처형자면 <paramref name="burstMul"/>(≥1).
        /// - 그 밖의 몹이면 1(무변). 잡몹 명중이 이 함수를 타도 배율이 안 붙게 대칭용 시그니처.
        /// </summary>
        public static float DamageMul(bool isExecutioner, float burstMul)
        {
            if (!isExecutioner) return 1f;
            return burstMul < 1f ? 1f : burstMul;
        }
    }
}
