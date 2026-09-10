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
# 의견 파일은 「최종 답」만 담는다 — 그록은 --json-schema 구조화 출력, 코덱스는 -o(마지막 메시지)로
# 받는다. stdout을 통째로 옮기면 조사 중 멘트가 제목이 된다(2026-09-10 Grok 자신이 지적).

set -u
export PATH="$HOME/.local/bin:$HOME/.grok/bin:${APPDATA:-/nonexistent}/npm:$PATH"
export PYTHONUTF8=1
# 클로드 세션 안에서 띄워졌어도 자식 claude -p가 「중첩 세션」으로 거부되지 않게
unset CLAUDECODE CLAUDE_CODE_ENTRYPOINT
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT" || exit 1
BOARD="$ROOT/loop/BOARD.md"
STOP="$ROOT/loop/STOP"
OUT="$ROOT/loop/opinions"
LAST="$OUT/_last-signature"
PROMPT_FILE="$OUT/_prompt.txt"
LIMIT="${DISPATCH_TIMEOUT:-420}"   # CLI 하나당 최대 초
SCHEMA='{"type":"object","properties":{"title":{"type":"string","description":"40자 이내 제목"},"body":{"type":"string","description":"3줄 이내 본문"}},"required":["title","body"]}'
mkdir -p "$OUT"
# 보드 서버가 detached로 띄울 때 로그를 직접 파일에 쓴다(서버가 죽어도 수집·로그가 이어진다)
if [ -n "${DISPATCH_LOG:-}" ]; then exec >>"$DISPATCH_LOG" 2>&1; fi

PY=python3; command -v python3 >/dev/null 2>&1 || PY=python

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
출력 형식(이것만, 마크다운 제목 기호 없이, 조사 과정 서술 금지):
1줄째: 제목 40자 이내
2줄째부터: 본문 3줄 이내 — 무엇을 어느 경로(git 경로)에 어떻게, 위험 한 가지.
EOF
}

with_limit() {
  # -k: TERM 뒤 15초 안에 안 죽으면 KILL — 응답 대기에 매달린 CLI가 수집 전체를 붙잡지 않게
  if command -v timeout >/dev/null 2>&1; then timeout -k 15 "$LIMIT" "$@"; else "$@"; fi
}

# 원시 출력 → 「1줄 제목 + 본문」. post: text | grok-json | file:<경로>
normalize() {
  local post="$1" raw="$2" dest="$3"
  "$PY" - "$post" "$raw" "$dest" <<'PYEOF'
import io, json, re, sys
post, raw, dest = sys.argv[1], sys.argv[2], sys.argv[3]
def read(p):
    try: return io.open(p, encoding="utf-8", errors="replace").read()
    except FileNotFoundError: return ""
text = ""
if post == "grok-json":
    try:
        outer = json.loads(read(raw))
        inner = outer.get("text", "") if isinstance(outer, dict) else ""
        try:
            obj = json.loads(inner)
            text = (obj.get("title", "").strip() + "\n" + obj.get("body", "").strip()).strip()
        except Exception:
            text = inner
    except Exception:
        text = read(raw)
elif post.startswith("file:"):
    text = read(post[5:]) or read(raw)
else:
    text = read(raw)
text = re.sub(r"\x1b\[[0-9;]*m", "", text)
lines = [l.rstrip() for l in text.splitlines()]
lines = [l for l in lines if l.strip()]
if lines:
    lines[0] = re.sub(r"^\s*(#+\s*|\*\*|제목\s*[:：]\s*)", "", lines[0]).strip().strip("*").strip()
    if len(lines) > 1 and re.match(r"^\s*(본문\s*[:：])", lines[1]):
        lines[1] = re.sub(r"^\s*본문\s*[:：]\s*", "", lines[1])
out = "\n".join(lines).strip()
io.open(dest, "w", encoding="utf-8", newline="\n").write(out + ("\n" if out else ""))
sys.exit(0 if out else 3)
PYEOF
}

# run_one <표시이름> <실행파일> <입력방식: stdin|file> <후처리: text|grok-json|file:경로> <명령...>
run_one() {
  local who="$1" bin="$2" mode="$3" post="$4"; shift 4
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
    with_limit "$@" <"$PROMPT_FILE" >"$OUT/$who.raw" 2>"$OUT/$who.err"; st=$?
  else
    with_limit "$@" >"$OUT/$who.raw" 2>"$OUT/$who.err"; st=$?
  fi
  if [ "$st" -eq 0 ] && normalize "$post" "$OUT/$who.raw" "$OUT/$who.md.tmp"; then
    mv -f "$OUT/$who.md.tmp" "$OUT/$who.md"
    printf 'ok %s\n' "$(now)" >"$OUT/$who.status"
    log "done $who"
  else
    # 실패 사유를 남긴다 — CLI는 인증 만료·한도 초과를 stdout에 찍기도 한다(클로드).
    if [ -s "$OUT/$who.raw" ]; then
      { echo "--- stdout ---"; tail -c 1500 "$OUT/$who.raw"; } >>"$OUT/$who.err"
    fi
    printf '%s 실패\n' "$who" >"$OUT/$who.md"
    printf 'fail rc=%s %s\n' "$st" "$(now)" >"$OUT/$who.status"
    rm -f "$OUT/$who.md.tmp"
    log "fail $who rc=$st ($(head -c 200 "$OUT/$who.err" | tr '\n' ' '))"
  fi
  rm -f "$OUT/$who.raw"
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

  run_one Claude claude stdin text claude -p --permission-mode plan --output-format text &
  run_one GPT    codex  stdin "file:$OUT/GPT.last" codex exec --sandbox read-only -o "$OUT/GPT.last" - &
  run_one 제미니 gemini stdin text gemini &
  run_one Grok   grok   file  grok-json grok --prompt-file "$PROMPT_FILE" --output-format json --json-schema "$SCHEMA" &
  wait
  rm -f "$OUT/GPT.last"
  printf 'idle %s\n' "$(now)" >"$OUT/_dispatch.status"
  log "의견 끝 $sig"
  commit_opinions "$sig"
  return 0
}

# 의견 원문(*.md)을 커밋·푸시 — 판정(BOARD.md)만 다른 기계로 가고 근거가 안 가는 일을 막는다(Claude 의견 2026-09-10).
# add와 commit은 한 호흡, 경로는 loop/opinions로 한정. 실패해도 수집 결과는 로컬에 남는다.
commit_opinions() {
  local sig="$1"
  git add -A -- loop/opinions >/dev/null 2>&1 || return 0
  if git diff --cached --quiet -- loop/opinions; then return 0; fi
  git commit -q -m "board: 의견 수집 ${sig:0:8} — $(ls "$OUT"/*.md 2>/dev/null | xargs -n1 basename 2>/dev/null | sed 's/\.md$//' | tr '\n' ' ')" -- loop/opinions \
    && { git push -q origin master >/dev/null 2>&1 || log "의견 커밋됨, 푸시 실패(다음 푸시 때 같이 감)"; }
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
