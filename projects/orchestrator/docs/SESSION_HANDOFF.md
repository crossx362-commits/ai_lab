# 오케스트레이터 세션 인수인계

> 이 파일은 `projects/orchestrator` 전용이다. `ai_lab/docs/SESSION_HANDOFF.md`(Ulon 검수 세션)와
> 다른 파일이니 서로 덮어쓰지 마라.

## 지금 상태 (2026-09-11 21:4x)
PHASE 0~8 + Provider 독립성까지 구현·검증 완료. NC 27종 전부 PASS(약 150초).
전체 사용법·원칙은 `README.md`, 진행판은 `BOARD.md`.

## 다음 사람이 이어서 할 것 (순서대로)
1. **23:41 이후** `./autodev providers --refresh`로 codex·astra 한도 해제 확인.
2. `./autodev review --task 223` — T3은 게이트를 전부 통과했고 리뷰만 못 받았다.
   승인되면 DONE으로 올라간다. 반려면 사유를 보고 재작업 여부를 판단한다.
3. `./autodev run-plan --plan 7 --keep-going` — T4·T5·T6 실행(유료).
   T4는 아직 한 줄도 못 만들었다(세 시도 모두 한도로 죽음).
4. 남은 단계: PHASE 9 Windows Worker(선택, 이 기계엔 Windows 없음) · PHASE 10 GUI
   (보드 `tools/board.py`가 이미 그 역할을 일부 한다).

## 주의
- 이 기계에는 **다른 세션의 Unity**가 같이 돈다. 전역 `pkill -f Unity` 금지 —
  `./autodev unity-kill`이 대상 프로젝트 것만 고른다.
- NC 스위트는 모델을 부르지 않는다(`AUTODEV_NO_CLOUD=1` 자물쇠). 비용 없이 반복해도 된다.
