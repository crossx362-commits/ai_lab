#!/bin/bash
# last_test_report.json HEAD 재실행 — 회의 20260827-073515 채택 #2.
# mock Unity 로 에디터 없이 dry-run·스킵·통과·실패·이미현재·훅을 실측한다.
# 사용법: bash loop/test_refresh_test_report.sh   (종료 0 = 전부 통과)
set -u

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WRAP="$HERE/refresh_test_report.sh"
LOOP="$HERE/loop.sh"
BOARD="$HERE/board.py"
PASS=0
FAIL=0

ok()   { echo "ok   - $1"; PASS=$((PASS + 1)); }
fail() { echo "FAIL - $1"; FAIL=$((FAIL + 1)); }

expect() { # expect <기대코드> <설명> <명령...>
  local want="$1" desc="$2"; shift 2
  local got=0
  "$@" >/dev/null 2>&1 || got=$?
  if [ "$got" -eq "$want" ]; then ok "$desc"
  else fail "$desc (기대 $want, 실제 $got)"; fi
}

output_has() { # output_has <설명> <패턴> <명령...>
  local desc="$1" pat="$2"; shift 2
  local out
  out="$("$@" 2>&1 || true)"
  if printf '%s' "$out" | grep -E -q -- "$pat"; then ok "$desc"
  else fail "$desc (출력에 '$pat' 없음)"; fi
}

json_field() { # json_field <파일> <키>
  python3 -c '
import json, sys
p, k = sys.argv[1], sys.argv[2]
with open(p, encoding="utf-8-sig") as f:
    d = json.load(f)
v = d.get(k)
if isinstance(v, bool):
    print("true" if v else "false")
elif v is None:
    print("")
else:
    print(v)
' "$1" "$2"
}

no_bom() { # no_bom <파일> <설명>
  python3 -c '
import sys
p = sys.argv[1]
b = open(p, "rb").read()
sys.exit(0 if not b.startswith(b"\xef\xbb\xbf") else 1)
' "$1"
  if [ $? -eq 0 ]; then ok "$2"
  else fail "$2"; fi
}

# --- 소스 계약 -------------------------------------------------------------
if grep -q -- 'AshesToStars.GameSweepSelfCheck.Run' "$WRAP"; then
  ok "refresh 가 GameSweepSelfCheck.Run 을 부른다"
else
  fail "refresh 에 GameSweepSelfCheck.Run 이 없다"
fi
if grep -q -- 'AshesToStars.GameFullCheck.Run' "$WRAP"; then
  fail "refresh 가 GameFullCheck 를 부르면 안 된다"
else
  ok "refresh 는 GameFullCheck 를 부르지 않는다"
fi
if grep -q -- 'maybe_refresh_test_report' "$LOOP"; then
  ok "loop.sh 에 maybe_refresh_test_report 훅이 있다"
else
  fail "loop.sh 에 maybe_refresh_test_report 가 없다"
fi
if grep -A2 'maybe_run_game_fullcheck "\$FC_LAP"' "$LOOP" | grep -q 'maybe_refresh_test_report'; then
  ok "성공 바퀴에서 GameFullCheck 훅 다음에 refresh 를 부른다"
else
  fail "성공 바퀴 refresh 호출 위치가 없다"
fi
if grep -q -- 'LOOP_SOURCE_ONLY' "$LOOP"; then
  ok "loop.sh 가 LOOP_SOURCE_ONLY 로 함수만 노출한다"
else
  fail "loop.sh 에 LOOP_SOURCE_ONLY 가 없다"
fi

REAL_HEAD="$(git -C "$HERE/.." rev-parse HEAD)"
if [ -z "$REAL_HEAD" ]; then
  fail "git HEAD 를 못 읽었다"
  echo "----------------------------------------"
  echo "통과 ${PASS} · 실패 ${FAIL}"
  exit 1
fi

TMPROOT="$(mktemp -d "${TMPDIR:-/tmp}/refresh_test_report.XXXXXX")"
trap 'rm -rf "$TMPROOT"' EXIT

PROJ="$TMPROOT/unity_meas"
mkdir -p "$PROJ/ProjectSettings" "$TMPROOT/results" "$TMPROOT/bin" "$TMPROOT/loop"
printf '%s\n' "m_EditorVersion: 6000.3.14f1" > "$PROJ/ProjectSettings/ProjectVersion.txt"

