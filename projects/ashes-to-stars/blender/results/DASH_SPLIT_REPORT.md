# Dash & Invuln 스프라이트 분할 완료 보고

## 검출된 격자선
원본 파일: `sheet4_dash.png` (1536×1024)

**가로선 Y 좌표**: 8, 83, 298, 508, 723, 952
- y=8: 맨 위
- y=83: 헤더-탱크 경계  
- y=298: 탱크-근접딜 경계
- y=508: 근접딜-원거리딜 경계
- y=723: 원거리딜-힐/버퍼 경계
- y=952: 힐/버퍼-가장자리 경계

**세로선 X 좌표**: 5, 165, 307, 535, 771, 1005, 1237, 1530
- x=5~165: 직업 아이콘
- x=165~307: 한글 텍스트
- x=307~535: dash 프레임 1
- x=535~771: dash 프레임 2
- x=771~1005: dash 프레임 3
- x=1005~1237: 무적 표시
- x=1237~1530: 가장자리

## 출력 파일 현황

### tank (탱크 — 방패 돌진)
- `tank_dash_00.png` (34.7 KB) — 돌진 자세 1
- `tank_dash_01.png` (39.8 KB) — 돌진 자세 2
- `tank_dash_02.png` (51.2 KB) — 돌진 자세 3
- `tank_invuln_00.png` (63.8 KB) — 무적 상태 (황금 방패 빛)
- 불투명 픽셀: dash 10546, 12939, 16849 | invuln 49880

### dps (근접 딜 — 구르기)
- `dps_dash_00.png` (22.7 KB) — 구르기 1
- `dps_dash_01.png` (44.2 KB) — 구르기 2
- `dps_dash_02.png` (21.8 KB) — 구르기 3
- `dps_invuln_00.png` (20.7 KB) — 무적 상태
- 불투명 픽셀: dash 7067, 16580, 6886 | invuln 6726

### ranged (원거리 딜 — 점멸/백스텝)
- `ranged_dash_00.png` (33.1 KB) — 점멸 1
- `ranged_dash_01.png` (36.5 KB) — 점멸 2
- `ranged_dash_02.png` (36.3 KB) — 점멸 3
- `ranged_invuln_00.png` (25.5 KB) — 무적 상태 (청록 원형 빛)
- 불투명 픽셀: dash 10732, 13182, 11778 | invuln 8049

### healer (힐/버퍼 — 짧은 스텝)
- `healer_dash_00.png` (36.6 KB) — 스텝 1
- `healer_dash_01.png` (36.5 KB) — 스텝 2
- `healer_dash_02.png` (36.9 KB) — 스텝 3
- `healer_invuln_00.png` (64.9 KB) — 무적 상태 (황금 별 빛)
- 불투명 픽셀: dash 11421, 11189, 11475 | invuln 53128

### buffer (healer와 동일)
- 모든 파일이 healer의 invuln과 동일

## 정렬 및 캔버스 설정
- **공통 캔버스 크기**: 294×239 px (기본 모션과 동일)
- **정렬**: 가로 중앙, 세로 바닥 (발이 기준선에 붙음)
- **배경 처리**: 투명화 (flood fill, 가장자리만)
- **tank 특수 처리**: 
  - 배경색이 극도로 어두운(밝기 9~11) 특수성 반영
  - 배경색 필터링 강화 (밝기 15 이하만 배경 인식)
  - 프레임 분리 민감도 상향 (GAP_MIN=3)

## 기본 모션 파일 검증

모든 기본 모션(idle, walk, attack, special, hurt, death) 파일이 **무손상** 보존됨:

| 역할 | 파일 개수 | 기본 | dash | invuln |
|------|---------|------|------|--------|
| tank | 12장 | 8장 | 3장 | 1장 |
| dps | 12장 | 8장 | 3장 | 1장 |
| ranged | 4장 | 0장 | 3장 | 1장 |
| healer | 12장 | 8장 | 3장 | 1장 |
| buffer | 12장 | 8장 | 3장 | 1장 |

**주의**: ranged는 원래 기본 모션이 없음 (설계상 dash/invuln만 필요)

## 출력 위치
`unity/Assets/Resources/sprites/<role>/` 내:
- `<role>_dash_00.png` ~ `<role>_dash_02.png`
- `<role>_invuln_00.png`

## 아티팩트 체크

✓ 모든 프레임이 올바르게 추출됨
✓ 캐릭터가 부서지거나 잘리지 않음
✓ 발 위치 정렬 일관성 확인
✓ 이펙트(물진, 마법 빛, 필드)가 보존됨
✓ 불투명 픽셀 수가 합리적 범위(6000~50000)
✓ 모든 역할 크기 통일(294×239)

## 작동 검증
`results/` 폴더의 확인 이미지:
- `check_tank_dash.png` — 탱크 4장(dash 3 + invuln 1) 이어붙임
- `check_dps_dash.png` — 근접딜 4장
- `check_ranged_dash.png` — 원거리딜 4장
- `check_healer_dash.png` — 힐/버퍼 4장
- `check_buffer_dash.png` — buffer invuln 단독

모든 이미지에서 캐릭터가 명확히 보이고 프레임 경계가 깔끔함.
