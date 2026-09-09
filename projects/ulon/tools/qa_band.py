#!/usr/bin/env python3
"""**띠 대비를 화면에서 잰다** — 산 사면의 어두운 톤과 밝은 톤이 얼마나 벌어져 있나(2026-09-09).

`DarkCliffAt`은 마스크(어두운 바위가 차지하는 몫)를 만들고, 화면에 나오는 것은 그 마스크가
섞은 **두 텍스처의 밝기 차**다. 마스크 분포를 재는 자(`OutdoorCensus.CliffChunks`)는 덩이가
**몇 개로 읽히나**를 말하지 **얼마나 세게 갈리나**를 말하지 않는다 — 그래서 이 자가 따로 있다.

**무엇을 세는지 먼저 밝힌다**(이 자의 한계):
- 사면 픽셀을 **색으로 고른다**: 초록 우세(g>r+16)도 파랑 우세(b>r+16)도 아닌 것 = 흙·바위 톤.
- 영역은 기본 **화면 위쪽 0~50%**(그 아래는 잔디밭과 바위 소품이라 사면이 아니다).
- 그래서 회색 바위 소품·몹 갑옷이 띠에 걸리면 같이 센다. **절대값이 아니라 전후 비교**로 쓴다.

**대비**: 고른 픽셀의 밝기(0~255)를 오츠 문턱으로 두 무리로 가르고 **두 무리의 평균 차**를 낸다.
문턱 자체가 아니라 평균 차를 쓰는 이유는, 나누는 폭을 넓히면 문턱은 그대로여도 중간톤이 늘어
**두 무리의 평균이 서로 다가오기** 때문이다 — 그게 「대비가 낮아졌다」의 뜻이다.

쓰는 법:
    python3 tools/qa_band.py builds/qa/03_hunt_mobs.png builds/qa/16_mountain_ridge.png
    python3 tools/qa_band.py --band 0.0 0.5 <png...>
"""
import sys

sys.path.insert(0, __file__.rsplit("/", 1)[0])
from qa_sky import read_png  # 같은 PNG 해독기를 쓴다(두 벌 두면 갈라진다)


def slope_hist(path, top=0.0, bottom=0.50):
    w, h, ch, rows = read_png(path)
    y0, y1 = int(h * top), int(h * bottom)
    hist = [0] * 256
    n = 0
    for y in range(y0, y1):
        row = rows[y]
        for x in range(w):
            r, g, b = row[x * ch], row[x * ch + 1], row[x * ch + 2]
            if g > r + 16 or b > r + 16:
                continue                      # 잔디·하늘은 사면이 아니다
            lum = (r * 299 + g * 587 + b * 114) // 1000
            hist[lum] += 1
            n += 1
    return hist, n, w * (y1 - y0)


def otsu(hist):
    total = sum(hist)
    if total == 0:
        return 0, 0.0, 0.0, 0.0
    s_all = sum(i * hist[i] for i in range(256))
    best_t, best_var = 0, -1.0
    w0 = 0
    s0 = 0
    for t in range(256):
        w0 += hist[t]
        s0 += t * hist[t]
        w1 = total - w0
        if w0 == 0 or w1 == 0:
            continue
        m0 = s0 / w0
        m1 = (s_all - s0) / w1
        var = w0 * w1 * (m0 - m1) * (m0 - m1)
        if var > best_var:
            best_var, best_t = var, t
    w0 = sum(hist[:best_t + 1])
    m0 = sum(i * hist[i] for i in range(best_t + 1)) / max(w0, 1)
    w1 = total - w0
    m1 = sum(i * hist[i] for i in range(best_t + 1, 256)) / max(w1, 1)
    return best_t, m0, m1, w0 / total


# **샷마다 사면이 있는 자리가 다르다** — 띠를 그때그때 손으로 주면 전후 비교가 흔들린다.
# 그래서 자리를 여기 원장으로 박는다(화면을 보고 잡았다: `03`은 산이 위쪽, `16`은 눈이 산 위라 아래쪽).
BANDS = {
    "03_hunt_mobs": (0.00, 0.42),
    "16_mountain_ridge": (0.35, 0.95),
}


if __name__ == "__main__":
    args, top, bot = [], None, None
    it = iter(range(1, len(sys.argv)))
    for i in it:
        a = sys.argv[i]
        if a == "--band":
            top, bot = float(sys.argv[i + 1]), float(sys.argv[i + 2])
            next(it); next(it)
        elif not a.startswith("--"):
            args.append(a)
    for p in args:
        name = p.split("/")[-1].rsplit(".", 1)[0]
        t0, b0 = (top, bot) if top is not None else BANDS.get(name, (0.0, 0.50))
        hist, n, total = slope_hist(p, t0, b0)
        t, m0, m1, share_dark = otsu(hist)
        print("%-26s 띠 %.2f~%.2f · 사면 픽셀 %7d/%7d (%4.1f%%) · 문턱 %3d · 어두운 %5.1f / 밝은 %5.1f · "
              "**대비 %5.1f** · 어두운 몫 %4.1f%%"
              % (p.split("/")[-1], t0, b0, n, total, 100.0 * n / max(total, 1), t, m0, m1, m1 - m0,
                 100.0 * share_dark))
