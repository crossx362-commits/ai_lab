#!/bin/bash
# 개발 보드 지킴이 — 보드를 계속 검증하고, 망가졌으면 스스로 고치는 에이전트 (오너 2026-08-23).
#
# 매 주기: [검증] 보드 응답 · state API · 테스트 스위트 → loop/board_keeper.json 기록
#         [수리] 검증 실패 시(시간당 1회 제한) opencode 새 세션으로 외과적 수리 → 테스트 → 커밋
#         [개선] 건강해도 BOARD_KEEPER_IMPROVE_EVERY 주기마다 한 씽 개선 세션
# 원칙은 loop/PROMPT.md와 같다: 새 세션 · 한 번에 하나 · 검사 통과 후 커밋 · 개선안 기록.

set -uo pipefail

DEPLOY_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TARGET_REPO="${1:-$(cd "$DEPLOY_ROOT/.." && pwd)}"
TARGET_REPO="$(cd "$TARGET_REPO" && pwd)"
if [ -f "$DEPLOY_ROOT/env.sh" ]; then
  # shellcheck source=/dev/null
  source "$DEPLOY_ROOT/env.sh"
fi

BOARD_URL="${BOARD_KEEPER_URL:-http://127.0.0.1:8766}"
RESULT_FILE="$TARGET_REPO/loop/board_keeper.json"
COUNT_FILE="$TARGET_REPO/loop/.keeper_count"
FIX_GUARD="$TARGET_REPO/loop/.keeper_last_fix"
BIN="${LOOP_OPENCODE_BIN:-opencode}"
MODEL="${LOOP_OPENCODE_MODEL:-opencode/x-preview-f-free}"
IMPROVE_EVERY="${BOARD_KEEPER_IMPROVE_EVERY:-48}"
mkdir -p "$TARGET_REPO/logs/board_keeper"

# 이중 실행 방지 (수리 세션이 길어져도 다음 주기와 겹치지 않게)
LOCK="$TARGET_REPO/loop/.keeper-lock"
if ! mkdir "$LOCK" 2>/dev/null; then
  echo "지킴이가 이미 진행 중 — 건너뛴다"
  exit 0
fi
trap 'rmdir "$LOCK" 2>/dev/null' EXIT

now_hhmm() { date "+%m-%d %H:%M"; }

# ── [1] 검증 ────────────────────────────────────────────────
FAILED=(); WARNS=()
code="$(curl -s -o /dev/null -w '%{http_code}' --max-time 5 "$BOARD_URL/" 2>/dev/null)"
[ "$code" = "200" ] || FAILED+=("보드응답:$code")

STATE_JSON="$(curl -s --max-time 8 "$BOARD_URL/api/state" 2>/dev/null)"
STATE_OK="$(printf '%s' "$STATE_JSON" | python3 -c '
import json, sys
try:
    d = json.load(sys.stdin)
except Exception:
    print("bad"); raise SystemExit
need = ("updated", "queue", "mcp", "runner", "council", "proposals")
missing = [k for k in need if k not in d]
print("bad" if missing or not isinstance(d.get("queue"), list) else "ok")' 2>/dev/null)"
[ "$STATE_OK" = "ok" ] || FAILED+=("stateAPI:응답이상")

SUITE_OK="$(cd "$TARGET_REPO" && python3 loop/test_board.py 2>&1 | grep -c '^OK$')"
if [ "$SUITE_OK" -lt 1 ]; then
  FAILED+=("테스트스위트:FAIL")
fi

for hook in 'id="glance"' 'id="ops-box"' 'id="usage-box"'; do
  grep -qF "$hook" "$TARGET_REPO/loop/board.html" || FAILED+=("HTML훅없음:$hook")
done

if ! pgrep -f "AI Lab Autonomous Loop/loop.sh" >/dev/null 2>&1 \
   && [ ! -f "$TARGET_REPO/loop/STOP" ] && [ ! -f "$TARGET_REPO/loop/HOLD" ]; then
  WARNS+=("루프꺼짐:STOP/HOLD 없는데 데몬 없음")
fi

