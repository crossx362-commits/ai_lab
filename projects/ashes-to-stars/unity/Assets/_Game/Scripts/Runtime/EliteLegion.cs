using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §10-2 정예 「군단장」 — 버퍼 역할의 거울(✅ 오너 결정 2026-08-13).
    /// 주변 잡몹에 **공속·이속 증가** 버프를 두른다. 방치하면 물량 압력이 급상승하므로
    /// 「처치 우선순위 2순위」(수호자·주술사 다음)로 끊는 것이 정답이 된다.
    ///
    /// 전투 소비처는 <see cref="W3Party"/>의 이동(TickMobs의 spd)과 근접·원거리 공격
    /// 쿨다운이다 — 오라 안 잡몹의 이동 속도에 이속 배율을 곱하고, 공격 쿨다운을
    /// 공속 배율로 나눈다(배율이 클수록 쿨이 짧아져 더 자주 때린다).
    /// QA_NO_ELITE_LEGION이면 두 배율이 전부 1로 돌아가 옛 동작이 된다(종별 네거티브
    /// 컨트롤, INBOX 2026-08-26 20:40).
    ///
    /// 원장(§10-2)은 「공속·이속 증가」만 확정하고 **수치는 미확정**이라 BalanceConfig에
    /// 노출하고 아래 기본값에 「원장 미확정」으로 명시한다. <see cref="NearbyMul"/>는
    /// 설정을 안 읽는 순수 함수라 TickMobs 핫패스에서 매 프레임 호출해도 싸고,
    /// SelfCheck가 합성 좌표로 그대로 검증한다.
    /// </summary>
    public static class EliteLegion
    {
        public const string EnvNo = "QA_NO_ELITE_LEGION";

        // 원장 미확정 — §10-2는 「공속·이속 증가」만 확정, 수치는 BalanceConfig에서.
        public const float DefaultAuraRadius = 4.0f;    // 오라 반경(유닛)
        public const float DefaultAtkSpdMul = 1.5f;     // 오라 안 잡몹 공속 배율(50% 빠르게)
        public const float DefaultMoveMul = 1.3f;       // 오라 안 잡몹 이속 배율(30% 빠르게)

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
        public static float AuraRadius() => Mathf.Max(0.1f, Read(c => c.군단장오라반경, DefaultAuraRadius));

        /// <summary>오라 안 잡몹의 공속 배율(≥1). 차단이면 1(버프 없음).</summary>
        public static float AtkSpdMul() =>
            Blocked ? 1f : Mathf.Max(1f, Read(c => c.군단장주변공속배율, DefaultAtkSpdMul));

        /// <summary>오라 안 잡몹의 이속 배율(≥1). 차단이면 1(버프 없음).</summary>
        public static float MoveMul() =>
            Blocked ? 1f : Mathf.Max(1f, Read(c => c.군단장주변이속배율, DefaultMoveMul));

        /// <summary>
        /// 대상이 받는 버프 배율(순수 함수 — 설정을 안 읽는다). 호출부가 캐시한 수치를 넘긴다.
        /// - 대상이 군단장 자신이면 1(자기 버프 없음 — 느린 지휘, 먼저 잡으라).
        /// - 그 밖의 잡몹은 살아있는 군단장 오라 반경 안이면 <paramref name="mul"/>.
        /// - 오라 밖이면 1. 군단장이 없으면 1.
        /// 이속·공속 두 축 모두 이 함수 하나로 처리한다(호출부가 각 축의 mul을 넘긴다).
        /// </summary>
        public static float NearbyMul(Vector2 targetPos, bool targetIsCommander,
                                      IReadOnlyList<Vector2> commanderPositions, int commanderCount,
                                      float radius, float mul)
        {
            if (targetIsCommander) return 1f;
            if (commanderPositions == null || commanderCount <= 0) return 1f;
            float r2 = radius * radius;
            for (int i = 0; i < commanderCount && i < commanderPositions.Count; i++)
                if ((commanderPositions[i] - targetPos).sqrMagnitude <= r2)
                    return mul;
            return 1f;
        }
    }
}
