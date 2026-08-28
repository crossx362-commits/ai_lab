#!/usr/bin/env python3
"""ox-alpha 영지 레벨 티어(_1/_2) 코드합성 — 채택된 _0 베이스에서 파생.

배경(오너 2026-08-25 "이미지 힉스필드로 하지말고 ox알파로 만들어서"): 영지 8동의 _0은
ox-alpha 코드합성으로 교체(256×256, ~5KB)됐으나 레벨 티어 _1/_2는 옛 나노바나나
(2000px·3~4MB)가 그대로 남아, 레벨 5+에서 건물이 톤·크기가 튀는 딴 화풍으로 바뀌었다
(EstateBuildings.PropOf: lvl 1-4→_0·5-9→_1·10-13→_2). 이 스크립트는 _0을 베이스로
같은 팔레트·같은 256 캔버스에서 「업그레이드로 읽히는」 장식(페넌트 깃발·금빛 첨탑)을
얹어 _1/_2를 만든다 — 파생이라 톤이 자동으로 일치한다.

§6-A 준수: 바닥에 큰 원/고리/초승달 금지(스킬 범위로 읽힘). 장식은 전부 지붕 위 상단에만,
글로우 없음. 티어는 깃발 수·첨탑 높이로만 읽힌다.

사용: python3 gen_oxalpha_estate_tiers.py <building>   (기본: keep)
      python3 gen_oxalpha_estate_tiers.py --self-test
출력: art/out_oxalpha_estate_<building>_1.png, _2.png
"""
from pathlib import Path
import sys

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent

# ox-alpha 영지 팔레트(‑0에서 채취한 계열): 돌 웜그레이·금빛 트림·붉은 깃발
GOLD = (216, 172, 96, 255)
GOLD_DK = (150, 110, 52, 255)
BANNER = (150, 34, 34, 255)
BANNER_DK = (86, 20, 20, 255)
OUTLINE = (40, 24, 20, 255)
OVERLAY_COLORS = {GOLD, GOLD_DK, BANNER, BANNER_DK, OUTLINE}


def validate_output(im, base, label):
    """반입 전 출력 계약: 256 RGBA·투명 여백·팔레트·면적 단조."""
    errors = []
    if im.mode != "RGBA":
        errors.append(f"모드 {im.mode} != RGBA")
    if im.size != (256, 256):
        errors.append(f"크기 {im.size} != 256x256")
    if errors:
        return errors

    pixels = list(im.get_flattened_data())
    base_data = list(base.get_flattened_data())
    base_pixels = set(base_data)
    clear = sum(px[3] == 0 for px in pixels)
    solid = sum(px[3] > 16 for px in pixels)
    base_solid = sum(px[3] > 16 for px in base_data)
    if clear == 0:
        errors.append("투명 여백 0픽셀")
    if solid == 0:
        errors.append("불투명 실체 0픽셀")
    if solid < base_solid:
        errors.append(f"불투명 면적 감소 {solid} < 베이스 {base_solid}")
    unexpected = {px for px in pixels if px not in base_pixels and px not in OVERLAY_COLORS}
    if unexpected:
        errors.append(f"팔레트 밖 신규 색 {len(unexpected)}개")
    return errors


def _opaque_bbox(im):
    px = im.load()
    w, h = im.size
    xs, ys = [], []
    for y in range(h):
        for x in range(w):
            if px[x, y][3] > 16:
                xs.append(x)
                ys.append(y)
    return (min(xs), min(ys), max(xs), max(ys))


