using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §10-2 정예 「수호자」 — 탱 역할의 거울(✅ 오너 결정 2026-08-13).
    /// 주변 잡몹에 **피해 감소 오라**를 두르고 **자신도 고방어**라, 방치하면 주변 몹이
    /// 안 죽는다 → 「오라 밖으로 유인하거나 먼저 처치」가 정답이 된다.
    ///
    /// 전투 소비처는 <see cref="W3Party"/>의 단일 피해 관문 DamageMob이다 — 잡몹이 받는
    /// 피해에 이 배율을 곱한다. QA_NO_ELITE_GUARDIAN이면 오라·고방어가 전부 꺼져 옛
    /// 동작(배율 1)으로 돌아간다(종별 네거티브 컨트롤, INBOX 2026-08-26 20:40).
    ///
    /// 원장(§10-2)은 「피해 감소 오라·고방어」만 확정하고 **수치는 미확정**이라
    /// BalanceConfig에 노출하고 아래 기본값에 「원장 미확정」으로 명시한다.
    /// <see cref="Multiplier"/>는 설정을 안 읽는 순수 함수라 DamageMob 핫패스에서
    /// 매 타 호출해도 싸고, SelfCheck가 합성 좌표로 그대로 검증한다.
    /// </summary>
    public static class EliteGuardian
    {
        public const string EnvNo = "QA_NO_ELITE_GUARDIAN";

        // 원장 미확정 — §10-2는 「피해 감소 오라·고방어」만 확정, 수치는 BalanceConfig에서.
        public const float DefaultAuraRadius = 3.5f;      // 오라 반경(유닛)
        public const float DefaultNearbyTakenMul = 0.5f;  // 오라 안 잡몹이 받는 피해 배율(50% 감소)
        public const float DefaultSelfTakenMul = 0.4f;    // 수호자 자신이 받는 피해 배율=고방어(60% 감소)

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
        public static float AuraRadius() => Mathf.Max(0.1f, Read(c => c.수호자오라반경, DefaultAuraRadius));

        /// <summary>오라 안 잡몹이 받는 피해 배율(0~1). 차단이면 1(감소 없음).</summary>
        public static float NearbyTakenMul() =>
            Blocked ? 1f : Mathf.Clamp01(Read(c => c.수호자주변피해배율, DefaultNearbyTakenMul));

        /// <summary>수호자 자신이 받는 피해 배율(0~1)=고방어. 차단이면 1(감소 없음).</summary>
        public static float SelfTakenMul() =>
            Blocked ? 1f : Mathf.Clamp01(Read(c => c.수호자자체피해배율, DefaultSelfTakenMul));

        /// <summary>
        /// 대상이 받는 피해 배율(순수 함수 — 설정을 안 읽는다). 호출부가 캐시한 수치를 넘긴다.
        /// - 대상이 수호자면 <paramref name="selfMul"/>(고방어)만 적용한다(오라와 이중 감소 안 함).
        /// - 그 밖의 잡몹은 살아있는 수호자 오라 반경 안이면 <paramref name="nearbyMul"/>.
        /// - 오라 밖이면 1. 수호자가 없으면 1.
        /// </summary>
        public static float Multiplier(Vector2 targetPos, bool targetIsGuardian,
                                       IReadOnlyList<Vector2> guardianPositions, int guardianCount,
                                       float radius, float nearbyMul, float selfMul)
        {
            if (targetIsGuardian) return selfMul;
            if (guardianPositions == null || guardianCount <= 0) return 1f;
            float r2 = radius * radius;
            for (int i = 0; i < guardianCount && i < guardianPositions.Count; i++)
                if ((guardianPositions[i] - targetPos).sqrMagnitude <= r2)
                    return nearbyMul;
            return 1f;
        }
    }
}
