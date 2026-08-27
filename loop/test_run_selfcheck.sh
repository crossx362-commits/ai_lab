#!/bin/bash
# run_selfcheck 래퍼 자동검사 — mock Unity 로 에디터 없이 통과·차단을 실측한다.
# 사용법: bash loop/test_run_selfcheck.sh   (종료 코드 0 = 전부 통과)
set -u

WRAP="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/run_selfcheck.sh"
PASS=0
FAIL=0

expect() { # expect <기대코드> <설명> <명령...>
  local want="$1" desc="$2"; shift 2
  local got=0
  "$@" >/dev/null 2>&1 || got=$?
  if [ "$got" -eq "$want" ]; then
    echo "ok   - $desc"
    PASS=$((PASS + 1))
  else
    echo "FAIL - $desc (기대 $want, 실제 $got)"
    FAIL=$((FAIL + 1))
  fi
}

output_has() { # output_has <설명> <패턴> <명령...>
  local desc="$1" pat="$2"; shift 2
  local out
  out="$("$@" 2>&1 || true)"
  if printf '%s' "$out" | grep -E -q -- "$pat"; then
    echo "ok   - $desc"
    PASS=$((PASS + 1))
  else
    echo "FAIL - $desc (출력에 '$pat' 없음)"
    FAIL=$((FAIL + 1))
  fi
}

TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/run_selfcheck_test.XXXXXX")"
trap 'rm -rf "$TMPROOT"' EXIT

PROJ="$TMPROOT/unity_meas"
mkdir -p "$PROJ/ProjectSettings" "$TMPROOT/results" "$TMPROOT/bin"
printf '%s\n' "m_EditorVersion: 6000.3.14f1" > "$PROJ/ProjectSettings/ProjectVersion.txt"

# mock Unity — -logFile 에 FAKE_UNITY_MODE 에 따른 내용을 쓴다.
FAKE="$TMPROOT/bin/FakeUnity"
cat > "$FAKE" << 'MOCK'
#!/bin/bash
set -u
touched="${FAKE_UNITY_TOUCHED:-}"
[ -n "$touched" ] && date > "$touched"
logfile=""
while [ $# -gt 0 ]; do
  if [ "$1" = "-logFile" ]; then
    logfile="$2"
    shift 2
    continue
  fi
  shift
done
mode="${FAKE_UNITY_MODE:-pass}"
if [ "$mode" = "nolog" ]; then
  exit 0
fi
if [ -z "$logfile" ]; then
  echo "mock unity: -logFile missing" >&2
  exit 1
fi
mkdir -p "$(dirname "$logfile")"
case "$mode" in
  pass)
    printf '%s\n' "PASS MockSelfCheck" "  PASS  ok" > "$logfile"
    exit 0
    ;;
  fail)
    printf '%s\n' "FAIL MockSelfCheck" "  FAIL  boom" > "$logfile"
    exit 0
    ;;
  compile)
    printf '%s\n' "Assets/_Game/x.cs(1,1): error CS0103: The name 'X' does not exist" > "$logfile"
    exit 1
    ;;
  missing_method)
    printf '%s\n' "executeMethod class 'AshesToStars.NoSuch.Run' could not be found." > "$logfile"
    exit 1
    ;;
  crash)
    printf '%s\n' "Unity Editor crash" > "$logfile"
    exit 1
    ;;
  *)
    echo "unknown FAKE_UNITY_MODE=$mode" >&2
    exit 1
    ;;
esac
MOCK
chmod 755 "$FAKE"

# 1) 사용법
expect 2 "인수 없으면 사용법 오류(2)" bash "$WRAP"
output_has "사용법 문구를 찍는다" "사용법" bash "$WRAP"

# 2) dry-run 배치 인자
DRY_OUT="$(bash "$WRAP" AshesToStars.BuildSlotsSelfCheck.Run --unity "$FAKE" --project "$PROJ" --log "$TMPROOT/results/dry.log" --dry-run 2>&1 || true)"
if printf '%s' "$DRY_OUT" | grep -Eq -- '-batchmode' \
  && printf '%s' "$DRY_OUT" | grep -Eq -- '-quit' \
  && printf '%s' "$DRY_OUT" | grep -Eq -- '-nographics' \
  && printf '%s' "$DRY_OUT" | grep -Eq -- '-executeMethod' \
  && printf '%s' "$DRY_OUT" | grep -Eq -- '-projectPath' \
  && printf '%s' "$DRY_OUT" | grep -Eq -- '-logFile'; then
  echo "ok   - dry-run 이 batchmode/quit/nographics/executeMethod/projectPath/logFile 을 찍는다"
  PASS=$((PASS + 1))
else
  echo "FAIL - dry-run 배치 인자가 빠졌다"
  echo "$DRY_OUT" | sed 's/^/  /'
  FAIL=$((FAIL + 1))
fi
if printf '%s' "$DRY_OUT" | grep -Eq -- '-executeMethod AshesToStars.BuildSlotsSelfCheck.Run'; then
  echo "ok   - dry-run 에 메서드 이름이 들어간다"
  PASS=$((PASS + 1))
else
  echo "FAIL - dry-run 메서드 이름 누락"
  FAIL=$((FAIL + 1))
fi
if printf '%s' "$DRY_OUT" | grep -F -q -- "$PROJ"; then
  echo "ok   - dry-run 에 --project 경로가 들어간다"
  PASS=$((PASS + 1))
else
  echo "FAIL - dry-run 프로젝트 경로 누락"
  FAIL=$((FAIL + 1))
fi

