#!/bin/bash
# 개발 보드 지킴이 — 무료 검증을 계속하고, 새 오류 지문만 한 번 수리한다.
#
# 매 주기: [검증] 보드 응답 · state API · 테스트 스위트 → loop/board_keeper.json 기록
#         [수리] 검증 실패 시 같은 오류 지문에 opencode 새 세션 최대 1회
# 건강한 상태의 정기 AI 개선은 호출하지 않는다. 상태·테스트 확인은 항상 로컬에서 끝낸다.

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
STATE_TOOL="$DEPLOY_ROOT/runtime_state.py"
STATE_FILE="${LOOP_RUNTIME_STATE_FILE:-$TARGET_REPO/loop/runtime_state.json}"
FAILURE_CONTEXT="$TARGET_REPO/loop/board_keeper_failure.log"
BIN="${BOARD_KEEPER_BIN:-${LOOP_OPENCODE_BIN:-opencode}}"
MODEL="${LOOP_OPENCODE_MODEL:-opencode/x-preview-f-free}"
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

if ! pgrep -f "$TARGET_REPO/loop/loop.sh" >/dev/null 2>&1 \
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

# ── [3] 새 오류 지문만 수리 ─────────────────────────────────
[ "${#FAILED[@]}" -gt 0 ] || exit 0

if [ ! -f "$STATE_TOOL" ]; then
  echo "수리 생략: runtime_state.py 없음"
  exit 1
fi
if [ -x "$BIN" ]; then
  AI_BIN="$BIN"
else
  AI_BIN="$(command -v "$BIN" 2>/dev/null || true)"
fi
if [ -z "$AI_BIN" ]; then
  echo "수리 대기: 실행 가능한 복구 AI 없음 ($BIN)"
  exit 0
fi

printf '%s\n' "${FAILED[@]}" > "$FAILURE_CONTEXT"
HEAD="$(git -C "$TARGET_REPO" rev-parse HEAD 2>/dev/null || echo nogit)"
FINGERPRINT="$(python3 "$STATE_TOOL" --path "$STATE_FILE" fingerprint \
  --provider "board_keeper:$BIN" --exit-code 1 --log "$FAILURE_CONTEXT" \
  --context-version "$HEAD")"
if ! python3 "$STATE_TOOL" --path "$STATE_FILE" claim "$FINGERPRINT"; then
  echo "동일 오류 지문 — 복구 AI를 다시 호출하지 않음: $FINGERPRINT"
  exit 0
fi

TASK="너는 재와 별 개발 보드 지킴이다. 비대화형 새 세션이다. 저장소: $TARGET_REPO
직전 자동 검증에서 다른 점을 발견했다: ${FAILED[*]}
먼저 읽어라: docs/feedback/INBOX.md → docs/STATUS.md → loop/board.py · loop/board.html (문제 지점 정독).
그리고 하나만 고쳐라: 실패 항목의 근본 원인 1건을 외과적으로. board.py·board.html·test_board.py만 만질 수 있다.
규칙: 고친 뒤 반드시 \`python3 loop/test_board.py\`가 OK로 끝나야 한다.
통과하면 고친 파일만 명시해 add·commit한다(메시지 '보드지킴이: …'). 통과 못 하면 네 변경을 되돌리고
docs/feedback/PROPOSALS.md에 관찰→제안 한 줄만 남긴다. 마지막에 결과 한 줄을 stdout으로."

echo "새 오류 지문 1회 수리 시작: $FINGERPRINT"
(
  cd "$TARGET_REPO" && \
  "$AI_BIN" run -m "$MODEL" "$TASK" >> "$TARGET_REPO/logs/board_keeper/session-$(date +%Y%m%d-%H%M%S).log" 2>&1
) || echo "지킴이 세션 비정상 종료 — 다음 주기에서 재판정"

# 세션 후 재검증으로 마무리
SUITE_AFTER="$(cd "$TARGET_REPO" && python3 loop/test_board.py 2>&1 | tail -1)"
echo "세션 후 스위트: $SUITE_AFTER"
