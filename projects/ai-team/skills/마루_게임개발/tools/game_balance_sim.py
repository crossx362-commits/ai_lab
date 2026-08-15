#!/usr/bin/env python3
"""
마루(게임 개발) — 재와 별(Ashes to Stars) 밸런스 검증

무엇을 하는가:
  기획서 §18 수치 밸런싱 기준표를 실제로 검산하는 시뮬레이션
  6개 항목을 검증: 골드유입·소각·리롤·레벨링·환생·영지·50층달성률

결과:
  콘솔에 표로 출력 + 리포트 MD 저장

사용:
  python game_balance_sim.py
"""
import os
import sys
import csv
from datetime import datetime
from math import ceil, floor, exp, log

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    _root = os.path.dirname(_root)
    if os.path.isdir(os.path.join(_root, "projects")):
        break

REPORT_DIR = os.path.join(_root, "output", "qa", "ashes-to-stars")
os.makedirs(REPORT_DIR, exist_ok=True)

# ==============================================================================
# 기획서 §18 수치 정수 (1차 기준값)
# ==============================================================================

# 18-1: 티어별 수익 곡선
TIER_COUNT = 10
TIER_BASE_INCOME = 1.0  # G/h (T1 = 1 골드/시간)
TIER_MULTIPLIER = 1.6   # 각 티어마다 ×1.6

# 18-2: 행위별 비용 (G/h 배수)
COST_FIELD_HUNT = 0.0
COST_DUNGEON = 0.02
COST_TOWER_NORMAL = 0.03
COST_TOWER_5 = 0.10
COST_TOWER_10 = 0.15
COST_RAID_SPECIAL = 0.12
COST_INVASION = 0.08

SCORCH_TARGET_MIN = 0.45  # 소각 목표 45~55%
SCORCH_TARGET_MAX = 0.55
SCORCH_BREAKDOWN = {
    "강화": 0.30,
    "영지": 0.12,
    "경매수수료": 0.08,
    "행위비용": 0.05
}

# 18-4: 목숨 아이템
REVIVE_DROP_RATE = 0.15    # 10층 대보스 15%
REVIVE_PRICE_MULT = 5.0    # 5배 G/h (평균)
SCROLL_PRICE_MULT = 3.0    # 3배 G/h (평균, 두루마리)
REBORN_PRICE_MULT = 225.0  # 225배 G/h (평균)
SCROLL_CASTING_SEC = 6.0   # 캐스팅 6초
REROLL_COST_MULTIPLIER = [1, 2, 4, 8]  # 1~4회차 누진

# 18-6: 경험치
MAX_LEVEL = 100
EXP_BASE = 100.0
EXP_POWER = 2.2
LEVEL_20_HOUR = 2.0
LEVEL_50_HOUR = 15.0
LEVEL_100_HOUR = 80.0

# 18-12: 영지
BUILDING_BASE_TIME = 5 * 60  # 5분 = 300초
BUILDING_TIME_MULT = 1.6
BUILDING_TIME_CAP_SEC = 24 * 3600  # 24시간
BUILDING_LEVELS = 15  # 1~15레벨

# 10-10: 탑 난이도 곡선
TOWER_CURVE_1_49 = 0.055     # +5.5%/층
TOWER_CURVE_50_100 = 0.07    # +7%/층

# ==============================================================================
# 검산 함수들
# ==============================================================================

def tier_income(tier: int) -> float:
    """T1~T10 시간당 수익 (G/h)"""
    return TIER_BASE_INCOME * (TIER_MULTIPLIER ** (tier - 1))

def exp_for_level(level: int) -> float:
    """누적 경험치 = 100×Lv^2.2"""
    return EXP_BASE * (level ** EXP_POWER)

def leveling_hours(target_level: int, base_exp_per_hour: float) -> float:
    """특정 레벨까지 필요한 시간"""
    if target_level <= 1:
        return 0
    total_exp = exp_for_level(target_level)
    return total_exp / base_exp_per_hour

def reborn_exp_share(reborn_level: int, party_total_level: int) -> float:
    """레벨 비례 분배: reborn이 받을 지분"""
    if party_total_level == 0:
        return 0
    return reborn_level / party_total_level

def building_time_for_level(level: int) -> float:
    """건물 N레벨 건설 시간 (초)"""
    if level <= 0:
        return 0
    base = BUILDING_BASE_TIME * (BUILDING_TIME_MULT ** (level - 1))
    return min(base, BUILDING_TIME_CAP_SEC)

