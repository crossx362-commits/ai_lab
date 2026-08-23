#!/bin/bash
# autonomous/integration → master 병합. 메인 루프 바퀴가 끝날 때마다 호출된다.
# master 작업중이면 건너뛰고, 충돌 나면 abort 후 다음 바퀴에 맡긴다.

set -uo pipefail
TARGET_REPO="${1:-$(pwd)}"
cd "$TARGET_REPO" || exit 0

git rev-parse --verify autonomous/integration >/dev/null 2>&1 || exit 0

BASE="$(git merge-base master autonomous/integration)"
TIP="$(git rev-parse autonomous/integration)"
[ "$BASE" = "$TIP" ] && exit 0   # 새로 적립된 것 없음

# master 작업 트리가 더러우면(메인 루프 진행 중) 건너뛴다
if [ -n "$(git status --porcelain | grep -v '^??')" ]; then
  echo "병합 보류 — master 작업 중"; exit 0
fi

if git merge --no-ff --no-edit -m "루프: 속도 레인 integration 병합 ($TIP)" autonomous/integration; then
  echo "integration → master 병합 완료: $(git rev-parse --short HEAD)"
else
  git merge --abort 2>/dev/null
  echo "병합 충돌 — abort, 다음 바퀴에서 재시도"
  exit 0
fi
