#!/bin/bash
# Editor SelfCheck 배치 진입점 — 정기 회의 20260827-073515 채택 #1
# (PROPOSALS 2026-08-27 01:45, 상 → 회의 073515·065728·030031 연속 채택).
#
# 루프 세션이 /Applications/Unity/... 를 직접 치면 작업 디렉터리 밖 바이너리라
# 세션마다 권한 게이트에 막힌다. 이 스크립트는 저장소 안 단일 경로라 허용 대상을
# 고정한다. 권한 대화상자·승격 프롬프트는 절대 쓰지 않는다.
#
# 사용법:
#   bash loop/run_selfcheck.sh <Method> [Log]
#   bash loop/run_selfcheck.sh <Method> --project PATH --log PATH --unity PATH
#   bash loop/run_selfcheck.sh <Method> --dry-run
#
# 예:
#   bash loop/run_selfcheck.sh AshesToStars.BuildSlotsSelfCheck.Run
#
# 기본:
#   --project  <repo>/projects/ashes-to-stars/unity_meas
#   --log      <repo>/projects/ashes-to-stars/results/run_selfcheck.log
#   --unity    UNITY_EDITOR_PATH 또는 프로젝트 ProjectVersion.txt 버전의 Hub 에디터
#
# 종료: 0 통과 · 1 실행/로그 실패(Unity 부재·FAIL·컴파일 오류) · 2 사용법
set -u

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"

usage() {
  echo "[run_selfcheck] 사용법: bash loop/run_selfcheck.sh <Method> [Log] [--project PATH] [--log PATH] [--unity PATH] [--dry-run]" >&2
}

METHOD=""
PROJECT=""
LOG=""
UNITY_BIN=""
DRY=0

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
    --project)
      if [ $# -lt 2 ]; then echo "[run_selfcheck] --project 값이 없다." >&2; exit 2; fi
      PROJECT="$2"
      shift 2
      ;;
    --log)
      if [ $# -lt 2 ]; then echo "[run_selfcheck] --log 값이 없다." >&2; exit 2; fi
      LOG="$2"
      shift 2
      ;;
    --unity)
      if [ $# -lt 2 ]; then echo "[run_selfcheck] --unity 값이 없다." >&2; exit 2; fi
      UNITY_BIN="$2"
      shift 2
      ;;
    --)
      shift
      break
      ;;
    -*)
      echo "[run_selfcheck] 알 수 없는 옵션: $1" >&2
      usage
      exit 2
      ;;
    *)
      if [ -z "$METHOD" ]; then
        METHOD="$1"
      elif [ -z "$LOG" ]; then
        LOG="$1"
      else
        echo "[run_selfcheck] 여분 인수: $1" >&2
        usage
        exit 2
      fi
      shift
      ;;
  esac
done

if [ -z "$METHOD" ]; then
  usage
  exit 2
fi

