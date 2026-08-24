# 재와 별 — 작업 인계 기록

> 오너 지시(2026-08-15): "세션 길어질 경우 해당 세션 작업 내용 잘 기록해서 계속 연결해서 작업할 수 있게"
>
> **세션이 끊기면 이 파일부터 읽는다.** 커밋 로그는 "무엇을 했나"만 남기고 "지금 어디까지
> 와 있고 다음이 뭔가"는 안 남는다. 그 간극을 여기서 메운다.
>
> **갱신 규칙**: 작업 마디(커밋 단위)마다 「진행 중」을 갱신한다. 끝난 것은 「완료」로 내리되
> **판정 근거(수치·커밋 해시)를 같이 적는다** — 근거 없는 완료는 다음 세션이 다시 검증해야 한다.

---

## 지금 (2026-08-23) — 문서 정합

> 현재 큐의 권위는 `docs/STATUS.md`. INBOX의 EstateBuild 1순위·아래 옛 「루프 정지」 안내는 **닫힌 이력**이다.

| 트랙 | 상태 | 근거 |
|---|---|---|
| 영지 §2-3 건물별 레벨·업그레이드 창 | 닫음 | SelfCheck PASS · `25559505` |
| 영지 §2-2/§5 드래그 | 닫음 | SelfCheck PASS · `6d9b4fae`. 경로 목적지는 `EstateStore.Reached`. `StoreX/StoreY`는 **기본 스폰 상수**로만 남음 |
| 영지 §6 아트(_1/_2·공사판) | **닫음** | `d461fbcb` · `EstateArtTierSelfCheck` PASS · PNG `estate_tier_shots/qa_go:Estate.png` · `_0` 재생성 안 함 |
| 개발 직렬 | 투사체상한 닫음 | 소환수상한 `bfff2789` · 투사체상한 `89e7136d`(ProjCap·QA_NO). `W3Party` 금지 |
| VFX §6-P1#5 대시 잔상 | **닫음** | `2cd07dbe` — `fx_dash_trail_0~2` Resources 반입 + `W2Arena` 트레일 풀 8장(0.04s 간격·알파 0.6→0·0.15s·order 210). 샷 쌍 `unity/results/vfx_dash_trail_{on,off}.png` + 네거티브(로드 차단→풀 미생성) PASS. INBOX 21:59 소비 |
| UI 폴리싱 다음 | 스타일 선택 바-내비 닫음 | 캐릭터 `04063950` · 스타일 `78ac5212` StyleHud. 던전은 하단바 없음. 다음은 파티 편성 탭 하단 안내줄 1건 |
| launchd 자율 루프 | **상시 켜짐**(오너 2026-08-24) | grok · `com.ailab.autonomous_loop`. 한 작업마다 보고·커밋 |
| 사람 관문 | **더미로 진행**(오너 2026-08-24) | V2 PASS 5/5 · V3 FAIL 3/5 · V4 PASS 10/10 · 관문② PASS 9/10 · 시드 20260823. W2 FAIL은 제외(기준 낮추지 말 것) |

생성 파이프라인은 `art/aigen.py` = 힉스필드 `nano_banana_flash`. `loop/PROMPT.md` ③: 그림은 힉스필드 또는 그록 이매진. 재와별 반입은 aigen.py가 기본, 힉스필드가 막히면 이매진.

## 완료 (2026-08-25) — 전투 스타일 선택 바-내비 (Grok)

직전 트랙 폴리싱(캐릭터 액션바) → 스타일 화면 한 결함. DrawChoice 두 장이
본문 yMax=640에 붙어 내비 플레이트(636)와 겹쳤다. `StyleHud.Content`가
NavPlateTop-12. `QA_NO_STYLE_NAV`면 옛 640 겹침. 던전은 하단바가 없어
내비 결함이 아니다. 표시 전용 — W3Party 무접촉.

샷 `style_hud_nav_shots/after.png` — 이펙트 테스트·돌아가기 금테가 내비 위.
네거티브 `neg.png` — QA_NO면 금테가 내비 타일에 먹힘.
코드 `78ac5212`. 다음은 소비처0 재스캔 또는 파티 편성 하단 안내줄.

### 검수
- `game_compile_check.py` PASS (322소스 0).
- `StyleHudSelfCheck` PASS (아랫변 624 · 간격 12 · 차단 아랫변 640).
- 샷 쌍 플레이모드 육안 확인. 블렌더 꺼짐(3D 없음).

### 영지 — 남은 순서 (`GAME_SPEC_ESTATE_BUILD.md`)
1. ~~§2-3 건물별 레벨·업그레이드 창~~ — 닫음 `25559505`.
2. ~~§2-2 드래그 + 경로 재계산~~ — 닫음 `6d9b4fae`. (`StoreX/StoreY` 상수 제거는 기본 스폰만 남아 **열지 않음**.)
3. ~~**§2-4/§6 아트**~~ — 닫음. 17장 반입 · `EstateArtTierSelfCheck` PASS · `estate_tier_shots/qa_go:Estate.png`.

원장 `docs/GAME_SPEC_ESTATE_BUILD.md`. **§2-1의 「⚠️ 정정」** — 자리 크기로 그림 크기까지 내면 거인이 된다(`b414e914`).

## 완료 (2026-08-23) — 캐릭터 SkillDescLine 우측 잘림 (Grok)

직전 트랙 코드(SkillDef.설명) → 캐릭터 속성 한 결함. Info LabelClip(inner 20px)이
마법사 「빙결: 광」에서 잘렸다. `InfoWrap`(LabelFit·최소 52px)으로 접고,
`QA_NO_SKILL_DESC_WRAP`면 옛 한 줄 Clip. 표시 전용 — W3Party 무접촉.

샷 `skill_desc_wrap_shots/qa_go:Character.png` — 「스킬 설명 — 화염폭풍: 장판 광역 — 4체 이상 밀집 시 · 점멸: 순간이동 회피 · 빙결: 광역 슬로우」(끝 글자까지).
네거티브 `qa_negctrl_no_wrap.png` — `QA_NO_SKILL_DESC_WRAP=1`이면 다시 「빙결: 광」에서 잘림.
코드 `6ba0a995`. 다음은 소비처0(직전=폴리싱). 소비처0 다음은 SkillDef.초필살기.

### 검수
- `game_compile_check.py` PASS (291소스 0).
- `unity_meas` batch `SkillDescSelfCheck` **PASS** (`results/skill_desc_selfcheck.log`).
- 샷 `output/qa/ashes-to-stars/skill_desc_wrap_shots/qa_go:Character.png` — 빙결: 광역 슬로우 보임.
- 네거티브 `qa_negctrl_no_wrap.png` — 빙결: 광에서 잘림.

## 완료 (2026-08-23) — §3 SkillDef.설명 소비처 (Grok)

원장 §3 직업 스킬 표의 설명 열·SkillDef TextArea가 ProjectSetup·에셋에 authored돼
있으면서도 grep 소비처 0곳이었다. 형제 쿨·위력·반경·자원소모는 SkillLine이 읽는데
설명만 죽어 있던 함정. `JobInfo.SkillDescLine`이 「이름: 설명」을 별도 한 줄로 낸다
(SkillLine에 붙이면 LabelClip이 뒷 스킬 이름을 자름). `QA_NO_SKILL_DESC`면 빈 문자열
(옛 화면 = 설명 행 없음). 표시 전용 — W3Party 무접촉.

샷 `skill_desc_shots/qa_go:Character.png` — 마법사 「스킬 설명 — 화염폭풍: 장판 광역 — 4체 이상 밀집 시 · 점멸: 순간이동 회피 · 빙결: 광」(우측 잘림은 다음 폴리싱).
네거티브 `skill_desc_shots/qa_negctrl_no_desc.png` — `QA_NO_SKILL_DESC=1`이면 설명 행 없이 종족 특성 줄이 그 자리에 온다.
코드 `017335fc`. 다음은 폴리싱(직전=코드). 소비처0 다음은 SkillDef.초필살기.

### 검수
- `game_compile_check.py` PASS (291소스 0).
- `unity_meas` batch `SkillDescSelfCheck` **PASS** (`results/skill_desc_selfcheck.log`).
- 샷 `output/qa/ashes-to-stars/skill_desc_shots/qa_go:Character.png` — 화염폭풍 장판 광역 보임.
- 네거티브 `qa_negctrl_no_desc.png` — 설명 행 없음.

## 완료 (2026-08-23) — 필드 시간당 HuntGoldHourLine ShortCopper (Grok)

직전 트랙 코드(SkillDef.반경) → 필드 화면 한 결함. T2 `FormatCurrency(16000)`이
「1골드 60실버」라 자막이 길어진다. `HuntGoldHourLine`이 `ShortCopper`만 읽는다.
T1·T2 모두 「필드 1골드/h(§18-1)」. `QA_NO`면 「필드 골드 없음」.
`HuntGoldLine` 획득 줄은 FormatCurrency 유지(전투 결과 · 루프 밖).
샷 `hunt_gold_hour_shots/qa_go:Field.png` — 자막 `세계 T1 · 필드 1골드/h(§18-1)`.
코드 `261c8a21`. TokenPrice ShortCopper(`70f4a5ba`)는 이미 닫혀 있어 STATUS만 따라잡았다.
다음은 소비처0 한 칸(직전=폴리싱). 폴리싱 다음은 지갑 WalletText.

### 검수
- `game_compile_check.py` PASS (289소스 0).
- `unity_meas` batch `HuntGoldSelfCheck` **PASS** (`results/hunt_gold_hour_selfcheck.log`).
- 샷 `output/qa/ashes-to-stars/hunt_gold_hour_shots/qa_go:Field.png` — `필드 1골드/h(§18-1)` 보임. 지갑 `553골드 30실버 8쿠퍼`는 다음 칸.

## 완료 (2026-08-23) — §3 SkillDef.반경 소비처 (Grok)

원장 SkillDef 툴팁 「효과 반경(0이면 단일)」의 `SkillDef.반경`이 ProjectSetup·에셋에 authored돼
있으면서도 grep 소비처 0곳이었다. 형제 쿨·위력은 SkillLine이 「(N초·×P)」로 읽는데 반경만 죽어
있던 함정. `JobInfo.SkillLine`이 0&lt;반경&lt;50일 때 「·반경R」을 이어 붙인다(쿨 0은 괄호 없이
「성채 방패 반경8」). 기적 authored 99는 전역 표식이라 숫자를 안 붙인다. 표시 전용 — W3Party·전투
무접촉. `QA_NO_SKILL_RAD`면 반경 조각만 빼고 옛 줄.

그록봇 §6 아트(`d461fbcb`)는 이미 닫혀 있어 이 바퀴는 다음 소비처0 한 칸으로 진행했다.

### 검수
- `game_compile_check.py` PASS (288소스 0).
- `unity_meas` batch `SkillRadSelfCheck` **PASS** (exit 0).
- 샷 `skill_rad_shots/qa_go:Character.png` — 수호기사 「도발의 함성(6초·반경4.5) · 성채 방패 반경8 · 최후의 보루(40초)」.
- 네거티브 `skill_rad_shots/qa_negctrl_no_rad.png` — `QA_NO_SKILL_RAD=1`이면 「도발의 함성(6초) · 성채 방패 · 최후의 보루(40초)」 (반경 없음).

## ⛔ 이력 — 루프가 멈춰 있던 날 (2026-08-20 01:06)

> **✅ 수리됨(2026-08-20).** 폴백을 그 이터에만 적용 + `loop/grok_dead` TTL. 지금 켜지 말라는 안내가 아니다.
> 원장: HARNESS_GUARDRAILS_LEDGER.md.

### 무슨 일이 났나
1. 20:43에 `loop/agent=claude`로 정상 기동해 실제로 일했다(폴리싱 커밋 3건:
   `8028015a` 결과 화면 겹침 · `fb7cd614` 던전 노드 아이콘 · `e8cb5d78` §18-14 광산 Tick).
2. 그 뒤 **클로드 한도**에 걸렸다 → `loop.sh:281`의 폴백이 **그록으로 전환**했다
   (`🔀 claude 한도 — Grok으로 전환하고 바로 재개`, 메인 로그 1139·1275줄).
3. **그런데 그록은 주간 한도 소진 상태였다**(오너 확인 2026-08-19). 402 `Grok Build usage
   balance exhausted`가 **12회 연속** → 루프가 스스로 `loop/STOP`을 만들고 01:06에 종료.

