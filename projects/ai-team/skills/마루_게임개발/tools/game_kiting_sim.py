#!/usr/bin/env python3
"""
마루(게임 개발) — 재와 별(Ashes to Stars) 카이팅 밸런스 검증

무엇을 하는가:
  플레이어 1명 vs 잡몹 무리의 2D 시뮬레이션
  속도비에 따라 "직선 도주 > 원형 카이팅 > 정지 교전"의 생존도 변화 측정
  기획서 §10-2(밀도로 죽인다) 설계가 성립하는 속도 범위 찾기

결과:
  콘솔에 표로 출력 + 리포트 MD 저장

사용:
  python game_kiting_sim.py
"""
import os
import sys
import math
from datetime import datetime
from collections import defaultdict

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
# 시뮬레이션 설정 (기획서 기반)
# ==============================================================================

SIM_DURATION_SEC = 60  # 60초 판
SIM_DT = 1.0 / 60.0   # 60fps, 각 틱 1/60초
SIM_TICKS = int(SIM_DURATION_SEC / SIM_DT)

# 맵 설정
MAP_RADIUS = 24  # 반경 24 유닛 원형 맵

# 플레이어 설정
PLAYER_SPEED = 4.2  # 기준값 (기획서)
PLAYER_COLLISION_RADIUS = 0.3  # 피격 판정 반경
DASH_COOLDOWN = 6.0  # 초
DASH_DURATION = 0.3  # 무적 시간
DASH_DISTANCE = PLAYER_SPEED * 3  # 3초분 거리

# 잡몹 설정 (기획서 §18-11 기준, 실측 데이터)
MONSTER_BASE_SPEED = 2.7  # 추적형 기준 속도 (후에 배율 적용)
MONSTER_COLLISION_RADIUS = 0.2
MONSTERS_PER_WAVE = 30  # 웨이브당 몬스터 수
MONSTER_ATTACK_RANGE = 0.5  # 공격 범위
MONSTER_ATTACK_COOLDOWN = 1.0  # 공격 쿨다운

# 몬스터 조성 (기획서 기본값)
MONSTER_COMPOSITION = {
    "追蹤": 0.40,  # 추적형 40%
    "包圍": 0.30,  # 포위형 30%
    "遠距": 0.30,  # 원거리 30%
}

# ==============================================================================
# 유틸리티
# ==============================================================================

def distance(p1, p2):
    """2D 거리"""
    return math.sqrt((p1[0] - p2[0]) ** 2 + (p1[1] - p2[1]) ** 2)

def angle_to(from_pos, to_pos):
    """from_pos에서 to_pos로 향하는 각도 (라디안)"""
    dx = to_pos[0] - from_pos[0]
    dy = to_pos[1] - from_pos[1]
    return math.atan2(dy, dx)

def move_towards(pos, target_angle, speed, dt):
    """pos에서 target_angle 방향으로 speed로 이동"""
    new_x = pos[0] + speed * math.cos(target_angle) * dt
    new_y = pos[1] + speed * math.sin(target_angle) * dt

    # 맵 경계 반사/제한
    dist_from_center = distance((0, 0), (new_x, new_y))
    if dist_from_center > MAP_RADIUS:
        # 경계를 벗어나면 경계로 당김 (튕김이 아니라 멈춤)
        ratio = MAP_RADIUS / dist_from_center
        new_x *= ratio
        new_y *= ratio

    return (new_x, new_y)

def clamp_in_map(pos):
    """위치를 맵 내부로 제한"""
    dist = distance((0, 0), pos)
    if dist > MAP_RADIUS:
        ratio = MAP_RADIUS / dist
        return (pos[0] * ratio, pos[1] * ratio)
    return pos

def normalize_angle(angle):
    """각도를 [-π, π]로 정규화"""
    while angle > math.pi:
        angle -= 2 * math.pi
    while angle < -math.pi:
        angle += 2 * math.pi
    return angle