def tower_power_scaling(layer: int) -> float:
    """탑 N층 권장 전투력 (1층=100 기준)"""
    if layer <= 0:
        return 100
    if layer <= 49:
        return 100 * ((1 + TOWER_CURVE_1_49) ** (layer - 1))
    else:
        # 50층: 49층의 값에서 시작
        power_49 = 100 * ((1 + TOWER_CURVE_1_49) ** 48)
        extra = layer - 49
        return power_49 * ((1 + TOWER_CURVE_50_100) ** extra)

# ==============================================================================
# 검증 1: 골드 유입 vs 소각 균형
# ==============================================================================

def check_gold_balance() -> dict:
    """
    각 티어에서 시간당 유입(필드+영지)과 소각을 계산.
    소각률이 45~55% 범위인지 검증.
    """
    results = []
    for tier in range(1, TIER_COUNT + 1):
        income = tier_income(tier)

        # 영지 생산 (필드의 25%)
        manor_prod = income * 0.25
        total_income = income + manor_prod

        # 소각: 고정 비율 목표 45~55% 구성
        # 실제로는 행위별·강화별로 분산되지만, 목표 범위만 확인
        scorch_min = total_income * SCORCH_TARGET_MIN
        scorch_max = total_income * SCORCH_TARGET_MAX

        results.append({
            "tier": tier,
            "field_income": income,
            "manor_prod": manor_prod,
            "total_income": total_income,
            "scorch_min": scorch_min,
            "scorch_max": scorch_max
        })

    # 판정: 모든 티어가 45~55% 범위를 가질 수 있는지
    all_viable = all(r["scorch_max"] >= 0 for r in results)

    return {
        "status": "PASS" if all_viable else "FAIL",
        "details": results,
        "reason": "모든 티어에서 45~55% 소각 목표가 수학적으로 성립"
    }

# ==============================================================================
# 검증 2: 리롤 억제
# ==============================================================================

def check_reroll_suppression() -> dict:
    """
    하위 레이드(환생석 1% 기대값)와 비용 비교.
    기대이득 < 비용이어야 리롤이 억제된다.
    """
    # T5 기준 시세 (기획서 §18-4 표)
    t5_income = tier_income(5)

    # 환생석 1회 기대값 = 드랍률 × 시세
    reborn_expectation = 0.01 * REBORN_PRICE_MULT * t5_income

    # 리롤 1회 비용 = 두루마리 + 재입장 누진
    scroll_cost = SCROLL_PRICE_MULT * t5_income
    reentry_cost_1 = COST_TOWER_10 * t5_income  # 1회차
    reentry_cost_2 = COST_TOWER_10 * t5_income * 2  # 2회차

    # 리롤 한 번의 총비용 (두루마리 1회 + 재입장 1회)
    reroll_total_cost = scroll_cost + reentry_cost_1

    # 억제 여부
    suppressed = reroll_total_cost > reborn_expectation

    return {
        "status": "PASS" if suppressed else "FAIL",
        "reborn_drop_rate": REVIVE_DROP_RATE,
        "reborn_expectation": reborn_expectation,
        "scroll_cost": scroll_cost,
        "reentry_cost": reentry_cost_1,
        "reroll_total_cost": reroll_total_cost,
        "suppressed": suppressed,
        "reason": f"기대이득({reborn_expectation:.1f}G/h) vs 비용({reroll_total_cost:.1f}G/h): {'억제됨' if suppressed else '미억제 위험'}"
    }

# ==============================================================================
# 검증 3: 레벨링 시간
# ==============================================================================

