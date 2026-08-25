# SESSION_HANDOFF — 재와 별 자율 루프 이어받기 (2026-08-25)

## 현재 상황
- 자율 개발 루프(`com.ailab.autonomous_loop`)는 정지 상태(`launchctl list` PID `-`, `loop/STOP` 존재):
  Codex 사용량 100%(재개 2026-08-31), Grok 100%(재개 2026-08-30), 헤드리스 `claude` CLI는
  OAuth 세션 만료로 재인증 필요(오너가 터미널에서 `claude` 1회 로그인해야 함 — 대신 할 수 없음).
- `launchctl getenv LOOP_AGENT`가 "claude"로 설정됨(내가 setenv) — `loop/agent` 파일보다 우선.
- 30분 주기 크론(job id `9903c5ca`, durable, 7일 후 자동 만료)이 루프 상태를 계속 확인 중.
  정지 상태면 클로드가 직접 한 항목씩 이어받아 완료하는 패턴으로 운영 중.
- 실행기 하나라도 복구되면 `rm -f loop/STOP && bash loop/deploy_launchd.sh`로 재기동.

## 완료된 것 (이번 세션)
UI 폴리싱 「PlayerCopy」 패턴(내부 §번호 숨기기)을 다음 화면에 순서대로 적용·커밋:
- FieldScreen.cs (`377b034a` 코드, `9ec2be44` 문서)
- ResultScreen.cs (`1eeb6f23` 코드, `31243530` 문서) — Play 스크린샷 확보
- BattleScreen.cs (`cebcf428` 코드, `98574c15` 문서) — SelfCheck PASS·컴파일 0에러,
  Play 스크린샷은 MCP 브릿지 불안정으로 5회 재시도 후 실패, SelfCheck만으로 대체(정직하게 기록)
- 문서 정리: 밀린 회의록·PROPOSALS 마커 커밋(`e4ada715`)

## 다음 후보
- **TitleScreen.cs 101·103번째 줄**: `"...100층을 다시 오를 수 있다(§8)"`,
  `"...전투력은 그대로(§8)"` — grep으로 재확인 완료, player-facing 리크, PlayerCopy 미적용.
  다음 세션이 여기부터 시작.
- §18-14 소환수 재소환(0.5G/h+쿨다운 30초)은 소환사 펫 시스템 자체가 없어 자동 루프 범위 밖 —
  오너 판단 필요, 재구현 시도하지 말 것.

## 손대지 않고 남겨둔 것 (건드리지 말 것)
- 오래된 미커밋 diff들(이번 세션 이전부터 있었음, 이번 정지와 무관): `loop/README.md`,
  `loop/com.ailab.loopwatch.plist`, `loop/deploy_launchd.sh`, `loop/last_test_report.json`,
  `loop/loop.sh`, `projects/ashes-to-stars/art/gen_sfx_grammar.py`,
  `Editor/ProjCapSelfCheck.cs`, `unity/Packages/packages-lock.json`.
- 손상된 `.meta` 파일 2개(1줄짜리, 정상은 11줄): `Editor/CompactInfoSelfCheck.cs.meta`,
  `Editor/WorldMapPlayerCopySelfCheck.cs.meta` — Unity가 제대로 재생성하기 전엔 커밋 금지.

## 확립된 패턴 (재사용할 것)
PlayerCopy 표준 구현(모든 화면 동일):
```csharp
public const string EnvNoPlayerCopy = "QA_NO_<SCREEN>_PLAYER_COPY";
public static string PlayerCopy(string value)
{
    if (string.IsNullOrEmpty(value)
        || Environment.GetEnvironmentVariable(EnvNoPlayerCopy) == "1")
        return value;
    return System.Text.RegularExpressions.Regex.Replace(
        value, @"\(§[0-9]+(?:-[0-9]+)?(?:[·,]§[0-9]+(?:-[0-9]+)?)*\)", "");
}
```
SelfCheck는 `Editor/BattlePlayerCopySelfCheck.cs`를 템플릿으로 복사(클래스명·상수명·검사
문자열·grep 대상 라인만 교체).
신규 `.meta`는 `python3 -c "import uuid; print(uuid.uuid4().hex)"`로 GUID 생성 후 표준 MonoImporter 블록.
Unity MCP `execute_code`가 간헐적으로 끊기면(`Connection closed`/`Timeout`) 2~5회 재시도, 짧은
`return "ping"` 호출로 연결 회복 확인 후 재시도. 그래도 안 되면 SelfCheck만으로 대체하고
커밋 메시지·STATUS.md에 사유 명시(선례: `377b034a`, `cebcf428`).

## 다음 세션이 할 일 (요약)
1. TitleScreen.cs에 PlayerCopy 패턴 적용(101·103번째 줄).
2. `TitlePlayerCopySelfCheck.cs` + `.meta` 생성, 컴파일 확인, SelfCheck PASS.
3. Play 스크린샷 시도(안 되면 SelfCheck만으로 대체하고 사유 기록).
4. 커밋(코드) → STATUS.md·PROPOSALS.md 갱신 → 커밋(문서).
5. 30분 크론이 계속 돌고 있으니, 루프가 재개됐는지(`launchctl list`, `git log` 최신 커밋 주체)
   매 확인 시 먼저 볼 것 — 재개됐으면 아무 것도 하지 않고 대기만.
