#!/bin/bash
# 공용 커밋 가드 — 정기 회의 20260825-203200 채택 #3 (PROPOSALS 2026-08-25 09:49, 상).
# 모든 자동 루프 커밋 직전에 실행한다: 인덱스에 스테이지된 경로가 허용 경로와
# 정확히 같은지 강제하고, 다르면 1로 끝나 커밋을 중단한다(타 세션 혼입 차단).
#
# 2단 검증 — 정기 회의 20260827-081437 채택 #2:
#   (a) 커밋 전 허용 경로 검사(기존)
#   (b) temp-index 커밋 뒤 `--from-head`: HEAD에서 경로를 재추출해 블롭=HEAD를
#       확인하고, HEAD~1..HEAD 경로가 허용 목록과 같은지 본다.
#   랩 종료 `--lap-end`: 허용 경로 없이, 빈 인덱스는 통과, 낡은 스냅만 차단
#   (타 세션의 신선한 스테이징은 존중).
#
# 사용법:
#   bash loop/commit_guard.sh <허용경로>...
#     - 공유 인덱스로 맨몸 커밋할 때: 커밋할 경로를 전부 나열한다.
#     - temp-index(commit-tree) 흐름일 때: GIT_INDEX_FILE=<임시인덱스> env와 함께
#       호출하면 그 임시 인덱스 기준으로 검사한다(git이 env를 따른다).
#   bash loop/commit_guard.sh --from-head <허용경로>...
#     - 커밋 직후 2단. temp-index를 HEAD에서 재추출한 뒤 블롭을 대조한다.
#   bash loop/commit_guard.sh --lap-end
#     - 랩 종료 재실행. 경로 인수 없음.
#   종료 코드: 0 통과 · 1 검사 실패(커밋 중단) · 2 사용법 오류
set -u

FROM_HEAD=0
LAP_END=0
while [ $# -gt 0 ]; do
  case "$1" in
    --from-head) FROM_HEAD=1; shift ;;
    --lap-end)   LAP_END=1; shift ;;
    --*)
      echo "[commit_guard] 알 수 없는 옵션: $1" >&2
      exit 2
      ;;
    *) break ;;
  esac
done