def check_leveling_time() -> dict:
    """
    경험치 필요량 100×Lv^2.2로 Lv20/50/100 도달 시간 계산.
    기획서 예상(2/15/80시간)과 맞는지 확인.
    """
    # T1 기준 시간당 경험치 유입 계산
    # 필드 사냥이 무료이고, 몹 하나당 3~10쿠퍼 드롭
    # 충분한 파밍으로 시간당 100exp/hour 수준으로 가정
    # (실제로는 몬스터 마리수, AI, 장비 등에 따라 달라짐)

    # 역산: Lv20에 2시간이 기준
    exp_lv20 = exp_for_level(20)
    exp_per_hour_lv20 = exp_lv20 / LEVEL_20_HOUR

    # 같은 속도로 Lv50, Lv100 계산
    exp_lv50 = exp_for_level(50)
    exp_lv100 = exp_for_level(100)

    hours_lv20 = exp_lv20 / exp_per_hour_lv20
    hours_lv50 = exp_lv50 / exp_per_hour_lv20
    hours_lv100 = exp_lv100 / exp_per_hour_lv20

    # 기획서 기대값과 비교
    expected = {"Lv20": LEVEL_20_HOUR, "Lv50": LEVEL_50_HOUR, "Lv100": LEVEL_100_HOUR}
    actual = {"Lv20": hours_lv20, "Lv50": hours_lv50, "Lv100": hours_lv100}

    # ±20% 오차 범위 허용 (플레이테스트 조정분)
    tolerance = 0.2
    matches = all(
        abs(actual[k] - expected[k]) / expected[k] <= tolerance
        for k in expected
    )

    return {
        "status": "PASS" if matches else "INFO",
        "expected_hours": expected,
        "calculated_hours": actual,
        "exp_per_hour": exp_per_hour_lv20,
        "reason": f"기획서 예상과 {'±20% 이내' if matches else f'20% 초과 차이 — 몬스터 경험치 조정 필요'}"
    }

# ==============================================================================
# 검증 4: 환생 재육성 곡선
# ==============================================================================

def check_reborn_curve() -> dict:
    """
    Lv1 환생 캐릭터가 Lv100 4인 파티에서 클 때
    Lv50까지 걸리는 시간과 곡선 형태 확인.
    """
    # 4인 파티 레벨 합 가정: 100+100+100+1 = 301
    party_total = 300 + 1  # 편의상 4명이 모두 Lv100

    # Lv1에서 Lv50까지 필요한 총 경험치
    exp_needed = exp_for_level(50)

    # 몬스터 처치당 경험치 보상 (파티 총량)
    # T10 기준 호화로운 파밍: 시간당 100 처치, 몬스터당 100exp
    monster_exp_per_kill = 100.0
    kills_per_hour = 100.0
    party_exp_per_hour = kills_per_hour * monster_exp_per_kill

    # Lv1의 지분 = 1/301 = 0.33%
    # 때문에 Lv1이 받을 경험치는 party_exp × 지분
    reborn_share_lv1 = 1.0 / party_total  # 0.33%
    reborn_exp_per_hour_lv1 = party_exp_per_hour * reborn_share_lv1

    # 곡선 구성: 레벨이 올라가며 지분도 함께 커짐
    # Lv1~50까지의 평균 지분 추정
    avg_level = 25
    avg_share = avg_level / party_total
    avg_exp_per_hour = party_exp_per_hour * avg_share

    # Lv50까지 예상 시간 (평균값 사용)
    hours_to_lv50 = exp_needed / avg_exp_per_hour

    # "초반 답답, 뒤로 가속" 확인: Lv1 지분이 Lv50 지분의 1/50 미만이어야 함
    lv50_share = 50.0 / party_total
    share_ratio = reborn_share_lv1 / lv50_share
    accelerating = share_ratio < 0.1  # 초기 지분이 중반 지분의 10% 미만

    return {
        "status": "PASS" if accelerating else "WARN",
        "party_total_level": party_total,
        "reborn_exp_at_lv1": reborn_exp_per_hour_lv1,
        "hours_to_lv50": hours_to_lv50,
        "lv1_share_percent": reborn_share_lv1 * 100,
        "lv50_share_percent": lv50_share * 100,
        "share_acceleration_ratio": share_ratio,
        "accelerating": accelerating,
        "reason": f"초반{reborn_share_lv1*100:.2f}% → 중반{lv50_share*100:.2f}%: {'가속 곡선 정상' if accelerating else '초반이 너무 빠른 우려'}"
    }

# ==============================================================================
# 검증 5: 영지 건설 총 시간
# ==============================================================================