FAKE="$TMPROOT/bin/FakeUnity"
cat > "$FAKE" << 'MOCK'
#!/bin/bash
set -u
touched="${FAKE_UNITY_TOUCHED:-}"
[ -n "$touched" ] && echo invoked >> "$touched"
logfile=""
while [ $# -gt 0 ]; do
  if [ "$1" = "-logFile" ]; then
    logfile="$2"
    shift 2
    continue
  fi
  if [ "$1" = "-executeMethod" ]; then
    echo "$2" >> "${FAKE_UNITY_METHODS:-/dev/null}"
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
    printf '%s\n' "PASS MockGameSweep" "  PASS  ok" > "$logfile"
    if [ -n "${FAKE_UNITY_REPORT:-}" ]; then
      mkdir -p "$(dirname "$FAKE_UNITY_REPORT")"
      printf '%s\n' '{"at":"2026-08-27 00:00","ok":true,"summary":"mock sweep PASS","items":[{"name":"mock","ok":true,"note":"통과"}]}' > "$FAKE_UNITY_REPORT"
    fi
    exit 0
    ;;
  fail)
    printf '%s\n' "FAIL MockGameSweep" "  FAIL  boom" > "$logfile"
    if [ -n "${FAKE_UNITY_REPORT:-}" ]; then
      mkdir -p "$(dirname "$FAKE_UNITY_REPORT")"
      printf '%s\n' '{"at":"2026-08-27 00:00","ok":false,"summary":"mock sweep FAIL","items":[{"name":"mock","ok":false,"note":"boom"}]}' > "$FAKE_UNITY_REPORT"
    fi
    exit 0
    ;;
  *)
    echo "unknown FAKE_UNITY_MODE=$mode" >&2
    exit 1
    ;;
esac
MOCK
chmod 755 "$FAKE"

run_wrap() {
  # run_wrap [extra args...]  — UNITY_EDITOR_PATH 를 FakeUnity 로 덮어 실 Unity 차단
  env UNITY_EDITOR_PATH="$FAKE" \
    bash "$WRAP" \
      --project "$PROJ" \
      --log "$TMPROOT/results/unity.log" \
      --unity "$FAKE" \
      "$@"
}

# --- 1) dry-run: HEAD 출력, 리포트 미작성 --------------------------------
REPORT_DRY="$TMPROOT/dry.json"
rm -f "$REPORT_DRY"
DRY_OUT="$(run_wrap --report "$REPORT_DRY" --dry-run 2>&1 || true)"
if printf '%s' "$DRY_OUT" | grep -F -q -- "$REAL_HEAD"; then
  ok "dry-run 이 현재 HEAD 를 찍는다"
else
  fail "dry-run 에 HEAD 가 없다"
  printf '%s\n' "$DRY_OUT" | sed 's/^/  /'
fi
if printf '%s' "$DRY_OUT" | grep -F -q -- "$REPORT_DRY"; then
  ok "dry-run 이 리포트 경로를 찍는다"
else
  fail "dry-run 에 리포트 경로가 없다"
fi
if printf '%s' "$DRY_OUT" | grep -q -- 'run_selfcheck.sh' \
  && printf '%s' "$DRY_OUT" | grep -q -- 'GameSweepSelfCheck.Run'; then
  ok "dry-run 이 래퍼 명령을 찍는다"
else
  fail "dry-run 에 래퍼 명령이 없다"
fi
if [ -e "$REPORT_DRY" ]; then
  fail "dry-run 이 리포트를 썼다"
else
  ok "dry-run 은 리포트를 쓰지 않는다"
fi
TOUCH="$TMPROOT/touched"
rm -f "$TOUCH"
FAKE_UNITY_TOUCHED="$TOUCH" run_wrap --report "$REPORT_DRY" --dry-run >/dev/null 2>&1 || true
if [ -e "$TOUCH" ]; then
  fail "dry-run 이 Unity 를 실행했다"
else
  ok "dry-run 은 Unity 를 실행하지 않는다"
fi

