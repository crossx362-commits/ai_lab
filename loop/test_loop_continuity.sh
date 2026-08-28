#!/bin/bash
# 실제 AI를 부르지 않고 한도 대기와 오류 복구 전이를 검증한다.
set -euo pipefail

SOURCE_LOOP="$(cd "$(dirname "$0")" && pwd)/loop.sh"
SOURCE_STATE="$(cd "$(dirname "$0")" && pwd)/runtime_state.py"

new_root() {
  local root
  root="$(mktemp -d "${TMPDIR:-/tmp}/loop-continuity.XXXXXX")"
  mkdir -p "$root/loop" "$root/docs/feedback" "$root/projects/ashes-to-stars" "$root/bin"
  cp "$SOURCE_LOOP" "$root/loop/loop.sh"
  cp "$SOURCE_STATE" "$root/loop/runtime_state.py"
  touch "$root/loop/PROMPT.md" "$root/docs/STATUS.md" "$root/docs/DESIGN.md" \
        "$root/docs/feedback/INBOX.md" "$root/projects/ashes-to-stars/CLAUDE.md"
  cat > "$root/loop/commit_guard.sh" <<'SH'
#!/bin/bash
exit 0
SH
  cat > "$root/loop/board.py" <<'PY'
#!/usr/bin/env python3
import json
print(json.dumps({"ok": True, "remain_pct": 100}))
PY
  chmod +x "$root/loop/loop.sh" "$root/loop/runtime_state.py" \
           "$root/loop/commit_guard.sh" "$root/loop/board.py"
  git -C "$root" init -q
  git -C "$root" config user.email "loop-test@example.invalid"
  git -C "$root" config user.name "loop test"
  git -C "$root" add -- loop docs projects
  git -C "$root" commit -qm "fixture"
  printf '%s\n' "$root"
}

run_quota_case() {
  local root="$1"
  cat > "$root/loop/board.py" <<'PY'
#!/usr/bin/env python3
import json
import os
from pathlib import Path

root = Path(os.environ["TEST_ROOT"])
counter = root / "usage_checks"
count = int(counter.read_text() if counter.exists() else "0") + 1
counter.write_text(f"{count}\n")
if count == 1:
    print(json.dumps({"ok": True, "remain_pct": 0}))
elif count in (2, 3):
    print("temporarily unavailable")
else:
    print(json.dumps({"ok": True, "remain_pct": 100}))
PY
  cat > "$root/bin/claude" <<'SH'
#!/bin/bash
n=0
[ -f "$TEST_ROOT/ai_calls" ] && n="$(cat "$TEST_ROOT/ai_calls")"
n=$((n + 1))
printf '%s\n' "$n" > "$TEST_ROOT/ai_calls"
echo "quota recovered" >> "$TEST_ROOT/docs/STATUS.md"
exit 0
SH
  cat > "$root/bin/codex" <<'SH'
#!/bin/bash
touch "$TEST_ROOT/wrong_provider_called"
exit 1
SH
  chmod +x "$root/loop/board.py" "$root/bin/claude" "$root/bin/codex"

  set +e
  PATH="$root/bin:/usr/bin:/bin" TEST_ROOT="$root" \
    LOOP_AGENT=claude LOOP_MAX_LOOPS=1 LOOP_COOLDOWN=0 \
    LOOP_FULLCHECK_EVERY=0 LOOP_COUNCIL_EVERY=0 \
    PROVIDER_RETRY_SECONDS=0 LOOP_HEARTBEAT_SECONDS=1 \
    bash "$root/loop/loop.sh" "$root" > "$root/quota.log" 2>&1
  local rc=$?
  set -e

  if [ "$rc" -ne 0 ]; then
    echo "FAIL: 한도 회복 뒤 정상 바퀴가 완료되지 않았다 (rc=$rc)"
    sed -n '1,120p' "$root/quota.log"
    return 1
  fi
  if [ "$(cat "$root/usage_checks")" -ne 4 ]; then
    echo "FAIL: 한도 확인 뒤 unknown 두 번을 한도 대기로 유지하지 않았다"
    return 1
  fi
  if [ "$(cat "$root/ai_calls")" -ne 1 ]; then
    echo "FAIL: 한도 대기 중 AI를 호출했거나 회복 뒤 호출 수가 잘못됐다"
    return 1
  fi
  if [ -e "$root/wrong_provider_called" ]; then
    echo "FAIL: 한도 대기 중 다른 AI로 전환했다"
    return 1
  fi
  if [ -e "$root/loop/STOP" ]; then
    echo "FAIL: 사용량 한도가 STOP을 만들었다"
    return 1
  fi
  if ! grep -q '사용량 회복 대기' "$root/quota.log"; then
    echo "FAIL: 한도 대기 전이가 로그에 없다"
    return 1
  fi
  local phase
  phase="$(python3 "$root/loop/runtime_state.py" \
    --path "$root/loop/runtime_state.json" get phase)"
  if [ "$phase" != "running" ]; then
    echo "FAIL: 회복 뒤 phase가 running이 아니다: $phase"
    return 1
  fi
}

