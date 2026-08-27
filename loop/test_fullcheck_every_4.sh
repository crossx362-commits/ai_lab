#!/bin/bash
# GameFullCheck 4바퀴 주기 — 회의 20260827-081437 채택 #3.
# mock Unity 로 에디터 없이 4번째 바퀴 호출·1–3 미호출·Unity 부재 스킵을 실측한다.
# 사용법: bash loop/test_fullcheck_every_4.sh   (종료 0 = 전부 통과)
set -u

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOOP="$HERE/loop.sh"
PASS=0
FAIL=0

ok()   { echo "ok   - $1"; PASS=$((PASS + 1)); }
fail() { echo "FAIL - $1"; FAIL=$((FAIL + 1)); }

expect_eq() { # expect_eq <기대> <실제> <설명>
  local want="$1" got="$2" desc="$3"
  if [ "$want" = "$got" ]; then ok "$desc"
  else fail "$desc (기대 '$want', 실제 '$got')"; fi
}

file_has() { # file_has <설명> <패턴> <파일>
  local desc="$1" pat="$2" f="$3"
  if [ -f "$f" ] && grep -E -q -- "$pat" "$f"; then ok "$desc"
  else fail "$desc (파일 $f 에 '$pat' 없음)"; fi
}

file_lacks() { # file_lacks <설명> <패턴> <파일>
  local desc="$1" pat="$2" f="$3"
  if [ -f "$f" ] && grep -E -q -- "$pat" "$f"; then fail "$desc (파일 $f 에 '$pat' 가 있다)"
  else ok "$desc"; fi
}

# --- 소스 계약 -------------------------------------------------------------
if grep -q -- 'AshesToStars.GameFullCheck.Run' "$LOOP"; then
  ok "loop.sh 가 AshesToStars.GameFullCheck.Run 을 부른다"
else
  fail "loop.sh 에 AshesToStars.GameFullCheck.Run 이 없다"
fi
if grep -q -- 'fullcheck_lap.count' "$LOOP"; then
  ok "랩 카운터를 loop/fullcheck_lap.count 에 영속한다"
else
  fail "loop.sh 에 fullcheck_lap.count 가 없다"
fi
if grep -q -- 'GameFullCheck 스킵 — Unity 없음' "$LOOP"; then
  ok "Unity 부재 스킵 문구가 loop.sh 에 있다"
else
  fail "Unity 부재 스킵 문구가 없다"
fi
# 카운터 write 가 STATUS.md 가 아님
if grep -A2 'fullcheck_lap_write' "$LOOP" | grep -q 'FULLCHECK_COUNT_FILE'; then
  ok "카운터 기록 대상은 STATUS.md 가 아니라 loop/ 파일"
else
  fail "fullcheck_lap_write 가 COUNT 파일을 안 쓴다"
fi
if grep -q -- 'LOOP_FULLCHECK_EVERY' "$HERE/env.sh"; then
  ok "env.sh 에 LOOP_FULLCHECK_EVERY 기본 4"
else
  fail "env.sh 에 LOOP_FULLCHECK_EVERY 가 없다"
fi