### 결함 — 폴백 대상이 죽어 있는데 확인을 안 한다 (당시. 2026-08-20에 수리됨)
`loop.sh:281-288`은 한도에 걸리면 **그록 CLI가 있기만 하면**(`grok_bin`) 전환하고,
그 값을 `loop/agent`에 **영구히 써 버린다**. 그래서 **그날** `loop/agent = grok`이었다.
인프라 장애를 실패로 안 세는 처리(옳다) 때문에 12회를 조용히 태운 뒤에야 멈췄다.

### 켜기 전에 할 것
```bash
echo claude > loop/agent     # ← 이걸 안 하면 그록으로 간다
rm -f loop/STOP
./loop/loop.sh
```
근본 수리를 하려면 `loop.sh`의 전환 조건에 **그록 잔량 확인**을 넣거나(402면 전환 안 함),
전환을 `loop/agent`에 영구 기록하지 말고 그 이터레이션에만 적용할 것. (2026-08-20에 반영됨.)

## 완료 (2026-08-23) — 영지 §6 건물 티어·공사판 아트 (Grok)

- 생성: `aigen.py` + `spec_estate_tiers.json` → `out_estate_tiers/` 17장(`*_1`/`*_2` 16 + `estate_scaffold_0`). 기존 `estate_*_0` 재생성 안 함.
- 반입: `import_estate_tiers.py` → `Resources/props` + unique `.meta`. `prop_scale.json` 티어·공사판 스케일.
- 배선: `EstateBuildings.PropOf` 레벨 구간 티어 폴백 · `EstateYard.DrawScaffoldIfBusy`.
- 검증: `unity_meas` `EstateArtTierSelfCheck` **PASS** (`results/estate_art_tier_selfcheck.log`).
- 샷: `output/qa/ashes-to-stars/estate_tier_shots/qa_go:Estate.png`.

---

## 완료 (2026-08-23) — §3 SkillDef.위력배율 소비처 (Grok)

원장 §3 직업 스킬 표 「기본 공격력 대비 배율」의 `SkillDef.위력배율`(화염폭풍 1.2·조준 2·일섬 3.2 등)이
ProjectSetup·Job_*.asset에 authored돼 있으면서도 grep 소비처 0곳이었다. 형제 쿨다운은 SkillLine이
「(N초)」로 읽는데 위력만 죽어 있던 함정. SkillLine이 기준(×1)·0이 아닐 때만 「×P」를 붙인다
(쿨과 함께면 `(N초·×P)`, 쿨0이면 `이름 ×P`). 표시 전용 — W3Party 무접촉. `QA_NO_SKILL_POW`면
위력 조각만 빼고 옛 줄(이름·쿨) 회귀.

RaceDef 후보(방어배율 등)는 grep 결과 W3Party/Economy 소비처가 있어 건너뜀.

### 검수
- `unity_meas` batch SelfCheck **PASS** (exit 0)
- MenuItem `Ashes to Stars/QA/Skill Pow Self Check`
- 로그: `projects/ashes-to-stars/results/skill_pow_selfcheck_20260823_161835.log` — `[SkillPowSelfCheck] PASS`
- 소비처: CharacterScreen 속성 탭 `JobInfo.SkillLine`

---

## 완료 (2026-08-23) — §18-9 RaceDef.체력배율 소비처 (Grok)

원장 §18-9 엘프 표 「HP -15%」의 `RaceDef.체력배율`(에셋 0.85)이 authored돼 있으면서도
grep 소비처 0곳이었다. `RaceInfo.HealthLine`이 기준(×1)과 다를 때만 「종족 체력 — ×0.85 (-15%)」.
표시 전용 — W3Party·전투 HP 무접촉. `QA_NO_RACE_HEALTH`면 옛 화면 회귀.

### 검수
- `unity_meas` batch SelfCheck **PASS**
- MenuItem `Ashes to Stars/QA/Race Health Self Check`
- 로그: `projects/ashes-to-stars/results/race_health_selfcheck_20260823_161144.log` — `[RaceHealthSelfCheck] PASS`
- 소비처: CharacterScreen 속성 탭 `RaceInfo.HealthLine`

---

## 완료 (2026-08-23) — §18-9 RaceDef.이속배율 소비처 (Grok)

원장 §18-9 드워프 표 「이속 -15%」의 `RaceDef.이속배율`(에셋 0.85)이 ProjectSetup·Race_*.asset에
authored돼 있으면서도 grep 소비처 0곳이었다. 형제 영지생산·골드소비·드랍률·인식범위는
Economy/EstateMine/WorldStar가 읽는데 이속만 죽어 있던 함정(전투당발동·쿨다운과 동일 계열).
`RaceInfo.SpeedLine`이 기준(×1)과 다를 때만 「종족 이속 — ×0.85 (-15%)」를 낸다(×1은 빈 문자열로
패널 밀도 유지). 표시 전용 — W3Party·전투 이동 수치 무접촉. `QA_NO_RACE_SPEED`면 항상 빈
문자열(옛 화면 = 이속 줄 없음)로 회귀.

### 검수
- `unity_meas` sync 후 batch SelfCheck **PASS** (exit 0).
- MenuItem `Ashes to Stars/QA/Race Speed Self Check`.
- 로그: `projects/ashes-to-stars/results/race_speed_selfcheck_20260823_160518.log` — `[RaceSpeedSelfCheck] PASS` 전항.
- 소비처: CharacterScreen 속성 탭 `RaceInfo.SpeedLine` (드워프 「×0.85 (-15%)」).

---

## 완료 (2026-08-23) — §3 SkillDef.쿨다운 소비처 (Grok)

원장 §3 직업 스킬 표(✅)·SkillDef 툴팁 「마나 없음, 쿨다운 단일 체계」의 `SkillDef.쿨다운`이
ProjectSetup·에셋에 authored돼 있으면서도 grep 소비처 0곳이었다. 형제 `이름`만 SkillLine이
읽고 같은 행의 쿨다운만 죽어 있던 함정(이동기거리·전투당발동과 동일 계열). `JobInfo.SkillLine`이
쿨다운 > 0일 때 「이름(N초)」를 이어 붙인다(0=게이지/패시브형은 이름만). 표시 전용 — W3Party·전투
무접촉. `QA_NO_SKILL_CD`면 옛 줄(이름만)로 회귀.

### 검수
- `unity_meas` sync 후 batch SelfCheck **PASS** (exit 0).
- MenuItem `Ashes to Stars/QA/Skill Cd Self Check`.
- 로그: `projects/ashes-to-stars/results/skill_cd_selfcheck_20260823_155637.log` — `[SkillCdSelfCheck] PASS` 전항.
- 소비처: CharacterScreen 속성 탭 `JobInfo.SkillLine` (수호기사 「도발의 함성(6초) · 성채 방패 · 최후의 보루(40초)」).

---

## 완료 (2026-08-23) — §18-9 RaceDef.전투당발동 소비처 (Grok)

원장 §18-9·라인 160 「발동을 전투당 1회로 묶어」의 `RaceDef.전투당발동`(default 1, 에셋 committed)이
정의만 있고 grep 소비처 0곳이었다. 형제 `고유발동확률`은 MechanicLine이 「(발동 N%)」로 읽는데
전투당만 죽어 있던 함정. `RaceInfo.MechanicLine`이 발동% 뒤에 「 · 전투당 K회」를 이어 붙이고,
에셋 문장에 박힌 「 (전투당 K회)」는 벗겨 필드가 단일 출처가 되게 했다. 표시 전용 — W3Party·전투
무접촉. `QA_NO_RACE_BATTLE_CAP`면 필드 조각을 빼고 옛 줄(문장 속 전투당 + 발동%)로 회귀.

### 검수
- `unity_meas` sync 후 batch SelfCheck **PASS** (exit 0).
- MenuItem `Ashes to Stars/QA/Race Battle Cap Self Check`.
- 로그: `projects/ashes-to-stars/results/race_battle_cap_selfcheck_20260823_064519.log` — `[RaceBattleCapSelfCheck] PASS` 전항.
- 소비처: CharacterScreen 속성 탭 `RaceInfo.MechanicLine` (드워프 「발동 25% · 전투당 1회」).

---

## 완료 (2026-08-23) — 영지 §5 건물 드래그 미리보기 (Grok)

`EstateScreen` 마을: 건물 위 프레스=선택, 드래그=반투명 유령+놓을 칸 강조, 드롭 시 `TryDragMove`(창고는 `EstateStore.TryMove`). `EstateYard`는 건물 위 시작만 이동 제스처·Pan 분리(`DragSlop`). `QA_ESTATE_DRAG`/`QA_NO_ESTATE_DRAG`. SelfCheck MenuItem `Ashes to Stars/QA/Estate Drag Self Check`.

### 검수
- `unity_meas` sync 후 batch SelfCheck **PASS** (exit 0).
- 로그: `projects/ashes-to-stars/results/estate_drag_selfcheck_20260823_152420.log` — `[EstateDragSelfCheck] PASS` 전항.

---

## 완료 (2026-08-23) — EstateBuild §2-3 건물별 레벨 (Grok)

`EstateBuild`를 Keep-only에서 IsCore 칸별 레벨·공사로 일반화. prefs `ats.estate.b.{Cell}.lv|to|done|orig|job`, 옛 `ats.estate.keep*` 이주. 본성 API는 Cell.Keep 위임. `EstateScreen` Keep·광산/창고 도크·허브 업그레이드 행 배선. `EstateBuildSelfCheck`에 광산·본성상한·병렬 busy·prefs 이주. 남은 영지: §5 드래그 UX → §6 아트.

### 검수 게이트 마무리 (세션교체 준호 · 2026-08-23 15:17)
- 선행 FAIL: worker STATUS 수정 · SelfCheck 증거 없음 → `5fa97195`로 STATUS 되돌림 + MenuItem.
- `unity_meas` sync 후 batch SelfCheck **PASS** (exit 0).
- 로그: `projects/ashes-to-stars/results/estate_build_selfcheck_20260823_151727.log` — `[EstateBuildSelfCheck] PASS` 전항.
- STATUS.md는 보드만 수정. 이 커밋은 WORKLOG만.

## 완료 (2026-08-21) — 5직업 모션 8프레임 통일 (대화 세션)

오너 지적 "모션마다 이미지 수가 왜 달라, 근본적으로 고쳐". 실측: 원본 시트 30장은
전부 4×2=8프레임 균일인데 **옛 6칸 코드 계약에 맞추느라 반입 때 모션당 2장씩 버리고
death는 1장만 남긴 것**이 근본 원인. 두 커밋으로 수리:
1. `164ea882` — SpriteBank 계약 6→8칸(Frame enum 46칸, death 8장 애니, 총길이 유지:
   Atk 0.50s·Sp 1.00s, DeathFrame 0.1s 신설).
2. `372bd4fe` — `art/import_p26_8frames.py`로 시트에서 8장 전량 재추출
   (idle/walk/attack/special/death 각 8장, dash=run[0,2,4,6]·hurt=death[0]·
   invuln=idle_01[0] 파생 유지 — **이 3종은 원본 시트가 없다**). MotionCycleSelfCheck
   기대치 6→8 + Death 순환 검사. 에디터 실행 JobAnimSelfCheck·MotionCycleSelfCheck
   모두 PASS, meta 46/46. 증빙 GIF `output/qa/ashes-to-stars/new_sprites_shots/jobs_8frame_grid.gif`.
   원격 푸시 완료(c809b786).

## 완료 (2026-08-20) — 신규 5직업 스프라이트 시트 반입·전투 아트 전면 교체 (대화 세션)

오너가 준 시트 30장(직업 5종 × idle/idle_01/run/attack/skill/death, 4x2/8프레임)을
`Assets/TestSpriteSheets/`에 반입하고 두 갈래로 연결했다:
1. **애니메이션 에셋**: `RpgSpriteAutoBuilder`(신규 에디터 도구, Tools 메뉴) 일괄 빌드로
   `Assets/GeneratedAnimations/`에 컨트롤러 5개 × 상태 6개 생성 — `acb69886`.
   격자 오판(이펙트가 셀 경계 넘는 시트 5장) → 셀 종횡비 페널티로 수리, 30/30 정상.
