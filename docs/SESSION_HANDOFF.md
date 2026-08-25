# SESSION_HANDOFF — 재와 별 자율 루프 이어받기 (2026-08-25)

## 현재 상황
- 자율 개발 루프(`com.ailab.autonomous_loop`)는 정지 상태(`launchctl list` PID `-`, `loop/STOP` 존재):
  Codex 사용량 100%(재개 2026-08-31), Grok 100%(재개 2026-08-30), 헤드리스 `claude` CLI는
  OAuth 세션 만료로 재인증 필요(오너가 터미널에서 `claude` 1회 로그인해야 함 — 대신 할 수 없음).
- `launchctl getenv LOOP_AGENT`가 "claude"로 설정됨(내가 setenv) — `loop/agent` 파일보다 우선.
- 30분 주기 크론(job id `9903c5ca`, durable, 7일 후 자동 만료)이 루프 상태를 계속 확인 중.
  정지 상태면 클로드가 직접 한 항목씩 이어받아 완료하는 패턴으로 운영 중.
- 실행기 하나라도 복구되면 `rm -f loop/STOP && bash loop/deploy_launchd.sh`로 재기동.

## 완료된 것 (이번 세션, 누적)
UI 폴리싱 「PlayerCopy」 패턴(내부 §번호 숨기기)을 `*Screen.cs` 7종 전부에 적용·커밋 완료:
- WorldMapScreen·TowerScreen·EstateScreen (이전 바퀴)
- FieldScreen.cs (`377b034a` 코드, `9ec2be44` 문서)
- ResultScreen.cs (`1eeb6f23` 코드, `31243530` 문서) — Play 스크린샷 확보
- BattleScreen.cs (`cebcf428` 코드, `98574c15` 문서) — SelfCheck만으로 검증(스크린샷 실패, 정직 기록)
- TitleScreen.cs (`e13909a6` 코드, `97eb0b55` 문서) — SelfCheck만으로 검증(스크린샷 실패, 정직 기록)

**`*Screen.cs` 대상 폴리싱은 이걸로 마감.** 다음은 아래 「다음 후보」의 패턴 확장 결정부터.

## 다음 후보 — 방침 결정이 먼저 필요
전 Runtime 재스캔(`grep -c §` per-file) 결과, `GameScreen` 하위가 아닌 **독립 정적
클래스**에도 player-facing `(§N)` 리크가 낱개로 남아있음:
- `AuctionHud.cs:79` — `"HUD는 경매 배경을 가리지 않는다(§16)"` (`Line()` 반환값, 화면에 그대로 뜸)
- `BagTextFmt.cs:53` — `"지갑 부제는 한 줄이다(§16)"`
- `TowerEnding.cs`, `TowerHubCap.cs` 등에도 1건씩 (대부분은 doc-comment이지 실제 UI 문자열이
  아닐 수 있음 — 각각 확인 필요)

**다음 세션이 시작하기 전에 먼저 정할 것**: 클래스마다 로컬 `PlayerCopy`+`EnvNoPlayerCopy`를
그대로 복제할지(기존 관행과 일관), 아니면 공용 `Runtime/PlayerCopy.cs` 정적 유틸(화면별
EnvNo 접두사를 매개변수로 받는 형태)로 통합할지. 이건 기존 패턴을 바꾸는 설계 결정이라
오너 확인 없이 임의로 통합 리팩터링하지 말 것 — 방침만 정하고 나면 클래스별로 하나씩
같은 절차(SelfCheck+검증+커밋)를 반복하면 됨.

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
PlayerCopy 표준 구현(모든 화면 동일, 클래스별 로컬 복제 방식일 경우 계속 이 형태):
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
SelfCheck는 `Editor/TitlePlayerCopySelfCheck.cs`를 템플릿으로 복사(클래스명·상수명·검사
문자열·grep 대상 라인만 교체).
신규 `.meta`는 `python3 -c "import uuid; print(uuid.uuid4().hex)"`로 GUID 생성 후 표준 MonoImporter 블록.
Unity MCP `execute_code`가 간헐적으로 끊기면(`Connection closed`/`Timeout`) 2~5회 재시도, 짧은
`return "ping"` 호출로 연결 회복 확인 후 재시도. 그래도 안 되면 SelfCheck만으로 대체하고
커밋 메시지·STATUS.md에 사유 명시(선례: `377b034a`, `cebcf428`, `e13909a6`).
동시세션 git 충돌: `git commit` 중 `index.lock` 에러가 나면 다른 세션(회의 스크립트 등)의
읽기 전용 git 프로세스가 순간적으로 락을 잡은 것일 수 있다 — `ps aux | grep git`으로 확인,
락 파일이 이미 사라졌으면 그냥 재시도(실측: 이번 세션에서 1회 발생, 재시도로 해결).

## 다음 세션이 할 일 (요약)
1. 위 「다음 후보」의 방침(로컬 복제 vs 공용 유틸)부터 결정 — 애매하면 기존 관행(로컬 복제)
   유지가 안전한 기본값.
2. `AuctionHud.cs`부터 시작(가장 명확한 단일 리크). PlayerCopy 적용 → SelfCheck 신설
   → 컴파일 확인 → Play 검증(안 되면 SelfCheck만으로 대체하고 사유 기록) → 커밋(코드)
   → STATUS.md·PROPOSALS.md 갱신 → 커밋(문서).
3. 30분 크론이 계속 돌고 있으니, 루프가 재개됐는지(`launchctl list`, `git log` 최신 커밋 주체)
   매 확인 시 먼저 볼 것 — 재개됐으면 아무 것도 하지 않고 대기만.