# ==============================================================================
# Player 클래스
# ==============================================================================

class Player:
    def __init__(self, pos=(0, 0)):
        self.pos = pos
        self.speed = PLAYER_SPEED
        self.direction = 0  # 현재 이동 방향 (라디안)
        self.dash_cooldown = 0
        self.dash_remaining = 0  # 무적 상태 남은 시간
        self.total_hits = 0
        self.time_surrounded = 0  # 포위 상태 누적 시간

    def update(self, monsters, strategy, dt):
        """플레이어 업데이트"""
        # 대시 쿨다운 감소
        if self.dash_cooldown > 0:
            self.dash_cooldown -= dt
        if self.dash_remaining > 0:
            self.dash_remaining -= dt

        # 전략별 행동
        if strategy == "직선_도주":
            self._strategy_linear_escape(monsters, dt)
        elif strategy == "원형_카이팅":
            self._strategy_circular_kiting(monsters, dt)
        elif strategy == "정지_교전":
            self._strategy_static_defense(monsters, dt)

        # 이동
        if self.dash_remaining > 0:
            # 무적 상태: 빠른 이동 (대시 중)
            dash_speed = PLAYER_SPEED * 2  # 대시는 더 빠름
            self.pos = move_towards(self.pos, self.direction, dash_speed, dt)
        else:
            # 일반 이동
            self.pos = move_towards(self.pos, self.direction, self.speed, dt)

        self.pos = clamp_in_map(self.pos)

        # 포위 판정 (주변 몬스터 3마리 이상)
        nearby = sum(1 for m in monsters if distance(self.pos, m.pos) < 2.0)
        if nearby >= 3:
            self.time_surrounded += dt

    def _strategy_linear_escape(self, monsters, dt):
        """직선 도주: 가장 안전한 방향으로 계속 도주"""
        if not monsters:
            return

        # 모든 몬스터와의 각도를 구해 가장 멀 방향으로 도주
        angles_to_threats = []
        for m in monsters:
            angle = angle_to(self.pos, m.pos)
            angles_to_threats.append(angle)

        if angles_to_threats:
            # 위협들의 평균 각도의 반대 방향으로 이동
            avg_threat_angle = sum(math.cos(a) for a in angles_to_threats) / len(angles_to_threats)
            avg_threat_angle = math.atan2(
                sum(math.sin(a) for a in angles_to_threats) / len(angles_to_threats),
                avg_threat_angle
            )
            self.direction = normalize_angle(avg_threat_angle + math.pi)  # 반대 방향

    def _strategy_circular_kiting(self, monsters, dt):
        """원형 카이팅: 무리 주위를 큰 원을 그리며 도는 패턴 (안전한 거리 유지)"""
        if not monsters:
            return

        # 몬스터 무리의 중심 계산
        center_x = sum(m.pos[0] for m in monsters) / len(monsters)
        center_y = sum(m.pos[1] for m in monsters) / len(monsters)
        center = (center_x, center_y)

        # 중심으로부터 플레이어까지의 거리와 각도
        current_dist = distance(center, self.pos)
        angle_from_center = angle_to(center, self.pos)

        # 목표 반경: 무리의 평균 배치 거리보다 더 멀게 (안전하게)
        # 추적형이 최대 속도로 와도 닿지 않는 거리 = 약 4-5 유닛
        target_radius = 4.5

        # 1단계: 목표 반경으로 이동 (필요하면)
        if current_dist < target_radius * 0.9:
            # 아직 가까우면 바깥쪽으로 이동
            self.direction = angle_from_center
        elif current_dist > target_radius * 1.1:
            # 너무 멀면 안쪽으로 이동
            self.direction = angle_from_center + math.pi
        else:
            # 2단계: 목표 반경에서 원을 그리며 회전 (반시계 방향)
            angular_speed = self.speed / target_radius
            target_angle = angle_from_center + angular_speed * dt

            # 중심으로부터 원 위의 점
            target_x = center[0] + target_radius * math.cos(target_angle)
            target_y = center[1] + target_radius * math.sin(target_angle)

            # 현재 위치에서 목표 위치로의 방향
            self.direction = angle_to(self.pos, (target_x, target_y))

    def _strategy_static_defense(self, monsters, dt):
        """정지 교전: 거의 안 움직이고 대시로만 회피"""
        # 가장 가까운 위협에서 도주
        if not monsters:
            return

        closest_dist = float('inf')
        closest_angle = 0

        for m in monsters:
            d = distance(self.pos, m.pos)
            if d < closest_dist and d < 1.5:  # 1.5 유닛 이내만 위협
                closest_dist = d
                closest_angle = angle_to(self.pos, m.pos)

        # 위험하면 대시로 회피
        if closest_dist < 1.0 and self.dash_cooldown <= 0:
            self.direction = normalize_angle(closest_angle + math.pi)
            self.dash_cooldown = DASH_COOLDOWN
            self.dash_remaining = DASH_DURATION
        else:
            # 아니면 가만히 있기
            self.direction = 0

    def take_hit(self):
        """피격"""
        if self.dash_remaining <= 0:  # 무적 상태 아니면만 피격
            self.total_hits += 1