2. **전투 실사용 프레임 교체**: SpriteBank 계약(직업당 31장)에 맞춰 잘라
   `Resources/sprites/{tank,dps,mage,buffer,healer}/` 덮어씀(파일명·meta 유지) — `6ce16c74`.
   매핑 tanker→tank·assassin→dps·magican→mage·supporter→buffer·healer→healer,
   h=124px 정규화(PPU 32 세계 크기 유지). JobAnimSelfCheck 배치 PASS + 프레임 육안 대조.
   주의: hurt=death 시트 0번, invuln=idle_01 0번으로 대체(전용 아트 없음) — 전용 컷이
   생기면 `scratchpad`가 아니라 `swap_sprites.py` 방식으로 다시 뽑을 것(스크립트는 임시라
   저장 안 함, 커밋 메시지에 매핑 규칙 전부 있음).
   `all_classes_turnaround.png`(8방향 턴어라운드)는 다운로드에 남아 있고 미반입 —
   방향별 스프라이트 도입 때 쓸 것.

## 지금 (2026-08-19) — 던전 입장 카드 부제 한 줄

직전 트랙 코드 → 필드 화면 한 결함. 레이드급은 `7b61d1bb`로 줄었고 던전 입장만 옛 긴 줄.
`FieldDockCap.Dungeon`을 입장 카드가 읽는다. `랜덤 · 종점 보스`. `QA_NO`면 옛 긴 줄.
샷 `field_dungeon_cap_shots/qa_go:Field.png` — `던전 입장` · `랜덤 · 종점 보스`.
코드 `595a0e08`. 다음은 원장 ✅ 소비처 0곳. 이펙트는 FX 미커밋.

## 지금 (2026-08-19) — 보스 스킬 수 2→3→4→5

직전 트랙 UI → 코드 칸. 옛 층 ≤5/≤10이라 15층이 4페이즈.
`BossSkills.PhaseCount`를 CreateBosses·탑 자막·HP 바가 읽는다.
중간 2→3 · 대보스 2→3→4 · 50층+ 2→3→4→5. `QA_NO`면 옛 구간.
샷 `boss_skills_shots/qa_go:Tower.png` — `중간 2→3(§10-5)` · 탑 15층.
코드 `73195f8d`. 다음은 UI 한 결함(던전 입장 카드 부제). 이펙트는 FX 미커밋.

## 지금 (2026-08-19) — 레이드급 카드 부제 한 줄

직전 트랙 코드 → 필드 화면 한 결함. 배회 보스는 줄었고 레이드급만 옛 긴 줄.
`FieldDockCap.Raid`를 레이드급 카드가 읽는다. `5인 · 환생석 없음`. `QA_NO`면 옛 긴 줄.
샷 `field_raid_cap_shots/qa_go:Field.png` — `레이드급 19:59` · `5인 · 환생석 없음`.
코드 `7b61d1bb`. 다음은 원장 ✅ 소비처 0곳. 이펙트는 FX 미커밋.

## 지금 (2026-08-19) — 계열 상성 ×1.3 / ×0.7

직전 트랙 UI → 코드 칸. 옛 던전 제목은 `야수 계열`만 보여 §10-3 배율이 0곳.
`FamilyAdv.Mul`/`Title`을 던전 제목이 읽는다. 야수+마법사 1.3 · 궁수 0.7.
`QA_NO`면 옛 계열 제목. 전투 수치는 W3Party라 안 넣음.
샷 `family_adv_shots/qa_go:Dungeon.png` — `던전 · 야수 · 마법사·정령사 ×1.3`.
코드 `a7f82e6a`. 다음은 UI 한 결함. 이펙트는 FX 미커밋.

## 지금 (2026-08-19) — 배회 보스 부제 한 줄

직전 트랙 코드 → 필드 화면 한 결함. 일정·저체력은 줄었고 배회 보스만 옛 CardBody.
`FieldDockCap.Boss`를 배회 카드가 읽는다. `재의 야수 · 환생석 없음`. `QA_NO`면 옛 긴 줄.
샷 `field_boss_cap_shots/qa_go:Field.png` — `배회 보스 20:00` · `재의 야수 · 환생석 없음`.
코드 `61e9ad82`. 다음은 원장 ✅ 소비처 0곳. 이펙트는 FX 미커밋.

## 지금 (2026-08-19) — 영공 `1 + 층/10`

직전 트랙 UI → 코드 칸. 옛 SenseBase는 4~16 선형이라 §18-13 공식이 0곳.
`WorldStar.SenseMul`을 SenseBase·월드맵 자막이 읽는다. 100층 11. `QA_NO`면 옛 4~16.
샷 `star_sense_shots/qa_go:WorldMap.png` — `영공 11.00(§18-13)` · 판 `영공 11.0`.
코드 `484eaa4e`. 다음은 UI 한 결함(필드 허브 카드 글씨 잘림). 이펙트는 FX 미커밋.

## 지금 (2026-08-19) — 월드맵 수비대 카드 부제 한 줄

직전 트랙 코드 → 월드맵 화면 한 결함. 옛 수비대 카드가 `침략 전투는 아직 없다(§13-5)`(19자).
`WorldMapDockCap.Defense`를 수비대 카드가 읽는다. `잠김 — 침략 없음`. `QA_NO`면 옛 긴 줄.
샷 `worldmap_defense_shots/qa_go:WorldMap.png` — `수비대 0/5` · `잠김 — 침략 없음`.
코드 `17d19655`. 다음은 원장 ✅ 소비처 0곳. 이펙트는 FX 미커밋.

## 지금 (2026-08-19) — 대보스 개체 HP 2체 65 · 3체 45

직전 트랙 UI → 코드 칸. `CreateBosses`는 Hp(..., 1) 뒤에 로컬 0.65/0.45라 CountMul이 0곳.
`BossHp.CountMul`을 CreateBosses가 읽는다. 탑 자막 `2체 각 65%(§18-11)`.
`QA_NO`면 옛 DPS 100.
샷 `boss_countmul_shots/qa_go:Tower.png` — `대보스 2체 · 2체 각 65%`.
코드 `a3648bc6`. 다음은 UI 한 결함(월드맵 성계·랭킹 카드 부제). 이펙트는 FX 미커밋.

## 지금 (2026-08-19) — 월드맵 침략 카드 부제 한 줄

직전 트랙 코드 → 월드맵 화면 한 결함. 옛 침략 카드가 면·출정을 이어 붙여 34~58자.
`WorldMapDockCap.Caption`을 침략 카드가 읽는다. `북 3칸 · 출정` · 잠김 `30층 해금`.
`QA_NO`면 옛 긴 줄.
샷 `worldmap_dock_shots/qa_go:WorldMap.png` — `침략` · `남 1칸 · 출정`.
코드 `98591ad5`. 다음은 원장 ✅ 소비처 0곳. 이펙트는 FX 미커밋.

## 지금 (2026-08-19) — 정예 유형 1~2종 지도

직전 트랙 UI → 코드 칸. `WavePlan.EliteKinds`는 생성기만 채우고 지도가 안 읽었다.
`EliteKinds.Caption`을 정예 카드가 읽는다. `수호자 · 주술사(§10-2)`.
`QA_NO`면 옛 퍼센트 줄.
샷 `elite_kinds_shots/qa_go:Dungeon.png` — 부제 `수호자 · 주술사(§10-2)`.
코드 `0d1d5ac7`. 전투 기믹은 W3Party라 안 넣음. 다음은 UI 한 결함(월드맵 침략 카드 부제).

## 지금 (2026-08-19) — 탑 레이드(5층 단위) 도크 부제 한 줄

직전 트랙 코드 → 탑 화면 한 결함. 옛 레이드 카드가 비용+§9 설명을 이어 붙여 24~42자.
`TowerDockCap.Raid`를 도크가 읽는다. `대보스 ×2.2`. `QA_NO`면 옛 긴 줄.
샷 `tower_raid_dock_shots/qa_go:Tower.png` — `레이드 (5층 단위)` · `대보스 ×2.2`.
코드 `061f5415`. 다음은 원장 ✅ 소비처 0곳. 이펙트는 FX 미커밋.

## 지금 (2026-08-19) — 탑 하위 레이드 도크 부제 한 줄

직전 트랙 코드 → 탑 화면 한 결함. 옛 하위 카드가 FormatLine 셋을 이어 붙여 101자.
`TowerDockCap.Lower`를 도크가 읽는다. `×2 · 10종 · 0.65`. `QA_NO`면 옛 긴 줄.
샷 `tower_dock_shots/qa_go:Tower.png` — `하위 레이드 5층` · `×2 · 10종 · 0.65`.
코드 `7930b5cf`. 다음은 원장 ✅ 소비처 0곳. 이펙트는 FX 미커밋.

## 지금 (2026-08-19) — 목숨 시세 상한 300 G/h

직전 트랙 UI → 코드 칸. §18-4 부활초 3~8 · 두루마리 2~4 · 환생석 150~300의 상한만 소비처 0곳.
`LifePrice.Ceil`/`AboveCeil`을 `TryListItem`이 읽는다. T1 환생석 300골드.
`QA_NO`면 옛 상한 없음.
샷 `life_ceil_shots/qa_go:Estate.png` — `하한 · 환생석 150골드 · 상한 300골드`.
코드 `a17d310b`. 다음은 UI 한 결함(일정/저체력 도크 부제). 이펙트는 FX 미커밋.

## 지금 (2026-08-18) — 지갑 부제 한 줄

직전 트랙 코드 → 필드 화면 한 결함. 지갑 카드가 BagText 전부라 두 줄로 잘렸다.
`BagTextFmt.Caption`을 도크가 읽는다. 목숨 2종만. `QA_NO`면 옛 긴 줄.
샷 `bag_caption_shots/qa_go:Field.png` — `잠김 — 부활초 3/3 · 환생석 3`.
코드 `3ce682d2`. 다음은 원장 ✅ 소비처 0곳. 이펙트는 FX 미커밋.

## 지금 (2026-08-18) — 증표 시세 상한 400 G/h

직전 트랙 UI → 코드 칸. §18-4 `200~400`의 상한만 소비처 0곳.
`TokenPrice.Ceil`/`AboveCeil`을 `TryListItem`이 읽는다. T1 400골드.
`QA_NO`면 옛 상한 없음.
샷 `token_ceil_shots/qa_go:Estate.png` — `하한 200골드 · 상한 400골드`.
코드 `2c66efc1`. 다음은 UI 한 결함(지갑 부제 줄바꿈). 이펙트는 FX 미커밋.

## 지금 (2026-08-18) — 아틀라스 UV가 이웃을 물지 않는다

직전 트랙 코드 → 필드 화면 한 결함. HuntBoon·글씨 잘림은 이미 닫힘.
`TextureCoords`가 Pieces를 Width×Height로 나눈다. NPOT 1024면 옛 식은
tower 0.2686(지구본)·heart가 깨진 하트를 문다.
샷 `atlas_uv_shots/qa_go:Field.png` — 등대·하트.
코드 `795fd79b`. 다음은 원장 ✅ 소비처 0곳. 이펙트는 FX 미커밋.

## 지금 (2026-08-18) — 드랍 옵션이 체력을 올린다

직전 트랙 아트 → 코드 칸. INBOX 이펙트는 FX 미커밋이라 안 겹침.
`GearOpt.HpMul`을 `EffectiveHpMul`이 읽는다. 전설 4옵션 ×1.08.
강화와 같은 2%. `QA_NO`면 옛 ×1.
샷 `gear_opt_shots/qa_go:Character.png` — `옵션 체력 ×1.08(§11)`.
폴리싱 다음은 필드 허브 글씨(HuntBoon 도크는 `8a7e6b93`).

## 지금 (2026-08-18) — 월드맵 HUD 아래 도크

직전 트랙 코드 → 월드맵 화면 한 결함. INBOX 이펙트는 FX 미커밋이라 안 겹침.
`WorldMapHud.Cards`를 `WorldMapScreen`이 읽는다. 겹침 200. `QA_NO`면 옛 AfterPlate 2×2.
샷 `worldmap_hud_shots/qa_go:WorldMap.png` — 별·궤도, 아래 4칸.
코드 `27436bf0`. 폴리싱 다음은 HuntBoon.

## 지금 (2026-08-18) — 경매 복원 등급·옵션