run_code_error_case() {
  local root="$1"
  cat > "$root/bin/claude" <<'SH'
#!/bin/bash
if printf '%s\n' "$*" | grep -q '오류 복구 세션'; then
  n=0
  [ -f "$TEST_ROOT/recovery_calls" ] && n="$(cat "$TEST_ROOT/recovery_calls")"
  printf '%s\n' "$((n + 1))" > "$TEST_ROOT/recovery_calls"
  printf '%s\n' "$*" > "$TEST_ROOT/recovery_prompt"
  token="$(printf '%s\n' "$*" | grep -o 'loop-recovery:[0-9a-f][0-9a-f]*' | head -1)"
  [ -n "$token" ] || exit 3
  printf '%s\n' "fixed" > "$TEST_ROOT/loop/recovered.txt"
  git -C "$TEST_ROOT" add -- loop/recovered.txt
  git -C "$TEST_ROOT" commit -qm "오류 수리 [$token]"
  exit 0
fi
n=0
[ -f "$TEST_ROOT/normal_calls" ] && n="$(cat "$TEST_ROOT/normal_calls")"
printf '%s\n' "$((n + 1))" > "$TEST_ROOT/normal_calls"
echo "Traceback: ValueError: invalid project state" >&2
exit 1
SH
  cat > "$root/bin/codex" <<'SH'
#!/bin/bash
touch "$TEST_ROOT/wrong_recovery_provider_called"
exit 1
SH
  chmod +x "$root/bin/claude" "$root/bin/codex"

  set +e
  PATH="$root/bin:/usr/bin:/bin" TEST_ROOT="$root" \
    LOOP_AGENT=claude LOOP_MAX_LOOPS=1 LOOP_COOLDOWN=0 \
    LOOP_FULLCHECK_EVERY=0 LOOP_COUNCIL_EVERY=0 \
    LOOP_RECOVERY_PROVIDERS=codex LOOP_RECOVERY_RETRY_SECONDS=0 \
    bash "$root/loop/loop.sh" "$root" > "$root/error.log" 2>&1
  local rc=$?
  set -e

  if [ "$rc" -ne 75 ]; then
    echo "FAIL: 복구 성공 뒤 새 원본 재기동 코드 75가 아니다 (rc=$rc)"
    sed -n '1,140p' "$root/error.log"
    return 1
  fi
  if [ -e "$root/loop/STOP" ]; then
    echo "FAIL: 일반 오류가 STOP을 만들었다"
    return 1
  fi
  if [ "$(cat "$root/recovery_calls")" -ne 1 ]; then
    echo "FAIL: 오류 지문 하나에 복구 AI가 한 번만 호출되지 않았다"
    return 1
  fi
  if [ -e "$root/wrong_recovery_provider_called" ]; then
    echo "FAIL: 사용 가능한 실패 AI 대신 다른 복구 AI로 불필요하게 전환했다"
    return 1
  fi
  if ! grep -q '새 기능을 만들지 않는다' "$root/recovery_prompt"; then
    echo "FAIL: 복구 프롬프트가 개발 작업을 차단하지 않는다"
    return 1
  fi
  if ! grep -q 'ValueError: invalid project state' "$root/recovery_prompt"; then
    echo "FAIL: 복구 프롬프트에 실제 오류가 없다"
    return 1
  fi
}