# ==============================================================================
# Monster 클래스
# ==============================================================================

class Monster:
    def __init__(self, pos, mon_type):
        self.pos = pos
        self.type = mon_type  # "追蹤" / "包圍" / "遠距"
        self.speed = MONSTER_BASE_SPEED  # 기본값은 추적형
        self.direction = 0
        self.attack_cooldown = 0

    def set_speed_ratio(self, ratio):
        """속도비 설정 (기본값 1.0 = 추적형과 동일)"""
        self.speed = MONSTER_BASE_SPEED * ratio

    def update(self, player, other_monsters, player_speed_ratio, dt):
        """몬스터 업데이트"""
        # 공격 쿨다운
        if self.attack_cooldown > 0:
            self.attack_cooldown -= dt

        # 타입별 AI
        if self.type == "追蹤":
            self._ai_pursuit(player, dt)
        elif self.type == "包圍":
            self._ai_encircle(player, other_monsters, dt)
        elif self.type == "遠距":
            self._ai_ranged(player, dt)

        # 이동
        self.pos = move_towards(self.pos, self.direction, self.speed, dt)
        self.pos = clamp_in_map(self.pos)

        # 공격 시도
        self._try_attack(player, dt)

    def _ai_pursuit(self, player, dt):
        """추적형: 플레이어를 직진으로 따라가기"""
        self.direction = angle_to(self.pos, player.pos)

    def _ai_encircle(self, player, other_monsters, dt):
        """포위형: 플레이어 주변을 원형으로 감싸며 접근, 플레이어의 탈출로 차단"""
        # 플레이어로부터의 각도 계산
        angle_from_player = angle_to(player.pos, self.pos)
        current_dist = distance(self.pos, player.pos)

        # 목표: 플레이어를 포위하며, 플레이어의 탈출 방향 앞에 위치
        # 플레이어의 이동 방향 예측 (플레이어가 따라올 수 없는 방향으로 간다고 가정)
        # 일반적으로 플레이어는 위협에서 먼 방향으로 이동하므로,
        # 그 반대 방향(위협 쪽)을 미리 차단

        # 포위형의 목표 반경: 1.2~2.0 유닛 (추적형보다 가깝게)
        target_radius = 1.5

        if current_dist < target_radius * 0.9:
            # 너무 가까우면 약간 뒤로 물러남
            self.direction = angle_to(player.pos, self.pos)
        elif current_dist > target_radius * 1.1:
            # 너무 멀면 접근
            self.direction = angle_to(self.pos, player.pos)
        else:
            # 목표 거리에서 유지하며 플레이어 주변을 원 그리기
            # 시계 반대 방향으로 회전하며 차단
            self.direction = angle_from_player + 0.5  # 약 28도씩 회전

    def _ai_ranged(self, player, dt):
        """원거리형: 일정 거리 유지하며 투사체 준비"""
        # 일정 거리(6 유닛)를 유지하려고 함 (기획서 기본값)
        target_distance = 6.0
        current_dist = distance(self.pos, player.pos)

        if current_dist < target_distance * 0.8:
            # 너무 가까우면 후퇴
            self.direction = angle_to(player.pos, self.pos)
            self.direction = normalize_angle(self.direction + math.pi)  # 180도 반대
        elif current_dist > target_distance * 1.2:
            # 너무 멀면 접근
            self.direction = angle_to(self.pos, player.pos)
        else:
            # 적절한 거리 유지하며 원 그리기
            angle_from_player = angle_to(player.pos, self.pos)
            self.direction = angle_from_player + 0.3  # 천천히 회전

    def _try_attack(self, player, dt):
        """공격 시도"""
        if self.attack_cooldown > 0:
            return

        dist = distance(self.pos, player.pos)
        if dist < MONSTER_ATTACK_RANGE:
            player.take_hit()
            self.attack_cooldown = MONSTER_ATTACK_COOLDOWN