# --- 2) Unity 부재 → exit 0, skip JSON -----------------------------------
REPORT_SKIP="$TMPROOT/skip.json"
MISSING="$TMPROOT/no-such-unity"
rm -f "$REPORT_SKIP"
expect 0 "없는 Unity 면 스킵 종료(0)" \
  env UNITY_EDITOR_PATH="" \
    bash "$WRAP" --report "$REPORT_SKIP" --project "$PROJ" \
      --log "$TMPROOT/results/missing.log" --unity "$MISSING"
if [ -f "$REPORT_SKIP" ]; then
  ok "스킵 시 JSON 을 쓴다"
  got_head="$(json_field "$REPORT_SKIP" head)"
  if [ "$got_head" = "$REAL_HEAD" ]; then ok "스킵 JSON head 가 현재 HEAD"
  else fail "스킵 JSON head 불일치 (got $got_head)"; fi
  if [ "$(json_field "$REPORT_SKIP" ok)" = "true" ]; then ok "스킵 JSON ok 는 true"
  else fail "스킵 JSON ok 가 true 가 아니다"; fi
  sum="$(json_field "$REPORT_SKIP" summary)"
  if printf '%s' "$sum" | grep -Eq '스킵|Unity'; then ok "스킵 summary 에 사유가 있다"
  else fail "스킵 summary 에 사유 없음 ($sum)"; fi
  note="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1],encoding="utf-8-sig"))["items"][0]["note"])' "$REPORT_SKIP")"
  if printf '%s' "$note" | grep -Eq '스킵|Unity'; then ok "스킵 item note 에 사유가 있다"
  else fail "스킵 note 에 사유 없음 ($note)"; fi
  no_bom "$REPORT_SKIP" "스킵 JSON 에 BOM 이 없다"
else
  fail "스킵 JSON 이 없다"
fi

# --- 3) mock Unity pass + 리포트 주입 ------------------------------------
REPORT_PASS="$TMPROOT/pass.json"
rm -f "$REPORT_PASS" "$TOUCH"
expect 0 "mock PASS 면 종료(0)" \
  env UNITY_EDITOR_PATH="$FAKE" FAKE_UNITY_MODE=pass \
      FAKE_UNITY_REPORT="$REPORT_PASS" FAKE_UNITY_TOUCHED="$TOUCH" \
    bash "$WRAP" --report "$REPORT_PASS" --project "$PROJ" \
      --log "$TMPROOT/results/pass.log" --unity "$FAKE"
if [ -f "$REPORT_PASS" ]; then
  if [ "$(json_field "$REPORT_PASS" head)" = "$REAL_HEAD" ]; then
    ok "mock PASS JSON head 가 현재 HEAD"
  else
    fail "mock PASS head 불일치"
  fi
  if [ "$(json_field "$REPORT_PASS" summary)" = "mock sweep PASS" ]; then
    ok "FakeUnity 가 쓴 summary 를 보존하고 head 만 주입한다"
  else
    fail "PASS summary 가 덮였다 ($(json_field "$REPORT_PASS" summary))"
  fi
  if [ "$(json_field "$REPORT_PASS" ok)" = "true" ]; then ok "mock PASS ok true"
  else fail "mock PASS ok 가 true 가 아니다"; fi
  no_bom "$REPORT_PASS" "PASS JSON 에 BOM 이 없다"
else
  fail "PASS JSON 이 없다"
fi
if [ -f "$TOUCH" ]; then ok "mock PASS 가 Unity 를 1회 호출했다"
else fail "mock PASS 가 Unity 를 호출하지 않았다"; fi

# --- 4) mock Unity fail → ok false, head, nonzero ------------------------
REPORT_FAIL="$TMPROOT/fail.json"
rm -f "$REPORT_FAIL"
expect 1 "mock FAIL 면 종료(1)" \
  env UNITY_EDITOR_PATH="$FAKE" FAKE_UNITY_MODE=fail \
      FAKE_UNITY_REPORT="$REPORT_FAIL" \
    bash "$WRAP" --report "$REPORT_FAIL" --project "$PROJ" \
      --log "$TMPROOT/results/fail.log" --unity "$FAKE"
if [ -f "$REPORT_FAIL" ]; then
  if [ "$(json_field "$REPORT_FAIL" head)" = "$REAL_HEAD" ]; then
    ok "mock FAIL JSON head 가 현재 HEAD"
  else
    fail "mock FAIL head 불일치"
  fi
  if [ "$(json_field "$REPORT_FAIL" ok)" = "false" ]; then ok "mock FAIL ok false"
  else fail "mock FAIL ok 가 false 가 아니다"; fi
  no_bom "$REPORT_FAIL" "FAIL JSON 에 BOM 이 없다"
