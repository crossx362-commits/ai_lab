// 나이스샷 → 스킬포인트 → SS(필살기) 규칙
//
// 원작 포트리스2:
//   - 파워 게이지 0~1 왕복 중 플레이어가 "원하는 지점"을 미리 표시
//   - 게이지가 정확히 그 지점에 멈추면 나이스샷 성공 → 스킬포인트 +1
//   - 2점 이상이면 SS(궁극기) 발동 가능
//
// 이 라이브러리:
//   - AI도 확률적으로 나이스샷 성공(난이도별 확률)
//   - SS = "강화된 2번탄" [추정] (원작 스킬 명세 미확인)
//   - 특수탄 3발 제한은 이 라이브러리로 통합됨

using System;

namespace Tankfall.Sim
{
    /// <summary>
    /// 나이스샷 판정 및 SS 규칙.
    /// </summary>
    public static class NiceShot
    {
        /// <summary>
        /// 파워 0~1 기준 허용 오차. 표시 지점 ±이 범위 안이면 나이스샷 성공.
        /// [추정] 원작 명세 없어 게임플레이(조작성)와 AI 균형에서 도출.
        /// </summary>
        public const float Tolerance = 0.015f;

        /// <summary>SS 발동에 필요한 스킬포인트.</summary>
        public const int SsCost = 2;

        /// <summary>
        /// 나이스샷 판정. 플레이어가 표시한 파워와 실제 발사 파워 비교.
        /// </summary>
        /// <param name="markedPower">플레이어가 미리 표시한 파워(0~1)</param>
        /// <param name="releasedPower">실제 발사 파워(0~1)</param>
        /// <returns>표시 지점 ±Tolerance 안이면 true</returns>
        public static bool Judge(float markedPower, float releasedPower)
        {
            float diff = Math.Abs(markedPower - releasedPower);
            return diff <= Tolerance;
        }

        /// <summary>
        /// AI 나이스샷 성공 확률. 난이도(오차)가 작을수록 높아야 한다.
        /// [추정] 포트리스 원작은 AI도 간헐적으로 나이스샷을 맞힌다 (작업 설명에서).
        ///
        /// 모델: 초보(0.04)가 최소 5% ~ 에이스(0.0075)가 95% 정도의 성공률을 갖도록 설정.
        /// 시그모이드 곡선으로 확률을 계산해 부드러운 난이도 곡선을 만든다.
        ///
        /// 식: prob = 1 / (1 + exp(-6 * (1 - errorRatio / ErrorNovice)))
        ///
        /// 예:
        ///   - errorRatio=0.0075(Ace)   → 96%
        ///   - errorRatio=0.015(Expert) → 85%
        ///   - errorRatio=0.025(Normal) → 60%
        ///   - errorRatio=0.040(Novice) → 9%
        /// </summary>
        /// <param name="errorRatio">난이도 오차(AiGunner의 ErrorNovice 등)</param>
        /// <param name="rng">결정론적 난수</param>
        /// <returns>true면 나이스샷 성공</returns>
        /// <summary>
        /// AI 나이스샷 확률 [추정]. 처음 시그모이드는 보통 88%·전문가 97% 로 **거의 매번 성공**해
        /// SS 가 상시 무기가 됐다. 나이스샷은 원작에서 잘하는 사람도 "가끔"이다. 선형으로 낮춘다:
        ///   초보(0.04) 10% · 보통(0.025) 44% · 전문가(0.015) 66% · 에이스(0.0075) 83%
        /// </summary>
        public static bool AiJudge(float errorRatio, ref Rng rng)
        {
            float normalized = (AiGunner.ErrorNovice - errorRatio) / AiGunner.ErrorNovice;   // 초보 0 … 에이스 0.81
            normalized = Math.Max(0f, Math.Min(1f, normalized));
            float prob = 0.10f + 0.90f * normalized;
            return rng.Float01() < prob;
        }

        public static TankStats ApplySs(TankStats specialShell)
        {
            var ss = specialShell;
            ss.BaseDamage *= 1.6f;
            ss.DirectDamage *= 1.6f;
            ss.BlastRadius *= 1.3f;
            ss.Name = "[SS] " + ss.Name;
            return ss;
        }
    }

    /// <summary>
    /// 턴 동안 누적되는 스킬포인트와 SS 사용 여부 관리.
    /// 라운드별로 초기화된다 (§2-5-1 턴 순서 정의에서).
    /// </summary>
    public struct SkillGauge
    {
        /// <summary>현재 스킬포인트 (0 이상).</summary>
        public int Points;

        /// <summary>
        /// 나이스샷 한 번 발생. 포인트를 1 증가시킨다.
        /// </summary>
        public void OnNiceShot()
        {
            Points++;
        }

        /// <summary>
        /// SS 발동이 가능한가 (포인트 >= SsCost).
        /// </summary>
        public bool CanSs()
        {
            return Points >= NiceShot.SsCost;
        }

        /// <summary>
        /// SS를 사용한다. 포인트를 SsCost만큼 감소시킨다.
        /// ⚠️ CanSs()를 먼저 확인하고 호출하라.
        /// </summary>
        public void SpendSs()
        {
            Points = Math.Max(0, Points - NiceShot.SsCost);
        }
    }
}
