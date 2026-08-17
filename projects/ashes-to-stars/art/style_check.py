#!/usr/bin/env python3
"""할로우 나이트 화풍 자동 판정.

왜 있나: 화풍을 산문으로만 적으면 검증할 수 없다. 실제로 2026-08-18에 "흑백으로
다시 뽑았다"고 커밋된 시트 10장이 전부 크로마 배경을 잃고 라벨까지 달고 나왔는데,
사람이 하나씩 열어보기 전에는 아무도 몰랐다. 기준은 `STYLE_HOLLOW.md`의 실측 표이고
여기서는 그 표를 코드로 집행한다.

핵심 판정은 **이중 봉우리**다 — 검은 몸(명도 0~0.2)과 창백한 가면(명도>0.8)에만
값이 쏠려야 한다. 평균만 맞추고 중간톤으로 채운 그림은 여기서 떨어진다.

    python3 style_check.py <파일|디렉터리> ...
"""
import colorsys
import os
import sys

from PIL import Image

# STYLE_HOLLOW.md 실측 표와 같은 값이다. 한쪽만 고치지 마라.
MAX_SAT = 0.30
MAX_VAL = 0.40
MIN_DARK = 0.35        # 명도 0~0.2 비율
MIN_GRAY = 0.35        # 채도 0.15 미만 비율
BRIGHT_RANGE = (0.02, 0.15)   # 명도 0.8 초과 비율 — 가면이 있되 뜨지 않는다

# 크로마 배경은 화풍 판정에서 빼야 한다. 안 빼면 마젠타가 채도를 통째로 올려
# 멀쩡한 그림이 떨어진다(2026-08-18에 실제로 이 착각을 한 번 했다).
def _is_chroma(h: float, s: float) -> bool:
    return s > 0.45 and 0.78 < h < 0.96


def measure(path: str) -> dict | None:
    im = Image.open(path).convert("RGBA")
    im.thumbnail((160, 160))
    sats, vals = [], []
    for r, g, b, a in im.getdata():
        if a < 160:
            continue
        h, l, s = colorsys.rgb_to_hls(r / 255, g / 255, b / 255)
        if _is_chroma(h, s):
            continue
        sats.append(s)
        vals.append(l)
    n = len(sats)
    if n < 200:                     # 표본이 없으면 판정 불가지 합격이 아니다
        return None
    return {
        "n": n,
        "sat": sum(sats) / n,
        "val": sum(vals) / n,
        "dark": sum(1 for v in vals if v <= 0.2) / n,
        "gray": sum(1 for s in sats if s < 0.15) / n,
        "bright": sum(1 for v in vals if v > 0.8) / n,
    }


def _is_character(path: str) -> bool:
    """가면 규칙은 **캐릭터에만** 적용된다.

    처음엔 전 자산에 걸었다가 멀쩡한 스킬 아이콘 37장을 "뼈색 가면이 없다"로
    떨어뜨렸다 — 아이콘엔 얼굴이 없으니 당연하다. 규칙을 자산 종류에 맞춰야
    검사기가 신뢰를 얻는다(전부 빨간불이면 아무도 안 본다).
    """
    p = path.replace("\\", "/").lower()
    return ("/sprites/" in p or "portrait" in p or "/frames_" in p
            or "/sheet_" in p or os.path.basename(p).startswith("sheet_"))


def judge(m: dict, path: str = "") -> list[str]:
    bad = []
    if m["sat"] > MAX_SAT:
        bad.append(f"채도 {m['sat']:.2f} > {MAX_SAT} (원색이 섞였다)")
    if m["val"] > MAX_VAL:
        bad.append(f"명도 {m['val']:.2f} > {MAX_VAL} (전체가 밝다 — 몸이 검지 않다)")
    if m["dark"] < MIN_DARK:
        bad.append(f"어두운 비율 {m['dark']:.0%} < {MIN_DARK:.0%} (검은 실루엣이 없다)")
    if m["gray"] < MIN_GRAY:
        bad.append(f"무채색 비율 {m['gray']:.0%} < {MIN_GRAY:.0%}")
    lo, hi = BRIGHT_RANGE
    if m["bright"] < lo and _is_character(path):
        bad.append(f"밝은 점 {m['bright']:.1%} < {lo:.0%} (뼈색 가면이 없다)")
    elif m["bright"] > hi:
        bad.append(f"밝은 점 {m['bright']:.1%} > {hi:.0%} (흰 면이 넓다 — 배경이 흰색일 수 있다)")
    return bad


def walk(target: str):
    if os.path.isfile(target):
        yield target
        return
    for root, _, files in os.walk(target):
        for f in sorted(files):
            if f.lower().endswith((".png", ".jpg", ".jpeg", ".webp")):
                yield os.path.join(root, f)


def main(argv: list[str]) -> int:
    if not argv:
        print(__doc__)
        return 2
    fail = 0
    for target in argv:
        for path in walk(target):
            m = measure(path)
            if m is None:
                print(f"❔ {path} — 불투명 픽셀이 너무 적어 판정 불가")
                fail += 1
                continue
            bad = judge(m, path)
            if bad:
                fail += 1
                print(f"❌ {path}")
                for b in bad:
                    print(f"     {b}")
            else:
                print(f"✅ {path} 채도{m['sat']:.2f} 명도{m['val']:.2f} "
                      f"어둠{m['dark']:.0%} 무채{m['gray']:.0%} 밝음{m['bright']:.1%}")
    print(f"\n불합격 {fail}건")
    return 1 if fail else 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
