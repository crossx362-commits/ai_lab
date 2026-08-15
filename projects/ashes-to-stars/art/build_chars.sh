#!/bin/bash
# 재와 별 — 캐릭터 4직업 13프레임 생성·분할·정렬·반입 파이프라인
#
# 13프레임 = 6셀 시트 A(idle·walk2·attack2·special) + 6셀 시트 B(dash4·hurt·death)
#            + 단독 C(invuln)
#
# 2026-08-15 실측으로 확정된 순서다. 각 단계가 왜 있는지:
#   ① wipe_gridlines — 프롬프트로 "no grid lines"를 넣어도 모델이 격자선을 그린다.
#      그 선이 마젠타를 가로지르면 자동 격자 검출이 실패한다("자동 검출 1셀")
#   ② split_ai_sheet — 고정 격자(--cols/--rows)를 쓰지 않는다. AI가 요청한 행·열을
#      안 지킨다(3×2를 요청했는데 3×3으로 나온 실측). 자동 검출이 유일하게 안전하다
#   ③ align_frames — 시트별로 따로 크롭하면 같은 시트 안에서만 캔버스가 같아져
#      애니메이션이 튄다(P2 몹이 walk_01→walk_02에서 33px 점프한 사고)
#
# 사용: ./build_chars.sh [직업...]   (기본: 4직업 전부)
set -euo pipefail
cd "$(dirname "$0")"

JOBS=("${@:-tank dps healer buffer}")
[ $# -eq 0 ] && JOBS=(tank dps healer buffer)

A_FRAMES=(idle_00 walk_00 walk_01 attack_00 attack_01 special_00)
B_FRAMES=(dash_00 dash_01 dash_02 dash_03 hurt_00 death_00)

for job in "${JOBS[@]}"; do
  echo "═══ $job ═══"
  spec="spec_char_${job}.json"
  [ -f "$spec" ] || { echo "  ⚠️ $spec 없음 — 건너뜀"; continue; }

  # 1. 생성 (힉스필드 고정 — aigen.py 기본값)
  python3 aigen.py --spec "$spec" --out-dir out_char

  # 2. 격자선 제거
  python3 wipe_gridlines.py out_char/char_${job}_A.png out_char/char_${job}_B.png

  # 3. 분할 (자동 격자 검출)
  dest="../unity/Assets/Resources/sprites/${job}"
  mkdir -p "$dest"
  python3 split_ai_sheet.py "out_char/char_${job}_A.png" \
      --names "${A_FRAMES[@]}" --out-dir "$dest" --prefix "$job" --height 128
  python3 split_ai_sheet.py "out_char/char_${job}_B.png" \
      --names "${B_FRAMES[@]}" --out-dir "$dest" --prefix "$job" --height 128
  # C는 단독 프레임이라 분할하지 않는다
  python3 split_ai_sheet.py "out_char/char_${job}_C.png" \
      --names invuln_00 --out-dir "$dest" --prefix "$job" --height 128

  # 4. 캔버스·발 정렬 (13프레임 전체를 한 번에 — 시트 경계를 넘어 통일해야 한다)
  python3 align_frames.py "$dest"
done

echo
echo "═══ 검증 ═══"
python3 ../../ai-team/skills/마루_게임개발/tools/game_asset_names.py 2>/dev/null \
  || python3 ../../../projects/ai-team/skills/마루_게임개발/tools/game_asset_names.py
