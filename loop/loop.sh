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

while true; do
  if [ -f "$STOP" ]; then
    echo "⏹  STOP 파일 발견 — 정지. 재개하려면: rm loop/STOP"
    exit 0
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
    echo "✅ #$ITER 완료"
    tail -5 "$LOG" | sed 's/^/   /'
  else
    FAILS=$((FAILS + 1))
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
  fi

  sleep "$COOLDOWN"
done
