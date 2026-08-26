#!/bin/bash
# 안전 커밋 한 줄 — temp-index 분리 + 커밋 가드 + 공유 인덱스 정리를 **한 번의 호출**로 끝낸다.
# (오너 2026-08-26 「커밋 가드로 혼입 확인하는 게 너무 오래 걸린다」)
#
# 느렸던 것은 가드가 아니다(60파일 실측 1.3초 · 파일당 0.02초). 느렸던 것은 그 앞뒤로 매번
# 새 LLM 턴이 붙는 왕복이었다: 스테이지 → 가드 실패 → 상태 확인 → reset → 다시 add → 가드 →
# 커밋 → 인덱스 정리. 한 바퀴에 가드가 8번 불린 실측이 있다. 그 전부를 이 스크립트가 대신한다.
#
# 사용법:
#   bash loop/safe_commit.sh <커밋할 경로...> <<'MSG'
#   제목 줄
#
#   본문
#   MSG
#
# 하는 일:
#   1) HEAD로 임시 인덱스를 만들고 지정 경로만 담는다(다른 세션 스테이징과 격리)
#   2) commit_guard로 경로·내용 일치를 확인한다(낡은 스냅·혼입 차단)
#   3) 커밋한다(메시지는 stdin — argv 개행 잘림 사고를 피한다)
#   4) 공유 인덱스에서 그 경로만 다시 add해 「낡은 스냅」이 남지 않게 한다
#      (`git reset`은 남의 스테이징까지 날리므로 절대 쓰지 않는다)
# 종료 코드: 0 커밋됨 · 1 가드 차단(커밋 안 함) · 2 사용법 오류
set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ $# -eq 0 ]; then
  echo "[safe_commit] 사용법: bash loop/safe_commit.sh <경로...> <<'MSG' ... MSG" >&2
  exit 2
fi
if ! git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  echo "[safe_commit] git 저장소 밖에서 실행됐다." >&2
  exit 2
fi
if [ -t 0 ]; then
  echo "[safe_commit] 커밋 메시지를 stdin으로 넘겨라 (<<'MSG' ... MSG)." >&2
  exit 2
fi

MSG="$(cat)"
if [ -z "${MSG//[[:space:]]/}" ]; then
  echo "[safe_commit] 커밋 메시지가 비었다." >&2
  exit 2
fi

PATHS=("$@")
TMP_INDEX="$(git rev-parse --git-path index)_safecommit_$$"
cleanup() { rm -f "$TMP_INDEX"; }
trap cleanup EXIT

export GIT_INDEX_FILE="$TMP_INDEX"
if ! git read-tree HEAD; then
  echo "[safe_commit] HEAD를 임시 인덱스로 읽지 못했다." >&2
  exit 2
fi
# 없는 경로·매칭 실패는 여기서 죽이지 않는다 — 아래 「담긴 변경이 없다」 검사가 같은 사유로
# 1(커밋 안 함)로 끝낸다. 사용법 오류(2)와 「커밋할 게 없다」(1)를 섞지 않기 위해서다.
git add -A -- "${PATHS[@]}" 2>/dev/null || true

# 가드는 같은 임시 인덱스를 본다(GIT_INDEX_FILE을 git이 그대로 따른다).
STAGED="$(git diff --cached --name-only)"
if [ -z "$STAGED" ]; then
  echo "[safe_commit] 중단: 담긴 변경이 없다 — 이미 커밋됐거나 경로가 틀렸다." >&2
  exit 1
fi
# shellcheck disable=SC2086
if ! bash "$HERE/commit_guard.sh" $STAGED; then
  echo "[safe_commit] 가드가 막았다 — 커밋하지 않았다. 위 사유를 먼저 해소하라." >&2
  exit 1
fi

if ! printf '%s\n' "$MSG" | git commit -q -F -; then
  echo "[safe_commit] 커밋 실패." >&2
  exit 1
fi
HASH="$(git rev-parse --short HEAD)"

# 공유 인덱스 정리 — 이걸 빼먹으면 다음 맨몸 커밋이 방금 커밋을 되돌린다(2026-08-26 사고).
unset GIT_INDEX_FILE
cleanup
git add -A -- "${PATHS[@]}" 2>/dev/null || true

echo "[safe_commit] 커밋됨 $HASH · $(printf '%s\n' "$STAGED" | wc -l | tr -d ' ')건 · 공유 인덱스 정리 완료"
exit 0
