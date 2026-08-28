#!/bin/bash
# 실제 launchd나 AI를 건드리지 않고 단일 제어 명령의 멱등성을 검증한다.
set -euo pipefail

SOURCE_DIR="$(cd "$(dirname "$0")" && pwd)"
TEST_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/loop-control.XXXXXX")"
TEST_ROOT="$(cd "$TEST_ROOT" && pwd)"
trap 'rm -rf "$TEST_ROOT"' EXIT

mkdir -p "$TEST_ROOT/loop" "$TEST_ROOT/bin" "$TEST_ROOT/LaunchAgents" "$TEST_ROOT/logs"
for file in control.sh deploy_launchd.sh runtime_state.py \
  com.ailab.autonomous_loop.plist com.ailab.speedlane.plist; do
  [ -f "$SOURCE_DIR/$file" ] && cp "$SOURCE_DIR/$file" "$TEST_ROOT/loop/$file"
done

cat > "$TEST_ROOT/bin/launchctl" <<'SH'
#!/bin/bash
printf '%s\n' "$*" >> "$TEST_ROOT/launchctl.calls"
case "${1:-}" in
  print)
    if [ -f "$TEST_ROOT/loop/STOP" ]; then
      rm -f "$TEST_ROOT/service.running"
    fi
    if [ -f "$TEST_ROOT/service.running" ]; then
      [ -f "$TEST_ROOT/service.path" ] && echo "    $(cat "$TEST_ROOT/service.path")"
      echo '    pid = 4242'
      exit 0
    fi
    if [ -f "$TEST_ROOT/service.loaded" ]; then
      [ -f "$TEST_ROOT/service.path" ] && echo "    $(cat "$TEST_ROOT/service.path")"
      exit 0
    fi
    exit 113
    ;;
  bootstrap)
    touch "$TEST_ROOT/service.loaded"
    printf '%s\n' "$TEST_ROOT/loop/loop.sh" > "$TEST_ROOT/service.path"
    exit 0
    ;;
  kickstart)
    if [ -f "$TEST_ROOT/fail_start" ]; then
      exit 0
    fi
    touch "$TEST_ROOT/service.loaded" "$TEST_ROOT/service.running"
    exit 0
    ;;
  bootout)
    case "${2:-}" in
      *com.ailab.autonomous_loop)
        if [ -f "$TEST_ROOT/fail_bootout" ]; then
          exit 1
        fi
        rm -f "$TEST_ROOT/service.loaded" "$TEST_ROOT/service.running" "$TEST_ROOT/service.path"
        ;;
    esac
    exit 0
    ;;
  enable)
    exit 0
    ;;
esac
exit 2
SH
chmod +x "$TEST_ROOT/bin/launchctl"

if [ ! -f "$TEST_ROOT/loop/control.sh" ]; then
  echo "FAIL: loop/control.sh가 없다"
  exit 1
fi
chmod +x "$TEST_ROOT/loop/control.sh" "$TEST_ROOT/loop/deploy_launchd.sh" \
  "$TEST_ROOT/loop/runtime_state.py"

run_control() {
  TEST_ROOT="$TEST_ROOT" \
  LOOP_CONTROL_ROOT="$TEST_ROOT" \
  LOOP_LAUNCHCTL_BIN="$TEST_ROOT/bin/launchctl" \
  LOOP_LAUNCH_AGENTS_DIR="$TEST_ROOT/LaunchAgents" \
  LOOP_CONTROL_WAIT_ATTEMPTS=2 \
  LOOP_CONTROL_WAIT_SECONDS=0 \
  LOOP_CONTROL_LOCK_ATTEMPTS=100 \
  LOOP_CONTROL_LOCK_WAIT_SECONDS=0.02 \
  LOOP_CONTROL_RESTART_STALE="${LOOP_CONTROL_RESTART_STALE:-0}" \
  bash "$TEST_ROOT/loop/control.sh" "$@"
}

touch "$TEST_ROOT/loop/STOP" "$TEST_ROOT/loop/HOLD" "$TEST_ROOT/loop/STOP_LANE"
run_control start codex > "$TEST_ROOT/start-first.log"

if [ -e "$TEST_ROOT/loop/STOP" ] || [ -e "$TEST_ROOT/loop/HOLD" ]; then
  echo "FAIL: start 한 번으로 STOP/HOLD가 해제되지 않았다"
  exit 1
