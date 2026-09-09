#!/bin/bash
# 검수용 화면 근거 — 배치모드에서 오프스크린 렌더(-nographics 없이). 에디터를 닫고 돌려라.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"   # 스크립트 자리 기준 — worktree로 옮겨도 제 트리를 잰다
UNITY="/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity"
LOG="$ROOT/unity/Logs/qa_shots.log"
SHOTS="$ROOT/builds/qa"

# **옛 이름의 그림을 남기지 않는다**(검수 지적 2026-09-09).
# 샷 이름이 바뀌면(`46_person_Trainer` → `46_person_Vendor`) 옛 PNG가 그대로 남고,
# **다음 사람은 그 옛 화면을 최신으로 읽는다** — 검수가 랩 ⑦에서 실제로 밟은 병이다.
# 그래서 이번 판이 다시 안 찍은 그림은 지운다. 기준은 이름 목록이 아니라 **이 판의 시각**이다
# (목록으로 지우면 목록을 고칠 때마다 같은 잔재가 다시 생긴다).
mkdir -p "$SHOTS"
MARK="$(mktemp)"
trap 'rm -f "$MARK"' EXIT
touch "$MARK"

set +e
"$UNITY" -batchmode -quit -projectPath "$ROOT/unity" -executeMethod Ulon.Editor.QaShots.Run -logFile "$LOG"
RC=$?
set -e

if [ "$RC" -eq 0 ]; then
  # **`*_before.png`는 지우지 않는다** — 이 판이 안 찍는 게 당연한 그림이고(전후 쌍의 「전」),
  # 첫 판에서 이 예외를 안 둬서 옛 전판 열여덟 장을 지웠다. **잔재와 증거는 다르다.**
  find "$SHOTS" -maxdepth 1 -name '*.png' ! -name '*_before.png' ! -newer "$MARK" -print -delete | while read -r stale; do
    echo "[qa_shots] 옛 이름 잔재 삭제: $(basename "$stale")"
  done
fi
exit "$RC"
