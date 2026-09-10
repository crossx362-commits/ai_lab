#!/usr/bin/env bash
# 지휘 보드 → 로컬 CLI. 이미 내는 구독(클로드/코덱스/제미니/그록)으로 의견을 받는다.
# API 과금 아님. 실행은 하지 않는다 — 채택 전이다.
#
#   bash loop/dispatch-board.sh                   # 상시 루프: BOARD.md 「명령」이 바뀔 때만 한 바퀴
#   DISPATCH_ONCE=1 bash loop/dispatch-board.sh   # 한 바퀴만
#   DISPATCH_FORCE=1 ...                          # 명령이 안 바뀌었어도 한 번 더
#   touch loop/STOP                               # 멈춤
#
# 산출물: loop/opinions/<AI>.md (1줄 제목 + 본문) · <AI>.status (running|ok|fail) · <AI>.err
#         loop/opinions/_dispatch.status (running|idle <시각>) — 보드 화면이 읽는다.
#
# 프롬프트는 argv가 아니라 파일/stdin으로 넘긴다 — Windows npm 셔임은 argv의 개행 뒤를 잘라
# 첫 줄만 CLI에 넘긴다(2026-07-10 사고). 그록은 --prompt-file, 나머지는 stdin.

set -u
export PATH="$HOME/.local/bin:$HOME/.grok/bin:${APPDATA:-/nonexistent}/npm:$PATH"
export PYTHONUTF8=1
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT" || exit 1
BOARD="$ROOT/loop/BOARD.md"
STOP="$ROOT/loop/STOP"
OUT="$ROOT/loop/opinions"
LAST="$OUT/_last-signature"
PROMPT_FILE="$OUT/_prompt.txt"
LIMIT="${DISPATCH_TIMEOUT:-420}"   # CLI 하나당 최대 초
mkdir -p "$OUT"

now() { date '+%Y-%m-%d %H:%M:%S'; }
log() { echo "[dispatch $(now)] $*"; }

# 「## 명령」 첫 항목. 없으면 빈 문자열.
current_command() {
  awk '/^## 명령/{f=1;next} /^## /{f=0} f && /^- ./{sub(/^- /,""); print; exit}' "$BOARD"
}

signature() {
  { current_command; } | { command -v md5sum >/dev/null && md5sum || cksum; } | awk '{print $1}'
}

write_prompt() {
  local cmd="$1"
  cat >"$PROMPT_FILE" <<EOF
너는 지휘 보드의 의견 담당이다. 먼저 loop/BOARD.md 를 읽어라(금지·연결 규칙 포함).
지금 오너의 명령(맨 위 한 건):
$cmd

규칙: 의견만 낸다. 코드를 고치지 마라. 커밋하지 마라. 오너에게 확인·설치·실행·다운로드를 시키지 마라.
모르면 저장소 파일을 읽어 확인하고, 추측이면 추측이라고 써라.
출력 형식(이것만, 마크다운 제목 기호 없이):
1줄째: 제목 40자 이내
2줄째부터: 본문 3줄 이내 — 무엇을 어느 경로(git 경로)에 어떻게, 위험 한 가지.
EOF
}

with_limit() {
  if command -v timeout >/dev/null 2>&1; then timeout "$LIMIT" "$@"; else "$@"; fi
}

# run_one <표시이름> <실행파일> <입력방식: stdin|file> <명령...>
run_one() {
  local who="$1" bin="$2" mode="$3"; shift 3
  if ! command -v "$bin" >/dev/null 2>&1; then
    log "skip $who ($bin 없음)"
    printf '%s 실패\n' "$who" >"$OUT/$who.md"
    printf 'fail no-cli %s\n' "$(now)" >"$OUT/$who.status"
    printf '%s CLI가 PATH에 없음\n' "$bin" >"$OUT/$who.err"
    return 0
  fi
  log "start $who"
  printf 'running %s\n' "$(now)" >"$OUT/$who.status"
  local st
  if [ "$mode" = "stdin" ]; then
    with_limit "$@" <"$PROMPT_FILE" >"$OUT/$who.md.tmp" 2>"$OUT/$who.err"; st=$?
  else
    with_limit "$@" >"$OUT/$who.md.tmp" 2>"$OUT/$who.err"; st=$?
  fi
  if [ "$st" -eq 0 ] && [ -s "$OUT/$who.md.tmp" ] && grep -q '[^[:space:]]' "$OUT/$who.md.tmp"; then
    mv -f "$OUT/$who.md.tmp" "$OUT/$who.md"
    printf 'ok %s\n' "$(now)" >"$OUT/$who.status"
    log "done $who"
  else
    printf '%s 실패\n' "$who" >"$OUT/$who.md"
    printf 'fail rc=%s %s\n' "$st" "$(now)" >"$OUT/$who.status"
    rm -f "$OUT/$who.md.tmp"
    log "fail $who rc=$st ($(head -c 200 "$OUT/$who.err" | tr '\n' ' '))"
  fi
}

cycle() {
  [ -f "$STOP" ] && { log stop; return 2; }
  local cmd sig
  cmd="$(current_command)"
  if [ -z "$cmd" ]; then
    log "명령 없음 — 대기"
    return 1
  fi
  sig="$(signature)"
  if [ "${DISPATCH_FORCE:-}" != "1" ] && [ -f "$LAST" ] && [ "$(cat "$LAST")" = "$sig" ]; then
    return 1
  fi
  echo "$sig" >"$LAST"
  write_prompt "$cmd"
  printf 'running %s\n' "$(now)" >"$OUT/_dispatch.status"
  log "명령: ${cmd:0:80}"

  run_one Claude claude stdin claude -p --permission-mode plan --output-format text &
  run_one GPT    codex  stdin codex exec --sandbox read-only - &
  run_one 제미니 gemini stdin gemini &
  run_one Grok   grok   file  grok --prompt-file "$PROMPT_FILE" &
  wait
  printf 'idle %s\n' "$(now)" >"$OUT/_dispatch.status"
  log "의견 끝 $sig"
  return 0
}

if [ "${DISPATCH_ONCE:-}" = "1" ]; then
  cycle
  exit $?
fi

while true; do
  git pull --ff-only --quiet >/dev/null 2>&1 || true
  cycle || true
  [ -f "$STOP" ] && { log stop; exit 0; }
  sleep 15
done