# dry-run 은 mock 을 실행하지 않는다
TOUCH="$TMPROOT/touched"
rm -f "$TOUCH"
FAKE_UNITY_TOUCHED="$TOUCH" bash "$WRAP" AshesToStars.X.Run --unity "$FAKE" --project "$PROJ" --log "$TMPROOT/results/dry2.log" --dry-run >/dev/null 2>&1 || true
if [ -e "$TOUCH" ]; then
  echo "FAIL - dry-run 이 Unity 를 실행했다"
  FAIL=$((FAIL + 1))
else
  echo "ok   - dry-run 은 Unity 를 실행하지 않는다"
  PASS=$((PASS + 1))
fi

# 기본 project 는 unity_meas
DEF="$(bash "$WRAP" AshesToStars.X.Run --unity "$FAKE" --dry-run 2>&1 || true)"
if printf '%s' "$DEF" | grep -q 'projects/ashes-to-stars/unity_meas'; then
  echo "ok   - 기본 projectPath 는 unity_meas"
  PASS=$((PASS + 1))
else
  echo "FAIL - 기본 projectPath 가 unity_meas 가 아니다"
  echo "$DEF" | sed 's/^/  /'
  FAIL=$((FAIL + 1))
fi

# positional log
POS="$(bash "$WRAP" AshesToStars.X.Run "$TMPROOT/results/pos.log" --unity "$FAKE" --project "$PROJ" --dry-run 2>&1 || true)"
if printf '%s' "$POS" | grep -F -q -- "$TMPROOT/results/pos.log"; then
  echo "ok   - positional <method> <log> 를 받는다"
  PASS=$((PASS + 1))
else
  echo "FAIL - positional log 미반영"
  FAIL=$((FAIL + 1))
fi

# 3) Unity 부재
expect 1 "없는 Unity 바이너리면 실패(1)" \
  bash "$WRAP" AshesToStars.X.Run --unity "$TMPROOT/no-such-unity" --project "$PROJ" --log "$TMPROOT/results/missing.log"
output_has "Unity 부재 시 대화상자를 띄우지 않는다고 알린다" "권한 대화상자" \
  bash "$WRAP" AshesToStars.X.Run --unity "$TMPROOT/no-such-unity" --project "$PROJ" --log "$TMPROOT/results/missing.log"

# 4) mock 실행
expect 0 "mock PASS 로그면 통과(0)" \
  env UNITY_EDITOR_PATH="$FAKE" FAKE_UNITY_MODE=pass \
    bash "$WRAP" AshesToStars.BuildSlotsSelfCheck.Run --project "$PROJ" --log "$TMPROOT/results/pass.log"

expect 1 "로그 FAIL 줄이면 실패(1) — Unity exit 0 이어도" \
  env UNITY_EDITOR_PATH="$FAKE" FAKE_UNITY_MODE=fail \
    bash "$WRAP" AshesToStars.BuildSlotsSelfCheck.Run --project "$PROJ" --log "$TMPROOT/results/fail.log"

expect 1 "컴파일 error CS 면 실패(1)" \
  env UNITY_EDITOR_PATH="$FAKE" FAKE_UNITY_MODE=compile \
    bash "$WRAP" AshesToStars.BuildSlotsSelfCheck.Run --project "$PROJ" --log "$TMPROOT/results/cs.log"

expect 1 "로그가 안 생기면 실패(1)" \
  env UNITY_EDITOR_PATH="$FAKE" FAKE_UNITY_MODE=nolog \
    bash "$WRAP" AshesToStars.BuildSlotsSelfCheck.Run --project "$PROJ" --log "$TMPROOT/results/nolog.log"

expect 1 "executeMethod 클래스 부재면 실패(1)" \
  env UNITY_EDITOR_PATH="$FAKE" FAKE_UNITY_MODE=missing_method \
    bash "$WRAP" AshesToStars.NoSuch.Run --project "$PROJ" --log "$TMPROOT/results/nomethod.log"

expect 1 "Unity non-zero (crash) 면 실패(1)" \
  env UNITY_EDITOR_PATH="$FAKE" FAKE_UNITY_MODE=crash \
    bash "$WRAP" AshesToStars.X.Run --project "$PROJ" --log "$TMPROOT/results/crash.log"

expect 1 "프로젝트 경로가 없으면 실패(1)" \
  env UNITY_EDITOR_PATH="$FAKE" FAKE_UNITY_MODE=pass \
    bash "$WRAP" AshesToStars.X.Run --project "$TMPROOT/no-proj" --log "$TMPROOT/results/noproj.log"

# mock 이 실제로 -logFile 에 썼는지
if grep -q 'PASS MockSelfCheck' "$TMPROOT/results/pass.log" 2>/dev/null; then
  echo "ok   - mock 이 -logFile 경로에 PASS 를 썼다"
  PASS=$((PASS + 1))
else
  echo "FAIL - mock PASS 로그가 없다"
  FAIL=$((FAIL + 1))
fi

# 소스 계약: 배치 전용 플래그가 고정돼 있다
if grep -q -- '-batchmode' "$WRAP" && grep -q -- '-nographics' "$WRAP" && grep -q -- '-executeMethod' "$WRAP"; then
  echo "ok   - 래퍼가 batchmode/nographics/executeMethod 를 고정한다"
  PASS=$((PASS + 1))
else
  echo "FAIL - 래퍼에 배치 플래그가 없다"
  FAIL=$((FAIL + 1))
fi

echo "----------------------------------------"
echo "통과 ${PASS} · 실패 ${FAIL}"
[ "$FAIL" -eq 0 ]
