#!/bin/bash
# 힉스필드 대기 잡 감시 — completed로 바뀌면 받아서 저장한다.
#
#   ./tools/hf_watch.sh            # 한 번 확인
#   ./tools/hf_watch.sh --loop     # 3분마다 확인, 다 받으면 종료
#
# 왜 필요한가 (2026-08-15):
#   힉스필드 서버 큐가 적체돼 잡 7개가 `waiting`으로 묶였다. 크레딧은 요청 시점에
#   차감되므로 **재요청하면 돈만 나가고 대기열만 길어진다.** 기다렸다 받는 게 유일한 답이다.
#   사람이 붙어서 계속 확인할 수 없으니 도구로 만든다.
#
# ⚠️ 재생성하지 마라. 이미 요청한 잡이 큐에 있다.

set -uo pipefail
cd "$(dirname "$0")/.."

OUT="${HF_WATCH_DIR:-$PWD/projects/ashes-to-stars/art/out_p2_new}"
mkdir -p "$OUT"

check() {
  local list waiting done_n
  list=$(higgsfield generate list 2>/dev/null) || { echo "  ⚠️ 목록 조회 실패"; return 1; }
  waiting=$(echo "$list" | grep -c "waiting" || true)
  echo "  $(date '+%H:%M:%S')  대기 $waiting개"

  # completed + URL이 있는 최근 잡을 받는다. 이미 받은 건 건너뛴다.
  done_n=0
  while read -r id url; do
    [ -z "${url:-}" ] && continue
    local f="$OUT/${id:0:8}.png"
    [ -f "$f" ] && continue
    if curl -sfL "$url" -o "$f" 2>/dev/null; then
      echo "    ⬇ ${id:0:8}.png ($(stat -f%z "$f") bytes)"
      done_n=$((done_n + 1))
    fi
  done < <(echo "$list" | awk '$0 ~ /completed/ {for(i=1;i<=NF;i++) if($i ~ /^https:/) {print $1, $i; break}}' | head -20)

  [ "$done_n" -gt 0 ] && echo "  ✅ 새로 받은 것 ${done_n}장 → $OUT"
  # 대기가 0이면 호출자에게 "끝났다"를 알린다
  [ "$waiting" = "0" ] && return 0 || return 2
}

if [ "${1:-}" = "--loop" ]; then
  echo "🔍 힉스필드 대기 감시 시작 (3분 간격, Ctrl+C로 중단)"
  for i in $(seq 1 60); do          # 최대 3시간
    check && { echo "🎉 대기 잡 전부 처리됨"; exit 0; }
    sleep 180
  done
  echo "⏱ 3시간 경과 — 여전히 대기 중인 잡이 있다"
  exit 2
fi

check