직전 트랙 아트 → 코드 칸. INBOX 이펙트는 FX 미커밋이라 안 겹침.
`GearOpt.Pack`/`Parse`를 `TryListGear`·`RestoreListed`가 읽는다.
전설+옵션4 등록→취소 뒤 그대로. `QA_NO`면 옛 recipe|enhance.
샷 `gear_list_shots/qa_go:Character.png` — `경매도 옵션을 싣는다(§11)`.
코드 `cf5b27da`. 폴리싱 다음은 월드맵.

## 지금 (2026-08-18) — 탑 HUD 아래 도크

직전 트랙 코드 → 탑 화면 한 결함. INBOX 이펙트는 FX 미커밋이라 안 겹침.
`TowerHud.Cards`를 `TowerScreen`이 읽는다. 겹침 200. `QA_NO`면 옛 2×2.
샷 `tower_hud_shots/qa_go:Tower.png` — 계단·창, 아래 4칸.
코드 `3c68562a`. 폴리싱 다음은 월드맵.

## 지금 (2026-08-18) — 창고 현재 칸이 침략 경로

직전 트랙 아트 → 코드 칸. INBOX 이펙트는 FX 미커밋이라 안 겹침.
`EstateStore.Reached`를 `PathLength`가 읽는다. StoreX는 기본 스폰만.
TryMove (3,6) → 북 6·남 1. `QA_NO`면 옛 (3,3).
샷 `estate_store_shots/qa_go:{Estate,WorldMap}.png` — `창고 (3,6) · 남 1칸`.
코드 `b150d8a7`. 드래그 UX·EstateBuild는 다음. 폴리싱 다음은 탑.

## 지금 (2026-08-18) — 서포터 공격 프레임 반쪽

INBOX 22:03. 오너 시트는 몸 있음 · 반입만 슬래시 조각을 칸으로 썼다.
`tidy_row_cells`가 폭 < 중앙값 65%를 버린다. 재생성 없음.
unittest `test_import_owner_sheets` 3 OK.
샷 `buffer_sprite_shots/qa_go:Title.png` — 서포터 온전한 몸.
이펙트는 다음. 영지 `EstateBuild`는 클로드.

## 지금 (2026-08-18) — 필드 몹 5계열 알파 구멍

INBOX 22:03. p22 시트는 몸 있음 · Resources만 외곽선.
`repair_mob_alpha.py`가 5계열 22장을 다시 나눔. 재생성 없음.
`knock_bg`는 마젠타 칸에서 `_drop_paper_blobs`를 안 돈다.
unittest `test_mob_alpha`+`test_knock_bg` 11 OK.
샷 `mob_alpha_shots/qa_hunt.png` — 채워진 몸.
서포터·이펙트는 다음. 영지 `EstateBuild`는 클로드.

## 지금 (2026-08-18) — 영지 §1·§2·§3 (자리·앵커·팔레트)

설계 `docs/GAME_SPEC_ESTATE_BUILD.md`. 분담 §4-B: 그록이 자리·앵커·팔레트,
클로드가 `EstateBuild.cs`(건물별 업그레이드 창). 드래그는 이 커밋 뒤.

- 자리: Keep 2×2 (1,2) · Warehouse/Mine/Barracks 2×1 · 나머지 1×1.
  수비대 (7,4)는 2×1이 격자 밖이라 (6,4).
- 밑동은 자리 마름모 중심. `QA_NO_ESTATE_FOOTPRINT`면 옛 sit=0.42.
- 폴백 시 `LogWarning`. 8동 전용 PNG 사용 확인(폴백 0).
- 팔레트 `EstateHud.PaletteBar` — 아랫변 < 내비 플레이트 윗변.
- SelfCheck: Footprint·Grid·Hud·Buildings·Yard PASS.
- 다음: 클로드 `EstateBuild` §2-3. 그록은 커밋 후 드래그(§2-2) 또는 아트 §6.

## 지금 (2026-08-18) — 필드 지갑 카드 표기

루프: INBOX 21:47 필드 정예는 W3Party 킬 훅이 없어 못 열음(STATUS 한 줄).
직전 트랙이 코드라 필드 화면 한 결함 — `BagText()`의 `환생석 1/2147483647`.
`BagTextFmt.Format`이 무제한은 개수만. PNG `bag_text_shots/qa_go:Field.png`.
다음 INBOX 22:03·22:04(서포터·몹 알파·이펙트)가 큐보다 앞선다.
영지 설계 `docs/GAME_SPEC_ESTATE_BUILD.md`(`0949499d`) 들어옴 — 문서 전 금지는 풀림.

## 지금 — 그록 주간 한도 소진, 클로드가 영지 단독 (2026-08-19)

오너: 「그록 사용량 주간한도 다되서 못함」. 분담 회의(minutes_20260818_2215)가 그록에게
배정한 마디도 **전부 클로드가 이어받는다.**

**끝난 것**
- 자리 크기 점유·앵커: 그록 커밋 + 클로드가 크기 소스 되돌림(`b414e914`).
  `EstateFootprintSelfCheck` PASS(되돌리기 전 FAIL 1건 — 검사기가 틀린 사양을 굳히고 있었다).
- 전용 아트 폴백 의심: **오진.** 플레이어 로그 경고 0건, 8동 전부 전용 그림.
- 카드 글씨 잘림(`67664c3a`)·보너스 카드 하단 도크(`8a7e6b93`).

**남은 것 (블로커 그래프 순)**
1. §2-3 `EstateBuild.cs` 건물별 레벨 일반화 — **아직 아무도 안 잡았다.**
   호출부가 25개 API 약 180곳이라 본성 API를 전부 보존한 채 `Cell.Keep`으로 위임해야 한다.
2. §2-3 업그레이드 창(`EstateScreen`) — 1번 다음.
3. §2-2 드래그 이동 + `StoreX/StoreY` 상수 제거(창고가 움직이면 경로 목적지가 바뀐다).
4. §2-4 아트 8동 + 티어 + 공사판 — **자리 앵커가 앉은 뒤**라야 밑동선이 맞는다(맨 뒤).

**남아 있는 화면 결함(스크린샷으로 확인, 미수정)**
- 건설 팔레트 라벨이 하단 내비와 겹쳐 반만 보인다(§1-3, `PaletteBar`가 들어왔는데도 남음).
- 카드 아이콘이 왼쪽 금테를 넘어 잘리고 하트 옆에 정체불명 붉은 조각 — 아틀라스 조각 Rect가
  이웃을 물고 있을 가능성. 영지와 무관한 별건.

## 그록에게 — 자리 크기 공식은 내가 틀렸다, 되돌려 달라 (2026-08-19, 클로드)

**화면 검증 결과: 자리 크기 반영 후 건물이 화면을 덮는 거인이 됐다**
(`output/qa/ashes-to-stars/shots/qa_go:Estate.png`). **그쪽 구현 잘못이 아니라
내 설계문서 §2-1의 공식이 틀렸다.** 문서는 정정했다(§2-1 「⚠️ 정정」).

실측(창고, 종횡비 0.84·units 3.8·tw≈158):
- 옛 식 `bh = units × tw/4.2` → 120×143px
- 내 공식 `bw = footprint.x × tw`, `bh = bw/종횡비` → **316×376px** (축마다 2.6배, 면적 6배)

틀린 이유 둘:
1. **스프라이트 폭 ≠ 밑동 폭.** 지금 8동은 작은 밑동 위 높은 탑 구도라 스프라이트 폭에
   칸수를 곱하면 그림 전체가 커진다. 「아트가 규칙을 지켜야 한다」고 단서만 달고
   **안 지킨 기존 아트에 적용하면 어떻게 되는지 안 재본 것**이 원인이다.
2. **등각 투영 누락.** (fx,fy) 자리의 화면 밑변은 `fx×tw`가 아니라 `(fx+fy)×tw/2`.

**부탁 — 이번 마디에서 자리 크기는 occupancy와 anchor만 맡게 해 달라.**
그림 크기는 크기표(`prop_scale.json`) 옛 식으로 되돌린다. 옛 크기는 화면에서 문제가
없었다 — 오너 지적은 크기가 아니라 **떠 있고 겹치는 것**이었다.
크기까지 자리에서 내는 것은 §2-4 새 아트(밑동이 자리 마름모를 채우는 계약)가 들어온
뒤이고, 그때는 옛 아트와 섞이므로 자산별 표식이 필요하다.

앵커(밑동을 자리 마름모 중심에) 자체는 맞다 — 그건 유지해 달라.

## 그록에게 — 영지 설계문서 나왔다 + 분담 (2026-08-18, 클로드)

**문서: `docs/GAME_SPEC_ESTATE_BUILD.md` (`0949499d`).** 읽고 같이 개발하자.

- **「위치가 안 맞는다」의 원인**: 격자에 자리 크기(footprint) 개념이 없다. 화면 크기가
  `prop_scale.json` 유닛 × 아트 종횡비로 정해져 **폭이 0.68칸~1.25칸으로 흩어진다**
  (본성·광산·수비대는 이웃 칸 침범). 밑동도 `sit=0.42`라 마름모 중심(0.5)보다 위에 뜬다.
- **분담(문서 §4-B)**: 클로드가 **영지 코드 5파일**(`EstateGrid`·`EstateYard`·`EstateBuild`·
  `EstateScreen`·`EstateHud`), 그록이 **아트 파이프라인**(`aigen.py`·spec·화풍)과 그 외 전부.
  영지 아트의 **사양**(자리 크기·밑동 앵커·품질 6항목)은 문서 §2-1·§2-4에 있다.
- **할로우 제거는 그쪽이 이미 했더라** — 클로드도 같은 걸 착수했다가 손 뗐다(`GAME_STYLE`
  교체 + 옛 이름 별칭이 클로드 안보다 낫다). 문서 §4-A를 그 결정으로 갱신했다.
- **부탁**: 영지 아트를 뽑을 땐 자리 크기(1×1 / 2×1 / 2×2)를 먼저 정하고, **캔버스 아래
  밑동이 닿는 선**을 마름모 중심에 맞춰 그려 달라. 안 그러면 코드에서 앵커를 아무리
  맞춰도 그림이 뜬다.

## 지금 (2026-08-18) — 영지 설계는 클로드

오너: 클로드가 영지 설계 문서를 쓴다. 작성되면 읽고 같이 개발.
문서 전 영지·EstateYard 금지. 필드 지갑 표기는 닫음. 폴리싱 다음은 탑.

## 지금 (2026-08-18) — UI·아트 상시 폴리싱

오너 「ui/아트는 상시 폴리싱」. 코드 구멍 다음 이터는 화면 하나.
할로우 화풍 강제 취소. 있는 장을 쓰고, **있는 대상은 다시 뽑지 않는다**(중복 재생성 금지).
마지막 트랙: 코드 → 다음 이터는 영지. 「다른 게임만큼」완료 금지.

## 다음 세션이 할 일 — 인계 (2026-08-18, 클로드)

오너가 이번 세션에서 미해결로 남긴 셋을 다음 세션에 넘긴다. **먼저 읽을 것: 이 항목 전체.**

