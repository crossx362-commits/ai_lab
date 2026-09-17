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
        /// 궁극기(§49 "궁극 충전") 발동에 필요한 스킬포인트.
        ///
        /// 출처(조사 2026-09-17): 원작 나이스샷 시스템은 **단계가 여럿이다** —
        /// "스킬포인트가 두 개 이상 모이면 스킬 버튼이 활성화되고, **포인트를 모을수록 더 강한 스킬**을 쓸 수 있다"
        /// (gamemeca.com/view.php?gid=119102). 우리 구현은 SS 한 단계뿐이라 그 축이 절반만 있었다.
        /// 기획서 §49 의 "궁극 충전"을 이 상위 단계로 만든다 — 새 자원을 발명하지 않고 **같은 게이지를 더 모은다.**
        ///
        /// ⚠️ 4 는 [추정]이다. 2(SS)와 너무 가까우면 SS 를 아무도 안 쓰고, 멀면 한 판에 한 번도 못 본다.
        ///    원작에 구체적 수치가 없어 §2-9 처럼 실측으로 조정해야 한다(AI 자가대전 발동 횟수를 보라).
        /// </summary>
        /// 실측(2026-09-17): 4점 + 아끼기 50% 로는 **궁극/판 0.01** — 100판에 한 번이라 없는 기능과 같았다.
        /// 판이 14~19턴이고 AI 나이스샷 성공이 중급 44% 라 4점은 사실상 도달 불가다. 3점으로 낮춘다
        /// (원작 근거 "2개 이상이면 스킬 활성화, 모을수록 더 강한 스킬" 과도 어긋나지 않는다).
        public const int UltimateCost = 3;

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

        /// <summary>
        /// AI 가 **SS 를 아껴 궁극기까지 모을지** 결정한다.
        ///
        /// ⚠️ 2026-09-17 사고: 이게 없을 때 궁극기는 **한 번도 발동하지 않았다.**
        ///    AI 는 포인트가 SsCost(2) 가 되는 순간 SS 를 써서 0 으로 되돌렸고,
        ///    그래서 UltimateCost(4) 에 **영원히 도달할 수 없었다.**
        ///    하네스에서 궁극기 ON/OFF 승률이 **숫자 하나까지 같게** 나와 들통났다
        ///    ("기능을 넣었다 ≠ 발동한다" — 응답 성공 ≠ 지시 전달 성공과 같은 함정).
        ///
        /// 정책[추정]: 궁극기에 못 미치는 포인트면 절반의 확률로 아낀다.
        ///   항상 아끼면 SS 가 죽고, 안 아끼면 궁극기가 죽는다. 둘 다 살아야 "포인트를 모을수록
        ///   더 강한 스킬"(원작 조사)이라는 선택이 성립한다.
        /// ⚠️ 게임과 하네스가 **이 함수 하나만** 쓴다. 한쪽에 정책을 또 적으면 승률이 게임의 것이 아니게 된다(§2-9-1).
        /// </summary>
        public static bool AiSaveForUltimate(int points, ref Rng rng)
        {
            if (points >= UltimateCost) return false;      // 이미 궁극기를 쓸 수 있다
            return rng.Float01() < 0.72f;   // 실측으로 올렸다 — 0.5 면 2점에서 SS 로 새어나가 궁극기가 안 모인다
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

        /// <summary>
        /// 궁극기 — SS 보다 한 단계 위. **기종 고유 효과는 그대로 두고 규모만 키운다.**
        ///
        /// 왜 이 형태인가: 기종마다 새 필살기를 발명하면 13종치 규칙을 내가 지어내는 셈이고,
        /// 그건 과거에 오너가 되돌린 방식이다(핸드오프 경고). 원작 근거가 말하는 건
        /// "포인트를 모을수록 **더 강한** 스킬" 뿐이므로 강도만 올린다 —
        /// 독구름은 더 넓은 독구름, 위성탄은 더 깊은 위성탄으로 남는다.
        ///
        /// ⚠️ 굴착 반경도 같이 키운다. 안 키우면 "화면에서 뭐가 달라졌는지" 안 보인다(§7 이 이 게임의 존재 이유다).
        /// </summary>
        /// <summary>기종을 모를 때의 궁극기(하위 호환). 새 코드는 기종을 넘기는 쪽을 써라.</summary>
        public static TankStats ApplyUltimate(TankStats specialShell)
            => ApplyUltimate(specialShell, (TankKind)(-1));

        /// <summary>
        /// 궁극기 — **기종마다 키우는 축이 다르다**(오너 지시 2026-09-17).
        ///
        /// 원칙: 새 규칙을 발명하지 않고 **그 기종이 원래 잘하던 것**을 극대화한다.
        /// 축이 전부 "피해"면 13종이 같은 궁극기를 갖는 것과 같으므로, 기종별로 다른 칸을 키운다 —
        /// 화력(캐논·크로스보우) / 광역·연사(캐터펄트·멀티미사일·레이저) / 굴착(캐롯·이온) /
        /// 지속(듀크·마인랜더) / 제어(포세이돈) / 조건부(세크윈드).
        /// 근거는 §2-9 원작 조사다(캐논 2번탄 420 최강, 캐롯 "2번탄 지형파괴 우수",
        /// 이온 "정타만 해도 지형 완전 파괴", 레이저 3연 회전, 멀티미사일 9연, 세크윈드 체력 50% 이하 +50%).
        ///
        /// ⚠️ 배율은 전부 [추정]이다. 승률 하네스의 `UltUsed` 와 매치업표로 조정하라 —
        ///    한 기종만 압도하면 그 기종의 축을 낮춰라(§2-9-5 처럼 단일 변수로 재라).
        /// ⚠️ 탄두 수는 여기가 아니라 `Spread.Pattern(kind, shell, ultimate:true)` 가 정한다. 두 곳을 같이 봐라.
        /// </summary>
        public static TankStats ApplyUltimate(TankStats specialShell, TankKind kind)
        {
            var u = specialShell;
            switch (kind)
            {
                // ── 화력형 ──
                case TankKind.Cannon:        // 원작 2번탄 빨콩 420 — 전무기 최강 단발. 그 축을 끝까지 민다.
                    u.BaseDamage *= 2.9f; u.DirectDamage *= 2.2f; u.BlastRadius *= 1.25f; u.CraterRadius *= 1.35f; break;

                case TankKind.CrossBow:      // 관통형 — 직격에 몰아준다(빗맞으면 손해인 고위험 궁극).
                    u.DirectDamage *= 3.2f; u.BaseDamage *= 1.6f; u.BlastRadius *= 1.15f; u.CraterRadius *= 1.25f; break;

                // ── 광역·연사형 (탄두 수는 Spread 가 늘린다) ──
                case TankKind.Catapult:      // 투석기 — 넓게 퍼뜨린다. 발당 피해는 조금만.
                    u.BaseDamage *= 1.5f; u.DirectDamage *= 1.3f; u.BlastRadius *= 1.85f; u.CraterRadius *= 1.45f; break;

                case TankKind.MultiMissile:  // 9연 → 탄막. 발당은 낮추고 수로 민다.
                    u.BaseDamage *= 1.35f; u.DirectDamage *= 1.25f; u.BlastRadius *= 1.20f; u.CraterRadius *= 1.20f; break;

                case TankKind.Laser:         // 3연 회전 레이저 → 관통 연사. 굴착이 깊다(CraterShape 가 세로로 판다).
                    u.BaseDamage *= 1.45f; u.DirectDamage *= 1.9f; u.BlastRadius *= 1.15f; u.CraterRadius *= 1.75f; break;

                case TankKind.Missile:       // 1번탄 4단 폭발 — 연쇄로 민다.
                    u.BaseDamage *= 1.6f; u.DirectDamage *= 1.6f; u.BlastRadius *= 1.45f; u.CraterRadius *= 1.40f; break;

                // ── 굴착형 ──
                case TankKind.Carrot:        // 원작 "2번탄 지형파괴 우수" — 지형을 갈아엎는 궁극.
                    u.BaseDamage *= 1.5f; u.DirectDamage *= 1.4f; u.BlastRadius *= 1.40f; u.CraterRadius *= 2.30f; break;

                case TankKind.IonAttacker:   // 원작 "정타만 해도 지형 완전 파괴" — 궤도 폭격.
                    u.BaseDamage *= 1.8f; u.DirectDamage *= 1.7f; u.BlastRadius *= 1.50f; u.CraterRadius *= 2.10f; break;

                // ── 지속·설치형 (장판·지뢰 지속은 ShellEffects 가 늘린다) ──
                case TankKind.Duke:          // 독구름 — 넓게 깔아 자리를 막는다.
                    u.BaseDamage *= 1.4f; u.DirectDamage *= 1.3f; u.BlastRadius *= 2.00f; u.CraterRadius *= 1.20f; break;

                case TankKind.MineLander:    // 지뢰밭 — 피해보다 설치 범위.
                    u.BaseDamage *= 1.4f; u.DirectDamage *= 1.3f; u.BlastRadius *= 1.60f; u.CraterRadius *= 1.30f; break;

                // ── 제어형 ──
                case TankKind.Poseidon:      // 속박(§2-9-7) — 묶어 두는 게 이 기종의 무기다.
                    u.BaseDamage *= 1.6f; u.DirectDamage *= 1.5f; u.BlastRadius *= 1.65f; u.CraterRadius *= 1.35f; break;

                // ── 조건부 ──
                case TankKind.SecWind:       // 원작 "체력 50% 이하 공격 +50%" — 몰린 쪽이 뒤집는 기종.
                    u.BaseDamage *= 2.4f; u.DirectDamage *= 1.8f; u.BlastRadius *= 1.30f; u.CraterRadius *= 1.30f; break;

                case TankKind.SuperTank:     // 방어 125 — 9연을 유지하며 고르게.
                    u.BaseDamage *= 1.5f; u.DirectDamage *= 1.5f; u.BlastRadius *= 1.35f; u.CraterRadius *= 1.35f; break;

                default:                     // 기종 미지정(하위 호환)
                    u.BaseDamage *= 2.2f; u.DirectDamage *= 2.0f; u.BlastRadius *= 1.55f; u.CraterRadius *= 1.60f; break;
            }
            u.Name = "[궁극] " + u.Name;
            return u;
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

        /// <summary>궁극기(§49)를 쓸 수 있는가.</summary>
        public bool CanUltimate()
        {
            return Points >= NiceShot.UltimateCost;
        }

        /// <summary>궁극기를 쓴다. ⚠️ CanUltimate() 를 먼저 확인하라.</summary>
        public void SpendUltimate()
        {
            Points = Math.Max(0, Points - NiceShot.UltimateCost);
        }
    }
}
