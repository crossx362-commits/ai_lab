#!/bin/bash
# 재와 별 — 자동 개발 루프
#
#   ./loop/loop.sh              # 무한 반복
#   LOOP_AGENT=grok ./loop/loop.sh   # Grok 헤드리스 (클로드·코덱스 한도일 때)
#   LOOP_AGENT=codex ./loop/loop.sh  # Codex 새 세션으로 반복
#   python3 loop/board.py       # 진행·체크·요청 페이지 (http://127.0.0.1:8766)
#   touch loop/STOP             # 다음 이터레이션 시작 전에 멈춘다
#   rm loop/STOP                # 다시 시작
#
# 설계 원칙 (오너 지침 2026-08-15):
#   **매번 기억이 없는 새 세션이다.** `--continue`를 쓰지 않는다 — 그게 핵심이다.
#   맥락이 이어지면 세션이 자기 기억에 의존하게 되고, 그러면 문서가 낡아도 아무도 모른다.
#   기억을 끊으면 **상태는 전부 파일에 있어야만** 하고, 그 강제가 인수인계 품질을 만든다.
#
#   상태 파일 3종:
#     docs/feedback/INBOX.md  — 오너 지시. 최우선
#     docs/STATUS.md          — 지금 위치·다음 할 일 큐·완료 기록
#     docs/DESIGN.md          — 무엇을 만드는가(헌법)
#
# 안전장치:
#   - STOP 파일: 이터레이션 **경계**에서만 본다. 작업 중간에 죽이지 않는다
#   - 연속 실패 상한: 같은 자리에서 계속 넘어지면 사람을 부른다
#   - 이터레이션 간 유예: 크레딧·레이트리밋을 몰아치지 않는다

set -uo pipefail
cd "$(dirname "$0")/.."

ROOT="$PWD"
STOP="$ROOT/loop/STOP"
HOLD="$ROOT/loop/HOLD"
LOG_DIR="$ROOT/loop/logs"
MAX_FAILS="${LOOP_MAX_FAILS:-3}"
COOLDOWN="${LOOP_COOLDOWN:-20}"
# 실행기: 환경변수 > loop/agent > grok.
# 클로드·코덱스 둘 다 한도면 그록으로 간다(오너 2026-08-16).
if [ -n "${LOOP_AGENT:-}" ]; then
  AGENT="$LOOP_AGENT"
elif [ -f "$ROOT/loop/agent" ]; then
  AGENT=$(tr -d ' \t\r\n' < "$ROOT/loop/agent")
else
  AGENT=grok
fi
[ -n "$AGENT" ] || AGENT=grok

grok_bin() {
  if [ -n "${GROK_BIN:-}" ] && [ -x "$GROK_BIN" ]; then printf '%s\n' "$GROK_BIN"; return 0; fi
  if command -v grok >/dev/null 2>&1; then command -v grok; return 0; fi
  for p in "$HOME/.grok/bin/grok" /opt/homebrew/bin/grok /usr/local/bin/grok; do
    if [ -x "$p" ]; then printf '%s\n' "$p"; return 0; fi
  done
  return 1
}

mkdir -p "$LOG_DIR"

PROMPT='너는 재와 별(Ashes to Stars) 유니티 게임을 개발하는 자동 루프의 한 이터레이션이다.
너에게는 이전 세션의 기억이 없다. 상태는 전부 파일에 있다.

## 시작 전에 반드시 이 순서로 읽어라
1. docs/feedback/INBOX.md  — 오너 지시. **최우선**이며 다른 모든 계획을 덮는다
2. docs/STATUS.md          — 지금 위치·다음 할 일 큐·완료 기록
3. docs/DESIGN.md          — 무엇을 만드는가(헌법). 원장은 docs/GAME_DESIGN_ASHES_TO_STARS.md
4. projects/ashes-to-stars/CLAUDE.md — 이 프로젝트의 함정 목록
5. `python3 loop/ollama_split.py classify` — 카피·분류는 로컬 올라마. owner=ollama면
   `copy --kind`로 문구를 받아 소비처에만 넣고, Unity 전투/게이트 코드는 네가 한다.
   `:cloud` 모델·유료 API는 쓰지 마라.