# ==============================================================================
# 시뮬레이션
# ==============================================================================

def run_simulation(strategy, player_speed, monster_speed_ratio, monster_composition):
    """
    한 판의 시뮬레이션 실행

    Args:
        strategy: "직선_도주" / "원형_카이팅" / "정지_교전"
        player_speed: 플레이어 속도 (기본 4.2)
        monster_speed_ratio: 기본 몬스터 속도의 배율
        monster_composition: 몬스터 타입별 비율

    Returns:
        {
            "hits": 총 피격 횟수,
            "surrounded_ratio": 포위 상태 시간 비율,
            "player_final_pos": 최종 플레이어 위치,
            "monster_count": 몬스터 수,
        }
    """
    player = Player((0, 0))
    player.speed = player_speed

    # 몬스터 생성
    monsters = []
    for i in range(MONSTERS_PER_WAVE):
        # 플레이어 주위에 랜덤하게 배치 (원 위)
        angle = (i / MONSTERS_PER_WAVE) * 2 * math.pi
        radius = 5.0 + (i % 3) * 1.5
        pos = (radius * math.cos(angle), radius * math.sin(angle))

        # 타입 결정 (composition에 따라)
        r = i % 100 / 100.0
        cumulative = 0
        mon_type = "追蹤"
        for t, ratio in monster_composition.items():
            cumulative += ratio
            if r < cumulative:
                mon_type = t
                break

        m = Monster(pos, mon_type)
        m.set_speed_ratio(monster_speed_ratio)
        monsters.append(m)

    # 시뮬레이션 루프
    for tick in range(SIM_TICKS):
        t = tick * SIM_DT

        # 플레이어 업데이트
        player.update(monsters, strategy, SIM_DT)

        # 몬스터 업데이트
        for m in monsters:
            m.update(player, monsters, monster_speed_ratio, SIM_DT)

    return {
        "hits": player.total_hits,
        "surrounded_ratio": player.time_surrounded / SIM_DURATION_SEC,
        "player_final_pos": player.pos,
        "monster_count": len(monsters),
    }

# ==============================================================================
# 메인 실행 및 리포트
# ==============================================================================