else
  fail "FAIL JSON 이 없다"
fi

# fail stub (Unity 가 JSON 을 안 씀)
REPORT_FAIL2="$TMPROOT/fail2.json"
rm -f "$REPORT_FAIL2"
expect 1 "mock FAIL 이고 JSON 없으면 스텁(1)" \
  env UNITY_EDITOR_PATH="$FAKE" FAKE_UNITY_MODE=fail \
    bash "$WRAP" --report "$REPORT_FAIL2" --project "$PROJ" \
      --log "$TMPROOT/results/fail2.log" --unity "$FAKE"
if [ -f "$REPORT_FAIL2" ] \
  && [ "$(json_field "$REPORT_FAIL2" ok)" = "false" ] \
  && [ "$(json_field "$REPORT_FAIL2" head)" = "$REAL_HEAD" ]; then
  ok "FAIL 스텁에 ok false 와 head 가 있다"
else
  fail "FAIL 스텁이 잘못됐다"
fi

# --- 5) 이미 현재 head → Unity 재호출 없음 --------------------------------
REPORT_CUR="$TMPROOT/current.json"
python3 -c '
import json, sys
p, h = sys.argv[1], sys.argv[2]
json.dump({"at":"2026-08-27 00:01","ok":True,"summary":"already","head":h,
           "items":[{"name":"keep","ok":True,"note":"그대로"}]},
          open(p,"w",encoding="utf-8"), ensure_ascii=False)
' "$REPORT_CUR" "$REAL_HEAD"
rm -f "$TOUCH"
expect 0 "이미 현재 head 면 종료(0)" \
  env UNITY_EDITOR_PATH="$FAKE" FAKE_UNITY_MODE=pass \
      FAKE_UNITY_TOUCHED="$TOUCH" \
    bash "$WRAP" --report "$REPORT_CUR" --project "$PROJ" \
      --log "$TMPROOT/results/cur.log" --unity "$FAKE"
if [ -e "$TOUCH" ]; then
  fail "이미 현재 head 인데 Unity 를 다시 불렀다"
else
  ok "이미 현재 head 면 Unity 를 부르지 않는다"
fi
if [ "$(json_field "$REPORT_CUR" summary)" = "already" ]; then
  ok "이미 현재 head 면 JSON 을 다시 쓰지 않는다"
else
  fail "이미 현재 head 인데 JSON 이 바뀌었다"
fi

# --force 는 다시 부른다
rm -f "$TOUCH"
expect 0 "--force 면 현재 head 여도 재실행(0)" \
  env UNITY_EDITOR_PATH="$FAKE" FAKE_UNITY_MODE=pass \
      FAKE_UNITY_TOUCHED="$TOUCH" FAKE_UNITY_REPORT="$REPORT_CUR" \
    bash "$WRAP" --report "$REPORT_CUR" --project "$PROJ" \
      --log "$TMPROOT/results/force.log" --unity "$FAKE" --force
if [ -e "$TOUCH" ]; then ok "--force 가 Unity 를 다시 부른다"
else fail "--force 가 Unity 를 안 불렀다"; fi

# --- 6) 낡은 JSON (잘못된/없는 head) → 재작성 ------------------------------
REPORT_STALE="$TMPROOT/stale.json"
printf '%s\n' '{"at":"2026-08-01 00:00","ok":false,"summary":"stale FAIL","items":[{"name":"EstateBuildings","ok":false,"note":"old"}]}' > "$REPORT_STALE"
expect 0 "head 없는 낡은 JSON 은 재작성(0)" \
  env UNITY_EDITOR_PATH="$FAKE" FAKE_UNITY_MODE=pass \
      FAKE_UNITY_REPORT="$REPORT_STALE" \
    bash "$WRAP" --report "$REPORT_STALE" --project "$PROJ" \
      --log "$TMPROOT/results/stale.log" --unity "$FAKE"
if [ "$(json_field "$REPORT_STALE" head)" = "$REAL_HEAD" ]; then
  ok "낡은 JSON 에 현재 HEAD 가 주입된다"
else
  fail "낡은 JSON head 미주입"
fi

