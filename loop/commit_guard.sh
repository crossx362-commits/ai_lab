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

COUNT="$(printf '%s\n' "$ACTUAL" | wc -l | tr -d ' ')"
echo "[commit_guard] PASS: 스테이지 ${COUNT}건 전부 허용 경로와 일치"
exit 0