## 오너의 자동 실행 승인
오너는 "기획서 반영해서 개발 가능한 부분 개발 루프 시작"을 명시적으로 지시했고,
확정 문서 안의 세부 구현 판단도 이 무인 루프에 위임했다. 이것이 이번 이터레이션의 사용자 승인이다.
문서에 확정된 안전한 저장소 내부 작업은 짧은 설계를 로그에 남긴 뒤 **별도 승인 질문 없이 구현**하고,
테스트·시각 QA·STATUS 인계·관련 파일 커밋까지 끝내라.
외부 결제·계정 권한·되돌리기 어려운 삭제처럼 새 권한이 정말 필요한 일만 막힘으로 기록하고,
그 경우에는 질문으로 이터레이션을 끝내지 말고 다음으로 실행 가능한 안전한 항목을 잡아라.
오너 2026-08-16 「간단한건 알아서」: 이미 있는 UI 조각 연결·헤더 아이콘·버튼 3상태·
확정된 소비처 0곳 배선은 보드 체크를 기다리지 말고 바로 한다.
오너 2026-08-16 「내 선택은 정말 중요한 것만」: 대장간·레벨업·캐스트 타이밍·UI 크롬·
외부 테스터/V4 70%는 세션이 정하고 진행한다. 사람 관문은 되돌릴 수 없는 OUT과
오너가 명시한 설계 분기뿐이다.

## 이번 이터레이션에 할 일
STATUS.md의 「다음 할 일」 큐에서 **맨 위 한 항목만** 잡는다. 여러 개를 벌이지 마라.
INBOX.md에 지시가 있으면 그것이 큐보다 앞선다.

### 잡을 것이 없으면 (오너 지시 2026-08-15 「대기 상태가 될 경우 기획서를 읽고 다른 일을 생각해서 진행」)
**대기하지 마라.** 큐가 비었거나 맨 위 항목이 남의 손을 기다리는 상태라면,
`docs/GAME_DESIGN_ASHES_TO_STARS.md`(원장)를 읽고 **✅ 확정인데 코드에 없는 것**을 찾아
직접 큐에 올린 뒤 그것을 한다. 찾는 방법:
- ✅ 항목의 핵심 낱말로 `grep -rn` 해서 **소비처가 0곳**인 것을 찾는다.
  이 저장소는 "정의만 있고 부르는 곳이 없다"가 반복해서 나왔다(CombatStyleDef·보스 기믹·
  RaceDef·prop_scale.json 전부 그랬다) — 그게 가장 확실한 미구현 신호다
- 화면 관련이면 `./tools/qa_shot.sh`로 **먼저 찍어 보고** 실제로 비어 있는지 확인한다.
  문서가 "있다"고 해도 화면에 없으면 없는 것이다
새 항목을 STATUS.md 큐에 적을 때 **통과 기준과 네거티브 컨트롤을 같이 적어라** —
그게 없으면 다음 세션이 완료 판정을 못 한다.

### 먼저 확인할 것 — 사람이 같은 걸 하고 있을 수 있다
오너와 대화하는 세션이 이 저장소에서 **동시에** 작업한다. 시작할 때 `git log --oneline -12`를
보고, 잡으려는 항목이 방금 처리됐는지 확인하라. 실제로 나무 반입을 두 쪽이 동시에 해서
`prop_scale.json`에 같은 키가 두 벌 들어간 적이 있다(2026-08-15).

## 완료의 정의 (DESIGN.md §3)
1. 통과 기준이 수치나 화면으로 표현될 것 — "잘 된다"는 완료가 아니다
2. 네거티브 컨트롤 — 되돌리면 다시 깨지는지 확인
3. 증거가 파일로 남을 것 — CSV·스크린샷·로그