run_provider_error_case() {
  local root="$1" provider_message="$2"
  cat > "$root/loop/board.py" <<'PY'
#!/usr/bin/env python3
import json
import os
from pathlib import Path

root = Path(os.environ["TEST_ROOT"])
counter = root / "usage_checks"
count = int(counter.read_text() if counter.exists() else "0") + 1
counter.write_text(f"{count}\n")
if count == 2:
    print(json.dumps({"error": "provider unavailable"}))
else:
    print(json.dumps({"ok": True, "remain_pct": 100}))
PY
  cat > "$root/bin/claude" <<'SH'
#!/bin/bash
n=0
[ -f "$TEST_ROOT/development_calls" ] && n="$(cat "$TEST_ROOT/development_calls")"
n=$((n + 1))
printf '%s\n' "$n" > "$TEST_ROOT/development_calls"
if [ "$n" -eq 1 ]; then
  printf '%s\n' "$PROVIDER_MESSAGE" >&2
  exit 1
fi
echo "provider recovered" >> "$TEST_ROOT/docs/STATUS.md"
exit 0
SH
  cat > "$root/bin/codex" <<'SH'
#!/bin/bash
touch "$TEST_ROOT/recovery_ai_called"
exit 1
SH
  chmod +x "$root/loop/board.py" "$root/bin/claude" "$root/bin/codex"

  TEST_ROOT="$root" PROVIDER_MESSAGE="$provider_message" \
    PATH="$root/bin:/usr/bin:/bin" \
    LOOP_AGENT=claude LOOP_MAX_LOOPS=1 LOOP_COOLDOWN=0 \
    LOOP_FULLCHECK_EVERY=0 LOOP_COUNCIL_EVERY=0 \
    PROVIDER_RETRY_SECONDS=0 LOOP_RECOVERY_RETRY_SECONDS=0 \
    LOOP_RECOVERY_PROVIDERS=codex \
    bash "$root/loop/loop.sh" "$root" > "$root/provider-error.log" 2>&1 &
  local pid=$! rc=124
  for _ in $(seq 1 100); do
    if [ -e "$root/recovery_ai_called" ]; then
      break
    fi
    if ! kill -0 "$pid" 2>/dev/null; then
      set +e
      wait "$pid"
      rc=$?
      set -e
      break
    fi
    sleep 0.05
  done
  if kill -0 "$pid" 2>/dev/null; then
    kill "$pid" 2>/dev/null || true
    wait "$pid" 2>/dev/null || true
  fi

  if [ -e "$root/recovery_ai_called" ]; then
    echo "FAIL: 공급자 외부 오류에 복구 AI를 호출했다: $provider_message"
    return 1
  fi
  if [ "$rc" -ne 0 ]; then
    echo "FAIL: 공급자 회복 뒤 같은 AI 개발로 복귀하지 않았다 (rc=$rc)"
    sed -n '1,180p' "$root/provider-error.log"
    return 1
  fi
  if [ "$(cat "$root/development_calls")" -ne 2 ]; then
    echo "FAIL: 공급자 오류 뒤 같은 개발 AI로 한 번만 재시도하지 않았다"
    return 1
  fi
}

run_preflight_error_case() {
  local root="$1"
  cat > "$root/loop/board.py" <<'PY'
#!/usr/bin/env python3
import json
import os
from pathlib import Path

root = Path(os.environ["TEST_ROOT"])
counter = root / "usage_checks"
count = int(counter.read_text() if counter.exists() else "0") + 1
counter.write_text(f"{count}\n")
if count == 1:
    print(json.dumps({"error": "로그인 없음"}, ensure_ascii=False))
else:
    print(json.dumps({"ok": True, "remain_pct": 100}))
PY
  cat > "$root/bin/claude" <<'SH'
#!/bin/bash
n=0
[ -f "$TEST_ROOT/development_calls" ] && n="$(cat "$TEST_ROOT/development_calls")"
printf '%s\n' "$((n + 1))" > "$TEST_ROOT/development_calls"
echo "preflight recovered" >> "$TEST_ROOT/docs/STATUS.md"
exit 0
SH
  cat > "$root/bin/codex" <<'SH'
#!/bin/bash
touch "$TEST_ROOT/recovery_ai_called"
exit 1
SH
  chmod +x "$root/loop/board.py" "$root/bin/claude" "$root/bin/codex"

  set +e
  TEST_ROOT="$root" PATH="$root/bin:/usr/bin:/bin" \
    LOOP_AGENT=claude LOOP_MAX_LOOPS=1 LOOP_COOLDOWN=0 \
    LOOP_FULLCHECK_EVERY=0 LOOP_COUNCIL_EVERY=0 \
    PROVIDER_RETRY_SECONDS=0 LOOP_RECOVERY_RETRY_SECONDS=0 \
    LOOP_RECOVERY_PROVIDERS=codex \
    bash "$root/loop/loop.sh" "$root" > "$root/preflight.log" 2>&1
  local rc=$?
  set -e

  if [ "$rc" -ne 0 ]; then
    echo "FAIL: 로그인 상태 회복 뒤 정상 개발로 복귀하지 않았다 (rc=$rc)"
    sed -n '1,160p' "$root/preflight.log"
    return 1
  fi
  if [ -e "$root/recovery_ai_called" ]; then
    echo "FAIL: 무료 사용량 조회 오류에 복구 AI를 호출했다"
    return 1
  fi
  if [ "$(cat "$root/development_calls")" -ne 1 ]; then
    echo "FAIL: 로그인 회복 뒤 정상 개발 AI 호출이 한 번이 아니다"
    return 1
  fi
  if [ "$(cat "$root/usage_checks")" -ne 3 ]; then
    echo "FAIL: 로그인 회복을 무료 사용량 조회로 확인하지 않았다"
    return 1
  fi
}