REPORT_WRONG="$TMPROOT/wrong.json"
python3 -c '
import json, sys
json.dump({"at":"x","ok":True,"summary":"wrong head","head":"deadbeef",
           "items":[{"name":"x","ok":True,"note":"n"}]},
          open(sys.argv[1],"w",encoding="utf-8"))
' "$REPORT_WRONG"
expect 0 "다른 head 인 JSON 은 재작성(0)" \
  env UNITY_EDITOR_PATH="$FAKE" FAKE_UNITY_MODE=pass \
      FAKE_UNITY_REPORT="$REPORT_WRONG" \
    bash "$WRAP" --report "$REPORT_WRONG" --project "$PROJ" \
      --log "$TMPROOT/results/wrong.log" --unity "$FAKE"
if [ "$(json_field "$REPORT_WRONG" head)" = "$REAL_HEAD" ]; then
  ok "잘못된 head 가 현재 HEAD 로 바뀐다"
else
  fail "잘못된 head 가 안 바뀌었다 ($(json_field "$REPORT_WRONG" head))"
fi

# --- 7) board.py load_test_report 가 스킵 JSON 을 읽는다 -------------------
BOARD_RC="$(python3 -c '
import json, sys
sys.path.insert(0, sys.argv[1])
import board
board.TEST_REPORT_PATH = __import__("pathlib").Path(sys.argv[2])
d = board.load_test_report()
ok = (
    isinstance(d, dict)
    and "at" in d and "ok" in d and "summary" in d and "items" in d
    and d.get("ok") is True
    and isinstance(d.get("items"), list) and d["items"]
    and d["items"][0].get("name")
)
print("ok" if ok else "bad:"+json.dumps(d, ensure_ascii=False)[:200])
' "$HERE" "$REPORT_SKIP" 2>/dev/null || echo import-fail)"
if [ "$BOARD_RC" = "ok" ]; then
  ok "board.py load_test_report 가 스킵 JSON 을 파싱한다"
else
  fail "board.py load_test_report 파싱 실패 ($BOARD_RC)"
fi

# --- 8) loop.sh 훅: 불일치면 refresh, 일치면 Unity 안 부름 ---------------
HARNESS="$(mktemp -d "${TMPDIR:-/tmp}/refresh_hook.XXXXXX")"
mkdir -p "$HARNESS/loop" \
  "$HARNESS/projects/ashes-to-stars/unity_meas/ProjectSettings" \
  "$HARNESS/projects/ashes-to-stars/results" \
  "$HARNESS/logs" \
  "$HARNESS/docs"
printf '%s\n' "m_EditorVersion: 6000.3.14f1" \
  > "$HARNESS/projects/ashes-to-stars/unity_meas/ProjectSettings/ProjectVersion.txt"
cp "$WRAP" "$HARNESS/loop/refresh_test_report.sh"
cp "$HERE/run_selfcheck.sh" "$HARNESS/loop/run_selfcheck.sh"
chmod 755 "$HARNESS/loop/refresh_test_report.sh" "$HARNESS/loop/run_selfcheck.sh"
git init -q "$HARNESS"
git -C "$HARNESS" config user.email t@t
git -C "$HARNESS" config user.name t
printf '%s\n' "# s" > "$HARNESS/docs/STATUS.md"
git -C "$HARNESS" add docs/STATUS.md
git -C "$HARNESS" commit -qm init >/dev/null
HARNESS_HEAD="$(git -C "$HARNESS" rev-parse HEAD)"

HOOK_FAKE="$HARNESS/FakeUnity"
cp "$FAKE" "$HOOK_FAKE"
chmod 755 "$HOOK_FAKE"
HOOK_TOUCH="$HARNESS/unity_calls"
HOOK_REPORT="$HARNESS/loop/last_test_report.json"

# source loop.sh (real) then retarget DEPLOY_ROOT to harness
hook_call() {
  local extra_env="${1:-}"
  # shellcheck disable=SC1090
  (
    set -- "$HARNESS"
    LOOP_SOURCE_ONLY=1
    # shellcheck source=/dev/null
    source "$LOOP"
    DEPLOY_ROOT="$HARNESS/loop"
    TARGET_REPO="$HARNESS"
    MAIN_LOG="$HARNESS/logs/loop_main.log"
    LAP_LOG="$HARNESS/logs/lap.log"
    mkdir -p "$HARNESS/logs"
    eval "$extra_env"
    maybe_refresh_test_report
  )
}