def _pole_pennant(d, x, top, height, flag_left):
    """지붕 위에 금빛 장대 + 붉은 삼각 페넌트 하나."""
    # 장대: 얇은 금빛 기둥 (아웃라인 → 금)
    d.line([(x, top), (x, top + height)], fill=OUTLINE, width=3)
    d.line([(x, top), (x, top + height)], fill=GOLD, width=1)
    # 깃발: 장대 위쪽에서 옆으로 뻗는 삼각 페넌트
    fw = 13  # 깃발 폭
    fy = top + 2
    fh = 9
    if flag_left:
        pts = [(x, fy), (x - fw, fy + fh // 2), (x, fy + fh)]
    else:
        pts = [(x, fy), (x + fw, fy + fh // 2), (x, fy + fh)]
    d.polygon(pts, fill=BANNER, outline=OUTLINE)
    # 그림자 결(하단 어둡게)
    if flag_left:
        d.line([(x - fw, fy + fh // 2), (x, fy + fh)], fill=BANNER_DK, width=1)
    else:
        d.line([(x + fw, fy + fh // 2), (x, fy + fh)], fill=BANNER_DK, width=1)


def _finial(d, cx, peak_y, height):
    """지붕 꼭대기 금빛 첨탑 — 티어가 오를수록 높고 화려하다."""
    # 첨탑 기둥
    d.line([(cx, peak_y), (cx, peak_y - height)], fill=OUTLINE, width=4)
    d.line([(cx, peak_y), (cx, peak_y - height)], fill=GOLD_DK, width=2)
    # 꼭대기 금빛 구슬
    r = 4
    top = peak_y - height
    d.ellipse([cx - r, top - r, cx + r, top + r], fill=GOLD, outline=OUTLINE)


def make_tier(base, tier):
    im = base.copy()
    d = ImageDraw.Draw(im)
    x0, y0, x1, y1 = _opaque_bbox(im)
    cx = (x0 + x1) // 2
    peak_y = y0  # 실루엣 최상단 = 지붕 꼭대기
    shoulder_y = y0 + max(6, (y1 - y0) // 6)  # 지붕 어깨(깃발이 서는 높이)
    span = max(20, (x1 - x0) // 3)

    if tier == 1:
        # _1: 어깨 양쪽 페넌트 2개 + 작은 첨탑
        _finial(d, cx, peak_y + 2, 12)
        _pole_pennant(d, cx - span, shoulder_y - 14, 20, flag_left=True)
        _pole_pennant(d, cx + span, shoulder_y - 14, 20, flag_left=False)
    else:
        # _2: 높은 첨탑 + 페넌트 4개(어깨 2 + 처마 2), 금빛 강조 최대
        _finial(d, cx, peak_y + 2, 20)
        _pole_pennant(d, cx - span, shoulder_y - 18, 26, flag_left=True)
        _pole_pennant(d, cx + span, shoulder_y - 18, 26, flag_left=False)
        eaves_y = y0 + (y1 - y0) // 2
        _pole_pennant(d, x0 + 6, eaves_y - 12, 18, flag_left=True)
        _pole_pennant(d, x1 - 6, eaves_y - 12, 18, flag_left=False)
    return im


def self_test():
    """정상 출력과 크기·알파·팔레트 네거티브를 실제 판정한다."""
    base = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    ImageDraw.Draw(base).rectangle([72, 72, 184, 220], fill=(120, 90, 60, 255))
    good = make_tier(base, 1)
    checks = [
        (not validate_output(good, base, "정상"), "정상 티어 통과"),
        (bool(validate_output(good.resize((128, 128)), base, "크기")), "네거티브 크기 탐지"),
        (bool(validate_output(Image.new("RGBA", (256, 256), (1, 2, 3, 255)), base, "알파")),
         "네거티브 투명 여백 탐지"),
    ]
    bad_color = good.copy()
    bad_color.putpixel((0, 0), (1, 255, 1, 255))
    checks.append((bool(validate_output(bad_color, base, "색")), "네거티브 팔레트 탐지"))
    failed = [name for ok, name in checks if not ok]
    for ok, name in checks:
        print(f"{'ok' if ok else 'FAIL'} - {name}")
    return not failed


def main():
    if len(sys.argv) > 1 and sys.argv[1] == "--self-test":
        sys.exit(0 if self_test() else 1)
    building = sys.argv[1] if len(sys.argv) > 1 else "keep"
    src = HERE / f"out_oxalpha_estate_{building}.png"
    if not src.exists():
        print(f"ERR: 베이스 없음 {src}")
        sys.exit(2)
    base = Image.open(src).convert("RGBA")
    if base.size != (256, 256):
        print(f"ERR: 베이스 크기 {base.size} != 256x256")
        sys.exit(2)
    for tier in (1, 2):
        out = HERE / f"out_oxalpha_estate_{building}_{tier}.png"
        image = make_tier(base, tier)
        errors = validate_output(image, base, out.name)
        if errors:
            print(f"ERR: {out.name}: {'; '.join(errors)}")
            sys.exit(2)
        image.save(out)
        print(f"→ {out.name} (256 RGBA·알파·팔레트 PASS)")


if __name__ == "__main__":
    main()