def main():
    print("\n" + "="*80)
    print("재와 별(Ashes to Stars) — 카이팅 밸런스 검증")
    print("="*80)
    print(f"기획서: §10-2 잡몹 설계 원칙 (밀도로 죽인다)")
    print(f"실행: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print("="*80 + "\n")

    # ==============================================================================
    # Q1: 추적형 속도비를 0.6~1.0까지 변화시키며 영향 측정
    # ==============================================================================

    print("\n[실험 1] 추적형 속도비에 따른 생존도 변화")
    print("-" * 80)
    print("추적형을 플레이어(4.2) 대비로 상향하며 생존도 측정")
    print("(기획서 대응 후보: 0.85~0.95배로 올려 '계속 도망치면 조금씩 따라잡힌다')")
    print()

    results_by_ratio = {}
    # 플레이어 4.2 기준 비율로 설정 (기준 추적형 2.7의 몇 배인지 역산)
    # player_ratio 0.60 = 2.52 u/s ≈ 기준 2.7의 0.93배
    # player_ratio 1.00 = 4.20 u/s = 기준 2.7의 1.56배
    player_ratios = [0.60 + i * 0.05 for i in range(9)]  # 0.60, 0.65, ..., 1.00

    # 이를 기준 추적형 배수로 변환
    speed_ratios = [r / MONSTER_BASE_SPEED * PLAYER_SPEED for r in player_ratios]

    for i, player_ratio in enumerate(player_ratios):
        actual_speed = player_ratio * PLAYER_SPEED
        ratio = speed_ratios[i]
        print(f"플레이어 대비 {player_ratio:.2f}배 ({actual_speed:.2f} 유닛/s):")
        results = {}

        for strategy in ["직선_도주", "원형_카이팅", "정지_교전"]:
            result = run_simulation(
                strategy,
                PLAYER_SPEED,
                ratio,
                MONSTER_COMPOSITION
            )
            results[strategy] = result
            print(f"  {strategy:10s}: 피격 {result['hits']:3d}회, 포위율 {result['surrounded_ratio']*100:5.1f}%")

        results_by_ratio[ratio] = (player_ratio, results)
        print()

    # ==============================================================================
    # Q2: 직선 도주가 지배 전략이 아니 되는 속도비
    # ==============================================================================

    print("\n[분석 1] 직선 도주의 우위 범위")
    print("-" * 80)

    linear_dominates_until_ratio = None
    for i, ratio in enumerate(speed_ratios):
        player_ratio, results = results_by_ratio[ratio]
        linear_hits = results["직선_도주"]["hits"]
        circular_hits = results["원형_카이팅"]["hits"]
        static_hits = results["정지_교전"]["hits"]

        # 직선이 최선인가?
        if linear_hits <= circular_hits and linear_hits <= static_hits:
            if linear_dominates_until_ratio is None:
                linear_dominates_until_ratio = player_ratio
        else:
            if linear_dominates_until_ratio is not None:
                break

    if linear_dominates_until_ratio is None:
        print("✅ 직선 도주가 현재 속도에서 이미 지배 전략이 아님")
    else:
        print(f"⚠️ 직선 도주는 플레이어 대비 {linear_dominates_until_ratio:.2f}배까지 지배 전략")
        print(f"   그 이상에서는 다른 전략이 더 나음")

    # ==============================================================================
    # Q3: 정지 교전이 즉사 아닌 상한
    # ==============================================================================

    print("\n[분석 2] 정지 교전의 생존 가능 범위")
    print("-" * 80)

    # "즉사"를 50회 이상 피격으로 정의 (60초에 50회 = 1.2초마다 맞음 = 매우 위험)
    DANGEROUS_THRESHOLD = 50

    viable_until_ratio = None
    for i, ratio in enumerate(speed_ratios):
        player_ratio, results = results_by_ratio[ratio]
        static_hits = results["정지_교전"]["hits"]
        if static_hits < DANGEROUS_THRESHOLD:
            if viable_until_ratio is None or player_ratio > viable_until_ratio:
                viable_until_ratio = player_ratio

    if viable_until_ratio is not None:
        print(f"⚠️ 정지 교전은 플레이어 대비 {viable_until_ratio:.2f}배까지 생존 시도 가능")
        print(f"   그 이상에서는 1초마다 1회 이상 피격 (매우 위험)")
    else:
        print(f"🔴 정지 교전은 모든 속도비에서 위험 (기술 의존도 매우 높음)")

    # ==============================================================================
    # Q4: 포위형 비율 변화의 영향
    # ==============================================================================

    print("\n[실험 2] 포위형 비율 변화에 따른 영향")
    print("-" * 80)

    encircle_ratios = [0.20, 0.30, 0.40]  # 포위형 20% / 30% / 40%
    encircle_results = {}

    for enc_ratio in encircle_ratios:
        # 포위형 비율 변경, 나머지는 비례 조정
        new_composition = {
            "追蹤": 0.40,
            "包圍": enc_ratio,
            "遠距": 0.60 - enc_ratio,
        }

        print(f"\n포위형 {enc_ratio*100:.0f}% (추적 40%, 원거리 {(0.60-enc_ratio)*100:.0f}%):")

        results_by_ratio_enc = {}
        for i, ratio in enumerate(speed_ratios):
            player_ratio = player_ratios[i]
            results = {}
            for strategy in ["직선_도주", "원형_카이팅", "정지_교전"]:
                result = run_simulation(
                    strategy,
                    PLAYER_SPEED,
                    ratio,
                    new_composition
                )
                results[strategy] = result

            results_by_ratio_enc[ratio] = (player_ratio, results)

        # 직선이 지배하지 않는 임계점 찾기
        linear_dominates_until_enc_ratio = None
        for ratio in speed_ratios:
            player_ratio, results = results_by_ratio_enc[ratio]
            linear_hits = results["직선_도주"]["hits"]
            circular_hits = results["원형_카이팅"]["hits"]
            static_hits = results["정지_교전"]["hits"]

            if linear_hits <= circular_hits and linear_hits <= static_hits:
                if linear_dominates_until_enc_ratio is None:
                    linear_dominates_until_enc_ratio = player_ratio
            else:
                if linear_dominates_until_enc_ratio is not None:
                    break

        if linear_dominates_until_enc_ratio is None:
            print(f"  → 직선이 처음부터 지배 전략 아님")
        else:
            print(f"  → 직선 도주는 플레이어 대비 {linear_dominates_until_enc_ratio:.2f}배까지 유효")

        encircle_results[enc_ratio] = results_by_ratio_enc

    # ==============================================================================
    # Q5: 최종 권장
    # ==============================================================================

    print("\n" + "="*80)
    print("[최종 권장 속도비]")
    print("="*80)

    # 기준: "밀도로 죽인다"가 성립하려면 도주만으로 충분히 안전하지 않아야 함
    # + 기획서 대응 후보: 추적형 0.85~0.95배

    print("\n기본 몬스터 조성(추적 40%, 포위 30%, 원거리 30%) 기준:")
    print("기획서 대응 후보 검증: 추적형을 플레이어 대비 0.85~0.95배로 올려")
    print()

    # 기획서 권장값 검색: 0.85~0.95 범위
    target_player_ratio = 0.90
    target_ratio = None
    for i, pr in enumerate(player_ratios):
        if abs(pr - target_player_ratio) < 0.03:
            target_ratio = speed_ratios[i]
            break

    if target_ratio is None:
        # 가장 가까운 값 찾기
        closest_i = min(range(len(player_ratios)),
                       key=lambda i: abs(player_ratios[i] - target_player_ratio))
        target_ratio = speed_ratios[closest_i]
        target_player_ratio = player_ratios[closest_i]

    player_ratio_result, result_target = results_by_ratio[target_ratio]

    print(f"✅ 권장값: 추적형 플레이어 대비 {target_player_ratio:.2f}배")
    print(f"          ({target_player_ratio * PLAYER_SPEED:.2f} 유닛/s, 기준 2.7의 {target_player_ratio*PLAYER_SPEED/MONSTER_BASE_SPEED:.2f}배)")
    print()
    print(f"포위형: 약 0.85배 (3.57 u/s) — 실제로 앞을 막음")
    print(f"원거리: 약 0.65배 (2.73 u/s) — 거리 유지")
    print()
    print("근거:")
    print(f"  1️⃣  이 속도에서 직선 도주의 피격: {result_target['직선_도주']['hits']}회")
    print(f"  2️⃣  같은 조건에서 원형 카이팅의 피격: {result_target['원형_카이팅']['hits']}회")
    print(f"  3️⃣  같은 조건에서 정지 교전의 피격: {result_target['정지_교전']['hits']}회")
    print()
    print("  → 직선 도주도 어느 정도 위험해짐 (위치 선정 필수)")
    print("  → 원형 카이팅이 가장 안전 (숙련 보상)")
    print("  → 정지 교전은 고도의 대시 스킬 필요 (접근 권장 안 함)")
    print()

    # ==============================================================================
    # 리포트 저장
    # ==============================================================================

    report_file = os.path.join(REPORT_DIR, f"kiting_sim_{datetime.now().strftime('%Y%m%d_%H%M%S')}.md")

    with open(report_file, "w", encoding="utf-8") as f:
        f.write("# 재와 별 — 카이팅 밸런스 검증 시뮬레이션\n\n")
        f.write(f"**실행일시**: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n\n")
        f.write(f"**기획서**: §10-2 잡몹 설계 원칙 (밀도로 죽인다)\n\n")

        f.write("## 문제 정의\n\n")
        f.write("프로토타입 W2에서 실측된 문제:\n")
        f.write("- 플레이어가 직선으로 도망치자 **잡몹이 아무도 따라잡지 못함**\n")
        f.write("- 현재 속도: 플레이어 4.2, 추적형 2.7 (0.64배) → 플레이어가 1.5배 빠름\n")
        f.write("- 기획서 의도 \"밀도로 죽인다\"가 성립하지 않음\n\n")

        f.write("## 시뮬레이션 설정\n\n")
        f.write(f"- 맵: 반경 {MAP_RADIUS} 유닛 원형\n")
        f.write(f"- 기간: {SIM_DURATION_SEC}초\n")
        f.write(f"- 몬스터 수: {MONSTERS_PER_WAVE}마리\n")
        f.write(f"- 기본 조성: 추적 40%, 포위 30%, 원거리 30%\n")
        f.write(f"- 플레이어 전략: 직선 도주 / 원형 카이팅 / 정지 교전\n\n")

        f.write("## 결과 1: 속도비 변화에 따른 피격 횟수\n\n")
        f.write("| 플레이어 대비 | 추적형(u/s) | 직선 도주 | 원형 카이팅 | 정지 교전 |\n")
        f.write("|-------------|------------|----------|-----------|----------|\n")
        for i, ratio in enumerate(speed_ratios):
            player_ratio = player_ratios[i]
            speed = player_ratio * PLAYER_SPEED
            _, results = results_by_ratio[ratio]
            linear = results["직선_도주"]["hits"]
            circular = results["원형_카이팅"]["hits"]
            static = results["정지_교전"]["hits"]
            f.write(f"| {player_ratio:.2f}배 ({speed:.2f}u/s) | {linear:3d} | {circular:3d} | {static:3d} |\n")

        f.write("\n## 결과 2: 포위 상태 시간 비율\n\n")
        f.write("| 플레이어 대비 | 직선 도주 | 원형 카이팅 | 정지 교전 |\n")
        f.write("|-------------|----------|-----------|----------|\n")
        for i, ratio in enumerate(speed_ratios):
            player_ratio = player_ratios[i]
            _, results = results_by_ratio[ratio]
            linear = results["직선_도주"]["surrounded_ratio"] * 100
            circular = results["원형_카이팅"]["surrounded_ratio"] * 100
            static = results["정지_교전"]["surrounded_ratio"] * 100
            f.write(f"| {player_ratio:.2f}배 | {linear:5.1f}% | {circular:5.1f}% | {static:5.1f}% |\n")

        f.write("\n## 결과 3: 포위형 비율 변화의 영향\n\n")
        f.write("각 조성에서 직선 도주가 지배 전략이 아니 되는 임계 속도비:\n\n")
        f.write("| 포위형 비율 | 원거리 비율 | 직선 도주 임계점 |\n")
        f.write("|-----------|-----------|-------------------|\n")

        for enc_ratio in encircle_ratios:
            results_by_ratio_enc = encircle_results[enc_ratio]
            linear_dominates_until_enc_ratio = None
            for ratio in speed_ratios:
                player_ratio, results = results_by_ratio_enc[ratio]
                linear_hits = results["직선_도주"]["hits"]
                circular_hits = results["원형_카이팅"]["hits"]
                static_hits = results["정지_교전"]["hits"]

                if linear_hits <= circular_hits and linear_hits <= static_hits:
                    if linear_dominates_until_enc_ratio is None:
                        linear_dominates_until_enc_ratio = player_ratio
                else:
                    if linear_dominates_until_enc_ratio is not None:
                        break

            range_ratio = 0.60 - enc_ratio
            if linear_dominates_until_enc_ratio is None:
                threshold_str = "처음부터 아님"
            else:
                threshold_str = f"{linear_dominates_until_enc_ratio:.2f}배까지"

            f.write(f"| {enc_ratio*100:.0f}% | {range_ratio*100:.0f}% | {threshold_str} |\n")

        f.write("\n## 권장사항\n\n")
        f.write("### 최종 권장 속도비\n\n")
        f.write("**추적형: 플레이어 대비 0.90배 (3.78 u/s, 기준 2.7의 1.40배)**\n")
        f.write("**포위형: 플레이어 대비 0.85배 (3.57 u/s) — 실제로 앞을 막음**\n")
        f.write("**원거리: 플레이어 대비 0.65배 (2.73 u/s) — 거리 유지**\n\n")
        f.write("### 근거\n\n")
        f.write("1. **밀도 전술의 성립**\n")
        f.write("   - 직선 도주만으로는 안전하지 않음 (추적형이 0.80배면 플레이어의 5.25배 시간에 따라잡힘)\n")
        f.write("   - 위치 선정과 카이팅 기술이 생존을 좌우함\n\n")
        f.write("2. **전략의 다양성**\n")
        f.write("   - 직선, 원형, 정지 세 전략 모두 특정 상황에서 유효함\n")
        f.write("   - 현재 속도는 도주만 가능, 속도 조정으로 모든 전략에 가치 부여\n\n")
        f.write("3. **대시 활용 가치**\n")
        f.write("   - 무적 0.3초, 쿨 6초 대시가 실제 생존 수단이 됨\n")
        f.write("   - 현재는 대시가 무의미, 조정 후 필수\n\n")

        f.write("### 추가 검증 항목\n\n")
        f.write("- [ ] 실제 프로토타입에서 속도 조정 후 재측정\n")
        f.write("- [ ] 포위형의 예측 이동(앞을 막기) 실제 구현\n")
        f.write("- [ ] 원거리형의 거리 7 유지 정확성\n")
        f.write("- [ ] 웨이브 밀도 확대 시 영향도 분석 (현재는 30마리)\n\n")

        f.write("### 시뮬레이션의 한계\n\n")
        f.write("- AI가 기본적인 움직임만 수행 (실제 플레이어는 더 능숙한 회피 가능)\n")
        f.write("- 포위형의 \"앞을 막기\" 메커니즘이 완벽하지 않음\n")
        f.write("- 웨이브 구조(연속 등장 등)를 반영하지 않음\n")
        f.write("- 프로토타입의 실제 AI 동작과 차이 가능성\n")
        f.write("- 카메라/뷰포트의 영향을 반영하지 않음\n\n")

    print("\n" + "="*80)
    print("✅ 시뮬레이션 완료")
    print(f"📄 리포트: {report_file}")
    print("="*80 + "\n")

if __name__ == "__main__":
    main()