# 8a) 리포트 없음 → Unity 호출
rm -f "$HOOK_TOUCH" "$HOOK_REPORT"
set +e
hook_call 'export UNITY_EDITOR_PATH="$HOOK_FAKE"; export FAKE_UNITY_TOUCHED="$HOOK_TOUCH"; export FAKE_UNITY_MODE=pass; export FAKE_UNITY_REPORT="$HOOK_REPORT"' \
  > "$HARNESS/hook1.log" 2>&1
HOOK1=$?
set -e
if [ "$HOOK1" -eq 0 ]; then ok "훅: 리포트 없을 때 종료 0"
else fail "훅: 리포트 없을 때 종료 $HOOK1"; fi
if [ -f "$HOOK_TOUCH" ]; then ok "훅: head 불일치(없음)면 Unity 를 부른다"
else fail "훅: 리포트 없는데 Unity 를 안 불렀다"; fi
if [ -f "$HOOK_REPORT" ] && [ -n "$(json_field "$HOOK_REPORT" head)" ]; then
  ok "훅: 불일치 시 JSON 을 다시 쓴다"
else
  fail "훅: 불일치 시 JSON 미작성"
fi
if grep -q 'GameFullCheck' "$HARNESS/hook1.log" 2>/dev/null; then
  fail "훅 로그에 GameFullCheck 가 있다"
else
  ok "훅은 GameFullCheck 를 부르지 않는다"
fi

# 8b) 현재 head 일치 → Unity 재호출 없음
python3 -c '
import json, sys
p, h = sys.argv[1], sys.argv[2]
json.dump({"at":"2026-08-27 00:02","ok":True,"summary":"hook current","head":h,
           "items":[{"name":"keep","ok":True,"note":"n"}]},
          open(p,"w",encoding="utf-8"), ensure_ascii=False)
' "$HOOK_REPORT" "$HARNESS_HEAD"
rm -f "$HOOK_TOUCH"
set +e
hook_call 'export UNITY_EDITOR_PATH="$HOOK_FAKE"; export FAKE_UNITY_TOUCHED="$HOOK_TOUCH"; export FAKE_UNITY_MODE=pass' \
  > "$HARNESS/hook2.log" 2>&1
HOOK2=$?
set -e
if [ "$HOOK2" -eq 0 ]; then ok "훅: head 일치 때 종료 0"
else fail "훅: head 일치 때 종료 $HOOK2"; fi
if [ -e "$HOOK_TOUCH" ]; then
  fail "훅: head 일치인데 Unity 를 다시 불렀다"
else
  ok "훅: head 일치면 Unity 를 부르지 않는다"
fi
if [ "$(json_field "$HOOK_REPORT" summary)" = "hook current" ]; then
  ok "훅: head 일치면 JSON 을 유지한다"
else
  fail "훅: head 일치인데 JSON 이 바뀌었다"
fi

# 8c) 훅 실패도 루프를 죽이지 않는다 (return 0)
python3 -c '
import json, sys
json.dump({"at":"x","ok":True,"summary":"stale","head":"deadbeef",
           "items":[{"name":"x","ok":True,"note":"n"}]},
          open(sys.argv[1],"w",encoding="utf-8"))
' "$HOOK_REPORT"
set +e
hook_call 'export UNITY_EDITOR_PATH="$HOOK_FAKE"; export FAKE_UNITY_MODE=fail; export FAKE_UNITY_REPORT="$HOOK_REPORT"' \
  > "$HARNESS/hook3.log" 2>&1
HOOK3=$?
set -e
if [ "$HOOK3" -eq 0 ]; then ok "훅: GameSweep FAIL 이어도 함수는 0 (루프 계속)"
else fail "훅: FAIL 인데 함수가 $HOOK3 로 죽었다"; fi
if grep -q '루프는 계속' "$HARNESS/hook3.log"; then
  ok "훅: FAIL 시 루프는 계속 이라고 남긴다"
else
  fail "훅: FAIL 계속 문구가 없다"
fi

rm -rf "$HARNESS" 2>/dev/null || true

echo "----------------------------------------"
echo "통과 ${PASS} · 실패 ${FAIL}"
[ "$FAIL" -eq 0 ]
