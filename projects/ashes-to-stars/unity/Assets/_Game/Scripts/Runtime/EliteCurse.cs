using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §10-2 정예 「저주술사」 — 디버퍼 역할의 거울(✅ 오너 결정 2026-08-13).
    /// 오라 안 **파티**에 회복량·이속 **감소** 저주를 건다(군단장·수호자가 잡몹을 강화하는
    /// 것과 반대로, 이쪽은 플레이어를 약화한다). 방치하면 힐이 마르고 발이 묶여 무너지므로
    /// 「사거리 밖으로 이탈한 뒤 처치」가 정답이 된다.
    ///
    /// 전투 소비처는 <see cref="W3Party"/>의 회복 단일 관문(Heal — amount에 회복 배율을 곱함)과
    /// 파티 이동(TickParty의 step — 이속 배율을 곱함)이다. QA_NO_ELITE_CURSE이면 두 배율이
    /// 전부 1로 돌아가 옛 동작(저주 없음)이 된다(종별 네거티브 컨트롤, INBOX 2026-08-26 20:40).
    ///
    /// 원장(§10-2)은 「회복량·이속 감소」만 확정하고 **수치는 미확정**이라 BalanceConfig에
    /// 노출하고 아래 기본값에 「원장 미확정」으로 명시한다. <see cref="NearbyMul"/>는
    /// 설정을 안 읽는 순수 함수라 회복·이동 핫패스에서 매 프레임 호출해도 싸고,
    /// SelfCheck가 합성 좌표로 그대로 검증한다.
    /// </summary>
    public static class EliteCurse
    {
        public const string EnvNo = "QA_NO_ELITE_CURSE";

        // 원장 미확정 — §10-2는 「회복량·이속 감소」만 확정, 수치는 BalanceConfig에서.
        // INBOX 2026-08-26 20:40 제안 시작값: 회복량 −30%·이속 −15%.
        public const float DefaultAuraRadius = 4.0f;    // 오라 반경(유닛)
        public const float DefaultHealMul = 0.7f;       // 오라 안 파티 회복 배율(30% 감소)
        public const float DefaultMoveMul = 0.85f;      // 오라 안 파티 이속 배율(15% 감소)

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

        /// <summary>오라 반경(유닛). 항상 양수.</summary>
        public static float AuraRadius() => Mathf.Max(0.1f, Read(c => c.저주술사오라반경, DefaultAuraRadius));

        /// <summary>오라 안 파티가 받는 회복 배율(0~1). 차단이면 1(저주 없음).</summary>
        public static float HealMul() =>
            Blocked ? 1f : Mathf.Clamp01(Read(c => c.저주술사회복배율, DefaultHealMul));

        /// <summary>오라 안 파티 이속 배율(0~1). 차단이면 1(저주 없음).</summary>
        public static float MoveMul() =>
            Blocked ? 1f : Mathf.Clamp01(Read(c => c.저주술사이속배율, DefaultMoveMul));

        /// <summary>
        /// 대상(파티원)이 받는 저주 배율(순수 함수 — 설정을 안 읽는다). 호출부가 캐시한 수치를 넘긴다.
        /// - 대상이 저주술사 자신이면 1(파티는 저주술사가 될 수 없으나 시그니처 대칭용).
        /// - 그 밖의 대상은 살아있는 저주술사 오라 반경 안이면 <paramref name="mul"/>(＜1).
        /// - 오라 밖이면 1. 저주술사가 없으면 1.
        /// 회복·이속 두 축 모두 이 함수 하나로 처리한다(호출부가 각 축의 mul을 넘긴다).
        /// </summary>
        public static float NearbyMul(Vector2 targetPos, bool targetIsCurser,
                                      IReadOnlyList<Vector2> curserPositions, int curserCount,
                                      float radius, float mul)
        {
            if (targetIsCurser) return 1f;
            if (curserPositions == null || curserCount <= 0) return 1f;
            float r2 = radius * radius;
            for (int i = 0; i < curserCount && i < curserPositions.Count; i++)
                if ((curserPositions[i] - targetPos).sqrMagnitude <= r2)
                    return mul;
            return 1f;
        }
    }
}