run_missing_recovery_executable_case() {
  local root="$1"
  PATH="$root/bin:/usr/bin:/bin" TEST_ROOT="$root" \
    LOOP_AGENT=claude LOOP_MAX_LOOPS=1 LOOP_COOLDOWN=0 \
    LOOP_FULLCHECK_EVERY=0 LOOP_COUNCIL_EVERY=0 \
    LOOP_RECOVERY_PROVIDERS=missing LOOP_RECOVERY_RETRY_SECONDS=1 \
    bash "$root/loop/loop.sh" "$root" > "$root/missing-recovery.log" 2>&1 &
  local pid=$! claims=""
  for _ in $(seq 1 80); do
    if grep -q '사용 가능한 복구 실행기 없음' "$root/missing-recovery.log" 2>/dev/null; then
      break
    fi
    sleep 0.05
  done
  claims="$(python3 "$root/loop/runtime_state.py" \
    --path "$root/loop/runtime_state.json" get recovery_claims)"
  kill "$pid" 2>/dev/null || true
  wait "$pid" 2>/dev/null || true

  if [ "$claims" != "[]" ]; then
    echo "FAIL: 복구 실행기가 없는데 오류 지문 선점권을 소비했다: $claims"
    return 1
  fi
}

run_recovery_attribution_case() {
  local root="$1" before
  before="$(git -C "$root" rev-parse HEAD)"
  printf '%s\n' unrelated > "$root/loop/unrelated.txt"
  git -C "$root" add -- loop/unrelated.txt
  git -C "$root" commit -qm "unrelated concurrent commit"

  if (
    LOOP_SOURCE_ONLY=1 LOOP_AGENT=claude \
      source "$root/loop/loop.sh" "$root"
    recovery_commit_matches "$before" "loop-recovery:testtoken123"
  ); then
    echo "FAIL: 복구 표식 없는 동시 커밋을 복구 성공으로 인정했다"
    return 1
  fi

  printf '%s\n' attributed > "$root/loop/attributed.txt"
  git -C "$root" add -- loop/attributed.txt
  git -C "$root" commit -qm "오류 수리 [loop-recovery:testtoken123]"
  if ! (
    LOOP_SOURCE_ONLY=1 LOOP_AGENT=claude \
      source "$root/loop/loop.sh" "$root"
    recovery_commit_matches "$before" "loop-recovery:testtoken123"
  ); then
    echo "FAIL: 고유 복구 표식이 있는 커밋을 복구 성공으로 인정하지 않았다"
    return 1
  fi
}

QUOTA_ROOT="$(new_root)"
CODE_ERROR_ROOT="$(new_root)"
PROVIDER_ERROR_ROOT="$(new_root)"
INFRA_ERROR_ROOT="$(new_root)"
PREFLIGHT_ROOT="$(new_root)"
MISSING_RECOVERY_ROOT="$(new_root)"
ATTRIBUTION_ROOT="$(new_root)"
trap 'rm -rf "$QUOTA_ROOT" "$CODE_ERROR_ROOT" "$PROVIDER_ERROR_ROOT" "$INFRA_ERROR_ROOT" "$PREFLIGHT_ROOT" "$MISSING_RECOVERY_ROOT" "$ATTRIBUTION_ROOT"' EXIT

run_quota_case "$QUOTA_ROOT"
run_code_error_case "$CODE_ERROR_ROOT"
run_provider_error_case "$PROVIDER_ERROR_ROOT" \
  "Your organization has disabled Claude subscription access"
run_provider_error_case "$INFRA_ERROR_ROOT" \
  "503 Service Unavailable"
run_preflight_error_case "$PREFLIGHT_ROOT"
run_missing_recovery_executable_case "$MISSING_RECOVERY_ROOT"
run_recovery_attribution_case "$ATTRIBUTION_ROOT"

echo "PASS: 사용량 한도는 같은 AI를 무료 대기하고 STOP 없이 재개한다"
echo "PASS: 코드 오류는 지문당 복구 AI 한 번 뒤 STOP 없이 재기동한다"
echo "PASS: 조직 접근 차단은 복구 AI 없이 무료 상태만 재확인한다"
echo "PASS: 503 공급자 장애도 복구 AI 없이 무료 상태만 재확인한다"
echo "PASS: 무료 조회의 로그인 오류는 복구 AI 없이 같은 개발 AI로 복귀한다"
echo "PASS: 복구 실행기가 없으면 오류 지문 선점권을 소비하지 않는다"
echo "PASS: 고유 표식 없는 동시 커밋은 복구 성공으로 오인하지 않는다"
