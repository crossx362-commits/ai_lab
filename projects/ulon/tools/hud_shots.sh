#!/bin/bash
# HUD가 찍힌 화면 증거 — 스탠드얼론을 띄워 ScreenCapture로 찍는다(창이 잠깐 뜬다).
# HUD는 IMGUI라 편집기 오프스크린 렌더(qa_shots)에 안 나온다. **매 랩 돌리지 마라** — HUD를 건드린 랩에서만.
#
# **상태를 고정해 찍는다**(랩 ㉮, 2026-09-09). 예전에는 저장된 계정(PlayerPrefs의 무작위 GUID)으로
# 돌아 **판마다 상태가 누적**됐다 — HP 46/51 대 50/50, STR 31 대 30, 사냥터 대 마을.
# 그래서 분할 전후 다섯 장이 「달라졌다」고 떴지만 달라진 것은 HUD가 아니라 세계였고,
# **전후 비교의 자가 아예 서지 않았다.** 이제 매 판 **같은 계정 이름으로, 저장분을 지우고** 시작한다:
# 캐릭터 생성부터 같은 순서로 도니 두 판이 같은 화면이 된다.
# 찍고 나서 그 계정을 **지운다** — 찍으려고 만든 상태를 저장소에 남기지 않는다(씨앗 랩과 같은 규칙).
#
# NC: `ULON_HUD_STATE_NC=1 ./tools/hud_shots.sh` — 고정을 **전부** 끄고 옛 방식(저장된 계정)으로 찍는다.
# 그러면 판마다 화면이 다시 갈려야 한다. 반만 끄면 약한 빨간불이라 더 위험하다(원장).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")/.." && pwd)"   # 스크립트 자리 기준 — worktree로 옮겨도 제 트리를 잰다
UNITY="/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity"
ACCOUNT="ulon-hudshot"
# **저장 자리는 앱 옆이다** — 스탠드얼론의 `Application.dataPath`는 앱 번들 안이라
# 계정 파일이 `builds/frameprobe/data/accounts/`에 생긴다. 처음엔 저장소 쪽 `data/accounts`를
# 지우게 써서 **아무것도 안 지우고 있었다**(그런데도 두 판이 같아 통과로 보였다 — 지우는 자리가
# 틀려도 화면은 조용할 수 있다).
SAVE="$ROOT/builds/frameprobe/data/accounts/$ACCOUNT.json"

ACCOUNT_ARGS=(-ulon-account "$ACCOUNT")
if [ "${ULON_HUD_STATE_NC:-}" = "1" ]; then
  echo "[hud_shots] ⚠ 상태 반대쪽 한계 판 — 계정을 고정하지 않고 저장분도 안 지운다(정상판 아님)"
  ACCOUNT_ARGS=(-hudstatenc)   # 시계 고정까지 끈다 — 반만 끄면 NC가 아니다
else
  rm -f "$SAVE"
fi
# 만든 계정은 찍고 나서 지운다 — 실패로 빠져나가도(trap) 저장소에 안 남는다.
cleanup() { [ "${ULON_HUD_STATE_NC:-}" = "1" ] || rm -f "$SAVE"; }
trap cleanup EXIT

"$UNITY" -batchmode -quit -projectPath "$ROOT/unity" -executeMethod Ulon.Editor.FrameProbeBuild.Run -logFile "$ROOT/unity/Logs/frame_probe_build.log"
"$ROOT/builds/frameprobe/Ulon.app/Contents/MacOS/Ulon" -hudshots -shotdir "$ROOT/builds/qa" \
  "${ACCOUNT_ARGS[@]+"${ACCOUNT_ARGS[@]}"}" -logFile "$ROOT/unity/Logs/hud_shots.log"
# 그려진 조작 수단 기록은 게이트가 읽는다 — builds/는 git 무시라 추적 경로로 복사한다.
{ echo "# 기록 $(date -u +%Y-%m-%dT%H:%M:%SZ) · HEAD $(git -C "$ROOT" rev-parse --short HEAD) · 이 파일은 hud_shots.sh가 만든다(손으로 채우지 마라)"
  cat "$ROOT/builds/qa/hud_controls.txt"; } > "$ROOT/docs/hud_controls.txt"
ls -la "$ROOT/builds/qa"/hud_*.png