# ── [2] 기록 ────────────────────────────────────────────────
python3 - "$RESULT_FILE" "${#FAILED[@]}" $' \n'"${FAILED[*]:-}" "${WARNS[*]:-}" <<'PY'
import json, sys
from datetime import datetime
path, nfail, failed, warns = sys.argv[1], int(sys.argv[2]), sys.argv[3], sys.argv[4]
data = {
    "at": datetime.now().strftime("%Y-%m-%d %H:%M"),
    "ok": nfail == 0,
    "failed": [x.strip() for x in failed.splitlines() if x.strip()],
    "warns": [x.strip() for x in warns.splitlines() if x.strip()],
}
open(path, "w", encoding="utf-8").write(json.dumps(data, ensure_ascii=False, indent=1))
print(("정상" if data["ok"] else "이상") + " · " + ", ".join(data["failed"] or ["검사 전부 통과"]))
PY

# ── [3] 수리·개선 세션 ──────────────────────────────────────
NTH=0; [ -f "$COUNT_FILE" ] && read -r NTH < "$COUNT_FILE"; NTH=$((NTH + 1)); echo "$NTH" > "$COUNT_FILE"

MODE=""
if [ "${#FAILED[@]}" -gt 0 ]; then
  LAST_FIX=0; [ -f "$FIX_GUARD" ] && LAST_FIX=$(cat "$FIX_GUARD" 2>/dev/null || echo 0)
  NOW=$(date +%s)
  if [ $((NOW - LAST_FIX)) -ge 3600 ]; then
    MODE="fix"; date +%s > "$FIX_GUARD"
  else
    echo "수리 요청이지만 시간당 1회 제한 — 다음 주기에 재시도"
  fi
elif [ "$IMPROVE_EVERY" -gt 0 ] && [ $((NTH % IMPROVE_EVERY)) -eq 0 ]; then
  MODE="improve"
fi

[ -z "$MODE" ] && exit 0

if [ "$MODE" = "fix" ]; then
  TASK="너는 재와 별 개발 보드 지킴이다. 비대화형 새 세션이다. 저장소: $TARGET_REPO
직전 자동 검증에서 다른 점을 발견했다: ${FAILED[*]}
먼저 읽어라: docs/feedback/INBOX.md → docs/STATUS.md → loop/board.py · loop/board.html (문제 지점 정독).
그리고 하나만 고쳐라: 실패 항목의 근본 원인 1건을 외과적으로. board.py·board.html·test_board.py만 만질 수 있다.
규칙: 고친 뒤 반드시 \`python3 loop/test_board.py\`가 OK로 끝나야 한다.
통과하면 고친 파일만 명시해 add·commit한다(메시지 '보드지킴이: …'). 통과 못 하면 네 변경을 되돌리고
docs/feedback/PROPOSALS.md에 관찰→제안 한 줄만 남긴다. 마지막에 결과 한 줄을 stdout으로."
else
  TASK="너는 재와 별 개발 보드 지킴이다. 비대화형 새 세션이다. 저장소: $TARGET_REPO
보드는 현재 건강하다. 이번에는 정기 개선 바퀴다 — docs/feedback/PROPOSALS.md에서 보드 관련 미처리 항목을 찾거나,
직접 화면·데이터를 살펴 가장 작은 가시적 개선 1씽을 하라. board.py·board.html 범위 안에서.
규칙: 고친 뒤 \`python3 loop/test_board.py\` OK 확인 → 고친 파일만 add·commit('보드지킴이 개선: …') →
PROPOSALS에 관찰→제안 한 줄 추가. 계획만 쓰면 실패다. 마지막에 결과 한 줄."
fi

echo "지킴이 세션 시작: 모드=$MODE"
(
  cd "$TARGET_REPO" && \
  "$BIN" run -m "$MODEL" "$TASK" >> "$TARGET_REPO/logs/board_keeper/session-$(date +%Y%m%d-%H%M%S).log" 2>&1
) || echo "지킴이 세션 비정상 종료 — 다음 주기에서 재판정"

# 세션 후 재검증으로 마무리
SUITE_AFTER="$(cd "$TARGET_REPO" && python3 loop/test_board.py 2>&1 | tail -1)"
echo "세션 후 스위트: $SUITE_AFTER"