if [ "$LAP_END" != "1" ] && [ "$FROM_HEAD" != "1" ] && [ $# -eq 0 ]; then
  echo "[commit_guard] 사용법: bash loop/commit_guard.sh [--from-head|--lap-end] <허용경로>..." >&2
  exit 2
fi
if [ "$FROM_HEAD" = "1" ] && [ $# -eq 0 ]; then
  echo "[commit_guard] 사용법: bash loop/commit_guard.sh --from-head <허용경로>..." >&2
  exit 2
fi

if ! git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  echo "[commit_guard] git 저장소 밖에서 실행됐다." >&2
  exit 2
fi

IDX="${GIT_INDEX_FILE:-$(git rev-parse --git-path index)}"

# 경로 목록에 대해 인덱스 블롭 ≠ 작업 트리 인 낡은 스냅을 모아 출력한다.
collect_stale() {
  local p idxblob workblob stale=""
  while IFS= read -r p; do
    [ -n "$p" ] || continue
    [ -e "$p" ] || continue                      # 실제로 지운 파일은 정상 삭제 커밋
    idxblob="$(git ls-files --stage -- "$p" | awk '{print $2}')"
    if [ -z "$idxblob" ]; then
      stale="${stale}${p} (파일이 있는데 삭제로 스테이지됨)"$'\n'
      continue
    fi
    workblob="$(git hash-object -- "$p" 2>/dev/null || true)"
    if [ -n "$workblob" ] && [ "$workblob" != "$idxblob" ]; then
      stale="${stale}${p} (스테이지 내용이 작업 트리와 다름)"$'\n'
    fi
  done <<COLLECT_STALE
$1
COLLECT_STALE
  printf '%s' "$stale"
}

# --- 랩 종료 재실행 (회의 20260827-081437 채택 #2) -----------------------------
# 빈 인덱스 = 깨끗하니 통과. 타 세션이 방금 스테이지한 신선한 내용은 존중하고,
# 인덱스 블롭이 작업 트리와 다른 낡은 스냅만 차단한다(2026-08-26 사고).
if [ "$LAP_END" = "1" ]; then
  STAGED="$(git diff --cached --name-only || true)"
  if [ -z "$STAGED" ]; then
    echo "[commit_guard] PASS (lap-end): 스테이지 없음 — 공유 인덱스 깨끗"
    exit 0
  fi
  STALE="$(collect_stale "$STAGED")"
  if [ -n "$STALE" ]; then
    echo "[commit_guard] 중단 (lap-end): 낡은 스냅이 스테이지돼 있다 — 다음 맨몸 커밋이 되돌린다 (인덱스: $IDX)" >&2
    printf '%s' "$STALE" | sed 's/^/[commit_guard]   - /' >&2
    echo "[commit_guard] 조치: 커밋한 경로만 git add 로 작업 트리에 맞춰라 (git reset 금지 — 남의 스테이징을 날린다)" >&2
    exit 1
  fi
  COUNT="$(printf '%s\n' "$STAGED" | wc -l | tr -d ' ')"
  echo "[commit_guard] PASS (lap-end): 스테이지 ${COUNT}건, 낡은 스냅 없음 (타 세션 스테이징은 존중)"
  exit 0
fi

# --- 2단: HEAD 재추출 (회의 20260827-081437 채택 #2) ---------------------------
# temp-index 커밋 직후 호출. 방금 커밋이 HEAD에 남긴 블롭만 믿고 인덱스를 다시 채운다.
# 커밋에 남의 경로가 섞였으면 HEAD~1..HEAD 가 허용 목록과 달라 여기서 잡힌다.
if [ "$FROM_HEAD" = "1" ]; then
  ALLOWED="$(printf '%s\n' "$@" | sort)"
  EXTRACT_FAIL=""
  WT_FAIL=""
  for p in "$@"; do
    [ -n "$p" ] || continue
    line="$(git ls-tree HEAD -- "$p")"
    if [ -z "$line" ]; then
      git update-index --force-remove -- "$p" 2>/dev/null || true
      idxblob="$(git ls-files --stage -- "$p" | awk '{print $2}')"
      if [ -n "$idxblob" ]; then
        EXTRACT_FAIL="${EXTRACT_FAIL}${p} (HEAD에 없는데 인덱스에 남음)"$'\n'
      fi
      if [ -e "$p" ] && [ "${COMMIT_GUARD_ALLOW_PARTIAL:-0}" != "1" ]; then
        WT_FAIL="${WT_FAIL}${p} (HEAD에 없는데 작업 트리에 있음)"$'\n'
      fi
      continue
    fi
    mode="$(printf '%s\n' "$line" | awk '{print $1}')"
    blob="$(printf '%s\n' "$line" | awk '{print $3}')"
    if ! git update-index --add --cacheinfo "$mode,$blob,$p"; then
      EXTRACT_FAIL="${EXTRACT_FAIL}${p} (HEAD 재추출 실패)"$'\n'
      continue
    fi
    idxblob="$(git ls-files --stage -- "$p" | awk '{print $2}')"
    if [ "$idxblob" != "$blob" ]; then
      EXTRACT_FAIL="${EXTRACT_FAIL}${p} (재추출 후 인덱스 블롭 ≠ HEAD)"$'\n'
    fi
    if [ "${COMMIT_GUARD_ALLOW_PARTIAL:-0}" != "1" ]; then
      if [ ! -e "$p" ]; then
        WT_FAIL="${WT_FAIL}${p} (HEAD에 있는데 작업 트리에 없음)"$'\n'
      else
        workblob="$(git hash-object -- "$p" 2>/dev/null || true)"
        if [ -n "$workblob" ] && [ "$workblob" != "$blob" ]; then
          WT_FAIL="${WT_FAIL}${p} (작업 트리 ≠ HEAD 블롭)"$'\n'
        fi
      fi
    fi
  done
  if [ -n "$EXTRACT_FAIL" ]; then
    echo "[commit_guard] 중단 (2단): HEAD 재추출 후 인덱스 블롭이 HEAD와 다르다 (인덱스: $IDX)" >&2
    printf '%s' "$EXTRACT_FAIL" | sed 's/^/[commit_guard]   - /' >&2
    exit 1
  fi
  if [ -n "$WT_FAIL" ]; then
    echo "[commit_guard] 중단 (2단): HEAD 재추출 뒤 작업 트리가 HEAD와 다르다 — 커밋된 내용이 디스크와 어긋난다 (인덱스: $IDX)" >&2
    printf '%s' "$WT_FAIL" | sed 's/^/[commit_guard]   - /' >&2
    exit 1
  fi
  if git rev-parse -q --verify HEAD~1 >/dev/null; then
    COMMITTED="$(git diff --name-only HEAD~1 HEAD | sort)"
    FOREIGN="$(comm -13 <(printf '%s\n' "$ALLOWED") <(printf '%s\n' "$COMMITTED"))"
    MISSING="$(comm -23 <(printf '%s\n' "$ALLOWED") <(printf '%s\n' "$COMMITTED"))"
    if [ -n "$FOREIGN" ] || [ -n "$MISSING" ]; then
      echo "[commit_guard] 중단 (2단): 방금 커밋 경로가 허용 목록과 다르다 (인덱스: $IDX)" >&2
      if [ -n "$FOREIGN" ]; then
        echo "[commit_guard] 커밋에 섞인 경로(타 세션 혼입 의심):" >&2
        printf '%s\n' "$FOREIGN" | sed 's/^/[commit_guard]   - /' >&2
      fi
      if [ -n "$MISSING" ]; then
        echo "[commit_guard] 선언했지만 커밋에 없는 경로:" >&2
        printf '%s\n' "$MISSING" | sed 's/^/[commit_guard]   - /' >&2
      fi
      exit 1
    fi
  fi
  COUNT="$(printf '%s\n' "$ALLOWED" | grep -c . || true)"
  echo "[commit_guard] PASS (2단): HEAD 재추출 ${COUNT}건 · 블롭=HEAD · 커밋 경로=허용 목록"
  exit 0
fi

# --- 1단: 커밋 전 허용 경로 (기존) --------------------------------------------
STAGED="$(git diff --cached --name-only || true)"

if [ -z "$STAGED" ]; then
  echo "[commit_guard] 중단: 스테이지된 경로가 없다 — 커밋할 내용이 없으면 조용히 끝나는 pathspec 커밋 실수를 막는다." >&2
  echo "[commit_guard] 인덱스: $IDX" >&2
  exit 1
fi

ALLOWED="$(printf '%s\n' "$@" | sort)"
ACTUAL="$(printf '%s\n' "$STAGED" | sort)"

FOREIGN="$(comm -13 <(printf '%s\n' "$ALLOWED") <(printf '%s\n' "$ACTUAL"))"
MISSING="$(comm -23 <(printf '%s\n' "$ALLOWED") <(printf '%s\n' "$ACTUAL"))"

if [ -n "$FOREIGN" ] || [ -n "$MISSING" ]; then
  echo "[commit_guard] 중단: 인덱스 스테이징이 허용 경로와 다르다 (인덱스: $IDX)" >&2
  if [ -n "$FOREIGN" ]; then
    echo "[commit_guard] 허용 안 된 스테이징(타 세션 혼입 의심):" >&2
    printf '%s\n' "$FOREIGN" | sed 's/^/[commit_guard]   - /' >&2
  fi
  if [ -n "$MISSING" ]; then
    echo "[commit_guard] 선언했지만 스테이지되지 않은 경로:" >&2
    printf '%s\n' "$MISSING" | sed 's/^/[commit_guard]   - /' >&2
  fi
  echo "[commit_guard] 조치: 내 분만 temp-index(commit-tree) 등으로 분리해 다시 통과시킨 뒤 커밋하라." >&2
  exit 1
fi

# --- 낡은 스냅 스테이징 차단 (2026-08-26 실측 사고) ---------------------------
# 경로가 맞아도 스테이지된 *내용*이 낡으면 이미 커밋된 작업을 되돌린다. 이날 공유
# 인덱스는 loop_watch.sh -81줄·TowerClimbCurveMeasure.cs -153줄·CharacterScreen.cs
# -17줄(§18-14 소환수 소비처)·영지 아트 png 8종 삭제를 예약한 상태였고, 경로만 보는
# 위 검사는 전부 통과시킨다. 자동 루프는 언제나 "작업 트리 현재 내용"을 커밋하므로
# 스테이지 블롭 ≠ 작업 트리면 사고다. 의도적 부분 스테이징(git add -p)만 예외로
# COMMIT_GUARD_ALLOW_PARTIAL=1을 준다.
if [ "${COMMIT_GUARD_ALLOW_PARTIAL:-0}" != "1" ]; then
  STALE="$(collect_stale "$ACTUAL")"
  if [ -n "$STALE" ]; then
    echo "[commit_guard] 중단: 낡은 스냅이 스테이지돼 있다 — 커밋하면 이미 반영된 작업이 되돌아간다 (인덱스: $IDX)" >&2
    printf '%s' "$STALE" | sed 's/^/[commit_guard]   - /' >&2
    echo "[commit_guard] 조치: git reset 으로 인덱스를 HEAD에 맞춘 뒤 커밋할 파일만 다시 git add 하라" >&2
    echo "[commit_guard]       (의도적 부분 스테이징이면 COMMIT_GUARD_ALLOW_PARTIAL=1)" >&2
    exit 1
  fi
fi

# --- png 실로드 (회의 20260827-081437 채택 #1) --------------------------------
# 보간 `$"FX/fx_dash_trail_{i}"` 접두가 가리키는 PNG 가 Resources 에 없으면
# Resources.Load 가 null 이 된다. 경로·내용 가드만으로는 안 잡힌다.
# QA_NO_PNG_LOAD=1 이면 건너뛴다(네거티브 픽스처·응급).
if [ "${QA_NO_PNG_LOAD:-0}" != "1" ]; then
  ROOT="$(git rev-parse --show-toplevel)"
  PNG_TOOL=""
  for f in "$ROOT"/projects/ai-team/skills/*/tools/game_asset_names.py; do
    if [ -f "$f" ]; then PNG_TOOL="$f"; break; fi
  done
  if [ -n "$PNG_TOOL" ]; then
    if printf '%s\n' "$ACTUAL" | grep -Eq \
      'projects/ashes-to-stars/unity/Assets/.*\.(cs|png)$|game_asset_names\.py$'; then
      if ! python3 "$PNG_TOOL" --png-load; then
        echo "[commit_guard] 중단: png 실로드 검사 실패 — 보간 Resources.Load 가 null 이 될 경로가 있다" >&2
        echo "[commit_guard]       (건너뛰려면 QA_NO_PNG_LOAD=1 — 네거티브 픽스처·응급만)" >&2
        exit 1
      fi
    fi
  fi
fi

# --- zip 실로드 (회의 20260827-081437 보류 #5, png `d30a135c` 후속) ----------
# 리터럴·보간 `.zip` 경로가 Resources/StreamingAssets 에 없거나 unzip 목록이
# 비면 Resources.Load / unzip 이 null 이다. 경로·내용 가드만으로는 안 잡힌다.
# QA_NO_ZIP_LOAD=1 이면 건너뛴다(네거티브 픽스처·응급).
if [ "${QA_NO_ZIP_LOAD:-0}" != "1" ]; then
  ROOT="$(git rev-parse --show-toplevel)"
  ZIP_TOOL=""
  for f in "$ROOT"/projects/ai-team/skills/*/tools/game_asset_names.py; do
    if [ -f "$f" ]; then ZIP_TOOL="$f"; break; fi
  done
  if [ -n "$ZIP_TOOL" ]; then
    if printf '%s\n' "$ACTUAL" | grep -Eq \
      'projects/ashes-to-stars/unity/Assets/.*\.(cs|zip)$|game_asset_names\.py$'; then
      if ! python3 "$ZIP_TOOL" --zip-load; then
        echo "[commit_guard] 중단: zip 실로드 검사 실패 — Resources.Load/unzip 가 null 이 될 경로가 있다" >&2
        echo "[commit_guard]       (건너뛰려면 QA_NO_ZIP_LOAD=1 — 네거티브 픽스처·응급만)" >&2
        exit 1
      fi
    fi
  fi
fi

COUNT="$(printf '%s\n' "$ACTUAL" | wc -l | tr -d ' ')"
echo "[commit_guard] PASS: 스테이지 ${COUNT}건 전부 허용 경로와 일치 · 내용도 작업 트리와 동일"
exit 0
