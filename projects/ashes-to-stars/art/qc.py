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

    # ④ ~~지면 조각~~ — **폐기(2026-08-14)**. 지표는 남기되 판정에는 쓰지 않는다.
    #
    #   알려진 불량 표본(지면 조각이 눈에 보였던 첫 세대)으로 판별력을 재보니:
    #     불량 _old_charred 0.61 · _old_bush 0.60
    #     정상 dungeon_pillar_1 1.03 · pillar_2 1.41 · wall_0 0.42 · barrel_0 0.83
    #   **불량이 정상보다 오히려 낮다.** 분포가 완전히 겹쳐 임계 조정으로 살릴 수 없다.
    #   실제로 이 지표가 낸 지적 2건은 전부 오탐이었다(기둥 받침대는 기둥의 일부다).
    #
    #   내가 이걸 네거티브 컨트롤 없이 넣은 것이 잘못이다. 오탐만 내는 검수 항목은
    #   없느니만 못하다 — 사람이 경고를 무시하기 시작하면 나머지 항목까지 같이 죽는다.
    #   지면 조각을 정말 잡으려면 폭이 아니라 **색**(본체와 다른 중성 지면색이 바닥에
    #   얇게 퍼짐)을 봐야 하고, 그건 표본을 더 모은 뒤에 검증하고 넣을 것.
    m = alpha > 128
    widths = m.sum(axis=1)
    h = len(widths)
    bot = widths[int(h * 0.88):].max() if h > 8 else 0
    mid = widths[int(h * 0.30):int(h * 0.70)].max() if h > 8 else 1
    out["base_ratio"] = round(bot / max(1, mid), 2)   # 참고값으로만 남긴다

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

    # 콘택트 시트·비교본은 에셋이 아니다. 접두 목록으로 막았더니 `sheet_all32`가 새어
    # 들어와 "지면 조각 의심"으로 잡혔다(2026-08-14) — 산출물 이름이 늘 때마다 이 목록도
    # 늘어야 하는 구조 자체가 약하다. 그래서 **요구 목록에 없는 이름은 에셋으로 안 본다**.
    files = sorted(f for f in os.listdir(ns.dir)
                   if f.endswith(".png") and not f.startswith("_")
                   and (ns.no_name_check or os.path.splitext(f)[0] in REQUIRED_PROPS))
    rows = [inspect(os.path.join(ns.dir, f), ns.gray) for f in files]

    # ① 이름 대조 — 코드가 요구하는데 없는 것
    have = {r["name"] for r in rows}
    missing = [] if ns.no_name_check else [n for n in REQUIRED_PROPS if n not in have]
    extra = [] if ns.no_name_check else [n for n in have if n not in REQUIRED_PROPS]

    # ⑦ 상대 크기 — 한 장 안에서는 절대 안 보이는 결함이다.
    #    32종을 전부 128px로 뽑았더니 바위가 사람만 했다(2026-08-14). 검수 항목이
    #    전부 "이미지 한 장 안"을 보게 설계돼 있어 이걸 통째로 놓쳤다.
    #    목표 크기가 선언되지 않은 프랍은 화면에서 크기가 사고로 정해진다.
    scale_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "prop_scale.json")
    no_scale = []
    if os.path.exists(scale_path) and not ns.no_name_check:
        with open(scale_path, encoding="utf-8") as f:
            scale = {k: v for k, v in json.load(f).items() if not k.startswith("_")}
        no_scale = [n for n in sorted(have) if n not in scale]

    flagged = [r for r in rows if r["flags"]]
    result = {"total": len(rows), "flagged": len(flagged),
              "missing_required": missing, "unknown_names": extra,
              "no_scale": no_scale, "rows": flagged}

    if ns.json:
        print(json.dumps(result, ensure_ascii=False))
        return 1 if (flagged or missing or no_scale) else 0

    print(f"검수 {len(rows)}장 — 지적 {len(flagged)}장")
    for r in flagged:
        print(f"  ⚠️ {r['name']:22} {r['size']:>9}  " + " · ".join(r["flags"]))
    if missing:
        print(f"  ❌ 코드가 요구하는데 없음({len(missing)}): {', '.join(missing)}")
    if extra:
        print(f"  ℹ️ 코드가 안 쓰는 이름({len(extra)}): {', '.join(sorted(extra))}")
    if no_scale:
        print(f"  ❌ 목표 크기 미선언({len(no_scale)}): {', '.join(no_scale)}"
              " — prop_scale.json에 넣지 않으면 화면 크기가 사고로 정해진다")
    if not flagged and not missing and not no_scale:
        print("  ✅ 지적 없음")
    return 1 if (flagged or missing or no_scale) else 0


if __name__ == "__main__":
    sys.exit(main())