## 아트 생성을 걸 때 (실측 사고 2026-08-15)
이미지 생성은 한 계열에 20~40분 걸린다. 그런데 **네가 백그라운드로 띄우고 이터레이션을
끝내면 그 프로세스는 세션과 함께 죽는다** — 실제로 charger 시트 A만 나오고 B는 영영
안 왔다(크레딧은 이미 나갔다). 다음을 지켜라:

1. **떼어내서 띄운다.** 맥에는 `setsid`가 없다(실측 — `nohup: setsid: No such file or directory`).
   파이썬으로 세션을 분리해 띄운다:
   ```
   python3 -c "import subprocess; f=open(\"/tmp/gen.log\",\"w\"); \
   subprocess.Popen([\"python3\",\"aigen.py\",\"--spec\",\"<spec>\",\"--out-dir\",\"<dir>\"], \
   stdout=f, stderr=subprocess.STDOUT, start_new_session=True)"
   ```
   `run_in_background`나 그냥 `&`로는 세션이 끝날 때 같이 죽는다.
   확인법: `ps -o pid,pgid -p <PID>`에서 **PGID == PID**여야 분리된 것이다
2. **표시를 남긴다.** 띄운 직후 `projects/ashes-to-stars/art/.generating`에
   `<spec 이름> <시작시각>`을 적는다
3. **다음 이터레이션은 먼저 그 표시와 출력 폴더를 본다.** 판정 순서를 지켜라:
   - 표시에 적힌 **PID가 죽었으면 그 표시는 낡은 것**이다 → 지우고 진행한다
     (`ps -p <PID>`). 생성이 끝나면 프로세스는 사라지는데 표시는 남는다 —
     실측 2026-08-15: 보스 4종이 다 나왔는데 낡은 표시 때문에 아트 트랙이 막힐 뻔했다
   - PID가 살아 있고 2시간이 안 지났으면 **절대 다시 걸지 마라**(크레딧 이중 소모)
   - 산출물이 다 나왔으면 표시를 지우고 반입 단계로 간다
   ⚠️ `pgrep -f aigen.py`로 확인하지 마라 — **이 프롬프트 본문에 그 문자열이 있어서
      루프 자신이 잡힌다**(실측). `pgrep -f "python3 aigen.py"`처럼 좁혀서 볼 것
4. 생성이 도는 동안 대기하지 말고 **코드 트랙의 다른 항목**을 진행한다

## 화면을 바꿨으면 반드시 눈으로 봐라
  ./tools/qa_shot.sh [dungeon|hunt|boss|raid|party] [프레임]
이 프로젝트는 "500체 700fps PASS"를 낸 화면이 텅 비어 있던 전례가 있다.
수치만 보고 통과라고 말하지 마라.

## 끝낼 때 (이걸 안 하면 다음 세션이 처음부터 다시 판단한다)
- docs/STATUS.md를 **인수인계서처럼** 갱신한다: 무엇을 했나(근거·커밋 해시),
  지금 어디까지 왔나, 다음 할 일 큐, 막힌 것과 그 이유
- 고친 파일만 지정해서 git add하고 곧바로 commit한다(git add -A 금지 —
  자동화 크론도 이 저장소에 직접 커밋한다)

## 하지 마라
- 유니티 에디터를 죽이지 마라(오너가 열어둔 것일 수 있다)
- 한 이터레이션에 여러 항목을 벌이지 마라
- 검증 없이 "완료"라고 STATUS.md에 적지 마라'

echo "🔁 재와 별 자동 루프 시작 ($AGENT) — 멈추려면: touch loop/STOP"
ITER=0
FAILS=0
INFRA=0

