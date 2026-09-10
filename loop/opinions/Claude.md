명령 차단 데드락·제미니 상시 실패, 수동 해제 없음
board-api.ts `/api/command`은 `dispatchState().running`이면 409인데, detached bash가 idle을 못 쓰고 죽으면 loop/opinions/_dispatch.status가 running으로 남아 STALE_MS(12분)까지 모든 신규 명령이 막힌다 — override/force 파라미터나 PID 생존 확인을 loop/command-board/server/board-api.ts에 넣어야 한다.
loop/dispatch-board.sh:162 제미니는 bare `gemini`에 stdin뿐이라 헤드리스 프롬프트 보장이 없고 지금도 429로 매 수집 실패 중인데, 보드는 조용히 4→3 의견으로 강등된다(실패 배지는 있으나 원인 표면화 약함) — 프롬프트 플래그 명시와 반복 실패 시 CLI 비활성 로스터 필요.
위험 한 가지: 강제 해제 경로가 없어 수집이 한 번 비정상 종료되면 오너가 최대 12분간 명령을 못 내려 보드 전체가 멈춘 것처럼 보인다.