fi
if [ ! -e "$TEST_ROOT/loop/STOP_LANE" ]; then
  echo "FAIL: 메인 start가 정지된 speed lane을 켰다"
  exit 1
fi
if [ "$(tr -d '[:space:]' < "$TEST_ROOT/loop/agent")" != "codex" ]; then
  echo "FAIL: 지정한 AI가 loop/agent에 기록되지 않았다"
  exit 1
fi
if ! grep -q '^bootstrap ' "$TEST_ROOT/launchctl.calls"; then
  echo "FAIL: 미등록 서비스를 bootstrap하지 않았다"
  exit 1
fi
if ! grep -q '^kickstart ' "$TEST_ROOT/launchctl.calls"; then
  echo "FAIL: 등록 뒤 서비스를 시작하지 않았다"
  exit 1
fi
if [ ! -f "$TEST_ROOT/LaunchAgents/com.ailab.autonomous_loop.plist" ]; then
  echo "FAIL: launchd plist를 등록하지 않았다"
  exit 1
fi

before="$(grep -c '^bootstrap ' "$TEST_ROOT/launchctl.calls")"
run_control start codex > "$TEST_ROOT/start-second.log"
after="$(grep -c '^bootstrap ' "$TEST_ROOT/launchctl.calls")"
if [ "$before" -ne "$after" ]; then
  echo "FAIL: 두 번째 start가 이미 실행 중인 서비스를 중복 등록했다"
  exit 1
fi

printf '%s\n' '/Users/junholee/Library/Application Support/ai_lab_loop/loop.sh' \
  > "$TEST_ROOT/service.path"
before_stale_path="$after"
run_control start codex > "$TEST_ROOT/start-old-path.log"
after_stale_path="$(grep -c '^bootstrap ' "$TEST_ROOT/launchctl.calls")"
if [ "$after_stale_path" -ne $((before_stale_path + 1)) ]; then
  echo "FAIL: 이미 실행 중인 예전 Application Support 정의를 원본 경로로 다시 등록하지 않았다"
  exit 1
fi
if [ "$(cat "$TEST_ROOT/service.path")" != "$TEST_ROOT/loop/loop.sh" ]; then
  echo "FAIL: 재등록 뒤 실제 launchd 실행 경로가 저장소 원본이 아니다"
  exit 1
fi

before_force="$after_stale_path"
LOOP_CONTROL_RESTART_STALE=1 run_control start codex > "$TEST_ROOT/start-stale.log"
after_force="$(grep -c '^bootstrap ' "$TEST_ROOT/launchctl.calls")"
if [ "$after_force" -ne $((before_force + 1)) ]; then
  echo "FAIL: stale 복구 start가 기존 서비스를 내리고 원본을 다시 등록하지 않았다"
  exit 1
fi

touch "$TEST_ROOT/fail_bootout"
before_failed_bootout="$after_force"
set +e
LOOP_CONTROL_RESTART_STALE=1 run_control start codex > "$TEST_ROOT/start-bootout-failure.log" 2>&1
failed_bootout_rc=$?
set -e
rm -f "$TEST_ROOT/fail_bootout"
after_failed_bootout="$(grep -c '^bootstrap ' "$TEST_ROOT/launchctl.calls")"
if [ "$failed_bootout_rc" -eq 0 ]; then
  echo "FAIL: bootout 실패로 예전 PID가 남았는데 시작 성공으로 보고했다"
  exit 1
fi
if [ "$after_failed_bootout" -ne "$before_failed_bootout" ]; then
  echo "FAIL: 기존 서비스 소멸 확인 전에 bootstrap을 시도했다"
  exit 1
fi

run_control stop > "$TEST_ROOT/stop.log"
if [ ! -e "$TEST_ROOT/loop/STOP" ]; then
  echo "FAIL: 명시적 stop이 STOP을 만들지 않았다"
  exit 1
fi
phase="$(python3 "$TEST_ROOT/loop/runtime_state.py" \
  --path "$TEST_ROOT/loop/runtime_state.json" get phase)"
if [ "$phase" != "owner_stopped" ]; then
  echo "FAIL: stop 뒤 phase가 owner_stopped가 아니다: $phase"
  exit 1
fi