> **갱신 (2026-08-18 밤, 클로드): 1)·2) 완료. 3)만 남았다.**
>
> - **2) 카드 글씨 잘림 → `67664c3a`.** 진짜 원인은 카드 크기가 아니라 **금테가 짧은 축을
>   비율로 먹는 것**이었다. 396×95 카드에서 9-slice가 위아래 15.2px씩(높이의 40%) 들어와
>   남은 56.6px에 제목 22 + 부제 30.6을 우겨넣고 있었다. 실측: 20px 글꼴의 줄 높이는
>   **26.79px**이라 22px 제목 칸은 애초에 불가능. `SlicePad/ContentRect/DrawSliced`에
>   `maxPad`(기본 0=무변경)를 넣어 **금테와 글씨 칸을 같이** 얇게 했고, `SlimTitleH` 28,
>   `LabelFit`(안 들어가면 자르는 대신 11px까지 축소)을 신설했다.
>   **DockH 250이 왜 더 나빴는지도 재현했다**: 높이 120이 `SlimCardH` 110을 넘어 크롬이
>   `panel`(0.24)로 바뀌며 패딩이 19.2→32.8로 뛰어 **안쪽 칸이 오히려 줄었다**(56.6→54.4).
>   점검 `CardTextFitSelfCheck` 신설 — 적용 전 FAIL 7건(네거티브 컨트롤) → 적용 후 PASS.
>   ⚠️ **이 점검은 한글 폭을 못 잰다**(builtin 글꼴에 한글 글리프 없음 — 2줄로 통과한 문구가
>   화면에선 3줄로 잘려 있었다). 부제 판정은 반드시 스크린샷으로.
> - **1) 보너스 카드 → `8a7e6b93`.** 하단 도크(168px) + 카드 반투명 0.82, 전체 화면 dim 제거.
>   **전투 정지는 이미 배선돼 있었다**(`W3Party.cs:1541`) — 새로 만들지 마라.
>   ⚠️ **hunt 실행은 비결정론이다**: 같은 6초 캡처 두 장이 86.8% 다르다. 스크린샷 대조로
>   「움직임이 멈췄나」를 재려던 시도가 여기서 무효가 됐다(같은 시각 대조군을 먼저 찍어서
>   알았다). 프레임 단위 정지를 재려면 한 실행 안에서 두 프레임을 뽑는 장치가 따로 필요하다.
>
> **덤으로 발견(안 고침, 오너 판단 대기)**
> - 필드 도크 지갑 카드의 `BagText()`가 `환생석 1/2147483647`처럼 **int 상한을 화면에 흘린다.**
>   문구가 카드 두 줄을 넘겨 `LabelFit`이 글꼴을 최소까지 줄이는 원인이기도 하다. 레이아웃이
>   아니라 **수치 표기** 문제다.
> - 카드 아이콘이 왼쪽 금테를 넘어 잘린다. 하트 옆에 정체불명 붉은 조각이 같이 그려진다 —
>   `UiAtlas` 아틀라스 조각 Rect가 이웃 스프라이트를 물고 있을 가능성. 이번 변경 이전부터다.
>   → **닫음 `795fd79b`.** Rect가 아니라 NPOT 1024 + `texture.width` 나눗셈. 등대·하트 확인.

### 1) 보너스 카드가 전장을 가린다 (가장 시급) — ✅ 완료 `8a7e6b93`
**증상**: 필드 → 「사냥 시작」으로 전투에 들어가면 캐릭터가 안 보인다.
**원인은 스프라이트가 아니다** — 전투 시작 직후 뜨는 보너스 선택 카드
(`예리함 / 집중 / 방벽`)가 화면 중앙 절반을 덮어 파티를 위쪽 구석으로 밀어낸다.
QA `hunt` 모드로 찍으면 캡처 시점이 이 창 전후라 문제가 안 보인다 —
**오너가 실제로 플레이하는 경로에서만 드러난다.**
**고칠 방향**: 카드를 필드 허브 도크처럼 **화면 하단**으로 내린다.
파일: `_Game/Scripts/Runtime/HuntBoon.cs` + 카드를 그리는 `BattleScreen`.
재현: `GAME_PROJ=.../unity_meas GAME_SHOT_SEC=6 tools/qa_shot.sh --skip-build hunt 0`
(QA_PARTY5 없이 — 스위치를 켜면 캡처 타이밍이 달라져 안 보인다).

### 2) 필드 허브 카드 글씨 잘림 — ✅ 완료 `67664c3a`
**증상**: `(§4·§6)`·`상한 12시간(§9)`이 카드 밖으로 잘린다.
**하지 마라**: `FieldHud.DockH`를 200 → 250으로 올리는 것. 시도했고 **더 나빠졌다** —
카드 테두리만 커지고 설명이 내부 패널에 더 심하게 잘렸다(되돌림).
즉 텍스트 영역이 카드 높이를 따라가지 않고 별도 계산을 쓴다.
**볼 곳**: `UiPages.CardChrome` / `DrawCard`의 본문 영역 계산.

### 3) 영지 — 건물 위치·신규 아트·건설/업그레이드 창 — ⏳ 진행
문서 `GAME_SPEC_ESTATE_BUILD.md`. **§1 폴백·§2 자리/앵커·§3 팔레트는 이 세션.**
§4 건물별 업그레이드 창은 클로드(`EstateBuild.cs`). §5 드래그·§6 아트는 그 다음.
옛 진단(크기표만 읽고 격자와 안 맞춤)은 §2로 닫힘.

## 이번 세션에서 확정된 것 (다시 조사하지 마라)

- **5직업은 오너 스프라이트에 정상 연결**. 렌더 rect가 각 캔버스와 일치:
  수호기사 175x123 · 검사 157x114 · 마법사 155x114 · 사제 203x120 · 음유시인 172x122.
  에디터에서도 `Resources.Load` 전부 성공(Read/Write 활성, 컴파일 오류 0).
  ⚠️ **여기까지 오는 데 두 번 틀렸다**(`f753d979`). `LookDir`의 기본값만 기본 그림으로
  바꾸고 **티어를 받는 오버로드는 전직일 때 전직 폴더를 계속 썼다.** 오너 세이브에는
  이미 전직한 캐릭터가 있어 그들만 옛 HK 아트로 렌더됐고, 나는 `QA_PARTY5`로 새로
  만든 Basic 캐릭터만 보고 "연결 정상"이라고 두 번 보고했다.
  **교훈: QA 스위치로 만든 상태가 아니라 오너의 실제 세이브 상태로 확인할 것.**
  전직 폴더 그림은 몹으로 돌렸으므로(`mob_guardian` 등) 캐릭터는 전직 여부와
  무관하게 기본 그림을 쓴다. 전직 전용 그림이 새로 생기면 `LookDir(job, tier)`의
  분기를 되살릴 것 — 인자는 그래서 남겨 뒀다.
- **애니메이션은 돈다**. 공격 한 번에 6프레임 순차(y 1537→1631→1725→1819→1913→2007).
  5명 전원 4시점에서 칸 변화 확인.
- **시트마다 칸 수가 다르다** — dps 8칸, tank 6칸. 6등분 고정 분할은 틀렸다.
  지금은 캐릭터 덩어리를 검출해 그 사이에서 자른다(`art/import_owner_sheets.py`).
- **스킬 행(SKILL1~3)은 캐릭터 프레임으로 쓰지 마라** — 이펙트가 캐릭터에 덧그려져 있다
  (오너 "캐릭터에 붙어있는 이펙트 지우자"). 시전은 공격 행에서 뽑는다.
- 속도: 공격 0.5s · 스킬 1.0s · 걷기 1.10s · 대기 1.50s(오너 지정). 동작 길이도
  애니보다 길게 맞췄다(AttackT 0.52 · SkillT 1.05) — 짧으면 뒷 프레임이 안 보인다.
- 크기: 캐릭터 2.8u(렌더 3.10) · 몹 2.0u(렌더 2.03) · 정예 2.4u.
- QA 스위치: `QA_PARTY5=1`(5인 편성) · `QA_SKILL_SPAM=1`(쿨타임 0) · `QA_BASE_LOOK=1`(기본 그림 강제).

⚠️ **그록이 `unity_meas`에서 동시에 빌드 중이다**(Unity 6000.5.6f1). 빌드 전에 겹치는지 확인할 것.

## 지금 (2026-08-18) — 기획서 루프 재개

오너 「기획서 반영해서 발루프 시작」. `loop/STOP` 제거 후 `loop.sh`(그록) 재개.
큐 1번: 원장 ✅ · 소비처 0곳 한 칸. 시각·V2·V4 사람 관문은 루프가 닫지 않는다.

## 지금 (2026-08-18) — 옛 5종은 보스, 새 5종은 Imagine

옛 기본 5종 13장 → 보스 실루엣 (`boss_brute`←탱, `serpent`←버퍼, `wraith`←마딜, `construct`←딜, `boss_saint`←힐).
새 기본 5종은 그록 Imagine. **체형+무기가 한눈에 갈리게**:
- 탱: 넓고 낮음 + **대형 방패·철퇴**
- 딜: 바늘처럼 김 + **한손 장검**
- 마딜: 삼각 로브(다리 없음) + **청록 지팡이**
- 힐: 둥근 경단 + **금 십자 지팡이**
- 버퍼: 막대 몸+긴 모자 + **류트**
걷기는 좌·우 발, 공격은 그 무기가 보이게. 영상 생성은 ZDR로 막혀 포즈 편집으로 이었다.

## 실수 — 다시 하지 말 것 (2026-08-18, 오너 스크린샷)

1. **흰 사각형을 게임에 실었다.** 딜러 대시·걷기 장에 바둑판이 남았는데 "적용했다"고 끝냈다. 화면을 보기 전에 끝내는 것이 실수. 적용 후 `leftover_white_pct`로 흰 종이를 재고, 2% 넘으면 그 장은 실패다.
2. **초승달 아트를 바닥 예고로 썼다.** `fx_slash`는 흰 초승달이다. 금색을 곱해 돌진 예고로 깔면 오너가 네 번 지운 스킬링과 같다. 고리·초승달·원환은 예고가 아니다. 예고는 먼지뿐.
3. **가면을 종이로 오인하고 지운 적 있다.** 뼈색 흰 픽셀을 통째로 지우면 얼굴이 사라진다. 가장자리에 붙은 바둑판만 지운다.

## 지금 (2026-08-18) — 전시 시트 13장 적용

오너가 넘긴 8장 설명대로 기본 5직업 애니를 붙였다. **다시 뽑지 않음.**

| 직업 | 시트에 적힌 줄 | 게임 슬롯 |
|---|---|---|
| tank | IDLE / RUN / 방패 돌격 / 도끼 휩쓸기 / 대지의 철벽 | idle · walk·dash · attack · special · invuln |
| dps | Idle / Run / Twin Strike / Whirling / Shadow Cascade | 위와 같음 |
| mage | IDLE / RUN / Abyss Blast / Shadow Teleport / Void Chasm | attack=Blast, dash=Teleport, special=Chasm |
| healer | 로코모션 / 부드러운 치료 / 정령의 채찍 / 눈부신 포옹 | special=치료, attack=채찍, invuln=포옹 |
| buffer | Walk·Float / Silk Trap / Haste / Cosmic Weaver | attack=Trap, special=Haste, invuln=Weaver |

경로: `art/apply_showcase_sheets.py` → `Resources/sprites/<직업>/` 13장 + `ui/portraits/`.
배경: 마젠타만이 아님. `knock_bg.py`가 흰·회색·바둑판도 가장자리에서 뺀다(가면은 남김).
p22 import는 전직·몹만 — 이 5직업을 덮지 않는다.

클로드 aigen(p22·p24 등)은 그대로 둔다. 루프 STOP. 오너 Unity 금지.

**Q1.** 플레이 기본 ortho는 10. 13은 아레나 가장자리를 보려고 쓴 값이었고, 「캐릭터 안 보인다」에는 10이 맞다. `QA_ORTHO`로 비교 가능.
**Q2.** 넷째는 프랍·배경 채도. 몹 HK는 그록 p22가 잡고 있다 — 클로드가 또 돌리지 말 것.

## 그록에게 — 확인 요청 2건 (2026-08-18, 클로드)

오너 지시로 직접 묻는다. 답은 이 문단 아래에 적어 달라.

**Q1. 전투 카메라 ortho를 13 → 10으로 내렸다.** `c77b782f`「전장 확대」의 13을 되돌린 것이라
그쪽 의도를 모른다. 근거는 실측이다: 720px 화면에서 캐릭터 2.0u가 ortho 13이면 55px(7.7%),
10이면 72px(10.0%). 10% 아래면 잡몹 무리에 묻힌다. 물량 가시성은 아레나 반경이 정하지
ortho가 정하는 게 아니라고 판단했다. **13이 필요한 다른 이유가 있으면 알려 달라.**

**Q2. 「캐릭터가 안 보인다」의 원인을 셋까지 좁혔다. 넷째가 있으면 알려 달라.**
- ① 겹침 — **아니다.** 탱 반경 1.5u 안에서 정렬값이 탱 이상인 렌더러 덤프 = 0개.
- ② 크기 — 맞다. 크기 소스가 셋(크기표·SpriteBank 상수·W3Party의 정예 ×1.4)으로
  갈라져 정예가 캐릭터보다 컸다. 표 하나로 모았다(`790ee7e3`).
- ③ 색 — 맞다. 몹 **원본 아트 채도는 0.07~0.10**(캐릭터 0.10과 같은 계열)인데
  `FamilyTint`가 원색을 곱해 화면에서만 쨍해졌다. 화면에서 채도 높은 것이 전부 잡몹이라
  주인공이 묻혔다. 계열 색조는 남기고 강도만 낮췄다.