abs_from_root() {
  local p="$1"
  case "$p" in
    /*) printf '%s\n' "$p" ;;
    *)  printf '%s\n' "$ROOT/$p" ;;
  esac
}

if [ -z "$PROJECT" ]; then
  PROJECT="$ROOT/projects/ashes-to-stars/unity_meas"
else
  PROJECT="$(abs_from_root "$PROJECT")"
fi

if [ -z "$LOG" ]; then
  LOG="$ROOT/projects/ashes-to-stars/results/run_selfcheck.log"
else
  LOG="$(abs_from_root "$LOG")"
fi

resolve_unity() {
  if [ -n "$UNITY_BIN" ]; then
    printf '%s\n' "$UNITY_BIN"
    return 0
  fi
  if [ -n "${UNITY_EDITOR_PATH:-}" ]; then
    printf '%s\n' "$UNITY_EDITOR_PATH"
    return 0
  fi
  local ver="" pv="$PROJECT/ProjectSettings/ProjectVersion.txt"
  if [ -f "$pv" ]; then
    ver="$(awk -F': *' '/^m_EditorVersion:/{gsub(/\r/,""); print $2; exit}' "$pv")"
  fi
  local c
  if [ -n "$ver" ]; then
    for c in \
      "/Applications/Unity/Hub/Editor/$ver/Unity.app/Contents/MacOS/Unity" \
      "$HOME/Applications/Unity/Hub/Editor/$ver/Unity.app/Contents/MacOS/Unity"
    do
      if [ -x "$c" ]; then
        printf '%s\n' "$c"
        return 0
      fi
    done
  fi
  return 1
}

UNITY_EXE="$(resolve_unity || true)"
if [ -z "$UNITY_EXE" ]; then
  echo "[run_selfcheck] Unity 에디터를 찾지 못했다 (프로젝트: $PROJECT)." >&2
  echo "[run_selfcheck] UNITY_EDITOR_PATH 또는 --unity 로 바이너리를 넘기거나, ProjectVersion.txt 버전을 Hub에 설치하라." >&2
  echo "[run_selfcheck] 권한 대화상자는 띄우지 않는다." >&2
  exit 1
fi
if [ ! -e "$UNITY_EXE" ]; then
  echo "[run_selfcheck] Unity 가 없다: $UNITY_EXE" >&2
  echo "[run_selfcheck] 권한 대화상자는 띄우지 않는다." >&2
  exit 1
fi
if [ "$DRY" != "1" ] && [ ! -x "$UNITY_EXE" ]; then
  echo "[run_selfcheck] Unity 가 실행 파일이 아니다: $UNITY_EXE" >&2
  exit 1
fi

CMD=("$UNITY_EXE" -batchmode -quit -nographics
  -projectPath "$PROJECT"
  -executeMethod "$METHOD"
  -logFile "$LOG")

echo "[run_selfcheck] method=$METHOD"
echo "[run_selfcheck] project=$PROJECT"
echo "[run_selfcheck] log=$LOG"
echo "[run_selfcheck] unity=$UNITY_EXE"
if [ "$DRY" = "1" ]; then
  echo "[run_selfcheck] DRY-RUN: ${CMD[*]}"
  exit 0
fi

if [ ! -d "$PROJECT" ]; then
  echo "[run_selfcheck] 프로젝트 경로가 없다: $PROJECT" >&2
  echo "[run_selfcheck] (측정 사본이면 projects/ashes-to-stars/sync_meas.sh 를 먼저 돌려라)" >&2
  exit 1
fi

mkdir -p "$(dirname "$LOG")"

# 배치 전용. stdin 을 닫아 프롬프트를 막고, 승격/대화상자 경로는 쓰지 않는다.
rc=0
"${CMD[@]}" </dev/null || rc=$?

if [ ! -f "$LOG" ]; then
  echo "[run_selfcheck] 로그가 안 생겼다 (Unity exit $rc): $LOG" >&2
  exit 1
fi

log_bad=0
if grep -E -q '^[[:space:]]*FAIL[[:space:]]' "$LOG"; then
  echo "[run_selfcheck] 로그에 FAIL 줄이 있다: $LOG" >&2
  log_bad=1
fi
if grep -E -q 'error CS[0-9]{4}' "$LOG"; then
  echo "[run_selfcheck] 로그에 컴파일 오류(error CS)가 있다: $LOG" >&2
  log_bad=1
fi
if grep -E -qi 'Scripts have compiler errors' "$LOG"; then
  echo "[run_selfcheck] 로그에 Scripts have compiler errors 가 있다: $LOG" >&2
  log_bad=1
fi
if grep -E -qi 'compilationhadfailure:[[:space:]]*True' "$LOG"; then
  echo "[run_selfcheck] 로그에 compilationhadfailure: True 가 있다: $LOG" >&2
  log_bad=1
fi
if grep -E -qi 'executeMethod class .* could not be found' "$LOG"; then
  echo "[run_selfcheck] executeMethod 클래스를 못 찾았다: $LOG" >&2
  log_bad=1
fi

if [ "$log_bad" -ne 0 ]; then
  exit 1
fi
if [ "$rc" -ne 0 ]; then
  echo "[run_selfcheck] Unity 가 non-zero 로 끝났다 (exit $rc). 로그: $LOG" >&2
  exit 1
fi

echo "[run_selfcheck] PASS"
exit 0
