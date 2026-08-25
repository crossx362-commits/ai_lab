# SESSION_HANDOFF — 재와 별 자율 루프 (2026-08-25)

## ⚠️ 오너 지시 (2026-08-25): 이 기계에서 개발 루프 전면 정지 — "다른데서 할게"
- 30분 주기 이어받기 크론(`9903c5ca`) **삭제됨**. 이 기계에서 클로드가 자동으로 개발을
  이어받는 패턴은 종료 — 새 세션은 오너가 직접 시키기 전엔 게임 개발 작업을 집지 마라.
- `loop/STOP` 유지(메인 루프 정지), `loop/STOP_LANE` 생성(속도 레인 정지 신호).
- 진행 중이던 "소비처 0곳" 재스캔 에이전트도 중지함(결과 미채택).

## 현재 상황
- 자율 개발 루프(`com.ailab.autonomous_loop`)는 정지 상태(`launchctl list` PID `-`, `loop/STOP` 존재).
  실측(2026-08-25, `python3 loop/board.py usage <name>`): **Claude = 로그인 없음(OAuth 재인증 필요,
  오너가 터미널에서 `claude` 1회 로그인해야 함), Grok = 사용량 100% 소진.** Codex도 100%(재개 2026-08-31).
- 다른 곳에서 재기동하려면: `rm -f loop/STOP loop/STOP_LANE && bash loop/deploy_launchd.sh`
  (커밋 `9b25f042` 이후 board.py도 함께 배포됨).

## Claude↔Grok 사용량 자동전환 — 구현 완료 (오너 지시 2026-08-25)
오너의 "자율 루프 처음부터 구축" 스펙을 AskUserQuestion으로 확인 → **"ashes-to-stars에 이식·병합"**
선택됨. 스펙 대부분은 기존 인프라가 이미 충족(DESIGN/STATUS/INBOX 존재, launchd
SuccessfulExit=false, qa_shot.sh 시각 QA, claude -p --no-session-persistence). 진짜 빠진 것
하나(사용량 기반 자동전환)만 추가 구현했다. 승인된 계획: `~/.claude/plans/spicy-sprouting-ripple.md`.

- **커밋 `0cbf91e8`** (6파일, +226/-12): `loop/loop.sh`(usage_check·provider_state·양쪽 소진 대기·
  랩로그 소진 폴백·EXHAUSTED_THIS_LAP은 FAILS 미증가), `loop/board.py`(`usage <claude|grok>` CLI
  서브커맨드 — 기존 claude_usage()/grok_usage() 재사용), `loop/env.sh`(LOOP_AUTO_SWITCH=1·
  PROVIDER_RETRY_SECONDS=1800·MAX_PROVIDER_FAILURES=6), `.gitignore`(loop/provider.state),
  `docs/DESIGN.md`(§5 완료기준·레퍼런스 — 원장에서 옮겨적기만), `README.md`(자동전환 사용법).
- **핵심 설계 결정(되돌리지 말 것)**: ① 수동 지정 판정은 "LOOP_AGENT/loop/agent 존재 여부"가
  아니라 **resolved 값이 codex/opencode일 때만** — `loop/agent`에 "claude"가 이미 들어있어서
  존재 여부로 판정하면 자동전환이 영구 비활성화된다. ② usage_check의 `error`는 "로그인 없음"
  문자열만 소진 취급, 그 외 error는 unknown(fail-open, 전환 안 함) — 일시 네트워크 오류를
  소진으로 오판 금지. ③ 소진으로 인한 무산 랩은 기존 MAX_FAILS=3 정체 감지를 안 건드린다
  (EXHAUSTED_THIS_LAP 가드 + 양쪽 소진 시 continue로 BEFORE/AFTER 블록 우회).
- **검증 완료**: bash -n 통과, board.py usage 실호출(claude→로그인 없음, grok→100%), 헬퍼 함수
  가짜 픽스처 단위검증. **라이브 MAX_LOOPS=2 검증은 실 API 사용량이 들어 미실행** — 오너가
  원할 때: `LOOP_MAX_LOOPS=2 ./loop/loop.sh /Users/junholee/ai_lab`.
- **후속 커밋(직전 작업)**: `loop/deploy_launchd.sh`가 board.py를 Application Support로 복사하지
  않아 배포본에서 usage_check가 조용히 unknown 폴백되는 버그 발견 → `cp "$ROOT/loop/board.py"
  "$APP/board.py"` 1줄 추가. 이 파일에는 남의 미커밋 `--no-start` diff가 섞여 있어 **git apply
  --cached로 내 1줄 훙크만 부분 스테이징해 커밋**(전체 파일 add 금지).

## UI 폴리싱 캠페인 — 완료·마감 (더 파지 말 것)
`PlayerCopy`(내부 §번호 숨기기) 패턴을 `*Screen.cs` 7종 전부 적용 완료: WorldMap·Tower·Estate·
Field(`377b034a`)·Result(`1eeb6f23`)·Battle(`cebcf428`)·Title(`e13909a6`) + AuctionHud 방어적
추가(`a3fd819c`). 남은 `*Hud.Line()`류는 전부 QA 전용 게이트 뒤라 일반 플레이 리크 없음
(정정 기록 `4dc069f4`). **후보 확정 전 실소비처 추적 필수** — grep 카운트만으로 리크 단정 금지.

## 다음 세션이 볼 것
1. 크론 확인 시 루프 재개 여부(`launchctl list`, `git log` 최신 커밋 주체) 먼저 — 재개됐으면 대기만.
2. `output/qa/ashes-to-stars/ORDERS.md` 항목 3개는 전부 `[오너 판단 필요]` — 자동 이어받기 금지.
3. 자동 이어받기 기본값: "소비처 0곳" 재스캔(✅ 확정 기능인데 읽는 코드 0곳). 단
   **MobDef.정예유형은 후보 아님** — 원장 §(정예 유형은 별개 축, 6×5=30조합)이 종별 독립 추첨을
   명시해서 소비처 0곳이 의도일 가능성이 높고, 정예 특수능력 6종 전체가 미구현이라 오너 설계
   판단이 필요한 큰 갭이다(자율 범위 밖).

## 손대지 않고 남겨둔 것 (건드리지 말 것)
- 남의 미커밋 diff들(정지와 무관): `loop/README.md`, `loop/com.ailab.loopwatch.plist`,
  `loop/deploy_launchd.sh`의 `--no-start` 부분, `loop/last_test_report.json`,
  `projects/ashes-to-stars/art/gen_sfx_grammar.py`, `Editor/ProjCapSelfCheck.cs`,
  `unity/Packages/packages-lock.json`.
- `loop/STOP` 상태 그대로(오너/사용량 사정) — 이번 작업은 로직만, 실행 여부는 별도 결정.

## 확립된 패턴 (재사용할 것)
PlayerCopy 표준 구현·SelfCheck 템플릿·.meta GUID 생성·Unity MCP 재시도·index.lock 재시도 요령은
직전 커밋들(`377b034a`·`cebcf428`·`e13909a6`·`a3fd819c`)과 CLAUDE.md §5 참고.
