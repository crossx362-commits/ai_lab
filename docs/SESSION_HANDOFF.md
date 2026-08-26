# SESSION_HANDOFF — 재와 별 자율 루프 (2026-08-27 새벽)

## ⚠️ 랩(lap) 에이전트에게: 루프를 멈추지 마라
- **loop/STOP · loop/STOP_LANE 파일을 touch하지 마라. launchctl bootout도 하지 마라.**
  과거 핸드오프에 있던 "끄려면 touch loop/STOP …" 안내를 자율 세션이 실행에 옮겨
  루프가 자멸한 사고가 2026-08-27 01:27에 있었다(autonomous_loop·loopwatch 동시 언로드).
  루프 정지는 **오너만** 결정한다. 너는 게임 구현(PROMPT.md 5절)만 한다.

## 현재 상태 (2026-08-27 01:30)
- **공급자 정책 변경(오너 지시 2026-08-27)**: 「클로드 소진 시 opencode 등으로 넘어가지 말고,
  클로드 할당량이 회복될 때까지 대기」. 반영:
  - `loop/env.sh` `LOOP_PROVIDERS_CHAIN`을 `claude` 단독으로.
  - `loop/loop.sh`에 `CHAIN_COUNT<=1` 분기 신설 — 단독 공급자면 소진돼도 자멸(STOP)하지 않고
    `PROVIDER_RETRY_SECONDS`(1800s)마다 회복을 무한 재확인한다. usage_check는 fail-open이라
    조회 실패('unknown')는 이 대기로 안 온다 — 여기 도달은 진짜 소진(remain<=0)·'로그인 없음'뿐.
  - `loop/provider.state` `current`를 `claude`로. (claude 잔여 85%, 정상 로그인 확인됨.)
  - 다시 다중 공급자로 되돌리려면 env.sh 체인에 쉼표로 실행기를 추가하면 옛 링 전환이 자동 복원.
- 재배포·재시작 완료(`bash loop/deploy_launchd.sh`). 첫 claude 바퀴가 도는지 확인 중.

## 다음 세션이 볼 것
1. `launchctl list | grep -E 'autonomous_loop|loopwatch'` 둘 다 있어야 한다. 없으면
   `bash loop/deploy_launchd.sh`(메인+레인) + loopwatch는 `launchctl bootstrap gui/$(id -u) ~/Library/LaunchAgents/com.ailab.loopwatch.plist`.
2. `tail logs/loop_main.log` — 「바퀴 시작 … agent=claude」→「바퀴 종료 … code=0」이 이어지는지.
   claude가 소진되면 「claude 소진 — 1800초 쉬고 할당량 회복 재확인(종료 안 함)」이 찍히고 대기한다(정상).
3. 저장소에 도는 **다른 Claude Desktop 자율 세션과의 충돌** 주의 — 두 세션이 같은 repo를 만지면
   커밋·launchctl이 엉킨다. 한 번에 한 루프만.

## 미커밋 diff (건드리지 말 것, 남의 것/루프 산출물)
`loop/README.md`, `loop/com.ailab.loopwatch.plist`, `loop/deploy_launchd.sh`,
`loop/last_test_report.json`, `art/gen_sfx_grammar.py`, `Editor/ProjCapSelfCheck.cs`,
`unity/Packages/packages-lock.json`, `docs/GAME_WORKLOG.md`.
※ 이번에 내가 고친 `loop/loop.sh`·`loop/env.sh`·`docs/SESSION_HANDOFF.md`는 공급자 정책 변경분 —
  오너 확인 후 커밋 대상.

## 확립된 패턴 (재사용)
PlayerCopy 표준 구현·SelfCheck 템플릿·.meta GUID·Unity MCP 재시도·index.lock 재시도는
커밋 `377b034a`·`cebcf428`·`e13909a6`·`a3fd819c` 및 CLAUDE.md §5 참고.