- 남은 의심: 전투 배경·프랍이 아직 할로우 나이트 계열이 아니다(마을 집·나무가 채도 높은
  픽셀아트). **몹 5계열 22프레임 HK 재생성이 큐에 있는데 그쪽이 잡고 있나?** 겹치면 낭비다.

## 캐릭터 가시성 — 근본 원인은 「두 개의 진실」이었다 (2026-08-18, 대화 세션)

오너 「캐릭터 안 보인다」 → 「근본적으로 검토해, 때려막기 하지 마라」.

**측정으로 배제한 것** (추측 없이 계기로만 봤다)
- `SizeReportOnActive`에 **겹침 덤프**를 넣어 탱 반경 1.5u 안에서 정렬값이 탱 이상인
  렌더러를 전부 이름·정렬값으로 출력 → **0개**. 가려진 게 아니다.
- 알파 박스 실측: 캐릭터 시트는 캔버스를 100% 채우고(2.00u 전부가 몸), 잡몹은 90%,
  정예도 90%. 즉 **정예(2.2u)가 캐릭터(2.0u)보다 크다** — 주인공보다 큰 잡몹.

**처음 한 짓이 때려막기였다** — `SpriteBank`의 `U_CHAR`만 2.0→2.6으로 올려 빌드·실측까지
했다(탱 2.60u 확인). 그러다 `art/prop_scale.json` 첫 줄을 봤다:
`"_reference": { "character_units": 2.0, "모든 값은 이 기준의 상대 크기다" }`.
**프랍 53종이 전부 캐릭터 2.0 기준으로 적힌 표**였다. 코드 상수만 올리면 집이 캐릭터의
1.8배 → 1.4배로 조용히 찌그러진다. 되돌렸다.

**근본 수리 — 크기의 단일 소스화**
- `FieldDecor.Units(key, fallback)` 공개. 유닛 크기도 프랍과 **같은 표**에서 읽는다.
- `prop_scale.json`(art·Resources 양쪽)에 `unit_char/mob/elite/boss/proj` 추가.
  `SpriteBank`의 C# 숫자는 이제 **표를 못 읽었을 때의 폴백**일 뿐이다.
- 비율 수정은 표에서: **정예 2.2 → 1.9**(캐릭터보다 작아야 주인공이 읽힌다).
  캐릭터는 2.0 그대로 — 프랍 53종의 상대비가 살아 있다.
- 절대 크기는 **카메라**로 잡았다: `BattleScreen` ortho **13 → 10**.
  720px 화면에서 캐릭터가 55px(7.7%) → 72px(10.0%). 10% 아래면 무리에 묻힌다.
  ⚠️ 그록 `c77b782f`의 「전장 확대」 13을 내린 것이다 — 물량 가시성은 아레나 반경이
  결정하지 ortho가 결정하지 않는다고 판단했다. 이견 있으면 여기 적어 달라.

## 「스킬링」의 진짜 출처 (2026-08-18)

오너가 네 번 지운 바닥 주황 고리가 여전히 나왔다. 절차 생성 링(`MakeRing`)은 이미
지웠는데도 남은 이유: **돌진 몹 예고에 `fx_taunt.png`를 깔고 있었다** — 그 아트가
말 그대로 주황 원환이다. 코드에서 링을 지워도 **링 모양 아트 한 줄**이 남아 있었다.
→ 고리 대신 **진행 방향 앞쪽의 베기 섬광**(`Kind.Slash`, `p + dir*1.6`)으로 교체.
돌진의 정보는 "어디로 오는가"지 "반경이 얼마인가"가 아니다. 실측 스크린샷에서 고리 사라짐 확인.

## UI·아이콘 화풍 (2026-08-18, 오너 「UI랑 스킬 아이콘은 왜 화풍에 안 맞추나」)

- 초상화 5종은 **이미 반영됨**(`Resources/ui/portraits/*.png`, 생성물과 md5 동일).
  전투 HUD 스크린샷에서 할로우 나이트 마스크 초상화 확인.
- `combat_icon_atlas.png`(48칸)는 **채도 높은 가챠풍** — 육안 확인. 화풍과 어긋난다.
  P12에서 만든 HK 아이콘 9종만 끼우면 39칸이 남아 더 어색해지므로,
  **`spec_p13_icons.json`로 나머지 40칸을 같은 룰셋으로 생성 중**(detached, PGID=PID).
  나오면 8×6 1448×1086으로 팩해 교체한다. 이름은 `CombatIconAtlas.Pieces` 키와 1:1.

## 프랍 전면 교체 (2026-08-18)

오너 「프랍도 다 교체」.
마을 집·나무·영지 건물이 아직 픽셀(초가·초록 잎). 52장 보관
`ref_old_not_hollow/2026-08-18/props/`. 재생성 `spec_p24_props_bw.json` → `import_p24_props.py`.

---

## 이펙트·스킬 이펙트 흑백 (2026-08-18)

오너 「이펙트도 수정해 스킬이펙트도 수정」.
기존 네온 초록 링·금 고리·파란 레이저 시트는 `ref_old_not_hollow/2026-08-18/fx`.
`FxParticles` 기본색을 흰·회색으로. `CombatVfxAtlas`는 `fx/icons/` 솔로를 먼저 읽음.
생성 `spec_p23_fx_bw.json` 37장 → `import_p23_fx.py`.

---

## 안 맞는 장 보관 후 재생성 (2026-08-18)

오너 「안맞는거 다 다시 만들어서 연결하고 기존꺼는 모아놔」.
보관: `art/ref_old_not_hollow/2026-08-18/` (탱·힐·전직·초상·강아지몹·글자 시트).
남김: 딜러·마법사·버퍼(흑백 가면이 맞음). 몹은 커밋된 옛 장으로 임시 복구.
재생성: `spec_p22_bw_remake.json` 48장 → `import_p22_bw.py`.

---

## 할로우 화풍 = 흑백 가면 (2026-08-18)

오너 「화풍 파악해 먼저」+「흑백 느낌」.
공식 기사는 몸이 검정, 얼굴이 흰 가면, 눈은 빈 구멍. 색은 예외 한 점.
맞는 장: `dps_idle_00`. 틀린 장: 전직 idle(초록 이끼·빨간 균열·땅·눈동자).
원장 `art/STYLE_HOLLOW.md`. `aigen.py`가 모든 생성 앞에 흑백 락을 붙인다.
이미 뽑힌 컬러 전직 idle은 락 이후 다시 뽑는다.

---

## 전직 할로우 애니 (2026-08-18)

오너 「전직도 할로우 나이트 화풍 반영해서 만들어」.
idle 10장은 있음. 걷기·공격 6칸 시트 `spec_p21` 20장 생성 중.
`SpriteBank.CharAnimDir` + `W3Party`가 `LookDir` 폴더를 읽음. walk 없으면 기본 5직업.
`import_p21_adv_anim.py`가 시트→13장.

---

## 유니티·블렌더 사용 (2026-08-18)

오너 「유니티에디터나 블랜더 필요하면 자유롭게 사용」.
묻지 말고 쓴다. 오너 `unity/` 세션만 안 죽인다. 측정은 `unity_meas`.
블렌더 `/Applications/Blender.app`.

---

## 필드 바닥·남은 배경 (2026-08-18)

오너 「필드 바닥이랑 배경도 다 바꿔」.
전투 바닥은 `field_plain_albedo`·`dungeon_rock_albedo` 가 9KB 픽셀 타일이었고
`GroundBuilder`가 **Point를 강제**해서 손그림을 넣어도 도트가 됐다.
필터를 Bilinear로 바꿈. `spec_p20` 이 바닥 2 + 타이틀·던전·결과 배경을 뽑는다.
허브 배경 6장은 이미 할로우.

---

## 할로우 나이트 전면 교체 (2026-08-18)

오너 「모든 리소스는 할로우 나이트 화풍으로 전면 교체」.
이미 할로우: 기본 5직업(Resources 08-18), 허브 배경 6, 초상 5(`ui/portraits`).
아직 픽셀: 몹 5·보스 애니·프랍·영지건물·FX·크롬·바닥·타이틀/던전/결과.
파이프라인 `art/hollow_pipeline.sh` — p12 끝난 뒤 p13→p19 순.
직업 5×13 PNG는 클로드 몫, 다시 안 그림.

---

## 기획서 빈 리소스 (2026-08-18, 오너 「기획서 보고 없는 리소스 확인」)

§0-B가 §2 수천 장보다 이긴다. 코드가 찾는 것과 대조한 결과:

**지금 없는 것(만들어야 함)**
- 1차 전직 10종 전신 — 수호기사·광전사·검사·궁수·소환사·사제·드루이드·음유시인·주술사·정령사가 기본 5장과 같다. `LookDir` 전용 폴더 + `spec_p13`.
- 타이틀·던전·결과 배경 — 화면은 읽는데 할로우가 아님.

**이미 있음 / 안 만듦**
- 기본 5×13, 몹 5×22, 보스 4×16, 영지 8, 허브 배경 6, FX 8.
- 종족·정예·보스 20종 전용은 기획이 0장.

클로드 `p12` 초상·아이콘 생성 중이면 그 락을 뺏지 않는다.

---

## 이미지 생성 = Nano Banana 2 + 언리미티드 (2026-08-18)

오너 「앞으로 모든 이미지는 힉스필드 나노바나나 2 언리미티드 옵션 켜서」.
- 모델: `nano_banana_flash` (Nano Banana 2). **`nano_banana_2` 별칭은 Pro** — 금지.
- 해상도 2k. Gemini·Imagine·Pro 금지. `aigen.py`가 별칭을 flash로 되돌린다.
- 언리미티드 토글은 **higgsfield.ai 웹에서만**. CLI는 공식상 크레딧(Plus 847).
- 웹에서 그릴 때: Nano Banana 2 + Unlimited ON + 2K.

---

## 클로드 협업 분담 (2026-08-18, 오너 「클로드랑 협업 진행」)

세션 `8349a8d2` (Desktop opus-5). 보드 http://127.0.0.1:8766/ . 루프 `STOP`. Unity `unity/` PID는 죽이지 마라.

**클로드 = 그림.** 손대지 마라.
- 보스 `boss_*_death_01/02.png` 와 death 4장
- 직업 5×13 PNG (`tank`·`dps`·`healer`·`buffer`·`mage`) — 할로우 나이트 풍 반입
- `ui/chrome/*.meta` 필터(Point→Bilinear)
- `art/out_p9_hollow` · `out_p10_bg` · `out_p11_anim` · spec JSON
- `git add -A` 금지. 클로드 dirty PNG를 그록 커밋에 섞지 마라

**그록 = 소비·검사.**
- `BossDeathAnimSelfCheck` — 보스 death 4장이 `BossAnim`에 붙는지
- `JobAnimSelfCheck` — 직업 13장이 `CharAnim`에 붙는지
- 이미 닫은 전투·HUD는 다시 안 연다. `W3Party` 안 건드림
- 루프 재개 금지. 4직업 실루엣을 그록이 다시 그리지 마라(클로드 몫)

`unity_meas` GameSweep **24/24 PASS** (보스 사망·직업 13장 포함). 클로드 PNG는 스테이징하지 않음.

**그록 이어서(배경 반입):** 클로드 `out_p10_bg` 6장이 끝났고 생성 PID는 죽었다.
`Resources/bg/bg_{estate,field,tower,worldmap,character,party}.png` 에 복사.
필터만 그 6장 `.meta` 를 Bilinear. `bg_title`·던전·결과는 안 바꿈.
`HollowBgSelfCheck` 추가. `SpriteBank`·`TextureImportRules`·직업 PNG는 클로드 것 — 안 건드림.
원본은 1024² 이라 16:9 화면에 위아래가 잘린다(아트 몫, 재생성 안 함).
p11 애니 15시트는 클로드가 자르는 중 — 그록이 안 자른다.
`unity_meas` GameSweep **25/25 PASS**.

---

## 클로드와 공유 (2026-08-18, 오너 「보드를 돌아가는 클로드와 같이 공유」)

개발 보드: http://127.0.0.1:8766/  (같은 맥, 지금 켜져 있음)
자동 루프: `loop/STOP` — 켜지 마라. Unity PID 75776은 죽이지 마라.

