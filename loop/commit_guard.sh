#!/bin/bash
# 공용 커밋 가드 — 정기 회의 20260825-203200 채택 #3 (PROPOSALS 2026-08-25 09:49, 상).
# 모든 자동 루프 커밋 직전에 실행한다: 인덱스에 스테이지된 경로가 허용 경로와
# 정확히 같은지 강제하고, 다르면 1로 끝나 커밋을 중단한다(타 세션 혼입 차단).
#
# 사용법:
#   bash loop/commit_guard.sh <허용경로>...
#     - 공유 인덱스로 맨몸 커밋할 때: 커밋할 경로를 전부 나열한다.
#     - temp-index(commit-tree) 흐름일 때: GIT_INDEX_FILE=<임시인덱스> env와 함께
#       호출하면 그 임시 인덱스 기준으로 검사한다(git이 env를 따른다).
#   종료 코드: 0 통과 · 1 검사 실패(커밋 중단) · 2 사용법 오류
set -u

if [ $# -eq 0 ]; then
  echo "[commit_guard] 사용법: bash loop/commit_guard.sh <허용경로>..." >&2
  exit 2
fi

if ! git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  echo "[commit_guard] git 저장소 밖에서 실행됐다." >&2
  exit 2
fi

IDX="${GIT_INDEX_FILE:-$(git rev-parse --git-path index)}"
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
  STALE=""
  while IFS= read -r p; do
    [ -n "$p" ] || continue
    [ -e "$p" ] || continue                      # 실제로 지운 파일은 정상 삭제 커밋
    # 인덱스 엔트리 유무는 ls-files로 본다 — `git rev-parse :경로`는 엔트리가 없어도
    # 다른 것을 물어와 삭제 예약을 「내용 불일치」로 오진한다(2026-08-26 실측).
    IDXBLOB="$(git ls-files --stage -- "$p" | awk '{print $2}')"
    if [ -z "$IDXBLOB" ]; then
      STALE="${STALE}${p} (파일이 있는데 삭제로 스테이지됨)"$'\n'
      continue
    fi
    WORKBLOB="$(git hash-object -- "$p" 2>/dev/null || true)"
    if [ -n "$WORKBLOB" ] && [ "$WORKBLOB" != "$IDXBLOB" ]; then
      STALE="${STALE}${p} (스테이지 내용이 작업 트리와 다름)"$'\n'
    fi
  done <<STALE_INPUT
$ACTUAL
STALE_INPUT
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

COUNT="$(printf '%s\n' "$ACTUAL" | wc -l | tr -d ' ')"
echo "[commit_guard] PASS: 스테이지 ${COUNT}건 전부 허용 경로와 일치 · 내용도 작업 트리와 동일"
exit 0
