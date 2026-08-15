"""
재와 별 — 생성물 자동 검수

왜 자동인가:
  `GAME_MONSTER_ANIM_SPRITE_PROMPTS.md` §9가 검수 7항을 정해두고 **"1·2·6·7은
  자동화 가능하다"**고 이미 적어뒀다. 사람이 32장을 매번 눈으로 보는 것은
  오래 못 간다 — 안 보게 되고, 안 보면 §9는 있으나 마나가 된다.

무엇을 보는가 (전부 실측, 판단은 사람 몫):
  ① 이름     — 코드가 요구하는 프랍 이름과 대조. 어긋나면 "보이지 않는 장애물"이 된다
  ② 마젠타   — 크로마키 잔상. 실루엣에 자주 테두리가 남으면 배경 위에서 티가 난다
  ③ 반투명   — 픽셀아트는 알파가 0 또는 255다. 중간값이 많으면 3D 티가 난다
  ④ 지면조각 — 바닥폭이 몸통폭에 육박하면 프랍 밑에 땅이 딸려온 것이다
  ⑤ 축소가독 — 128px로 줄였을 때 남는 내용. 너무 작으면 화면에서 진흙이 된다
  ⑥ 채도     — 잡몹은 무채색이어야 계열 색조(sr.color 곱셈 틴트)가 산다

사용:
    python3 qc.py out_ai                 # 전체
    python3 qc.py out_ai --json          # 훅에서 쓰는 기계 판독용
    python3 qc.py out_ai --gray          # 무채색이어야 하는 대상(잡몹)
"""
from __future__ import annotations

import argparse
import json
import os
import sys

import numpy as np
from PIL import Image

# 코드가 실제로 요구하는 프랍 이름 — FieldDecor.GetPropNames() + ArenaLayout.
# ⚠️ 여기를 손으로 고치지 말고, 코드가 바뀌면 코드에서 다시 뽑아 맞출 것.
REQUIRED_PROPS = [
    *(f"field_bush_{i}" for i in range(3)),
    *(f"field_rock_{i}" for i in range(3)),
    *(f"field_stump_{i}" for i in range(2)),
    *(f"ash_bone_{i}" for i in range(2)),
    *(f"ash_charred_{i}" for i in range(3)),
    *(f"dungeon_crystal_{i}" for i in range(3)),
    *(f"dungeon_pillar_{i}" for i in range(3)),
    *(f"dungeon_rubble_{i}" for i in range(3)),
    *(f"dungeon_wall_{i}" for i in range(3)),
    *(f"dungeon_cover_{i}" for i in range(3)),
    *(f"estate_crate_{i}" for i in range(2)),
    *(f"estate_barrel_{i}" for i in range(2)),
]

MAGENTA_TOL = 25      # min(R,B) - G 가 이보다 크면 마젠타로 물든 픽셀
SEMI_ALPHA = (16, 239) # 이 사이의 알파는 반투명


def inspect(path: str, want_gray: bool = False) -> dict:
    im = Image.open(path).convert("RGBA")
    a = np.asarray(im).astype(int)
    alpha = a[..., 3]
    vis = a[alpha > 128]
    out = {"name": os.path.splitext(os.path.basename(path))[0],
           "size": f"{im.width}x{im.height}", "flags": []}
    if len(vis) == 0:
        out["flags"].append("빈 이미지")
        return out

    r, g, b = vis[:, 0], vis[:, 1], vis[:, 2]

    # ② 마젠타 잔상
    mag = ((np.minimum(r, b) - g) > MAGENTA_TOL).sum() / len(vis)
    out["magenta"] = round(mag * 100, 1)
    if mag > 0.03:
        out["flags"].append(f"마젠타 잔상 {mag*100:.1f}%")

    # ③ 반투명 — 픽셀아트는 알파가 이진이어야 한다
    semi = ((alpha > SEMI_ALPHA[0]) & (alpha < SEMI_ALPHA[1])).sum()
    solid = (alpha >= SEMI_ALPHA[1]).sum()
    ratio = semi / max(1, solid + semi)
    out["semi_alpha"] = round(ratio * 100, 1)
    if ratio > 0.12:
        out["flags"].append(f"반투명 가장자리 {ratio*100:.1f}%")

    # ④ 지면 조각 — 바닥이 몸통만큼 넓으면 땅이 딸려온 것
    m = alpha > 128
    widths = m.sum(axis=1)
    h = len(widths)
    bot = widths[int(h * 0.88):].max() if h > 8 else 0
    mid = widths[int(h * 0.30):int(h * 0.70)].max() if h > 8 else 1
    out["base_ratio"] = round(bot / max(1, mid), 2)
    if out["base_ratio"] > 0.95:
        out["flags"].append(f"지면 조각 의심 (바닥/몸통 {out['base_ratio']})")

    # ⑤ 축소 가독성 — 실사용 크기에서 내용이 남는가
    small = im.resize((max(1, round(im.width * 128 / im.height)), 128), Image.Resampling.BOX)
    sa = np.asarray(small)[..., 3]
    fill = (sa > 128).sum() / sa.size
    out["fill_128"] = round(fill * 100, 1)
    if fill < 0.06:
        out["flags"].append(f"128px에서 너무 작다 (채움 {fill*100:.1f}%)")

    # ⑥ 채도 — 무채색이어야 하는 대상만
    sat = (vis[:, :3].max(axis=1) - vis[:, :3].min(axis=1)).mean()
    out["saturation"] = round(float(sat), 1)
    if want_gray and sat > 18:
        out["flags"].append(f"무채색이어야 하는데 채도 {sat:.0f}")

    return out


def main(argv=None):
    ap = argparse.ArgumentParser(description="재와 별 생성물 검수")
    ap.add_argument("dir")
    ap.add_argument("--json", action="store_true")
    ap.add_argument("--gray", action="store_true", help="무채색이어야 하는 대상")
    ap.add_argument("--no-name-check", action="store_true")
    ns = ap.parse_args(argv)

    files = sorted(f for f in os.listdir(ns.dir)
                   if f.endswith(".png") and not f.startswith(("_", "compare")))
    rows = [inspect(os.path.join(ns.dir, f), ns.gray) for f in files]

    # ① 이름 대조 — 코드가 요구하는데 없는 것
    have = {r["name"] for r in rows}
    missing = [] if ns.no_name_check else [n for n in REQUIRED_PROPS if n not in have]
    extra = [] if ns.no_name_check else [n for n in have if n not in REQUIRED_PROPS]

    flagged = [r for r in rows if r["flags"]]
    result = {"total": len(rows), "flagged": len(flagged),
              "missing_required": missing, "unknown_names": extra, "rows": flagged}

    if ns.json:
        print(json.dumps(result, ensure_ascii=False))
        return 1 if (flagged or missing) else 0

    print(f"검수 {len(rows)}장 — 지적 {len(flagged)}장")
    for r in flagged:
        print(f"  ⚠️ {r['name']:22} {r['size']:>9}  " + " · ".join(r["flags"]))
    if missing:
        print(f"  ❌ 코드가 요구하는데 없음({len(missing)}): {', '.join(missing)}")
    if extra:
        print(f"  ℹ️ 코드가 안 쓰는 이름({len(extra)}): {', '.join(sorted(extra))}")
    if not flagged and not missing:
        print("  ✅ 지적 없음")
    return 1 if (flagged or missing) else 0


if __name__ == "__main__":
    sys.exit(main())