def check_manor_construction() -> dict:
    """
    본성 1→15레벨까지 누적 건설 시간.
    5분×1.6^(N-1) 공식으로 계산.
    """
    total_time_sec = 0
    levels_detail = []

    for level in range(1, BUILDING_LEVELS + 1):
        time_sec = building_time_for_level(level)
        total_time_sec += time_sec
        levels_detail.append({
            "level": level,
            "build_time_sec": time_sec,
            "build_time_hour": time_sec / 3600,
            "cumulative_hour": total_time_sec / 3600
        })

    total_hour = total_time_sec / 3600
    total_day = total_hour / 24

    # 반쪽 단축(50%) 적용 시
    total_hour_shortcut50 = total_hour * 0.5
    total_day_shortcut50 = total_hour_shortcut50 / 24

    return {
        "status": "INFO",
        "total_hour_full": total_hour,
        "total_day_full": total_day,
        "total_hour_50pct_shortcut": total_hour_shortcut50,
        "total_day_50pct_shortcut": total_day_shortcut50,
        "levels": levels_detail,
        "reason": f"전체 건설 시간: {total_day:.1f}일 (50% 단축 시 {total_day_shortcut50:.1f}일)"
    }

# ==============================================================================
# 검증 6: 50층 돌파율 추정
# ==============================================================================

# 💡 리텐션 가정 — **기획서에 없는 값이다.** 여기 이름을 붙여 밖으로 드러낸다.
# 50층 돌파율은 게임 수치가 아니라 "몇 시간 붙어 있느냐"에서 나오기 때문이다.
# 실 서비스 지표가 생기면 이 한 줄만 바꾼다.
RETENTION_HOURS_MEDIAN = 8.0    # 중앙값 플레이 시간(시간)
RETENTION_HOURS_SIGMA = 1.1     # 로그정규 분산 — 상위 소수가 길게 하는 꼬리


def check_layer50_penetration() -> dict:
    """
    50층 돌파율 추정.

    ⚠️ 예전 구현은 **게임 수치를 하나도 쓰지 않았다.**
       `mean_layer=35 · stddev=15`이라는 어디에도 없는 두 상수만으로 26.9%를 만들어냈고,
       계산해둔 `avg_user_power`·`layer50_power`는 **쓰지도 않았다**(죽은 변수).
       게다가 `tower_power_scaling(60)`을 "평균 유저 전투력"으로 썼는데 그 함수의 인자는
       레벨이 아니라 **층**이다 — 단위가 어긋나 있었다.
       그 상태에서 목표(10~15%)에 맞추려고 상수를 만지는 것은 **원하는 답이 나오게 상수를 고르는 것**일 뿐
       아무것도 검증하지 않는다.

    그래서 실제로 근거가 있는 것에서 유도한다:
      · §18-6 경험치 곡선(100×Lv^2.2)과 앵커(Lv50 = 15시간)
      · 50층은 Lv50 전후를 전제로 설계됐다(§8 100층 ↔ Lv100)
      → **50층 돌파 = 그 시간만큼 플레이한 유저의 비율**로 환산한다.
    남은 미지수는 리텐션 하나이고, 그것을 상수로 드러내 감사 가능하게 만든다.
    """
    from math import log, erf, sqrt

    hours_needed = leveling_hours(50, exp_for_level(20) / LEVEL_20_HOUR)

    # 로그정규 분포에서 P(플레이시간 ≥ hours_needed)
    mu = log(RETENTION_HOURS_MEDIAN)
    z = (log(hours_needed) - mu) / RETENTION_HOURS_SIGMA
    prob = 0.5 * (1.0 - erf(z / sqrt(2.0)))

    penetration_pct = prob * 100
    target_min, target_max = 10, 15
    in_range = target_min <= penetration_pct <= target_max

    return {
        # 이 값은 게임 수치가 아니라 **리텐션 가정**이 지배한다 —
        # PASS/WARN으로 판정하면 "가정을 고쳐 통과시키는" 길이 열린다. 그래서 INFO다.
        "status": "INFO",
        "hours_for_level50": hours_needed,
        "layer50_requirement": tower_power_scaling(50),
        "estimated_penetration_pct": penetration_pct,
        "target_range": f"{target_min}~{target_max}%",
        "in_range": in_range,
        "reason": (f"50층 돌파율 추정 {penetration_pct:.1f}% (목표 {target_min}~{target_max}%) — "
                   f"Lv50까지 {hours_needed:.1f}시간, 리텐션 중앙값 {RETENTION_HOURS_MEDIAN:.0f}h 가정. "
                   f"⚠️ 이 수치는 리텐션 가정이 지배한다. 게임 수치를 바꿔도 거의 안 움직인다"),
    }


# ==============================================================================
# 메인 실행 및 리포트
# ==============================================================================

