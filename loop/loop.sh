#!/bin/bash
# 재와 별 — 자동 개발 루프
#
#   ./loop/loop.sh              # 무한 반복
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

mkdir -p "$LOG_DIR"

PROMPT='너는 재와 별(Ashes to Stars) 유니티 게임을 개발하는 자동 루프의 한 이터레이션이다.
너에게는 이전 세션의 기억이 없다. 상태는 전부 파일에 있다.

## 시작 전에 반드시 이 순서로 읽어라
1. docs/feedback/INBOX.md  — 오너 지시. **최우선**이며 다른 모든 계획을 덮는다
2. docs/STATUS.md          — 지금 위치·다음 할 일 큐·완료 기록
3. docs/DESIGN.md          — 무엇을 만드는가(헌법). 원장은 docs/GAME_DESIGN_ASHES_TO_STARS.md
4. projects/ashes-to-stars/CLAUDE.md — 이 프로젝트의 함정 목록

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

echo "🔁 재와 별 자동 루프 시작 — 멈추려면: touch loop/STOP"
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
  echo "─────────────────────────────────────────"
  echo "▶ 이터레이션 #$ITER  $(date '+%H:%M:%S')  → $LOG"

  # --continue를 쓰지 않는다. 매번 새 세션인 것이 이 루프의 핵심이다.
  # `< /dev/null` — 무인 실행이라 stdin이 없다. 안 막으면 매번 3초를 기다리며
  # "no stdin data received" 경고를 낸다(실측).
  if claude -p "$PROMPT" --permission-mode acceptEdits >"$LOG" 2>&1 < /dev/null; then
    FAILS=0
    INFRA=0
    echo "✅ #$ITER 완료"
    tail -5 "$LOG" | sed 's/^/   /'
  elif grep -qiE 'not logged in|please run /login|oauth|api error|rate.?limit|session limit|usage limit|limit .*resets|quota|overloaded|insufficient_quota|credit|network|timed out|ECONN|ENOTFOUND' "$LOG"; then
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
      echo "❌ 인프라 장애가 ${INFRA}회 연속 — 사람이 봐야 한다(구독 토큰 만료 의심: claude auth login)"
      {
        echo ""
        echo "## ⚠️ 루프 정지 — 인프라 장애 지속 ($(date '+%Y-%m-%d %H:%M'))"
        echo "\`claude -p\`가 ${INFRA}회 연속 실패했다. 마지막 로그: \`$LOG\`"
        echo "1순위 의심: **구독 토큰 만료**. \`echo ok | claude -p\`로 직접 찔러 확인하고,"
        echo "만료면 \`claude auth login\`(브라우저 OAuth라 사람만 가능) 후 \`rm loop/STOP\`."
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
