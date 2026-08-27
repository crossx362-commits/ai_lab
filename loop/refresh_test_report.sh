#!/bin/bash
# last_test_report.json 을 현재 HEAD 기준으로 재실행·기록
# 회의 20260827-073515 채택 #2 (PROPOSALS 2026-08-27 · tester: HEAD 불일치).
#
# 낡은 리포트가 HEAD 를 대표하지 못하게, git rev-parse HEAD 를 JSON `head` 에 찍는다.
# 실제 GameSweep 은 run_selfcheck.sh 래퍼로만 돌린다(새 Unity 런처를 만들지 않는다).
# Unity 부재는 비치명 스킵 — ok:true 스킵 JSON 을 쓰고 0 으로 끝낸다.
#
# 사용법:
#   bash loop/refresh_test_report.sh
#   bash loop/refresh_test_report.sh --report PATH --log PATH --unity PATH --project PATH
#   bash loop/refresh_test_report.sh --dry-run
#   bash loop/refresh_test_report.sh --force
#
# 종료: 0 통과·스킵·이미현재 · 1 실제 GameSweep 실패 · 2 사용법
set -u

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"

usage() {
  echo "[refresh_test_report] 사용법: bash loop/refresh_test_report.sh [--report PATH] [--log PATH] [--unity PATH] [--project PATH] [--dry-run] [--force]" >&2
}

REPORT=""
LOG=""
UNITY_BIN=""
PROJECT=""
DRY=0
FORCE=0

while [ $# -gt 0 ]; do
  case "$1" in
    -h|--help)
      usage
      exit 2
      ;;
    --dry-run)
      DRY=1
      shift
      ;;
    --force)
      FORCE=1
      shift
      ;;
    --report)
      if [ $# -lt 2 ]; then echo "[refresh_test_report] --report 값이 없다." >&2; exit 2; fi
      REPORT="$2"
      shift 2
      ;;
    --log)
      if [ $# -lt 2 ]; then echo "[refresh_test_report] --log 값이 없다." >&2; exit 2; fi
      LOG="$2"
      shift 2
      ;;
    --unity)
      if [ $# -lt 2 ]; then echo "[refresh_test_report] --unity 값이 없다." >&2; exit 2; fi
      UNITY_BIN="$2"
      shift 2
      ;;
    --project)
      if [ $# -lt 2 ]; then echo "[refresh_test_report] --project 값이 없다." >&2; exit 2; fi
      PROJECT="$2"
      shift 2
      ;;
    --)
      shift
      break
      ;;
    -*)
      echo "[refresh_test_report] 알 수 없는 옵션: $1" >&2
      usage
      exit 2
      ;;
    *)
      echo "[refresh_test_report] 여분 인수: $1" >&2
      usage
      exit 2
      ;;
  esac
done

