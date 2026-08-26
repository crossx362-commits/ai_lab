#!/bin/bash
# 바퀴 오리엔테이션 한 방 — 매 바퀴 첫 15~20 턴을 잡아먹던 「지금 어디까지 왔나」 탐색을
# **한 번의 명령**으로 끝낸다. (오너 2026-08-26 「빠르게 진행되게 해줘」)
#
# 실측 근거(logs/2026-08-26/lap-20260826-202756-2.log · 57분 바퀴):
#   셸 명령 43회 중 1~21번이 전부 오리엔테이션이었다 — git status·git log·유니티 실행 여부·
#   배치 명령 위치·sync_meas 위치·직전 스윕 결과 재발견. 유니티 배치 자체는 싸다(아래 실측).
#   한 왕복 ≈ 1분이므로 이 21턴이 곧 20분이다. 이 스크립트가 그 전부를 대신한다.
#
# 사용: bash loop/lap_brief.sh      (읽기 전용 — 아무것도 바꾸지 않는다)
set -uo pipefail
cd "$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

hr() { printf '\n=== %s ===\n' "$1"; }

hr "HEAD·최근 커밋"
git log --oneline -8 | cut -c1-140

hr "작업 트리 (요약)"
ST="$(git status --porcelain)"
printf '변경 %s건 · 스테이지됨 %s건\n' \
  "$(printf '%s\n' "$ST" | grep -c .)" "$(git diff --cached --name-only | grep -c .)"
printf '%s\n' "$ST" | grep -v '^??' | head -25
UNTRACKED="$(printf '%s\n' "$ST" | grep -c '^??')"
[ "$UNTRACKED" -gt 0 ] && printf '(추적 안 됨 %s건 — 대부분 무시 대상, 필요할 때만 git status --porcelain로 확인)\n' "$UNTRACKED"

hr "플래그"
for f in loop/COUNCIL_NOW loop/STOP loop/STOP_LANE; do
  [ -e "$f" ] && echo "있음: $f" || echo "없음: $f"
done

hr "유니티 프로세스"
if pgrep -fl "Unity.app/Contents/MacOS/Unity" | grep -v VBCSCompiler | grep -q .; then
  pgrep -fl "Unity.app/Contents/MacOS/Unity" | grep -v VBCSCompiler | \
    sed -e 's#/Applications/Unity/Hub/Editor/[^ ]*Unity#Unity#' | cut -c1-160
  echo "→ 오너 에디터(-useHub·unity/)는 죽이지 마라. 배치는 unity_meas/ 사본으로만."
else
  echo "없음 (오너 에디터 꺼져 있음 — 눈확인은 배치 SelfCheck 로그로 대체)"
fi

hr "직전 배치 결과 (최근 5개 로그)"
find output/qa/ashes-to-stars -name '*.log' -newermt '-2 days' 2>/dev/null |
  xargs -I{} stat -f '%m|%N' {} 2>/dev/null | sort -rn | head -5 |
  while IFS='|' read -r _ p; do
    # 판정 줄만 센다 — 본문에 박힌 "…FAIL" 문구나 아무 숫자쌍(해상도 등)을 성적표로 오독하지
    # 않기 위해 줄머리 PASS/FAIL만 본다.
    pn="$(grep -ac '^ *PASS' "$p" 2>/dev/null)"; fn="$(grep -ac '^ *FAIL' "$p" 2>/dev/null)"
    if [ "${fn:-0}" -gt 0 ]; then v="FAIL ${fn} (PASS ${pn})"; else v="PASS ${pn} · 실패 0"; fi
    printf '%s  %s  [%s]\n' "$(stat -f '%Sm' -t '%m-%d %H:%M' "$p")" "${p#output/qa/ashes-to-stars/}" "${v:-?}"
  done

hr "다음 할 일 (STATUS.md 큐 머리 · 전문은 파일에서)"
awk '/^## 다음 할 일 \(/{f=1} f&&/^[0-9]+\./{n++; print substr($0,1,220); if(n>=3) exit}' docs/STATUS.md

hr "오너 최신 지시 (INBOX 마지막 헤딩 3개)"
grep -n '^#\{2,4\} ' docs/feedback/INBOX.md | tail -3

hr "복사해 쓰는 명령 (경로 다시 찾지 마라)"
cat <<'RUNBOOK'
# 측정용 사본 동기화 (배치 전 소스 변경이 있었으면 매번)
cd projects/ashes-to-stars && ./sync_meas.sh && cd -

# 단일 SelfCheck — 개발 중에는 이것만 (실측 3~4초)
UNITY=/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity
"$UNITY" -batchmode -quit -nographics \
  -projectPath projects/ashes-to-stars/unity_meas \
  -executeMethod AshesToStars.<이름>SelfCheck.Run \
  -logFile output/qa/ashes-to-stars/<이름>.log; echo "EXIT=$?"

# 전수 스윕 195종 — 커밋 직전 1회만 (실측 74초)
"$UNITY" -batchmode -quit -nographics \
  -projectPath projects/ashes-to-stars/unity_meas \
  -executeMethod AshesToStars.GameSweepSelfCheck.Run \
  -logFile output/qa/ashes-to-stars/sweep_<주제>.log; echo "EXIT=$?"

# 커밋은 이 한 줄로 (스테이지·가드·커밋·인덱스 정리 전부 포함 — git reset 쓰지 마라)
bash loop/safe_commit.sh <경로...> <<'MSG'
제목 줄

본문
MSG
RUNBOOK
echo
