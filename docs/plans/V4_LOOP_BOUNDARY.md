# V4 영구삭제 수용성 — 루프 자동 vs 사람 표본 경계

> 권위: 원장 §21-1 · §21-6 · `docs/V4_EXTERNAL_PLAYTEST.md`.
> 작성: 재와별·기획검증 (2026-08-23 초안). 읽기 검수 후보: 재와별·루프검수.
> W3Party·전투 로직·STATUS 직접 수정 금지. 구현은 §6·소비처0 뒤 보드 배정.

---

## 1. 한 줄

**루프는 “삭제→재건 경계가 로그로 남았는가”만 본다. 70% 계속·24h 재실행은 사람 표본.**

SelfCheck·자동 삭제는 V4 PASS를 대신하지 못한다(원장 §21-6).

---

## 2. 경계표

| 구분 | 담당 | 무엇 | PASS 증거 |
|---|---|---|---|
| 루프 자동 | 개발+루프검수 | 삭제 이벤트·재건/영입 경계 로그, 30분 성장 가드, 이탈사유 enum | 로그 경로 + 스키마 |
| 사람 부채 | 오너/진행자 | 외부 ≥10명, 즉시 계속 ≥7, 24h 재실행 ≥7 | `V4_EXTERNAL_PLAYTEST` 배정표 |

---

## 3. 루프가 자동 확인하는 것

1. **삭제 판정 소스**: 파티 카드 「삭제됨」Cap 문자열이 **아니라** `LifeSystem` / `Memorial` 이벤트·로그.
2. **성장 가드**: 이름·장비·성장이 **벽시계 30분 이상**인 캐릭터만 삭제 세션 대상으로 표시(저장 조작 즉시삭제 금지 — §21-1).
3. **경계 로그** (최소 필드): `session_id,build,t_utc,char_id,event,reason_enum`
   - `event` ∈ {`permadeath`,`rebuild_offer`,`rebuild_accept`,`rebuild_decline`,`session_end`}
4. **이탈사유 enum** (다섯만, playtest_sheet와 동일):  
   `조작 불명확` / `난이도` / `손실 분노` / `다시 키우기 지루함` / `기술 문제`
5. **즉시계속 UI**: 관문②와 같이 **일시정지 오버레이** 중앙 카드만. 도크·부제·YardInspect에 섞지 않음.

경로 제안: `output/qa/ashes-to-stars/v4/session_<id>.log` (또는 jsonl).

---

## 4. 루프가 FAIL로 잡는 것

- Cap 문자열만으로 삭제 판정
- 30분 미만 캐릭터를 V4 표본으로 기록
- 설문/오버레이만 있고 경계 로그 0
- worker가 STATUS에 V4 PASS 기록
- W3Party 수정

---

## 5. 사람 부채 (루프 밖)

- 외부 테스터 ≥10, 즉시 다음 캐릭터 영입/환생 ≥7, 24h 재실행 ≥7 (§21-1)
- 진행자 배정표 (`docs/feedback/playtest_sheet.md`)
- 애매(40~70%) 시 완화안 후 재측정 — 루프가 수치를 바꾸지 않음

---

## 6. 원장·STATUS 반영 방식

- 원장 §22 / STATUS 관문부채: 이 문서 **경로 한 줄**만 (보드가 합의 후).
- 구현 훅·오버레이: 보드가 §6·소비처0 이후 개발·UI에 각각 1건 배정.

---

## 7. 조사 보강 (2026-08-23 · 루프/사람 나눔 근거)

출처 요약:
- Polaris — permanent loss는 공정·가독성 있는 대가일 때 수용된다. 「Getting Over It Acceptance」= 승리에 신경 쓰되 언제든 잃을 수 있음을 이미 이해한 상태. https://polarisgamedesign.com/2025/embracing-player-pain-designing-for-permanent-loss/
- Game Wisdom — 의도적 마찰(뭘 배웠는가) vs 우발 마찰(메뉴 혼란·대기). 우발 마찰은 같은 지점에서 세션 이탈로 드러남. https://game-wisdom.com/general/permadeath-vs-churn-friction-problem-game-design
- ICCD 2025 — 삭제 후 재시작까지 시간·세션 빈도·D1 이탈이 수용성 보조 지표. https://iccd.asia/developing-significant-permadeath-effects-that-enhance-roguelike-player-participation/

루프에 넣을 수 있는 것 (추가):
- `rebuild_offer`→`rebuild_accept|decline` **지연초** (재시작까지 시간). 임계는 사람 부채 — 루프는 기록만.
- `reason_enum`을 우발(`조작 불명확`·`기술 문제`) vs 의도/감정(`손실 분노`·`난이도`·`다시 키우기 지루함`)으로 집계 가능하게 유지.

루프가 하면 안 되는 것:
- 메타진행·완화안 수치를 자동으로 「고쳐서 PASS」
- 70%/24h를 로그 비율로 대체