while true; do
  if [ -f "$STOP" ]; then
    echo "⏹  STOP 파일 발견 — 정지. 재개하려면: rm loop/STOP"
    exit 0
  fi

  # ── HOLD: 잠깐 비켜 준다(정지가 아니다) ────────────────
  # 왜 STOP과 따로 있나: STOP은 사람이 다시 켜야 하는 **종료**다. 그런데 필요한 건
  # "대화 세션이 공유 파일을 만지는 동안만 손을 떼는 것"이라, 끝나면 **스스로 재개**해야 한다.
  # 실제 사고(2026-08-15): 대화 세션이 W3Party.cs를 고치는 중에 루프가 같은 파일을
  # 커밋해 **남의 미커밋 변경을 자기 커밋에 쓸어담았다**(귀속이 어긋남, 내용 손실은 없었다).
  # CLAUDE.md §7이 경고한 「커밋 오염」이 그대로 재발한 것이다.
  if [ -f "$HOLD" ]; then
    echo "⏸  HOLD — 다른 세션이 작업 중이다. 풀릴 때까지 기다린다($(date '+%H:%M:%S'))"
    WAITED=0
    while [ -f "$HOLD" ]; do
      sleep 15
      WAITED=$((WAITED + 15))
      # 누가 HOLD를 만들어 놓고 잊으면 루프가 영원히 선다. 상한을 두고 스스로 푼다.
      if [ "$WAITED" -ge "${LOOP_HOLD_MAX:-1800}" ]; then
        echo "⚠️ HOLD가 ${WAITED}초째다 — 방치로 보고 해제한다(loop/HOLD 삭제)"
        rm -f "$HOLD"
        break
      fi
    done
    echo "▶ HOLD 해제 — 재개"
  fi

  ITER=$((ITER + 1))
  TS=$(date +%Y%m%d_%H%M%S)
  LOG="$LOG_DIR/iter_${TS}.log"
  STATUS_BEFORE=$(cksum < "$ROOT/docs/STATUS.md")
  echo "─────────────────────────────────────────"
  echo "▶ 이터레이션 #$ITER  $(date '+%H:%M:%S')  → $LOG"

  # --continue를 쓰지 않는다. 매번 새 세션인 것이 이 루프의 핵심이다.
  # `< /dev/null` — 무인 실행이라 stdin이 없다. 안 막으면 매번 3초를 기다리며
  # "no stdin data received" 경고를 낸다(실측).
  # ⚠️ `--allowedTools`가 없으면 **시각 QA가 통째로 막힌다.** `acceptEdits`는 파일 편집만
  #    자동 승인하고 Bash는 여전히 승인을 묻는데, 무인 세션에는 답할 사람이 없어
  #    `This command requires approval`로 거부된다. 실제로 charger 검증이 그렇게 막혀
  #    "GUI 세션 대기"로 인계됐다(2026-08-15). 이 프로젝트는 **화면 확인이 완료 기준**이라
  #    그게 막히면 루프가 완료 판정을 스스로 못 한다.
  #    `settings.local.json`의 allow 목록만으로는 안 됐다 — 실측으로 확인했고,
  #    `--allowedTools`로 넘긴 패턴은 통과했다(878KB 캡처 생성 확인).
  if [ "$AGENT" = "grok" ]; then
    # 그록 헤드리스는 stdin을 프롬프트로 읽지 않는다(실측 문서). --prompt-file 필수.
    # -p/--single 이어도 도구를 여러 번 돌릴 수 있다. --continue 금지.
    GB=$(grok_bin) || { echo "❌ grok CLI 없음" | tee -a "$LOG"; RESULT=127; GB=""; }
    if [ -n "$GB" ]; then
      printf '%s\n' "$PROMPT" > "$LOG_DIR/.prompt.txt"
      "$GB" --prompt-file "$LOG_DIR/.prompt.txt" \
        --cwd "$ROOT" \
        --always-approve \
        --permission-mode bypassPermissions \
        --output-format plain \
        --no-plan \
        --max-turns "${LOOP_GROK_MAX_TURNS:-80}" \
        >"$LOG" 2>&1
      RESULT=$?
    fi
  elif [ "$AGENT" = "codex" ]; then
    # `codex exec`는 호출마다 새 세션이다. --ephemeral로 세션 파일도 남기지 않는다.
    # 실제 창 QA의 UPM IPC와 지정 파일 커밋은 workspace-write에서 차단됐다(2026-08-16 실측).
    # 이 저장소 전용 자동 개발 승인 범위에서 danger-full-access를 사용한다.
    printf '%s\n' "$PROMPT" | codex exec --ephemeral --sandbox danger-full-access \
      --cd "$ROOT" --color never - >"$LOG" 2>&1
    RESULT=$?
  else
    printf '%s\n' "$PROMPT" | claude -p --permission-mode acceptEdits \
         --allowedTools \
           "Bash(./tools/qa_shot.sh:*)" \
           "Bash(tools/qa_shot.sh:*)" \
           "Bash(GAME_SHOT_SEC=*)" \
           "Bash(GAME_START=*)" \
           "Bash(GAME_SHOT_DIR=*)" \
           "Bash(/Applications/Unity/Hub/Editor/*)" \
           "Bash(python3:*)" \
           "Bash(git:*)" \
         >"$LOG" 2>&1
    RESULT=$?
  fi

  # 종료 코드 0만으로 완료라 하지 않는다. 실제 재현(2026-08-16): 에이전트가 설계 승인만
  # 질문하고 정상 종료해 같은 질문을 반복했다. 인수인계 파일 갱신이 완료의 최소 증거다.
  STATUS_AFTER=$(cksum < "$ROOT/docs/STATUS.md")
  if [ "$RESULT" -eq 0 ] && [ "$STATUS_BEFORE" = "$STATUS_AFTER" ]; then
    echo "[loop-guard] STATUS.md 갱신 없음 — 구현 없는 정상 종료를 실패로 분류" >> "$LOG"
    RESULT=86
  fi

  if [ "$RESULT" -eq 0 ]; then
    FAILS=0
    INFRA=0
    echo "✅ #$ITER 완료"
    tail -5 "$LOG" | sed 's/^/   /'
  elif grep -qiE 'not logged in|please run /login|oauth|api error|rate.?limit|session limit|usage limit|limit .*resets|quota|overloaded|insufficient_quota|credit|network|timed out|ECONN|ENOTFOUND' "$LOG"; then
    # 클로드·코덱스 한도 → 그록으로 즉시 전환. 복구 시각까지 자면 개발이 멈춘다.
    if [ "$AGENT" != "grok" ] && grok_bin >/dev/null && \
       grep -qiE 'weekly limit|session limit|usage limit|limit .*resets|try again at' "$LOG"; then
      echo "🔀 $AGENT 한도 — Grok으로 전환하고 바로 재개"
      AGENT=grok
      printf '%s\n' "$AGENT" > "$ROOT/loop/agent"
      ITER=$((ITER - 1))
      continue
    fi
    # ── 인프라 장애는 **작업 실패로 세지 않는다.**
    #    2026-08-15 실측: 인증이 잠깐 흔들린 44초 사이에 재시도 3회가 전부 소진돼
    #    루프가 자멸했다(직후 `claude -p`는 정상 동작). 크레딧·레이트리밋·네트워크도
    #    같은 계열이다 — 이건 "같은 자리에서 계속 넘어지는 것"이 아니라 **기다리면
    #    풀리는 것**이므로, 횟수를 태우지 말고 물러섰다 다시 온다.
    #    (이 저장소가 펫나 엔진에서 이미 배운 규칙이다: 인프라 실패 ≠ 이슈 실패)
    INFRA=$((INFRA + 1))
    WAIT=$((60 * INFRA * INFRA))
    [ "$WAIT" -gt 900 ] && WAIT=900          # 상한 15분 — 더 벌리면 복구를 놓친다

    # ── 언제 풀리는지 **서버가 알려주면** 그때까지 기다린다.
    #    구독 한도는 `You've hit your session limit · resets 8:10pm` 형태로 복구 시각을
    #    준다. 이걸 무시하고 제곱 백오프만 돌면 풀리지도 않은 채 재시도를 반복하거나,
    #    반대로 풀린 뒤에도 한참 자고 있게 된다.
    #    실측 2026-08-15: 18:26에 한도에 걸렸는데 분류기가 'session limit'을 몰라
    #    작업 실패로 세어 3회 만에 루프가 죽었고, 20:10 복구 뒤에도 **1시간 44분간
    #    아무도 재개하지 않았다.**
    RESET=$(grep -oiE 'resets? +[0-9]{1,2}(:[0-9]{2})? *(am|pm)' "$LOG" | head -1 \
            | grep -oiE '[0-9]{1,2}(:[0-9]{2})? *(am|pm)')
    if [ -n "$RESET" ]; then
      NOW_S=$(date +%s)
      # 오늘 그 시각. 이미 지났으면 내일로 넘긴다(자정을 넘긴 한도).
      TGT_S=$(date -j -f "%Y-%m-%d %I:%M %p" "$(date +%Y-%m-%d) $(echo "$RESET" | tr 'a-z' 'A-Z' | sed -E 's/^([0-9]{1,2})( *)(AM|PM)$/\1:00 \3/; s/ +/ /g')" +%s 2>/dev/null || echo "")
      if [ -n "$TGT_S" ]; then
        [ "$TGT_S" -le "$NOW_S" ] && TGT_S=$((TGT_S + 86400))
        UNTIL=$((TGT_S - NOW_S + 60))        # 1분 여유 — 경계에서 다시 걸리지 않게
        if [ "$UNTIL" -gt 0 ] && [ "$UNTIL" -lt 43200 ]; then
          WAIT=$UNTIL
          echo "   ↳ 서버가 복구 시각을 알려줬다($RESET) — 그때까지 ${WAIT}초 기다린다"
        fi
      fi
    fi
    echo "⏸  #$ITER 인프라 장애 (${INFRA}회) — ${WAIT}초 뒤 재시도. 실패로 세지 않는다"
    grep -iE 'not logged in|api error|rate.?limit|overloaded|credit|network' "$LOG" | head -2 | sed 's/^/   /'
    if [ "$INFRA" -ge "${LOOP_MAX_INFRA:-12}" ]; then
      echo "❌ $AGENT 실행기 인프라 장애가 ${INFRA}회 연속 — 사람이 봐야 한다"
      {
        echo ""
        echo "## ⚠️ 루프 정지 — 인프라 장애 지속 ($(date '+%Y-%m-%d %H:%M'))"
        echo "\`$AGENT\` 실행기가 ${INFRA}회 연속 실패했다. 마지막 로그: \`$LOG\`"
        echo "구독 한도·로그인·네트워크 상태를 확인한 뒤 \`rm loop/STOP\`으로 재개할 것."
      } >> "$ROOT/docs/STATUS.md"
      touch "$STOP"
      exit 1
    fi
    sleep "$WAIT"
    ITER=$((ITER - 1))                        # 인프라 장애는 이터레이션을 소비하지 않는다
    continue
  else
    FAILS=$((FAILS + 1))
    INFRA=0
    echo "⚠️ #$ITER 실패 ($FAILS/$MAX_FAILS)"
    tail -15 "$LOG" | sed 's/^/   /'
    if [ "$FAILS" -ge "$MAX_FAILS" ]; then
      # 조용히 계속 돌면 실패가 쌓이는 걸 아무도 모른다. 멈추고 흔적을 남긴다.
      echo "❌ 연속 $MAX_FAILS회 실패 — 정지한다. 로그: $LOG"
      {
        echo ""
        echo "## ⚠️ 루프 자동 정지 ($(date '+%Y-%m-%d %H:%M'))"
        echo "연속 $MAX_FAILS회 실패로 멈췄다. 마지막 로그: \`$LOG\`"
        echo "원인을 확인하고 \`rm loop/STOP\` 후 재개할 것."
      } >> "$ROOT/docs/STATUS.md"
      touch "$STOP"
      exit 1
    fi
    # 작업 실패도 곧바로 다시 던지지 않는다 — 같은 실패를 20초 간격으로 반복하면
    # 상한만 빨리 태우고 아무것도 달라지지 않는다.
    sleep $((COOLDOWN * FAILS * 3))
  fi

  sleep "$COOLDOWN"
done
