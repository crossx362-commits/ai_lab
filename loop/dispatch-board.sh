#!/usr/bin/env bash
# 지휘 보드 → 로컬 CLI. 이미 내는 구독(클로드/코덱스/제미니/그록)으로 의견을 받는다.
# API 과금 아님. 창에 다시 치지 않음.
#
#   bash loop/dispatch-board.sh
#   touch loop/STOP   # 멈춤
#
# 실행은 하지 않는다. 채택 전이다.

set -uo pipefail
cd "$(dirname "$0")/.."
ROOT="$PWD"
STOP="$ROOT/loop/STOP"
OUT="$ROOT/loop/opinions"
LAST="$ROOT/loop/.board-dispatch-last"
mkdir -p "$OUT"

PROMPT='loop/BOARD.md 를 읽어라.
너는 의견만 낸다. 코드를 고치지 마라. 커밋하지 마라. 오너에게 일을 시키지 마라.
출력은 두 줄만: 1줄 제목, 1줄 본문.'

run_one() {
  local who="$1" bin="$2"
  shift 2
  command -v "$bin" >/dev/null 2>&1 || return 0
  echo "[dispatch] $who"
  if printf '%s\n' "$PROMPT" | "$@" >"$OUT/$who.md.tmp" 2>"$OUT/$who.err"; then
    mv "$OUT/$who.md.tmp" "$OUT/$who.md"
  else
    echo "$who 실패" >"$OUT/$who.md"
    rm -f "$OUT/$who.md.tmp"
  fi
}

while true; do
  [ -f "$STOP" ] && echo stop && exit 0
  git pull --ff-only >/dev/null 2>&1 || true
  sig=$(git log -1 --format=%H -- loop/BOARD.md 2>/dev/null || echo none)
  if [ -f "$LAST" ] && [ "$(cat "$LAST")" = "$sig" ]; then
    sleep 15
    continue
  fi
  echo "$sig" >"$LAST"
  grep -q '## 명령' loop/BOARD.md || { sleep 15; continue; }

  run_one Claude claude claude -p --permission-mode plan &
  run_one GPT codex codex exec --ephemeral --sandbox read-only &
  run_one 제미니 gemini gemini -p &
  run_one Grok grok grok --no-interactive &
  wait
  echo "[dispatch] 의견 끝 $sig"
  sleep 15
done
