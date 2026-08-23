# 오너 지시함 (INBOX)

> 개발 루프 세션이 가장 먼저 읽고 처리해야 할 최우선 지시 사항입니다.
> 여기에 작성된 지시는 STATUS.md의 일반 큐보다 우선하여 즉시 반영됩니다.

## 최우선 작업 지시
- 

## 피드백 및 요구사항
-

## 대기 중

### 📌 보드·루프 (오너, 2026-08-23 14:22)

- Claude 주간 한도 — **Codex/Grok만**으로 재와 별 루프 계속
- 1순위: 영지 `EstateBuild` (레벨·업그레이드 창) — `GAME_SPEC_ESTATE_BUILD.md` §2-3
- STATUS 큐·WORKLOG 「아직 안 한 것」 따름. 한 바퀴에 하나만.
- EstateBuild 소유권 (한시): Claude 주간 한도(~2026-08-24 23:00 KST) 동안
  `EstateBuild.cs`·업그레이드 창(`EstateScreen`)은 **Codex/Grok**이 닫는다.
  SPEC §4-B 「그록 미접촉」은 한도 해소 또는 이 지시 철회까지 **정지**.
  HOLD: `touch loop/HOLD` 후 해당 파일만, 커밋 직후 해제. 클로드와 동시 수정 금지.


## 처리 완료

### 📌 개발루프 (오너, 2026-08-23 14:00)

- 지시: 기획서와 다음 할 일 문서를 읽고 루프 진행
- 처리: `docs/GAME_WORKLOG.md`와 루트 `DESIGN.md`를 플래너·작업자의 필수 입력으로 연결
- 반영 커밋: `30f28857`, `6bd0e86f` (`codex/autonomous-loop`)
