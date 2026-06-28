# AI Team 정비 작업 요약 — 2026-06-27

가짜 에이전트를 걷어내고 실존 3명(예원·영숙·소미) 기준으로 코드·스케줄·대시보드·문서를 일관되게 맞춘 작업의 전체 요약이다.

---

## 1. 배경

`agents.ts`에는 11명, `extension.ts` 내부 로직에는 옛 콘텐츠팀(youtube·instagram·business·designer 등), 실제 구현(스킬 폴더 + 실행 데몬)은 3명 — 이렇게 세 갈래의 로스터가 서로 어긋나 있었다. 실제로 백엔드가 있는 에이전트는 다음 3명뿐이다.

| 에이전트 | 역할 | 스킬 폴더 |
|---|---|---|
| 예원 (ceo) | CEO · 오케스트레이터 | `예원_CEO` |
| 영숙 (secretary) | 비서 · 일정/브리핑 | `영숙_비서` |
| 소미 (somi) | 국내주식 수급 분석가 | `소미_분석가` |

나머지(데이브·레오·시그널·코다리·케빈·경수·티모·로율, 그리고 youtube·instagram·business·editor·writer·researcher·arin·루나·영식)는 정의·잔재만 있고 실체가 없어 모두 제거했다.

---

## 2. 코드 정비 (`projects/ai-team/src`)

- **`agents.ts`**: AGENTS 맵을 3명으로 축소, `AGENT_ORDER`·`SPECIALIST_IDS` 갱신. 예원 페르소나를 YouTube 편중에서 실제 역할(작업 분해·하네스·스킬 감사·피드백)로 재정의.
- **`extension.ts`**: 가상 사무실 책상 좌표(`DESK_LAYOUT.agents`·`CUSTOM_MAP_DESKS`)와 책상 화면 CSS를 3명으로 정리, council·기본 활성 세트·키워드 라우터·도구 카탈로그(`AGENT_TOOLS_CATALOG`)를 실존 3명 기준으로 교체, 가짜 에이전트 도구 시드 분기 삭제.
- 타입 체크는 기존 `_extCtx` 경고 1건 외 신규 오류 0건으로 통과.

---

## 3. 소미 보고: 우리기술 고정 → watchlist 기반

기존에는 `somi_kis_reporter`가 우리기술(032820) 한 종목만 하루 4번 보고하도록 하드코딩돼 있었다. 이를 사용자가 등록한 종목(watchlist)만 보고하도록 전환했다.

- `watchlist_manager`: 기본값 우리기술 제거 → 빈 목록.
- `somi_kis_reporter`: `--send`/`--daemon`이 watchlist 종목을 순회 보고(비면 안내), `--symbol`로 단건 지정 가능. 우리기술 고정 기본값 제거.
- `somi_price_monitor`: 이미 watchlist 다중 종목 감시였고(`run_multi`), 우리기술은 무해한 잔재라 정리.
- 종목 등록은 텔레그램 "관심종목 추가 &lt;코드&gt; &lt;종목명&gt;"으로.

---

## 4. 스케줄 정비 (`schedules.json` + `schedule_manager`)

추천 루틴을 추가하고, 소미를 하루 3회로 병합했으며, 스케줄러가 실제로 명령을 실행하도록 고쳤다.

**최종 자동 루틴 (8개)**

| 시각 | 에이전트 | 작업 | 실행 방식 |
|---|---|---|---|
| 평일 08:00 | 영숙 | 아침 브리핑 | schedule_manager 실행 |
| 매일 04:00 | 영숙 | 리포트 정리 | schedule_manager 실행 |
| 매일 09:00 | 예원 | 일일 피드백 평가 | schedule_manager 실행 |
| 월 09:00 | 예원 | 주간 스킬 감사 | schedule_manager 실행 |
| 금 17:00 | 예원 | 주간 종합 리포트 | schedule_manager 실행 |
| 평일 08:50 | 소미 | 오전 스캔+분석 | reporter `--daemon` 전담 |
| 평일 12:30 | 소미 | 정오 관심종목 분석 | reporter `--daemon` 전담 |
| 평일 15:40 | 소미 | 마감 관심종목 분석 | reporter `--daemon` 전담 |

- `schedule_manager`를 알림 전용에서 **실행기**로 전환(python command를 `cwd=ai_lab`에서 실행). 자체 데몬이 있는 소미 항목은 `"run": false`로 중복 방지.
- 영숙 아침 브리핑용 `morning_brief.py` 신규 작성(일정·예정 루틴·에이전트 현황 → 텔레그램).
- 리포트 정리 command를 `reports cleanup`(미동작) → `reports_manager.py cleanup`으로 수정.

---

## 5. 대시보드

- **`projects/ai-team/dashboard.html` (신규)**: 3명 카드 + 루틴 일정. 일/주/월 동적 뷰, ◀▶ 날짜 이동, 에이전트 필터, 월간 날짜 클릭→일간 이동, 소미 관심종목(watchlist) 섹션. cron 전개 로직은 node로 검증.
- **Cowork 고정 아티팩트 `ai-team-dashboard`**: `AGENT_MAP`을 실존 3명으로 교체(소미 역할을 "메일 매니저"→"국내주식 수급 분석가"로 수정), 2주 그리드 각 칸에 이모지+작업명 표시.

---

## 6. 캘린더 연동 (Google Calendar)

- 가짜 에이전트 반복 일정 7개 시리즈(경수·아린·영식·루나) 삭제.
- 실존 3명의 자동 루틴을 반복 일정으로 등록(에이전트별 색상·실행 명령 description 포함). 고정 대시보드 아티팩트가 이 일정을 읽어 표시.

---

## 7. 검수

- **하네스(`check_all.py`)**: 스케줄 인식·구조·인코딩 정상. 치명적 꼬임 없음.
- **스킬·스크립트**: 35개 `.py` 문법 통과, 스케줄 command 경로 전부 존재, SKILL.md 3개 존재, 핵심 스크립트 import/실행 smoke test 통과.

---

## 8. 문서·정리

- **CLAUDE.md 현행화**: Windows `D:\` → macOS 경로, "13 Agents" → 실존 3명, 가짜 크립토봇(upbit) 섹션 → 소미 국내주식, 깨진 참조(`launchd/install.sh`·`agent_registry.py`·없는 트레이딩 스크립트) 수정.
- `reports/harness/*.json` 6개 git 추적 해제(.gitignore 대상이었음).
- 날씨 기능(telegram_receiver) 등 미커밋분 커밋.

---

## 9. 커밋

```
34d4fdb  reports/harness/*.json 추적 해제
d6f40e94  전체 정리 — 우리기술 잔재 제거·문서 현행화·추적 정리
2723e733  소미 watchlist 기반 보고 + 스케줄러 실행기화 + 대시보드 동적화
5ab3b2e6  가짜 에이전트 제거하고 실존 3명 기준 정비
```

---

## 10. 맥에서 마무리할 것

샌드박스에서 파일 삭제·git lock 제거가 권한 차단되어, 아래는 직접 실행 필요.

```bash
cd ~/ai_lab
rm -f .git/*.lock .git/refs/heads/*.lock   # stale lock 정리
git reset --hard 34d4fdb                    # 인덱스/워킹트리 동기화 (워킹트리 내용은 동일)
find . -path ./node_modules -prune -o -name __pycache__ -type d -exec rm -rf {} +
find . -path ./node_modules -prune -o -name '*.pyc' -delete   # 캐시 정리
```