# --- 픽스처 ----------------------------------------------------------------
mk_harness() {
  # mk_harness → 전역 TEST_ROOT, FAKE 설정. 호출마다 새 임시 저장소.
  TEST_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/fullcheck_every_4.XXXXXX")"
  mkdir -p \
    "$TEST_ROOT/loop" \
    "$TEST_ROOT/docs/feedback" \
    "$TEST_ROOT/logs" \
    "$TEST_ROOT/bin" \
    "$TEST_ROOT/projects/ashes-to-stars/unity_meas/ProjectSettings" \
    "$TEST_ROOT/projects/ashes-to-stars/results"
  printf '%s\n' "m_EditorVersion: 6000.3.14f1" \
    > "$TEST_ROOT/projects/ashes-to-stars/unity_meas/ProjectSettings/ProjectVersion.txt"
  printf '%s\n' "# status" > "$TEST_ROOT/docs/STATUS.md"
  touch "$TEST_ROOT/docs/feedback/INBOX.md"
  cat > "$TEST_ROOT/loop/board.py" <<'PY'
#!/usr/bin/env python3
import json
print(json.dumps({"ok": True, "remain_pct": 100}))
PY
  chmod 755 "$TEST_ROOT/loop/board.py"

  git init -q "$TEST_ROOT"
  git -C "$TEST_ROOT" config user.email t@t
  git -C "$TEST_ROOT" config user.name t
  git -C "$TEST_ROOT" add docs/STATUS.md
  git -C "$TEST_ROOT" commit -qm init >/dev/null

  cat > "$TEST_ROOT/bin/codex" << 'FAKE_CODEX'
#!/bin/bash
# 매 바퀴 STATUS.md 를 바꿔 COUNT 가 오르게 한다. STOP 은 만들지 않는다.
n=0
[ -f "$TEST_ROOT/lap_n" ] && n="$(cat "$TEST_ROOT/lap_n")"
n=$((n + 1))
echo "$n" > "$TEST_ROOT/lap_n"
echo "fake lap $n" >> "$TEST_ROOT/docs/STATUS.md"
exit 0
FAKE_CODEX
  chmod 755 "$TEST_ROOT/bin/codex"

  FAKE="$TEST_ROOT/bin/FakeUnity"
  cat > "$FAKE" << MOCK
#!/bin/bash
set -u
echo invoked >> "$TEST_ROOT/unity_calls"
logfile=""
while [ \$# -gt 0 ]; do
  if [ "\$1" = "-executeMethod" ]; then
    echo "\$2" >> "$TEST_ROOT/unity_methods"
    shift 2
    continue
  fi
  if [ "\$1" = "-logFile" ]; then
    logfile="\$2"
    shift 2
    continue
  fi
  shift
done
if [ -z "\$logfile" ]; then
  echo "mock unity: -logFile missing" >&2
  exit 1
fi
mkdir -p "\$(dirname "\$logfile")"
printf '%s\n' "PASS MockGameFullCheck" "  PASS  ok" > "\$logfile"
exit 0
MOCK
  chmod 755 "$FAKE"
}

run_loop() { # run_loop <max> [unity_path]
  local max="$1"
  local unity="${2:-$FAKE}"
  rm -f "$TEST_ROOT/loop/STOP"
  env \
    TEST_ROOT="$TEST_ROOT" \
    LOOP_AGENT=codex \
    LOOP_CODEX_BIN="$TEST_ROOT/bin/codex" \
    LOOP_BOARD_PY="$TEST_ROOT/loop/board.py" \
    LOOP_MAX_LOOPS="$max" \
    LOOP_COOLDOWN=0 \
    LOOP_MAX_FAILS=8 \
    LOOP_AUTO_SWITCH=0 \
    LOOP_COUNCIL_EVERY=0 \
    LOOP_FULLCHECK_EVERY=4 \
    LOOP_REFRESH_TEST_REPORT=0 \
    UNITY_EDITOR_PATH="$unity" \
    bash "$LOOP" "$TEST_ROOT"
}

unity_call_count() {
  if [ -f "$TEST_ROOT/unity_calls" ]; then
    wc -l < "$TEST_ROOT/unity_calls" | tr -d ' '
  else
    echo 0
  fi
}

read_counter() {
  if [ -f "$TEST_ROOT/loop/fullcheck_lap.count" ]; then
    tr -d '[:space:]' < "$TEST_ROOT/loop/fullcheck_lap.count"
  else
    echo ""
  fi
}

# --- 1) 1–3바퀴는 Unity 를 부르지 않는다 ---------------------------------
mk_harness
set +e
run_loop 3 > "$TEST_ROOT/run3.log" 2>&1
RC3=$?
set -e
expect_eq 0 "$RC3" "3바퀴 루프가 정상 종료(0)한다"
expect_eq 0 "$(unity_call_count)" "1–3바퀴는 mock Unity 를 호출하지 않는다"
expect_eq 3 "$(read_counter)" "3바퀴 뒤 카운터가 3 이다"
file_lacks "3바퀴 로그에 GameFullCheck 전수 호출이 없다" "GameFullCheck 전수" "$TEST_ROOT/run3.log"
if [ -f "$TEST_ROOT/logs/loop_main.log" ]; then
  file_lacks "main 로그 1–3바퀴에 전수가 없다" "GameFullCheck 전수" "$TEST_ROOT/logs/loop_main.log"
else
  fail "3바퀴 main 로그가 없다"
fi

# --- 2) 4번째 바퀴(영속 카운터 이어받기)가 GameFullCheck 를 부른다 ----------
set +e
run_loop 1 >> "$TEST_ROOT/run4.log" 2>&1
RC4=$?
set -e
expect_eq 0 "$RC4" "4번째 바퀴가 정상 종료(0)한다"
expect_eq 1 "$(unity_call_count)" "4번째 바퀴가 mock Unity 를 1회 호출한다"
expect_eq 4 "$(read_counter)" "4바퀴 뒤 카운터가 4 이다"
if [ -f "$TEST_ROOT/unity_methods" ] && grep -qx 'AshesToStars.GameFullCheck.Run' "$TEST_ROOT/unity_methods"; then
  ok "4번째 바퀴 executeMethod 가 AshesToStars.GameFullCheck.Run 이다"
else
  fail "executeMethod 가 AshesToStars.GameFullCheck.Run 이 아니다"
  [ -f "$TEST_ROOT/unity_methods" ] && sed 's/^/  /' "$TEST_ROOT/unity_methods"
fi
MAIN4="$TEST_ROOT/logs/loop_main.log"
file_has "4번째 바퀴 로그에 전수 호출이 있다" "GameFullCheck 전수" "$MAIN4"
file_has "4번째 바퀴가 GameFullCheck PASS 를 남긴다" "GameFullCheck PASS" "$MAIN4"

# 랩 로그에도 남는지 (4번째 랩)
LAP4="$(ls -1t "$TEST_ROOT/logs"/*/lap-*-4.log 2>/dev/null | head -1 || true)"
if [ -z "$LAP4" ]; then
  # 초 단위 충돌 대비: 전수 문구가 있는 랩 로그
  LAP4="$(grep -l 'GameFullCheck 전수' "$TEST_ROOT/logs"/*/lap-*.log 2>/dev/null | head -1 || true)"
fi
if [ -n "$LAP4" ]; then
  file_has "랩 로그에 GameFullCheck 전수가 있다" "GameFullCheck 전수" "$LAP4"
else
  fail "4번째 랩 로그를 못 찾았다"
fi

HARNESS_KEEP="$TEST_ROOT"

# --- 3) Unity 부재면 비치명 스킵 + 사유 -----------------------------------
mk_harness
printf '%s\n' "3" > "$TEST_ROOT/loop/fullcheck_lap.count"
MISSING="$TEST_ROOT/no-such-unity"
set +e
run_loop 1 "$MISSING" > "$TEST_ROOT/run_skip.log" 2>&1
RCS=$?
set -e
expect_eq 0 "$RCS" "Unity 없어도 루프는 비치명으로 정상 종료(0)"
expect_eq 0 "$(unity_call_count)" "없는 Unity 바이너리를 실행하지 않는다"
expect_eq 4 "$(read_counter)" "스킵해도 카운터는 4로 진행한다"
file_has "스킵 사유 'Unity 없음' 이 출력에 있다" "GameFullCheck 스킵 — Unity 없음" "$TEST_ROOT/run_skip.log"
file_has "run_selfcheck 부재 사유가 로그에 남는다" "Unity 가 없다" "$TEST_ROOT/run_skip.log"
MAIN_S="$TEST_ROOT/logs/loop_main.log"
file_has "main 로그에 스킵 사유가 있다" "GameFullCheck 스킵 — Unity 없음" "$MAIN_S"
LAP_S="$(grep -l 'GameFullCheck 스킵' "$TEST_ROOT/logs"/*/lap-*.log 2>/dev/null | head -1 || true)"
if [ -n "$LAP_S" ]; then
  file_has "랩 로그에 Unity 스킵 사유가 있다" "GameFullCheck 스킵 — Unity 없음" "$LAP_S"
else
  fail "스킵 사유가 들어간 랩 로그가 없다"
fi

# --- 4) 한 프로세스 4바퀴 — 호출은 4번째만 --------------------------------
mk_harness
set +e
run_loop 4 > "$TEST_ROOT/run_all4.log" 2>&1
RC14=$?
set -e
expect_eq 0 "$RC14" "한 프로세스 4바퀴가 정상 종료한다"
expect_eq 1 "$(unity_call_count)" "한 프로세스 4바퀴에서 Unity 호출은 1회"
expect_eq 4 "$(read_counter)" "한 프로세스 4바퀴 뒤 카운터는 4"
file_has "한 프로세스 4바퀴 로그에 전수가 1회 있다" "GameFullCheck 전수" "$TEST_ROOT/logs/loop_main.log"
# 전수 문구 횟수
N_FC="$(grep -c 'GameFullCheck 전수' "$TEST_ROOT/logs/loop_main.log" || true)"
expect_eq 1 "$N_FC" "전수 호출 로그가 정확히 1줄"

# 정리 (마지막 픽스처 + 이어받기 픽스처)
rm -rf "$TEST_ROOT" "$HARNESS_KEEP" 2>/dev/null || true

echo "----------------------------------------"
echo "통과 ${PASS} · 실패 ${FAIL}"
[ "$FAIL" -eq 0 ]