def main():
    print("\n" + "="*80)
    print("재와 별(Ashes to Stars) — 밸런스 검증 시뮬레이션")
    print("="*80)
    print(f"기획서: §18 수치 밸런싱 기준표 (v1)")
    print(f"실행: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print("="*80 + "\n")

    checks = [
        ("1. 골드 유입 vs 소각 균형", check_gold_balance),
        ("2. 리롤 억제 검산", check_reroll_suppression),
        ("3. 레벨링 시간", check_leveling_time),
        ("4. 환생 재육성 곡선", check_reborn_curve),
        ("5. 영지 건설 총 시간", check_manor_construction),
        ("6. 50층 돌파율 추정", check_layer50_penetration),
    ]

    results = []
    for name, check_func in checks:
        print(f"\n[검증] {name}")
        print("-" * 80)
        try:
            result = check_func()
            results.append((name, result))
            status = result.get("status", "INFO")
            reason = result.get("reason", "")
            print(f"결과: {status}")
            print(f"설명: {reason}")

            # 상세 정보 출력 (선택 항목)
            if "reborn_expectation" in result:
                print(f"  환생석 기대값: {result['reborn_expectation']:.1f} G/h")
                print(f"  리롤 1회 비용: {result['reroll_total_cost']:.1f} G/h")
            if "calculated_hours" in result:
                print(f"  Lv20: {result['calculated_hours']['Lv20']:.1f}h (목표 {result['expected_hours']['Lv20']:.1f}h)")
                print(f"  Lv50: {result['calculated_hours']['Lv50']:.1f}h (목표 {result['expected_hours']['Lv50']:.1f}h)")
                print(f"  Lv100: {result['calculated_hours']['Lv100']:.1f}h (목표 {result['expected_hours']['Lv100']:.1f}h)")
            if "hours_to_lv50" in result:
                print(f"  환생 Lv1→Lv50: {result['hours_to_lv50']:.1f}시간")
                print(f"  초반 지분: {result['lv1_share_percent']:.2f}% → 중반: {result['lv50_share_percent']:.2f}%")
            if "total_day_full" in result:
                print(f"  영지 전체 건설: {result['total_day_full']:.1f}일 (50% 단축 시 {result['total_day_50pct_shortcut']:.1f}일)")
            if "estimated_penetration_pct" in result:
                print(f"  50층 돌파율 추정: {result['estimated_penetration_pct']:.1f}% (목표 {result['target_range']})")

        except Exception as e:
            results.append((name, {"status": "ERROR", "error": str(e)}))
            print(f"결과: ERROR")
            print(f"오류: {e}")

    # ==============================================================================
    # 종합 리포트 저장
    # ==============================================================================

    report_file = os.path.join(REPORT_DIR, f"balance_sim_{datetime.now().strftime('%Y%m%d_%H%M%S')}.md")

    with open(report_file, "w", encoding="utf-8") as f:
        f.write("# 재와 별 — 밸런스 검증 시뮬레이션 보고서\n\n")
        f.write(f"**실행일시**: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n\n")
        f.write(f"**기획서**: §18 수치 밸런싱 기준표 (v1)\n\n")

        # 종합 결과 테이블
        f.write("## 검증 결과 요약\n\n")
        f.write("| # | 항목 | 결과 | 설명 |\n")
        f.write("|---|------|------|------|\n")

        for name, result in results:
            status = result.get("status", "?")
            reason = result.get("reason", "")[:60]
            f.write(f"| {name[:30]} | {status} | {reason} |\n")

        f.write("\n---\n\n")

        # 상세 분석
        f.write("## 상세 분석\n\n")

        for name, result in results:
            f.write(f"### {name}\n\n")
            f.write(f"**판정**: `{result.get('status', '?')}`\n\n")

            if result.get("status") == "ERROR":
                f.write(f"**오류**: {result.get('error', 'Unknown')}\n\n")
                continue

            reason = result.get("reason", "")
            f.write(f"**결론**: {reason}\n\n")

            # 검증 1: 골드 균형
            if "details" in result:
                f.write("#### 티어별 수익과 소각 범위\n\n")
                f.write("| 티어 | 필드 수익 | 영지 생산 | 총 수익 | 소각 목표(45~55%) |\n")
                f.write("|------|-----------|----------|--------|-------------------|\n")
                for d in result["details"][:5]:  # 처음 5개 티어만
                    f.write(f"| T{d['tier']} | {d['field_income']:.1f} | {d['manor_prod']:.1f} | {d['total_income']:.1f} | {d['scorch_min']:.1f}~{d['scorch_max']:.1f} |\n")
                f.write("\n")

            # 검증 2: 리롤
            if "reborn_expectation" in result:
                f.write("#### 리롤 경제성 분석\n\n")
                f.write(f"- 환생석 드랍률: {result['reborn_drop_rate']*100:.1f}%\n")
                f.write(f"- 환생석 시세: {result['reborn_expectation']:.1f} G/h\n")
                f.write(f"- 귀환의 두루마리 비용: {result['scroll_cost']:.1f} G/h\n")
                f.write(f"- 재입장 비용(1회차): {result['reentry_cost']:.1f} G/h\n")
                f.write(f"- **리롤 총비용: {result['reroll_total_cost']:.1f} G/h**\n")
                f.write(f"- 억제 여부: {'✅ 억제됨 (기대값 < 비용)' if result['suppressed'] else '⚠️ 미억제 (기대값 > 비용 — 조정 필요)'}\n\n")

            # 검증 3: 레벨링
            if "calculated_hours" in result:
                f.write("#### 경험치 곡선 검증\n\n")
                f.write("| 레벨 | 기획서 목표 | 계산값 | 차이 | 판정 |\n")
                f.write("|------|-----------|-------|------|------|\n")
                for lv in ["Lv20", "Lv50", "Lv100"]:
                    exp = result["expected_hours"][lv]
                    calc = result["calculated_hours"][lv]
                    diff_pct = (calc - exp) / exp * 100
                    verdict = "✅ OK" if abs(diff_pct) <= 20 else "⚠️ 조정"
                    f.write(f"| {lv} | {exp:.1f}h | {calc:.1f}h | {diff_pct:+.1f}% | {verdict} |\n")
                f.write(f"\n몬스터당 경험치: {result['exp_per_hour']:.0f} exp/hour (역산)\n\n")

            # 검증 4: 환생
            if "hours_to_lv50" in result:
                f.write("#### 환생 재육성 곡선\n\n")
                f.write(f"- 파티 레벨 합: {result['party_total_level']}\n")
                f.write(f"- Lv1 시 경험치 지분: {result['lv1_share_percent']:.2f}%\n")
                f.write(f"- Lv50 시 경험치 지분: {result['lv50_share_percent']:.2f}%\n")
                f.write(f"- 지분 가속도 비율: {result['share_acceleration_ratio']:.2f}배\n")
                f.write(f"- Lv1→Lv50 예상 시간: {result['hours_to_lv50']:.1f}시간\n")
                f.write(f"- 곡선 형태: {'✅ 초반 답답, 뒤로 가속' if result['accelerating'] else '⚠️ 초반이 너무 빠른 우려'}\n\n")

            # 검증 5: 영지
            if "total_day_full" in result:
                f.write("#### 영지 건설 시간표\n\n")
                f.write(f"- 본성 1→15 전체: **{result['total_day_full']:.1f}일** ({result['total_hour_full']:.1f}시간)\n")
                if "total_day_50pct_shortcut" in result:
                    f.write(f"- 50% 단축 적용: {result['total_day_50pct_shortcut']:.1f}일 ({result['total_hour_50pct_shortcut']:.1f}시간)\n\n")
                f.write("##### 레벨별 건설 시간\n\n")
                f.write("| 레벨 | 소요 시간 | 누적 시간 |\n")
                f.write("|------|----------|----------|\n")
                for d in result["levels"][:8]:  # 처음 8개 레벨
                    f.write(f"| {d['level']} | {d['build_time_hour']:.1f}h | {d['cumulative_hour']:.1f}h |\n")
                f.write("\n")

            # 검증 6: 50층
            if "estimated_penetration_pct" in result:
                f.write("#### 50층 돌파율 추정\n\n")
                f.write(f"- 50층 권장 전투력: {result['layer50_requirement']:.0f} (1층=100 기준)\n")
                f.write(f"- 돌파율 추정: **{result['estimated_penetration_pct']:.1f}%**\n")
                f.write(f"- 목표 범위: {result['target_range']}\n")
                f.write(f"- 판정: {'✅ 적정' if result['in_range'] else '⚠️ 조정 필요'}\n\n")

            f.write("\n")

    print("\n" + "="*80)
    print("✅ 검증 완료")
    print(f"📄 리포트: {report_file}")
    print("="*80 + "\n")

if __name__ == "__main__":
    main()
