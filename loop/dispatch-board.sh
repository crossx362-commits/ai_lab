#!/usr/bin/env bash
# 지휘 보드 → 로컬 CLI. 이미 내는 구독(클로드/코덱스/제미니/그록)으로 의견을 받는다.
# API 과금 아님. 창에 다시 치지 않음.
#
#   bash loop/dispatch-board.sh
#   DISPATCH_ONCE=1 bash loop/dispatch-board.sh   # 한 바퀴
#   touch loop/STOP   # 멈춤
#
# 실행은 하지 않는다. 채택 전이다.

export PATH="$HOME/.local/bin:$HOME/.grok/bin:$PATH"
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
  command -v "$bin" >/dev/null 2>&1 || { echo "[dispatch] skip $who"; return 0; }
  echo "[dispatch] $who"
  set +e
  if [ "$bin" = "codex" ]; then
    printf '%s\n' "$PROMPT" | "$@" >"$OUT/$who.md.tmp" 2>"$OUT/$who.err"
  else
    "$@" >"$OUT/$who.md.tmp" 2>"$OUT/$who.err"
  fi
  local st=$?
  set +e
  if [ "$st" -eq 0 ] && [ -s "$OUT/$who.md.tmp" ]; then
    mv "$OUT/$who.md.tmp" "$OUT/$who.md"
  else
    echo "$who 실패" >"$OUT/$who.md"
    rm -f "$OUT/$who.md.tmp"
  fi
}

cycle() {
  [ -f "$STOP" ] && echo stop && return 2
  git pull --ff-only >/dev/null 2>&1 || true
  sig=$(git log -1 --format=%H -- loop/BOARD.md 2>/dev/null || echo none)
  if [ -f "$LAST" ] && [ "$(cat "$LAST")" = "$sig" ]; then
    echo "[dispatch] 변화 없음"
    return 1
  fi
  echo "$sig" >"$LAST"
  grep -q '## 명령' loop/BOARD.md || return 1

  run_one Claude claude claude -p --permission-mode plan "$PROMPT" &
  run_one GPT codex codex exec --ephemeral --sandbox read-only &
  run_one 제미니 gemini gemini -p "$PROMPT" &
  run_one Grok grok grok -p "$PROMPT" &
  wait
  echo "[dispatch] 의견 끝 $sig"
  return 0
}

if [ "${DISPATCH_ONCE:-}" = "1" ]; then
  cycle
  exit $?
fi

while true; do
  cycle || true
  [ -f "$STOP" ] && echo stop && exit 0
  sleep 15
done
