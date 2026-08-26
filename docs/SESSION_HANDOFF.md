# SESSION_HANDOFF — 재와 별 자율 루프 (2026-08-26 저녁)

## 현재 상태 (2026-08-26 20:30 대화 세션 검수 실측)
- 메인 PID 12651 가동, 바퀴 `20260826-194324-1` 진행 중. `loop_watch` 재설계 후 실제로
  19:53에 자동 kickstart 1회 성공 — 감시 장치 작동 확인.
- 속도 레인은 `loop/TASKS.json` tasks가 0건이라 매분 "할 일 없음" 공회전 중(무해하나 무의미).
- 공급자 현황: **claude = 터미널 로그인 없음(오너가 `claude` 1회 실행 필요)**,
  grok = 소진+로그인 만료(주기 8/30까지), codex = 8/31 재개 → 당분간 opencode 의존.
- 끄려면: `touch loop/STOP loop/STOP_LANE`(다음 랩 체크 시 정상 종료).

## 이 세션이 처리한 것 (2026-08-26 20:30, 오너 지시)
- **공유 인덱스 오염 해소**: 인덱스가 HEAD보다 뒤진 스냅이라 맨몸 커밋 시 loop_watch.sh -81·
  TowerClimbCurveMeasure.cs -153·CharacterScreen.cs -17(§18-14 소비처)이 되돌아가고 영지 아트
  png 8종이 삭제될 상태였다. 전 파일 작업트리==HEAD 확인 후 `git reset`으로 해소(무손실).
- **재발 방지**: `loop/commit_guard.sh`가 경로 일치뿐 아니라 **스테이지 블롭 == 작업 트리**까지
  본다(`878ed6b5`, 테스트 18/18). 부분 스테이징은 `COMMIT_GUARD_ALLOW_PARTIAL=1`.
- **죽은 바퀴 잔여 인수 완료**: 속성 탭 스크롤 하단 소비처 복구(`f5115e72` — InfoAt REF_H
  하드컷 → `InfoFoldLimit` 필드화). 12:19·12:21 배치 로그로 대체 검증(SelfCheck PASS·전수 195/195).
  ※ ORDERS③ TowerClimb 178줄은 **이미 커밋돼 있었다**(낡은 인덱스가 미커밋처럼 보이게 한 것).

## 다음 세션이 볼 것
1. `launchctl list | grep autonomous_loop` + `tail logs/loop_main.log` 로 재바퀴 성패 확인.
   opencode가 또 code=1 반복하면 공급자 상태 점검(claude 로그인 여부·grok 주기 8/30까지 소진·codex 8/31 재개).
2. `output/qa/ashes-to-stars/ORDERS.md` 3건은 **전부 종결됐다**(①`bf979fc9` ②`W3Party.StyleFor`
   멤버별 스타일+`QA_NO_MEMBER_STYLE` ③TowerClimb 50층·G3). 옛 「오너 판단 필요·자동 이어받기
   금지」 표기는 낡은 것이라 삭제했다.
3. 자동 이어받기 기본값: "소비처 0곳" 재스캔(✅ 확정 기능인데 읽는 코드 0곳).
   **정예 특수능력 4종(수호자·군단장·저주술사·처형자)은 이제 자율 구현 승인 대상**이다 —
   원장 §10-2가 ✅ 오너 결정(2026-08-13)으로 능력·방치 결과까지 확정해 뒀으므로 새 설계 판단이
   아니다. 조건·우선순위·수치 규칙은 INBOX 「막힌 항목 전수 재판정」(2026-08-26 20:40) 참조.
   보류 해제분도 같은 항목에 있다(§15 동맹 쿨다운 로컬 선행 · G15 저장 분리 절반 · §6 잔여 칸).

## 손대지 않고 남겨둔 것 (건드리지 말 것)
- 남의 미커밋 diff들: `loop/README.md`, `loop/com.ailab.loopwatch.plist`,
  `loop/deploy_launchd.sh`의 `--no-start` 부분, `loop/last_test_report.json`,
  `projects/ashes-to-stars/art/gen_sfx_grammar.py`, `Editor/ProjCapSelfCheck.cs`,
  `unity/Packages/packages-lock.json`, `docs/GAME_WORKLOG.md`.
  (전부 `git reset` 후에도 작업 트리에 그대로 남아 있다 — 스테이징만 풀렸다.)

## 확립된 패턴 (재사용할 것)
PlayerCopy 표준 구현·SelfCheck 템플릿·.meta GUID 생성·Unity MCP 재시도·index.lock 재시도 요령은
커밋들(`377b034a`·`cebcf428`·`e13909a6`·`a3fd819c`)과 CLAUDE.md §5 참고.
Claude↔Grok 사용량 자동전환 설계 결정 3가지는 이전 판 git 이력(커밋 `0cbf91e8`) 참조.

## 바퀴 가속 (2026-08-26 21:35, 오너 「빠르게 진행되게 해줘」)
- 재측정 결과 유니티 배치는 싸다(단일 SelfCheck 3~4초·전수 스윕 74초). 비싼 건 왕복 —
  57분 바퀴에 셸 명령 43회(1회당 ≈1분), 그중 1~21번이 오리엔테이션.
- `loop/lap_brief.sh` 신설(0.47초에 현황+명령표) · PROMPT ②에 「첫 명령」으로 못박음 ·
  board.py 왕복 계측 부풀림 수정(`7e13608a`). 다음 바퀴부터 적용된다 — 명령 43회가
  20대로 떨어지는지 board 「바퀴당 왕복」으로 확인할 것.
- **남은 최대 병목은 공급자**: `claude -p` = "OAuth session expired". 전 바퀴가 opencode
  무료 프리뷰 모델(x-preview-f-free)로 돈다. 오너가 터미널에서 `claude` 1회 로그인하면
  체인 1순위(fable)로 복귀해 턴당 생성 시간이 크게 줄어든다. 이건 오너만 할 수 있다.
