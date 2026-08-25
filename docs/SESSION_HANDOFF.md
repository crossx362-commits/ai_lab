# SESSION_HANDOFF — 재와 별 자율 루프 이어받기 (2026-08-25)

## 현재 상황
- 자율 개발 루프(`com.ailab.autonomous_loop`)는 정지 상태(`launchctl list` PID `-`, `loop/STOP` 존재):
  Codex 사용량 100%(재개 2026-08-31), Grok 100%(재개 2026-08-30), 헤드리스 `claude` CLI는
  OAuth 세션 만료로 재인증 필요(오너가 터미널에서 `claude` 1회 로그인해야 함 — 대신 할 수 없음).
- `launchctl getenv LOOP_AGENT`가 "claude"로 설정됨(내가 setenv) — `loop/agent` 파일보다 우선.
- 30분 주기 크론(job id `9903c5ca`, durable, 7일 후 자동 만료)이 루프 상태를 계속 확인 중.
  정지 상태면 클로드가 직접 한 항목씩 이어받아 완료하는 패턴으로 운영 중.
- 실행기 하나라도 복구되면 `rm -f loop/STOP && bash loop/deploy_launchd.sh`로 재기동.

## UI 폴리싱 캠페인 — 완료·마감
`PlayerCopy`(내부 §번호 숨기기) 패턴을 `*Screen.cs` 7종 전부에 적용 완료:
WorldMap·Tower·Estate·Field(`377b034a`)·Result(`1eeb6f23`)·Battle(`cebcf428`)·Title(`e13909a6`).
이 화면들의 **상시 노출** Subtitle/Hint 문구에서 `(§N)` 제거는 이걸로 끝.

**중요한 정정(2026-08-25 19:15, `4dc069f4`)**: 지난 바퀴에 "AuctionHud.cs·BagTextFmt.cs 등
비-Screen 클래스에도 리크가 남았다"고 적었는데, 이번에 실소비처를 추적해보니 **틀렸다**.
`AuctionHud.Line()`의 유일한 호출부(`EstateScreen.cs:46`)는 `AuctionHud.ShowQa`(QA 전용
env 플래그)로 게이트돼 있고, 그마저 `EstateScreen.PlayerSubtitle()`을 거쳐 이미 `(§N)`이
제거된 채 뜬다 — 이중 안전. `TowerEnding.cs`의 § 참조도 doc-comment일 뿐 실제 문자열엔 없음.
**grep 카운트만으로 "리크"를 단정하면 안 된다** — `grep -rn "ClassName\."`로 실소비처를 찾고
(a) 게이트 조건 (b) 상위 호출부의 기존 scrub 여부까지 확인한 뒤에야 후보로 확정할 것.
(AuctionHud.cs에는 그래도 일관성 차원에서 방어적 PlayerCopy를 추가함 — `a3fd819c`,
`AuctionHudPlayerCopySelfCheck` PASS, Play 검증은 QA 오버레이 진입에 리플렉션이 필요해 생략.)

**결론: 이 UI 폴리싱 캠페인은 여기서 마감.** 남은 `*Hud.Line()`류는 전부 QA 전용 게이트 뒤
디버그 오버레이라 일반 플레이에서 절대 안 뜬다. 다음 세션은 이 방향으로 더 파지 말 것.

## 다음 세션이 볼 것
1. **`output/qa/ashes-to-stars/ORDERS.md`의 항목 3개는 전부 `[오너 판단 필요]` 표시가 있다** —
   자동 이어받기 대상이 아니다. 오너가 직접 승인하기 전엔 손대지 말 것.
2. 자동으로 이어받을 다음 후보가 마땅치 않으면, CLAUDE.md §5 관행대로 "소비처 0곳"
   (✅ 확정 기능인데 실제로 읽는 코드가 0곳인 것) 재스캔이 안전한 기본값 — 과거 예시:
   MobShotCadence(§10-2), FamilyAdv(§10-3). `game_asset_names.py` 또는 `grep -rn`으로
   `*Def.cs`/ScriptableObject 필드 중 소비처 없는 것을 찾을 것.
3. 30분 크론이 계속 돌고 있으니, 루프가 재개됐는지(`launchctl list`, `git log` 최신 커밋 주체)
   매 확인 시 먼저 볼 것 — 재개됐으면 아무 것도 하지 않고 대기만.

## 손대지 않고 남겨둔 것 (건드리지 말 것)
- 오래된 미커밋 diff들(이번 세션 이전부터 있었음, 정지와 무관): `loop/README.md`,
  `loop/com.ailab.loopwatch.plist`, `loop/deploy_launchd.sh`, `loop/last_test_report.json`,
  `loop/loop.sh`, `projects/ashes-to-stars/art/gen_sfx_grammar.py`,
  `Editor/ProjCapSelfCheck.cs`, `unity/Packages/packages-lock.json`.
- 손상된 `.meta` 파일 2개(1줄짜리, 정상은 11줄): `Editor/CompactInfoSelfCheck.cs.meta`,
  `Editor/WorldMapPlayerCopySelfCheck.cs.meta` — Unity가 제대로 재생성하기 전엔 커밋 금지.

## 확립된 패턴 (재사용할 것 — 새 화면·클래스에 §-leak이 실제로 확인되면)
PlayerCopy 표준 구현(클래스별 로컬 복제 방식):
```csharp
public const string EnvNoPlayerCopy = "QA_NO_<이름>_PLAYER_COPY";
public static string PlayerCopy(string value)
{
    if (string.IsNullOrEmpty(value)
        || Environment.GetEnvironmentVariable(EnvNoPlayerCopy) == "1")
        return value;
    return System.Text.RegularExpressions.Regex.Replace(
        value, @"\(§[0-9]+(?:-[0-9]+)?(?:[·,]§[0-9]+(?:-[0-9]+)?)*\)", "");
}
```
SelfCheck는 `Editor/TitlePlayerCopySelfCheck.cs`를 템플릿으로 복사.
신규 `.meta`는 `python3 -c "import uuid; print(uuid.uuid4().hex)"`로 GUID 생성 후 표준 MonoImporter 블록.
Unity MCP `execute_code`가 간헐적으로 끊기면(`Connection closed`/`Timeout`) 2~5회 재시도, 짧은
`return "ping"` 호출로 연결 회복 확인 후 재시도. 그래도 안 되면 SelfCheck만으로 대체하고
커밋 메시지·STATUS.md에 사유 명시(선례: `377b034a`, `cebcf428`, `e13909a6`, `a3fd819c`).
동시세션 git 충돌: `git commit` 중 `index.lock` 에러가 나면 다른 세션(회의 스크립트 등)의
읽기 전용 git 프로세스가 순간적으로 락을 잡은 것일 수 있다 — `ps aux | grep git`으로 확인,
락 파일이 이미 사라졌으면 그냥 재시도(실측: 이번 세션 1회 발생, 재시도로 해결).
**후보 확정 전 실소비처 추적 필수**: `grep -c §`만으로 후보를 정하지 말 것(위 정정 사례 참고).
