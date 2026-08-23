# 관문②(5h 지루함) — 루프 자동 체크리스트 · CSV 훅

> 권위: 원장 §22 관문② · STATUS「관문 부채」. 이 파일이 **루프가 자동 확인하는 항목**의 실무 원장.
> 사람 표본(의무숙제감 설문·중도포기 사유)은 **루프 밖 부채** — 여기 PASS로 닫지 않는다.
> 작성: 재와별·기획검증 (2026-08-23 초안). 읽기 검수 후보: 재와별·루프검수.
> W3Party·전투 로직·STATUS 직접 수정 금지.

---

## 1. 경계

| 구분 | 누가 | 무엇 |
|---|---|---|
| 루프 자동 | 개발 구현 + 루프검수 | CSV 스키마·heartbeat·5h 기록 연속성 |
| 사람 부채 | 오너/진행자 | 의무숙제감 1–5, 중도포기 사유, 최종 판정 |

원장 §22에는 이 문서 경로 **링크 한 줄**만 둔다.

---

## 2. CSV 경로·스키마 (루프 PASS용)

- 경로: `output/qa/ashes-to-stars/gate2/session_<id>.csv`
- 인코딩: UTF-8, 헤더 1행 필수
- 헤더(고정 순서):

```
session_id,build,t_utc,screen,floor,lv,deaths,revive_herb,action
```

| 열 | 의미 | 금지 |
|---|---|---|
| session_id | 한 플레이 세션 ID | 세션 중 변경 |
| build | 빌드/커밋 짧은 해시 | 공백 |
| t_utc | ISO-8601 UTC | 로컬 TZ만 |
| screen | 허브/필드/탑/영지/기타 | W3Party 내부 상태명 |
| floor | 탑 층(없으면 0) | |
| lv | 파티 대표 Lv | |
| deaths | 누적 사망 | |
| revive_herb | 부활초 잔량 | |
| action | 짧은 태그(hunt/tower/estate/shop/idle/…) | 자유문 장문 |

---

## 3. 루프가 자동 PASS로 인정하는 조건

1. 위 헤더와 **완전 일치**한 CSV가 존재한다.
2. `heartbeat`에 해당하는 행(또는 action=`hb`)이 **≤60초** 간격으로 이어진다.
3. 벽시계 **5시간** 구간에서 heartbeat 공백이 **연속 2분 초과**가 없다.
4. 10분 버킷마다 `action` 데이터 행 ≥1.
5. `session_end`가 있고 `action`∈{quit,crash,timeout} (또는 동등 열 규칙 — 구현 시 문서에 한 줄 명시).
6. 로그/샷만 있고 CSV 0이면 **FAIL**.

---

## 4. 루프가 FAIL로 잡는 것

- CSV 없음 · 헤더 불일치 · STATUS를 worker가 같이 커밋
- heartbeat 공백 >2분 · 5h 미달인데 관문② 완료 주장
- W3Party/`write_paths` 밖 전투 파일 수정
- 설문 UI만 추가하고 계측 로그 0

---

## 5. 사람 부채 (루프 밖 · STATUS 관문부채)

- 의무숙제감 자가진단 1–5 (120분·종료)
- 중도포기 시 사유 한 줄
- 최종 「관문② PASS/FAIL」판정은 사람

---

## 6. 구현 메모 (개발 · 보드 배정 후)

- 로거 위치: 전투/`W3Party` **밖** 세션 로거만 (`output/qa/ashes-to-stars/gate2/`)
- fire-and-forget, 게임 루프 await 금지
- SelfCheck: 헤더·샘플 3행·간격 시뮬 정도면 충분 (실 5h는 사람/야간 잡)

---

## 7. 검수 읽기 체크 (루프검수)

- [ ] 이 문서 §2–4와 커밋 diff가 일치하는가
- [ ] 증거 경로에 CSV+한 줄 로그가 있는가
- [ ] STATUS diff가 worker 커밋에 없는가
