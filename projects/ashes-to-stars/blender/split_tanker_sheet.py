"""
재와 별 — 오너 제공 **방향별** 탱커 시트 분할 (2026-08-13)

    python split_tanker_sheet.py [--apply]

이 시트가 앞선 것들과 다른 점:
  - **8방향 스프라이트**다. 기획서가 "나중에 준다"던 그 자료(GAME_ART_RESOURCES §0-A).
  - 블록 6개(대기·이동·도발·공격·피격·사망)가 한 장에 들어 있고 블록마다 행 수가 다르다.
  - 셀 안에 라벨("정면"·"도발 1")과 방향 표시(N/NE/E…)가 겹쳐 있다 — 시트가 아니라 **문서**에 가깝다.

그래서 격자 자동 검출이 신뢰할 수 없었다(캐릭터 색이 격자선 밝기와 겹친다).
대신 블록 경계를 실측해 상수로 박고 **블록 안에서 균등 분할**한다 —
확대해서 확인한 결과 셀 간격이 균일했다.

⚠️ 해상도: 셀이 약 96×78px이고 그 안 캐릭터는 60~70px이다.
   현재 쓰는 tank 스프라이트는 캐릭터 높이가 150px이므로 **교체하면 해상도가 절반 이하로 떨어진다.**
   방향별을 얻는 대신 선명도를 잃는 거래다 — 판단은 오너 몫이라 기본 동작은 미리보기이고,
   --apply 를 줘야 실제로 교체한다.
"""
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.abspath(os.path.join(
    HERE, "..", "unity", "Assets", "Resources", "sprites", "tanker.png"))
OUT = os.path.abspath(os.path.join(HERE, "..", "unity", "Assets", "Resources", "sprites"))
PREVIEW = os.path.abspath(os.path.join(HERE, "..", "results"))

# 블록 실측 좌표 (x0, y0, x1, y1, 열, 행, 상태이름, 방향라벨)
# 방향 라벨은 시트에 적힌 순서 그대로다. 시트가 8방향을 다 주지 않고
# 블록마다 다른 방향 조합을 준다 — 없는 방향은 나중에 좌우 반전으로 채운다.
BLOCKS = [
    (23, 197, 409, 430, 4, 3, "idle",   ["n", "ne", "e"]),
    (447, 57, 845, 430, 4, 4, "walk",   ["n", "e", "sw", "nw"]),
    (854, 57, 1264, 430, 4, 4, "taunt", ["n", "s", "sw", "nw"]),
    (23, 478, 409, 785, 4, 3, "attack", ["w", "s", "w2"]),
    (447, 478, 845, 785, 4, 3, "hurt",  ["n", "sw", "nw"]),
    (854, 478, 1264, 785, 4, 3, "death", ["a", "b", "c"]),
]

PAD = 2


def cells(im):
    """블록별로 균등 분할해 (상태, 방향, 프레임번호, 이미지)를 뽑는다."""
    out = []
    for x0, y0, x1, y1, cols, rows, state, dirs in BLOCKS:
        cw = (x1 - x0) / cols
        ch = (y1 - y0) / rows
        for r in range(rows):
            for c in range(cols):
                box = (int(round(x0 + c * cw)) + PAD, int(round(y0 + r * ch)) + PAD,
                       int(round(x0 + (c + 1) * cw)) - PAD, int(round(y0 + (r + 1) * ch)) - PAD)
                out.append((state, dirs[r] if r < len(dirs) else f"r{r}", c, im.crop(box)))
    return out


def main():
    apply_it = "--apply" in sys.argv
    im = Image.open(SRC).convert("RGBA")
    print(f"원본 {im.size}")

    got = cells(im)
    print(f"셀 {len(got)}장")

    # 미리보기 — 자른 결과를 원본 배치대로 이어 붙여 눈으로 확인한다.
    # 이 단계를 건너뛰면 행이 밀려도 알 수 없다(이 저장소가 실제로 겪은 실패다).
    os.makedirs(PREVIEW, exist_ok=True)
    tw, th = got[0][3].size
    per = 16
    sheet = Image.new("RGBA", (tw * per, th * ((len(got) + per - 1) // per)), (40, 44, 52, 255))
    for i, (_, _, _, img) in enumerate(got):
        sheet.paste(img, ((i % per) * tw, (i // per) * th))
    p = os.path.join(PREVIEW, "check_tanker_split.png")
    sheet.save(p)
    print(f"미리보기 → {p}")

    counts = {}
    for state, d, c, _ in got:
        counts[state] = counts.get(state, 0) + 1
    print("상태별 셀 수:", counts)

    if not apply_it:
        print("\n(미리보기만 했다. 실제 교체는 --apply)")
        return

    d = os.path.join(OUT, "tank_dir")
    os.makedirs(d, exist_ok=True)
    for state, dr, c, img in got:
        img.save(os.path.join(d, f"tank_{state}_{dr}_{c:02d}.png"))
    print(f"\n{len(got)}장 → {d}")


if __name__ == "__main__":
    main()