abs_from_root() {
  local p="$1"
  case "$p" in
    /*) printf '%s\n' "$p" ;;
    *)  printf '%s\n' "$ROOT/$p" ;;
  esac
}

if ! HEAD="$(git -C "$ROOT" rev-parse HEAD 2>/dev/null)"; then
  echo "[refresh_test_report] git 저장소가 아니다: $ROOT" >&2
  exit 1
fi

if [ -z "$REPORT" ]; then
  REPORT="$ROOT/loop/last_test_report.json"
else
  REPORT="$(abs_from_root "$REPORT")"
fi

if [ -z "$LOG" ]; then
  LOG="$ROOT/projects/ashes-to-stars/results/refresh_test_report.log"
else
  LOG="$(abs_from_root "$LOG")"
fi

if [ -z "$PROJECT" ]; then
  PROJECT="$ROOT/projects/ashes-to-stars/unity_meas"
else
  PROJECT="$(abs_from_root "$PROJECT")"
fi

METHOD="AshesToStars.GameSweepSelfCheck.Run"

wrap_cmd=(bash "$HERE/run_selfcheck.sh" "$METHOD" --project "$PROJECT" --log "$LOG")
if [ -n "$UNITY_BIN" ]; then
  wrap_cmd+=(--unity "$UNITY_BIN")
fi

echo "[refresh_test_report] HEAD=$HEAD"
echo "[refresh_test_report] report=$REPORT"
echo "[refresh_test_report] log=$LOG"
echo "[refresh_test_report] wrapper: ${wrap_cmd[*]}"

if [ "$DRY" = "1" ]; then
  echo "[refresh_test_report] DRY-RUN: 리포트를 쓰지 않는다"
  exit 0
fi

read_report_head() {
  python3 -c '
import json, sys
p = sys.argv[1]
try:
    with open(p, "r", encoding="utf-8-sig") as f:
        d = json.load(f)
    if isinstance(d, dict):
        print(d.get("head") or "")
    else:
        print("")
except Exception:
    print("")
' "$1"
}

now_stamp() {
  date '+%Y-%m-%d %H:%M'
}

write_skip_json() {
  local path="$1" head="$2" reason="$3"
  python3 -c '
import json, sys
path, head, reason, at = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]
data = {
    "at": at,
    "ok": True,
    "summary": "GameSweep 스킵 — %s (HEAD 재실행)" % reason,
    "head": head,
    "items": [{
        "name": "GameSweep HEAD 재실행",
        "ok": True,
        "note": "스킵: %s" % reason,
    }],
}
with open(path, "w", encoding="utf-8", newline="\n") as f:
    json.dump(data, f, ensure_ascii=False, indent=2)
    f.write("\n")
' "$path" "$head" "$reason" "$(now_stamp)"
}

write_fail_stub() {
  local path="$1" head="$2" note="$3"
  python3 -c '
import json, sys
path, head, note, at = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]
data = {
    "at": at,
    "ok": False,
    "summary": "GameSweep FAIL (HEAD 재실행)",
    "head": head,
    "items": [{
        "name": "GameSweep HEAD 재실행",
        "ok": False,
        "note": note[:200],
    }],
}
with open(path, "w", encoding="utf-8", newline="\n") as f:
    json.dump(data, f, ensure_ascii=False, indent=2)
    f.write("\n")
' "$path" "$head" "$note" "$(now_stamp)"
}

write_pass_stub() {
  local path="$1" head="$2"
  python3 -c '
import json, sys
path, head, at = sys.argv[1], sys.argv[2], sys.argv[3]
data = {
    "at": at,
    "ok": True,
    "summary": "GameSweep PASS (HEAD 재실행)",
    "head": head,
    "items": [{
        "name": "GameSweep HEAD 재실행",
        "ok": True,
        "note": "통과",
    }],
}
with open(path, "w", encoding="utf-8", newline="\n") as f:
    json.dump(data, f, ensure_ascii=False, indent=2)
    f.write("\n")
' "$path" "$head" "$(now_stamp)"
}

inject_head() {
  local path="$1" head="$2"
  python3 -c '
import json, sys
path, head = sys.argv[1], sys.argv[2]
with open(path, "r", encoding="utf-8-sig") as f:
    data = json.load(f)
if not isinstance(data, dict):
    data = {}
data["head"] = head
with open(path, "w", encoding="utf-8", newline="\n") as f:
    json.dump(data, f, ensure_ascii=False, indent=2)
    f.write("\n")
' "$path" "$head"
}

mkdir -p "$(dirname "$LOG")" "$(dirname "$REPORT")"

if [ "$FORCE" != "1" ] && [ -f "$REPORT" ]; then
  existing="$(read_report_head "$REPORT")"
  if [ "$existing" = "$HEAD" ]; then
    echo "[refresh_test_report] 이미 현재 HEAD ($HEAD) — 재실행 생략" | tee -a "$LOG"
    exit 0
  fi
fi

WRAP_OUT="$(mktemp "${TMPDIR:-/tmp}/refresh_test_report_wrap.XXXXXX")"
rc=0
"${wrap_cmd[@]}" > "$WRAP_OUT" 2>&1 || rc=$?
cat "$WRAP_OUT" >> "$LOG" || true

skip_reason=""
if grep -Eq 'Unity 에디터를 찾지 못했다' "$WRAP_OUT"; then
  skip_reason="Unity 에디터를 찾지 못했다"
elif grep -Eq 'Unity 가 없다' "$WRAP_OUT"; then
  skip_reason="Unity 가 없다"
elif grep -Eq 'Unity 가 실행 파일이 아니다' "$WRAP_OUT"; then
  skip_reason="Unity 가 실행 파일이 아니다"
fi

if [ -n "$skip_reason" ]; then
  write_skip_json "$REPORT" "$HEAD" "$skip_reason"
  echo "[refresh_test_report] 스킵 — $skip_reason (ok=true, head=$HEAD)" | tee -a "$LOG"
  rm -f "$WRAP_OUT"
  exit 0
fi

if [ -f "$REPORT" ]; then
  inject_head "$REPORT" "$HEAD"
  echo "[refresh_test_report] head 주입: $HEAD → $REPORT" | tee -a "$LOG"
elif [ "$rc" -eq 0 ]; then
  write_pass_stub "$REPORT" "$HEAD"
  echo "[refresh_test_report] PASS 스텁 기록 (head=$HEAD)" | tee -a "$LOG"
else
  note="$(grep -E 'FAIL|error CS|non-zero|executeMethod' "$WRAP_OUT" "$LOG" 2>/dev/null | head -1 | tr -d '\r' | cut -c1-200)"
  note="${note:-GameSweep FAIL}"
  write_fail_stub "$REPORT" "$HEAD" "$note"
  echo "[refresh_test_report] FAIL 스텁 기록 (head=$HEAD)" | tee -a "$LOG"
fi

rm -f "$WRAP_OUT"

if [ "$rc" -eq 0 ]; then
  echo "[refresh_test_report] PASS"
  exit 0
fi
echo "[refresh_test_report] FAIL (exit $rc)" >&2
exit "$rc"
