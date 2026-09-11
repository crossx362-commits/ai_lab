# 오케스트레이터 보드

공유 원본. 이 파일과 `state/autodev.sqlite3`가 보드 화면의 전부다.
화면: `python3 tools/board.py` → http://127.0.0.1:8767

## 금지
**오너에게 일을 시키지 말 것.** 받는 것은 명령·채택·반려뿐이다.
예외 셋(오너 지시 2026-09-08): ①파일 다운로드·외부 게시 ②되돌릴 수 없는 삭제 ③돈 드는 것.

## 규칙
1. AI가 "완료했다"고 말한 것은 완료가 아니다. 변경·컴파일·테스트가 통과해야 완료다.
2. 확인 못 한 것은 UNKNOWN. 초록불이 안 뜬 이유를 숨기지 않는다.
3. main 자동 병합 금지. 브랜치까지만.
4. 게이트를 새로 만들면 **네거티브 컨트롤부터** 통과시킨다(`tools/nc_suite.sh`).
5. 무한 재시도 금지(기본 3회). 인프라 실패는 시도 미차감.

## 형식
항목은 한 줄에 하나(`- `로 시작). 화면이 이 형식을 그대로 읽는다.
- 명령: `[YYYY-MM-DD HH:MM] 본문` — 맨 위가 현재 명령
- 결정대기: 오너 예/아니오만. 답은 줄 끝 `→ 예 [시각]` / `→ 아니오 [시각]`
- 실행: `[ ]`/`[x]` + `[시각] 제목`

## 단계
- PHASE 0 환경 탐지: 완료 [2026-09-11]
- PHASE 1 CLI Orchestrator: 완료 [2026-09-11]
- PHASE 2 Git Worktree 격리: 완료 [2026-09-11]
- PHASE 3 Unity Runner(컴파일·EditMode·PlayMode): 완료 [2026-09-11]
- PHASE 4 자동 수정 루프: 완료 [2026-09-11]
- PHASE 5 Multi Agent(Claude·Grok·Ollama 라우팅): 완료 [2026-09-11]
- PHASE 6 Astra Supervisor(작업 분해·승격·최종 리뷰): 완료 [2026-09-11]
- PHASE 7 Memory Manager: 완료 [2026-09-11]
- PHASE 8 Crash Recovery: 대기
- PHASE 9 Windows Worker: 대기
- PHASE 10 GUI: 대기

## 명령
- [2026-09-11 19:20] 지휘보드 참고해서 오케스트레이터 보드를 만들어라
- [2026-09-11 18:40] 순서대로 개발

## 결정대기

## 실행
- [x] [2026-09-11] PHASE 1~4 + 네거티브 컨트롤 스위트 10종
- [x] [2026-09-11] PHASE 5 — Claude/Grok/Ollama 어댑터와 승격 라우팅
- [x] [2026-09-11] PHASE 6 — Astra Supervisor(작업 분해·최종 리뷰)
- [x] [2026-09-11] PHASE 7 — Memory Manager(압박·스왑 추세 판정, Unity 슬롯 축소, `unity-kill` 범위 한정)
- [ ] [2026-09-11] PHASE 8 — Crash Recovery(RUNNING으로 남은 task 회수)

## 막힘
- Unity 라이선스 핸드셰이크에서 멈추면 진행 없음을 감지하지 못한다 — 타임아웃까지 기다린다.
- RUNNING으로 남은 task를 회수하는 경로가 없다 — PHASE 8에서 처리(지금은 `clean`으로 수동).
