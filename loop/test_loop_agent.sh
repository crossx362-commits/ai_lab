#!/bin/bash

set -euo pipefail

TEST_AREA="$(mktemp -d /tmp/ai-loop-shell-test.XXXXXX)"
trap 'rm -rf "$TEST_AREA"' EXIT

DEPLOY_DIR="$TEST_AREA/deploy with space"
TARGET_REPO="$TEST_AREA/target repo"
mkdir -p "$DEPLOY_DIR" "$TARGET_REPO/loop"
cp "$(dirname "$0")/loop.sh" "$DEPLOY_DIR/loop.sh"
cp "$(dirname "$0")/env.sh" "$DEPLOY_DIR/env.sh"
cp "$(dirname "$0")/PROMPT.md" "$DEPLOY_DIR/PROMPT.md"

cat > "$DEPLOY_DIR/agent_runner.py" <<'PY'
from __future__ import annotations

import os
from pathlib import Path
import sys

record = Path(os.environ["FAKE_RECORD"])
with record.open("a", encoding="utf-8") as handle:
    handle.write(f"pid={os.getpid()} args={sys.argv[1:]!r}\n")
print("fake lap complete")
raise SystemExit(int(os.environ.get("FAKE_EXIT", "0")))
PY

chmod +x "$DEPLOY_DIR/loop.sh"

FAKE_RECORD="$TEST_AREA/calls.log" \
LOOP_MAX_LOOPS=2 \
LOOP_COOLDOWN=0 \
LOOP_PYTHON="$(command -v python3)" \
bash "$DEPLOY_DIR/loop.sh" "$TARGET_REPO" > "$TEST_AREA/two-laps.log" 2>&1

if [ "$(wc -l < "$TEST_AREA/calls.log" | tr -d ' ')" -ne 2 ]; then
  echo "FAIL: 두 바퀴가 각각 새 coordinator 프로세스를 호출하지 않았다"
  exit 1
fi

if [ "$(sed -E 's/^pid=([0-9]+).*/\1/' "$TEST_AREA/calls.log" | sort -u | wc -l | tr -d ' ')" -ne 2 ]; then
  echo "FAIL: 바퀴 간 coordinator PID가 재사용됐다"
  exit 1
fi

if ! grep -Fq -- "--repo-root', '$TARGET_REPO'" "$TEST_AREA/calls.log"; then
  echo "FAIL: 배포 경로와 대상 저장소가 분리되지 않았다"
  cat "$TEST_AREA/calls.log"
  exit 1
fi

if ! grep -Fq -- "--prompt-file', '$DEPLOY_DIR/PROMPT.md'" "$TEST_AREA/calls.log"; then
  echo "FAIL: 배포본 PROMPT.md가 coordinator에 전달되지 않았다"
  exit 1
fi

if [ "$(find "$TARGET_REPO/logs" -type f -name 'lap-*.log' | wc -l | tr -d ' ')" -ne 2 ]; then
  echo "FAIL: 날짜별 두 바퀴 로그가 생성되지 않았다"
  find "$TARGET_REPO" -maxdepth 4 -type f -print
  exit 1
fi

: > "$TEST_AREA/calls.log"
touch "$TARGET_REPO/loop/STOP"
FAKE_RECORD="$TEST_AREA/calls.log" \
LOOP_MAX_LOOPS=1 \
LOOP_COOLDOWN=0 \
LOOP_PYTHON="$(command -v python3)" \
bash "$DEPLOY_DIR/loop.sh" "$TARGET_REPO" > "$TEST_AREA/stopped.log" 2>&1

if [ -s "$TEST_AREA/calls.log" ]; then
  echo "FAIL: 시작 전 STOP인데 coordinator를 호출했다"
  exit 1
fi

rm "$TARGET_REPO/loop/STOP"
set +e
FAKE_RECORD="$TEST_AREA/calls.log" \
FAKE_EXIT=7 \
LOOP_MAX_LOOPS=1 \
LOOP_COOLDOWN=0 \
LOOP_PYTHON="$(command -v python3)" \
bash "$DEPLOY_DIR/loop.sh" "$TARGET_REPO" > "$TEST_AREA/fatal.log" 2>&1
FATAL_RC=$?
set -e

if [ "$FATAL_RC" -ne 7 ]; then
  echo "FAIL: coordinator 치명 오류가 정상 종료로 숨겨졌다 (rc=$FATAL_RC)"
  exit 1
fi

echo "PASS: 새 프로세스 2바퀴, 경로 분리, 날짜 로그, STOP, fatal 전달"
