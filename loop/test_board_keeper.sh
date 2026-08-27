#!/bin/bash
# 실제 보드·AI를 건드리지 않고 건강한 주기 0회 호출과 오류 지문 중복 제거를 검증한다.
set -euo pipefail

SOURCE_DIR="$(cd "$(dirname "$0")" && pwd)"
TEST_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/board-keeper.XXXXXX")"
trap 'rm -rf "$TEST_ROOT"' EXIT
mkdir -p "$TEST_ROOT/loop" "$TEST_ROOT/bin" "$TEST_ROOT/logs/board_keeper" \
  "$TEST_ROOT/docs/feedback" "$TEST_ROOT/docs"
cp "$SOURCE_DIR/board_keeper.sh" "$TEST_ROOT/loop/board_keeper.sh"
cp "$SOURCE_DIR/runtime_state.py" "$TEST_ROOT/loop/runtime_state.py"

cat > "$TEST_ROOT/loop/test_board.py" <<'PY'
#!/usr/bin/env python3
print("OK")
PY
cat > "$TEST_ROOT/loop/board.html" <<'HTML'
<div id="glance"></div><div id="ops-box"></div><div id="usage-box"></div>
HTML
cat > "$TEST_ROOT/bin/curl" <<'SH'
#!/bin/bash
case "$*" in
  *"%{http_code}"*) printf '%s' "${BOARD_TEST_HTTP_CODE:-200}" ;;
  *"/api/state"*)
    printf '%s\n' '{"updated":"now","queue":[],"mcp":{},"runner":{},"council":{},"proposals":[]}'
    ;;
esac
SH
cat > "$TEST_ROOT/bin/pgrep" <<'SH'
#!/bin/bash
exit 0
SH
cat > "$TEST_ROOT/bin/opencode" <<'SH'
#!/bin/bash
n=0
[ -f "$TEST_ROOT/ai_calls" ] && n="$(cat "$TEST_ROOT/ai_calls")"
printf '%s\n' "$((n + 1))" > "$TEST_ROOT/ai_calls"
printf '%s\n' "$*" >> "$TEST_ROOT/ai_args"
exit 0
SH
chmod +x "$TEST_ROOT/loop/board_keeper.sh" "$TEST_ROOT/loop/runtime_state.py" \
  "$TEST_ROOT/loop/test_board.py" "$TEST_ROOT/bin/curl" "$TEST_ROOT/bin/pgrep" \
  "$TEST_ROOT/bin/opencode"

git -C "$TEST_ROOT" init -q
git -C "$TEST_ROOT" config user.email "keeper-test@example.invalid"
git -C "$TEST_ROOT" config user.name "keeper test"
git -C "$TEST_ROOT" add -- loop docs
git -C "$TEST_ROOT" commit -qm fixture

run_keeper() {
  TEST_ROOT="$TEST_ROOT" \
  PATH="$TEST_ROOT/bin:/usr/bin:/bin" \
  LOOP_OPENCODE_BIN="$TEST_ROOT/bin/opencode" \
  BOARD_KEEPER_IMPROVE_EVERY=1 \
  BOARD_TEST_HTTP_CODE="${BOARD_TEST_HTTP_CODE:-200}" \
  bash "$TEST_ROOT/loop/board_keeper.sh" "$TEST_ROOT"
}

BOARD_TEST_HTTP_CODE=200 run_keeper > "$TEST_ROOT/healthy.log"
if [ -e "$TEST_ROOT/ai_calls" ]; then
  echo "FAIL: 건강한 보드의 정기 개선이 AI를 호출했다"
  exit 1
fi

BOARD_TEST_HTTP_CODE=500 run_keeper > "$TEST_ROOT/failure-first.log"
if [ "$(cat "$TEST_ROOT/ai_calls" 2>/dev/null || echo 0)" -ne 1 ]; then
  echo "FAIL: 실제 보드 오류의 최초 복구 AI 호출이 한 번이 아니다"
  exit 1
fi

# 옛 시간 제한을 무력화해도 새 오류 지문 claim이 중복 호출을 막아야 한다.
printf '%s\n' 0 > "$TEST_ROOT/loop/.keeper_last_fix"
BOARD_TEST_HTTP_CODE=500 run_keeper > "$TEST_ROOT/failure-repeat.log"
if [ "$(cat "$TEST_ROOT/ai_calls")" -ne 1 ]; then
  echo "FAIL: 같은 보드 오류 지문에 복구 AI를 다시 호출했다"
  exit 1
fi
if ! grep -q '동일 오류 지문' "$TEST_ROOT/failure-repeat.log"; then
  echo "FAIL: 중복 복구를 생략한 이유가 로그에 없다"
  exit 1
fi

echo "PASS: 건강한 보드 검사는 AI를 호출하지 않는다"
echo "PASS: 동일 보드 오류는 지문당 복구 AI를 한 번만 호출한다"