before_ensure="$(grep -c '^bootstrap ' "$TEST_ROOT/launchctl.calls")"
run_control ensure-running codex > "$TEST_ROOT/ensure-stopped.log"
after_ensure="$(grep -c '^bootstrap ' "$TEST_ROOT/launchctl.calls")"
if [ ! -e "$TEST_ROOT/loop/STOP" ]; then
  echo "FAIL: 감시용 ensure-running이 오너 STOP을 해제했다"
  exit 1
fi
if [ "$before_ensure" -ne "$after_ensure" ]; then
  echo "FAIL: 감시용 ensure-running이 owner_stopped 서비스를 다시 등록했다"
  exit 1
fi

rm -f "$TEST_ROOT/loop/STOP"
python3 "$TEST_ROOT/loop/runtime_state.py" \
  --path "$TEST_ROOT/loop/runtime_state.json" set running \
  --provider codex --reason test >/dev/null
touch "$TEST_ROOT/service.loaded" "$TEST_ROOT/service.running"
printf '%s\n' "$TEST_ROOT/loop/loop.sh" > "$TEST_ROOT/service.path"
mkdir "$TEST_ROOT/loop/.control-lock"
printf '%s\n' "$$" > "$TEST_ROOT/loop/.control-lock/pid"
run_control ensure-running codex > "$TEST_ROOT/ensure-race.log" 2>&1 &
ensure_pid=$!
sleep 0.1
if ! kill -0 "$ensure_pid" 2>/dev/null; then
  echo "FAIL: 감시용 재개와 stop이 공유 제어 잠금으로 직렬화되지 않았다"
  wait "$ensure_pid" 2>/dev/null || true
  exit 1
fi
touch "$TEST_ROOT/loop/STOP"
python3 "$TEST_ROOT/loop/runtime_state.py" \
  --path "$TEST_ROOT/loop/runtime_state.json" set owner_stopped \
  --provider codex --reason test >/dev/null
rm -f "$TEST_ROOT/loop/.control-lock/pid"
rmdir "$TEST_ROOT/loop/.control-lock"
wait "$ensure_pid"
if [ ! -e "$TEST_ROOT/loop/STOP" ]; then
  echo "FAIL: 잠금 대기 중 생긴 오너 STOP을 감시용 재개가 지웠다"
  exit 1
fi

rm -f "$TEST_ROOT/loop/STOP" "$TEST_ROOT/service.loaded" "$TEST_ROOT/service.running"
touch "$TEST_ROOT/fail_start"
set +e
run_control start claude > "$TEST_ROOT/start-failure.log" 2>&1
failure_rc=$?
set -e
if [ "$failure_rc" -eq 0 ]; then
  echo "FAIL: 실제 PID/heartbeat 확인 실패를 시작 성공으로 보고했다"
  exit 1
fi
phase="$(python3 "$TEST_ROOT/loop/runtime_state.py" \
  --path "$TEST_ROOT/loop/runtime_state.json" get phase)"
if [ "$phase" != "recovering" ]; then
  echo "FAIL: 시작 확인 실패가 recovering으로 기록되지 않았다: $phase"
  exit 1
fi

if ! grep -q '/Users/junholee/ai_lab/loop/loop.sh' \
  "$SOURCE_DIR/com.ailab.autonomous_loop.plist"; then
  echo "FAIL: 메인 plist가 저장소 loop.sh 원본을 직접 가리키지 않는다"
  exit 1
fi
if grep -q '<key>KeepAlive</key>' "$SOURCE_DIR/com.ailab.speedlane.plist"; then
  echo "FAIL: speed lane plist에 상시 재기동이 남아 있다"
  exit 1
fi
if ! grep -A1 '<key>RunAtLoad</key>' "$SOURCE_DIR/com.ailab.speedlane.plist" | grep -q '<false/>';
then
  echo "FAIL: speed lane이 RunAtLoad=false가 아니다"
  exit 1
fi

echo "PASS: start는 정지 신호 해제·AI 지정·실행 확인을 한 번에 수행한다"
echo "PASS: 반복 start는 서비스를 중복 등록하지 않는다"
echo "PASS: stale 복구 start만 기존 서비스를 원본으로 강제 재기동한다"
echo "PASS: stop만 STOP을 만들고 시작 확인 실패는 recovering으로 남긴다"
echo "PASS: 메인 원본 직접 실행·speed lane 상시 중단 정책이 고정됐다"