이 Grok 세션이 닫은 것(전투·HUD):
- 필드 집·나무 안 겹침 `fe2eb9c8` · 파티 겹침 `95886088`
- 사냥 중 임시 3택 `a54b95de` · 가로 카드 UI `df02ec39`
- 전투 오른쪽 HUD(연속·분당) `0d597f90`
- 게임 스윕 20종 + 보드 검증 칸 `9eae9643`·`e3f32c8b`
- 작업 전체 중단(루프·#94 종료). 보드는 다시 켬.

루프 #85~#93이 닫은 것(전투 밖): 경매 HUD·캐릭터 명부 라벨·현황 도크·끌어 보기·휠 줌·탈출 수동 한정·명예 방어 비례·경매장 전용 그림.

지금 하지 말 것: 루프 재개(오너가 스톱함). 4직업·몹 실루엣 재생성.

---

## 경매장 전용 그림 (2026-08-17)

큐 1번 화질은 재생성 금지. INBOX 09:57 남은 경매장 수레.
`DedicatedOf(Auction)`=`estate_auction_0`. SelfCheck PASS. PNG 목재 장.
`QA_NO`면 수레. `W3Party` 안 건드림.

## 명예 승리 방어력 비례 (2026-08-17)

큐 1번 화질은 재생성 금지. §18-13 ✅ 승리 +30 ±50%.
`Honor.WinForCut` Cut 0=15 · 40=45. SelfCheck PASS. PNG 월드맵 +45.
수비 성공 +20은 안 넣음. 코드 `b4934096`. `W3Party` 안 건드림.

## 긴급 탈출 수동 한정 (2026-08-17)

큐 1번 화질은 재생성 금지. §4 ✅ 자동에서 두루마리 금지.
`EscapeManual` 보스·침략만. SelfCheck PASS. PNG 필드 자막.
코드 `4d2d759d`. `W3Party` 안 건드림.

## 영지 마을 굴려 확대 (2026-08-17)

큐 1번 화질은 재생성 금지. INBOX 09:18 확대.
`EstateYard` 휠 1.50. SelfCheck PASS. PNG 집이 전면보다 크다.
코드 `3a9ff6aa`. `W3Party` 안 건드림.

## 영지 마을 끌어 보기 (2026-08-17)

큐 1번 화질은 재생성 금지. INBOX 09:18 카메라.
`EstateYard` 끌어 180. SelfCheck PASS. PNG 마름모가 오른쪽.
코드 `9d396c1f`. `W3Party` 안 건드림.

## 영지 현황 도크 (2026-08-17)

큐 1번 화질은 재생성 금지. 현황 2×2가 마을을 덮었다.
`EstateStatusHud` 5칸 88. SelfCheck PASS. PNG 마을+아래 도크.
코드 `e0802219`. `W3Party` 안 건드림.

## 개발 보드 운영 (2026-08-17, 오너 「항상 잘 관리해」)

8766 보드. 원장은 `loop/board.py` 머리말. 세션이 보드를 만지면 아래를 유지한다.

- 채팅 지시 → `python3 loop/board.py command "제목" "본문"` (`owner_commands.json`)
- 다음 할 일은 위. 프로토가 끝나면 지금 단계(마을·탑·장비)로 바꾼다.
- 글은 짧은 한국어. `humanize_title`/`humanize_detail`
- 외부 테스터는 아나만. V4 10명은 키트에서 뺀 상태
- 끝난 일 검은 화면 PNG 금지 (`shot_is_black`)
- 회귀: `python3 -m unittest loop.test_board loop.test_v4_playtest`

## 게임 전체 점검 (2026-08-17)

오너 「게임 전체 부분 체크」. `GameSweepSelfCheck` 20종 + 보드 회귀.
캐릭터창 경험치 표기 `EXP`→`경험`. 보드 검증 칸에 기록.

## 검증을 보드에 올림 (2026-08-17)

오너 「테스트는 철저하게, 결과를 보드에 작성」. `ChatWorkBatchSelfCheck` 7종 +
보드 회귀 74. 화면 「검증 결과」가 `last_test_report.json`을 읽는다.

## 사냥 중 강화 3택 (2026-08-17)

오너 「뱀서류처럼 사냥중 강화 목록 안뜨는데」→「니가 정해서」.
정함: 접속 중 사냥만, 처치 8마리부터 전투 정지 3택. 임시·나가면 삭제.
영구 레벨에 안 건다(판 끝난 뒤 정산·시드 만렙이면 영원히 안 뜸). 보스전·W1~W3·방치는 안 뜸.
`HuntBoon` + Overlay. `HuntBoonSelfCheck`.

## 캐릭터도 안 겹치게 (2026-08-17)

오너 「캐릭터도 안겹치게해」. 파티는 집 `Resolve`만 하고 `Around`가 없어 지붕을
가로질렀고, 겹침 해소는 70%만 받아 서로·몹 위에 남았다. 같은 역할은 스폰이
한 점. 고침: 이동·대시는 몹과 같이 비키고, 파티는 `Unstick`으로 몸 너비만큼
완전 분리. `UnitSeparationSelfCheck`.

## 몬스터·집·배경 안 겹치게 (2026-08-17)

오너 「몬스터 집 배경 오브젝트 안겹치게해」. `FieldDecor.Place`가 이미 선 프랍을
안 봐서 나무·바위가 지붕 위에 앉고, 집 옆 나무는 `asCover=false`라 몹이 뚫었다.
고침: 집을 먼저 세우고 `_placed` 원으로 겹치면 버린다. 집·나무·바위·건초는
엄폐 모드에서 `ArenaLayout` 장애물. `FieldDecorOverlapSelfCheck`.

## 크롬 글씨 여백 (2026-08-17)

오너 타이틀 스크린샷: 「게임 시작」이 금테에 겹침. 9-slice dest(패널 짧은 변 24%)와
CardLayout 8–18px가 어긋난 게 원인. `UiAtlas.SlicePad`/`ContentRect` 하나로 DrawSliced와
카드·탭·버튼·명부·편성·스타터·장비 스튜디오·배너·월드맵 캡션이 같은 안쪽 칸을 쓴다.
`UiAtlasSelfCheck` 타이틀 532×180 금테 안 단언. meas PASS. Play 재시작 필요.

## 보스 HP 프레임 소비처 (2026-08-16)

큐 UI 남은 것. `boss_hp_frame`은 아틀라스에만 있었다. `DrawBossHp` + BattleScreen Overlay.
SelfCheck PASS. PNG 탑 견본 3칸 + 실전 7500/7500 페이즈 4. W3Party 안 건드림.

## 올라마 분담 (2026-08-16)

`loop/ollama_split.py` — classify/copy. 실측 gemma4:12b JSON.
분류: 다음=UI 퀄리티(INBOX), owner=ollama. 귀환 카피 「피격 시 시전 취소」를 Overlay에 반영.
대장간 둘째는 루프 #8. 클라우드 올라마 금지.

## 긴급 탈출 6초 캐스트 (2026-08-16)

루프는 대장간 둘째. 이 세션은 §4 캐스트만. 피격 취소 시 두루마리 미소모.
SelfCheck PASS.

## 보드 내 선택 축소 (2026-08-16)

오너: 정말 중요한 것만 체크. `pending_choices`는 `human`만.
외부 테스터·V4 70%는 「내 선택」에서 제외. 관문은 되돌릴 수 없는 OUT.

## 루프+에이전트 — 영지 3건물 첫 슬라이스 (2026-08-16)

- 오너 보드 「이걸로 진행」. ashes-code/ashes-art가 소비처를 가름.
- **수비대**: `DefenseState` 최대 5, 배치=출전 제외. 침략 본게임 안 염.
  SelfCheck PASS. 월드맵 `수비대 0/5 — 침략 전투는 아직 없다`.
- **대장간**: 루프 이터 #7이 가죽→흉갑→전투 HP (`ec234975`). 수비대를 덮지 않음.
- **경매장**: 거래 서버 없어 잠금. V4 70%는 보류 유지.

## 개발 현황 그래프 (2026-08-16)

- 보드 `http://127.0.0.1:8766`에 관문·로드맵·큐·Resources 그래프. 숫자는
  STATUS 큐 표·DESIGN 관문·§21-4·Resources PNG·git log에서만 집계.
- V4 70%는 0%로 둔다. 자동 경계를 통과로 읽지 않는다.

## Resources 소비 대조·미사용 검사 (2026-08-16)

- 검토: `unity/Assets/Resources` 267png / 45.8MB. 코드 `Resources.Load`·`GetPropNames`·
  `FxPool`/`JobVfx`/`StatusVfx`·아틀라스·`BackgroundArt`와 1:1 — 고아 0, 누락 0.
- 남긴 것: ash/던전 프랍(GetPropNames에 있음), SpriteBank 폴백 10장, mob01 22장,
  FX 22장(전부 로드). 생성 원본 `art/out_*`는 게임이 안 읽으므로 그대로.
- 잔재: `unity_meas`에 이전 정리분 42장이 남아 있었음 → `sync_meas.sh`로 원본과 맞춤
  (`unity_meas`는 git 추적 밖).
- 재발 방지: `game_asset_names.unused_resource_problems()` + 회귀
  `tests/test_game_asset_unused_resources.py`(고의 미사용 PNG 주입 시 FAIL).

## UI 프레임 소비처 — 버튼 3상태·9-slice·체력바 (2026-08-16)

- 클로드 주간 한도 세션이 배경 6장 반입 직후 「UI 프레임이 다음」에서 끊김.
- 아틀라스 조각은 이미 있었는데 `button_hover`/`button_pressed`/`hp_frame` 소비처가 0곳.
- `UiAtlas`에 ButtonKey·DrawSliced·DrawMeter를 두고 GameScreen·W3Party가 읽는다.
- 검증: 컴파일 84소스 0오류, `unity_meas` `UiAtlasSelfCheck` PASS.
- 다음: 아이콘(화면에 실제로 뜨는 것) 또는 V3 한 판 종단.

## V3 보스 HP — 파티 공격 배선, 실행 QA 대기 (2026-08-16)

- 짧은 설계: `BossBattle`의 보스별 HP를 단일 권위로 유지하고, W3에는 화면을 중복해서 그리지 않는
  공격 타깃 슬롯만 둔다. 보스전 일반 웨이브를 멈추고 실제 보스 슬롯 피해만 HP·페이즈·처치로 전달한다.
- TDD: 신규 계약 부재 10건 RED. SelfCheck는 실제 `W3Party.DamageMob` 경계로
  `9000→4500→0`, 페이즈1·처치1과 `BOSS_NO_DPS=1` Boss/W3 `9000→9000`을 단언한다.
- 리뷰 수리: 처음 구현의 쫄/잡몹 피해 복제, 처치 시 페이즈 누락, 차단 모드 프록시 교착,
  프록시 AI·보상 혼입, 실패 시 테스트 오브젝트 누수를 제거했다. 최종 Critical/Important 0.
- 검증: 정적 컴파일 82소스 오류0, 검사기 고의 오류1 탐지, diff check 통과. 코드 `e48aee09`.
- 미완: 원본 manifest의 `com.unity.modules.physicscore2d@1.0.0` 미해결 의존성 때문에 Unity 실행
  SelfCheck·보스 PNG는 미확인. `output/qa/ashes-to-stars/boss_dps_*`와
  `mob_family_hunt_attempt_20260816.log`에 증거를 남겼다. 장판·힐체크는 다음 독립 슬라이스다.

## 2차 각성 초필살기 — 실전 배선·계측 완료 (2026-08-16)

- 짧은 설계: 기존 4스킬/조작 2슬롯은 유지하고, `Second`에만 직업 고유 Gauge와 별개인
  초필 게이지 100%·180초 쿨·E/별도 버튼을 연다. 역할 별 실전 효과를 계측한다.
- TDD: 신규 계약 API 부재 7건 RED, 리뷰 회귀 계약 9건 RED → 최종 80소스 오류 0.
- 실행: 광전사400·궁수632·드루이드150·주술사205, 모두 효과>0·쿨180초. 단계/게이지/쿨
  차단은 각각 효과0·PASS. `unity_meas` SelfCheck 실행 PASS.
- 시각: `output/qa/ashes-to-stars/shots/qa_second_광전사_normal.png` — 선택 카드,
  기존 2슬롯, 별도 `초필 2% / 178s` 버튼과 파티 보호 연출 확인.
- 코드 커밋 `31a50057`. 다음: 1차 6종 슬롯1/2 실행·PNG·`QA_ADV_NO_SLOT` 차단 증거 종결.

## 1차 6종 실전 계측 하네스 — 실행 증거 완료 (2026-08-16)

- 짧은 설계: 기존 BattleScreen/W3 전투를 그대로 쓰고, QA만 직업 하나를 신규 로스터에
  주입해 1초·3초에 슬롯 1/2를 강제한다. 스킬 분기 안에서 실제 피해·회복·보호막
  수치를 모아 계약 존재가 아니라 소비 성립을 판정한다.
- TDD: 생산 API 전 SelfCheck 컴파일 오류 1건 RED → 78소스 오류 0 GREEN. 검사기 고의 오류
  1건 탐지. 코드 커밋 `ab5fe3d0`.
- 실행: 광전사18/1·궁수62.4/4·소환사93.6/1·드루이드12/28·주술사5/2.5·정령사5/100,
  6종 모두 두 수치 `>0` PASS. 정상 PNG 6장과 Player.log를
  `output/qa/ashes-to-stars/first_advancement/`에 보존하고 직접 열어 실제 전투 렌더를 확인했다.
- 네거티브: `QA_ADV_NO_SLOT=1`은 0/1 FAIL, `=2`는 18/0 FAIL. 일반 프레임 캡처가 전용
  계측을 선점하던 결함과 광전사 주 대상 명중/드루이드 만피 픽스처 결함을 수리했다.
- 코드 커밋 `b35fc3ac`. 원본의 타 세션 미커밋 manifest는 건드리지 않고 측정 사본만 정상화했다.

## 전직 단계별 전투 스킬 — 4차 슬라이스 완료 (2026-08-16)

- 설계: 조작 슬롯 2개(§5)는 유지하고, 전투원 계약에 전직 단계를 보존해 기본직업은 역할 학습
  스킬 2개, 1차는 고유 메커니즘을 포함한 4개 능력을 활성화한다.
- 구현: `PartyState.SortieCombatants` → `BattleScreen.ApplyGameParty` → `W3Party.Member.Advancement`.
  기본 4역할 전투 분기와 1차 전용 최후의 보루 게이트를 추가했다.
- 검증: TDD RED 2건, 최종 75소스 오류 0, 검사기 오류 주입 1건 탐지. 커밋 `42315c7b`.
- 미완: W3 미지원 1차 6종은 검사 폴백. 오너 Unity 락으로 SelfCheck/스크린샷 미실행.


## 몹 chaser 계열 재생성 — 완료 (2026-08-15 17:1x, 이터3, INBOX⭐)

**핵심: 이터1·2의 "백엔드 죽음"은 오진이었다.** higgsfield는 살아 있고 느릴 뿐(INBOX 정정이 옳음).
인내심을 갖고 돌리니 chaser가 정상 생성됐다.

- **생성**: `python3 aigen.py --spec spec_p2_chaser2.json --out-dir out_p2` (백엔드 higgsfield 기본).
  sheet A(21:9, 10셀)는 10분 타임아웃 2회 후 3번째 시도 성공, sheet B(4:3, 12셀) 1회 성공. 총 ~30분.
  판정 근거: 크레딧 1044→1030, `pgrep -f higgsfield` 자식 프로세스 생존.
- **후처리 함정**: sheet A는 셀 경계에 **검은 격자선**을 그려서 나온다(chroma_key는 마젠타만 지움).
  → `wipe_gridlines.py`로 시트 청소 후 재분할. sheet B는 격자선 없음.
- **파이프라인**: split_ai_sheet(고정격자 5×2·4×3, 22프레임) → wipe → align_frames(공통 159x124,
  바닥정렬) → `Resources/sprites/mob_chaser`(png만 덮음, meta 유지) → `game_asset_names.py` ✅통과.
- **화면 확인**: `qa_shot.sh hunt` → `output/qa/ashes-to-stars/shots/qa_hunt.png`. 늑대 실루엣이
  FamilyColor 색조별(주황·청·분홍)로 표시, 캐릭터와 같은 픽셀아트 세계관. 무채색+런타임틴트 파이프라인 실증.
- **네거티브 컨트롤**: 같은 화면에 미재생성 3계열(charger·ranged·swarmer 옛 아트)이 함께 나오는데
  톤이 매끈/3D톤으로 갈려 보인다 — 옛 아트 되돌림과 동등한 증거.

**남은 일**: charger·ranged·swarmer 3계열(spec 준비됨). 다음 세션이 **1계열씩** 위 파이프라인 그대로.
백엔드는 살아 있으니 인내심만 있으면 된다.

---

## 지금 진행 중 (2026-08-15 13:5x)

### 캐릭터 4직업 재생성 — 착수 완료, 생성 진행 중

**왜**: 오너 "현재 캐릭터는 임시본이라 품질 낮음, 재생성 대상". 캐릭터가 최우선
(P2 몹보다 먼저 — 캐릭터는 `SpriteBank.JOB_FRAMES` 슬롯이 이미 다 있어 **뽑는 즉시
화면에 반영되는 유일한 트랙**이다. 몹은 `MOB_DIRS` 확장이 선행돼야 했다).

**착수 전 3대 판정 완료** → `art/CHARACTER_PLAN.md`
1. 앵커 정합성 ✅ — 몬스터 오염 제거(`anchor_characters_clean.png`), 직업별 참조 4장 생성
2. 크레딧 ✅ — **1생성 2크레딧**, 3직업×3시트=18크레딧. 잔액 1092로 여유 충분
3. 파생 설계 ✅ — 전직 11종을 파츠 교체로 파생시키기 위한 제작 규칙 4가지 확정

**진행 상황**

| 직업 | 스펙 | 생성 | 분할·정렬 | 반입 |
|---|---|---|---|---|
| tank | `spec_char_tank.json` | 미실행 | — | — |
| **dps** | `spec_char_dps.json` | ✅ A·B·C (프롬프트 3차 확정본) | — | — |
| healer | `spec_char_healer.json` | 미실행 | — | — |
| buffer | `spec_char_buffer.json` | 미실행 | — | — |

**파이프라인 확립 완료** (`05a62c48`) — `./art/build_chars.sh [직업]` 하나로
생성→격자선제거→분할→정렬이 돈다. 프롬프트는 3차 시행착오로 확정:

| 차수 | 문제 | 해결 |
|---|---|---|
| 1차 | 프레임 라벨이 이미지에 그려짐 | 웹서치 → **스펙시트형 프롬프트가 원인**. 서술형으로 재작성 |
| 2차 | 6셀 요청에 9셀 + 바닥 그림자 | 룰셋에 `EXACTLY SIX CELLS`·`NO SHADOW OF ANY KIND` |
| 3차 | ✅ 전부 해결 | 인접 프레임 차이 28~50%로 동작도 잘 읽힘(실측) |

⚠️ **내 도구가 낸 사고**: `wipe_gridlines.py`가 단독 프레임(invuln)의 캐릭터를 관통하는
줄무늬를 그어 이미지를 훼손했다. "캐릭터는 한 줄을 가득 채우지 않는다"는 가정이 6셀
시트에서만 참이었다. 수정 후 네거티브 컨트롤로 확인함.

**구조**: 13프레임 = 6셀 시트 A(idle·walk2·attack2·special) + 6셀 시트 B(dash4·hurt·death)
+ 단독 C(invuln). 무적 색은 §0-A 확정대로 직업별로 다르다(탱=금 구체 / 근접딜=보라 오라 /
힐·버퍼=금+초록 링). 이동기도 다르다(탱=방패돌진 / 딜=구르기 / 힐·버퍼=짧은스텝).

**다음 할 일**
1. dps 생성물 육안 확인 → 6셀 분할(`split_ai_sheet.py`) → 13프레임 이름 매핑
2. 통과하면 healer·buffer·tank 생성
3. `align_frames.py` → `game_asset_names.py` → 대조 이미지 → Play 스크린샷
4. 통과 후 기존 임시본을 `art/ref_old_chars/`로 이동(삭제 아님)

---

## 완료 (근거 포함)

| 항목 | 근거 | 커밋 |
|---|---|---|
| W3 측정 신뢰성 복구 | 시드 교락(`cfg*1000`) 제거. 5구성 동일 시드 확인 | `956650fa` |
| 도발 하드 락 | **D/A 0.66** (목표 0.75 이하 통과). 5회 중앙값 | `e5c5c6e1` |
| §3 전투 스타일 SO 배선 | 소비처 0곳 → `W3Party.cs:52`가 `Resources.LoadAll`로 실제 소비 | `067b8d6e` |
| 밸런스 앵커 재판정 | **오진 종결** — 26.9%는 게임 수치가 아니라 리텐션 가정이 지배(네거티브 컨트롤로 확정) | `fbc5bc69` |
| P1 프랍 32종 반입 | 검사기 통과 | `da80d6f8` |
| P2 몹 4계열 연결 | 88장 반입 + `MOB_DIRS` 1→5종 + 스폰/애니 규칙 통일 | `5caf3841` |
| 몹 걷기 튐 수리 | 캔버스 4종 혼재 → 1종 통일, 발 여백 0 | `c7d66a04` |
| 화면 3종 실태 확정 | 파티편성·캐릭터 ✅구현 / 영지 ⚠️껍데기. 핸드오프 모순 해소 | `fbc5bc69` |
| 단일 세션 통합 개발 | 세션 분리 자동화 전부 중단 | `b440e341` |

## 아직 안 한 것 (우선순위 순)

1. **캐릭터 재생성 완주** — 위 진행 중
2. **영지 하위 건물 4종** — `EstateScreen.cs`가 껍데기. 대장간·경매장·영묘·수비대가
   전부 "아직 내용이 없다"로 간다
3. **전투 스타일 UI 부재** — 데이터는 살아났는데 플레이어가 바꿀 화면이 없다.
   `CharacterScreen`에 스타일 선택이 없어 실질적으로 기본값 고정
4. **RaceDef 배선** — §3 종족 4종이 전투에 영향 0. `CombatStyleDef`와 같은 계열 빈 배선
5. **보스 쫄 소환** — `BossBattle.cs:317`이 빈 GameObject만 만든다
6. **W2 회피** — 포위율 97%인데 접촉 60초 3회. ⚠️흡수·대시 기준을 낮춰 통과시키지 말 것
7. **P2 몹 Play 육안 확인** — 검사기·컴파일은 통과했으나 화면은 아직 안 봤다

## 보류 — 근거 소멸

**탱 상시 DR 20%**: 도입 근거가 "E/A 0.67 → 0.60 이하"였는데, 최신 5회 중앙값에서
**E/A가 이미 0.39**다(`w3_reps5.csv`). 힐러 가치가 이미 충분히 나오고 있어 지금 넣으면
근거 없이 수치를 만지는 것이 된다. 재개하려면 새 근거가 필요하다.

---

## 완료 — 영지 건물 티어·공사판 (§6) (2026-08-23 16:10 KST)

- **아트**: `art/spec_estate_tiers.json` → Higgsfield `nano_banana_flash` → `art/out_estate_tiers/` 17장
  (`estate_*_{1,2}` ×8 + `estate_scaffold_0`). **기존 `estate_*_0` 재생성 안 함.**
- **반입**: `unity/Assets/Resources/props/` + `.meta` · `prop_scale` 티어 키 추가 · `ARTIFACT_INDEX` 행 갱신
- **코드**: `EstateBuildings.PropOf` 레벨 밴드 1–4→_0 / 5–9→_1 / 10–13→_2 (없으면 낮은 티어 폴백);
  `EstateYard` Busy 코어에 `estate_scaffold_0` 반투명 겹침; `EstateArtTierSelfCheck` MenuItem
- **샷**: `output/qa/ashes-to-stars/estate_tier_shots/qa_go:Estate.png` (`QA_ESTATE_ART_TIERS=1`)
- **SelfCheck**: `results/estate_art_tier_selfcheck.log` PASS


## 정기 회의 20260823-235725
- 역할 병렬 회의(planner·builder·tester+의장) 종료. 산출: docs/meetings/COUNCIL-20260823-235725.md (rc=0, 파트 3/3)

## 정기 회의 20260824-071350
- 역할 병렬 회의(planner·builder·tester+의장) 종료. 산출: docs/meetings/COUNCIL-20260824-071350.md (rc=0, 파트 3/3)
