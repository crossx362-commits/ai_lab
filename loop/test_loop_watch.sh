#!/bin/bash
# 실제 launchd를 건드리지 않고 상태+heartbeat 기반 감시 판정을 검증한다.
set -euo pipefail

SOURCE_DIR="$(cd "$(dirname "$0")" && pwd)"
TEST_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/loop-watch.XXXXXX")"
trap 'rm -rf "$TEST_ROOT"' EXIT
mkdir -p "$TEST_ROOT/loop" "$TEST_ROOT/logs" "$TEST_ROOT/bin"
cp "$SOURCE_DIR/loop_watch.sh" "$TEST_ROOT/loop/loop_watch.sh"
cp "$SOURCE_DIR/runtime_state.py" "$TEST_ROOT/loop/runtime_state.py"

cat > "$TEST_ROOT/loop/control.sh" <<'SH'
#!/bin/bash
printf '%s:%s\n' "${LOOP_CONTROL_RESTART_STALE:-0}" "$*" >> "$TEST_ROOT/control.calls"
if [ "${1:-}" = "status" ]; then
  echo "phase=test"
  echo "provider=test"
  echo "heartbeat_at=0"
  echo "pid=${FAKE_LOOP_PID:-777}"
fi
exit 0
SH
cat > "$TEST_ROOT/bin/launchctl" <<'SH'
#!/bin/bash
printf '%s\n' "$*" >> "$TEST_ROOT/launchctl.calls"
exit 0
SH
chmod +x "$TEST_ROOT/loop/loop_watch.sh" "$TEST_ROOT/loop/runtime_state.py" \
  "$TEST_ROOT/loop/control.sh" "$TEST_ROOT/bin/launchctl"

set_phase() {
  local phase="$1" provider="$2"
  python3 "$TEST_ROOT/loop/runtime_state.py" \
    --path "$TEST_ROOT/loop/runtime_state.json" set "$phase" \
    --provider "$provider" --reason test >/dev/null
}

make_heartbeat_stale() {
  python3 - "$TEST_ROOT/loop/runtime_state.json" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
data = json.loads(path.read_text(encoding="utf-8"))
data["heartbeat_at"] = 1
path.write_text(json.dumps(data), encoding="utf-8")
PY
}

run_watch() {
  TEST_ROOT="$TEST_ROOT" \
  PATH="$TEST_ROOT/bin:/usr/bin:/bin" \
  LOOP_WATCH_ROOT="$TEST_ROOT" \
  WATCH_STALE_SECONDS=120 \
  FAKE_LOOP_PID="${FAKE_LOOP_PID:-777}" \
  bash "$TEST_ROOT/loop/loop_watch.sh"
}

count_resume() {
  if [ ! -f "$TEST_ROOT/control.calls" ]; then
    echo 0
    return
  fi
  grep -c ':ensure-running\b' "$TEST_ROOT/control.calls" || true
}

assert_no_resume() {
  if [ -f "$TEST_ROOT/control.calls" ] && grep -q ':ensure-running\b' "$TEST_ROOT/control.calls"; then
    echo "FAIL: $1 상태에서 감시자가 ensure-running을 호출했다"
    exit 1
  fi
}

rm -f "$TEST_ROOT/control.calls"
set_phase quota_wait claude
run_watch
assert_no_resume quota_wait

rm -f "$TEST_ROOT/control.calls"
set_phase recovering claude
run_watch
assert_no_resume recovering

rm -f "$TEST_ROOT/control.calls"
set_phase owner_stopped claude
run_watch
assert_no_resume owner_stopped

rm -f "$TEST_ROOT/control.calls"
set_phase running claude
make_heartbeat_stale
run_watch
if [ "$(count_resume)" -ne 1 ] || ! grep -q '^1:ensure-running claude$' "$TEST_ROOT/control.calls"; then
  echo "FAIL: 오래된 running heartbeat를 강제 재기동하지 않았다"
  exit 1
fi

rm -f "$TEST_ROOT/control.calls"
set_phase running codex
FAKE_LOOP_PID=none run_watch
if [ "$(count_resume)" -ne 1 ] || ! grep -q '^0:ensure-running codex$' "$TEST_ROOT/control.calls"; then
  echo "FAIL: 실행 서비스가 사라진 running 상태를 재개하지 않았다"
  exit 1
fi

if grep -qi 'speedlane\|speed_lane' "$TEST_ROOT/control.calls" "$TEST_ROOT/launchctl.calls" 2>/dev/null; then
  echo "FAIL: 감시자가 비활성 speed lane을 다시 시작했다"
  exit 1
fi

echo "PASS: quota_wait·recovering·owner_stopped의 정상 heartbeat를 건드리지 않는다"
echo "PASS: stale running과 사라진 서비스만 STOP을 해제하지 않는 ensure-running으로 재개한다"
echo "PASS: speed lane은 감시·재기동 대상이 아니다"
