#!/usr/bin/env python3
"""QA 샷 두 판을 견준다 — **눈에 보이는 차이만 센다**(랩 ㉬, 2026-09-09).

    python3 tools/qa_diff.py <전 폴더> [<후 폴더, 기본 builds/qa>]

**왜 자를 바꿨나**: 「다른 그림 몇 장」으로 세던 옛 자는 **채널차 1짜리 흔들림**까지 「다름」으로
셌다. 그래서 같은 코드·같은 씬으로 두 번 찍어도 50장이 「다르다」고 나왔고, 바닥이 그만큼 부풀어
**「바닥과 같은 얼굴인가」라는 판정 자체가 무뎌졌다.** 실제로 던전·보스 샷의 최대 채널차는 1~3,
즉 **사람 눈에 안 보이는 양자화 잡음**이었다.

그래서 이 자는 두 가지를 나눠 적는다:
  **보이는 차이** — 채널차 `VISIBLE`(8) 넘는 픽셀이 `MIN_PIXELS`(200) 이상인 그림. **이것만 회귀 후보다.**
  **잡음**       — 0은 아니지만 그 아래인 그림. 세되 판정에 쓰지 않는다.

**이 자가 못 보는 것 둘** — 양쪽 끝이 다 샌다(검수 2026-09-09):
  ① **옅고 넓은 변화** — 화면 전체가 1씩 어두워지는 식은 전부 「잡음」으로 떨어진다.
     조명을 만진 랩에서는 이 자만 믿지 말고 **눈으로 봐라**.
  ② **작고 진한 변화** — 1280×720에서 200픽셀은 손톱만 하다. **먼 소품 하나가 사라져도 「0장」이 나온다.**
     소품·배치를 만진 랩에서 「0장」은 「아무것도 안 없어졌다」는 뜻이 **아니다** — 개수를 세는
     게이트나 눈으로 따로 확인해라.
"""
import os
import sys

from PIL import Image, ImageChops

VISIBLE = 8
MIN_PIXELS = 200


def main() -> int:
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    before, after = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "builds/qa"
    visible, noise, missing = [], [], []
    names = [f for f in sorted(os.listdir(after))
             if f.endswith(".png") and not f.endswith("_before.png")]
    for f in names:
        old = os.path.join(before, f)
        if not os.path.exists(old):
            missing.append(f)
            continue
        a = Image.open(old).convert("RGB")
        b = Image.open(os.path.join(after, f)).convert("RGB")
        if a.size != b.size:
            visible.append((f, -1, -1))
            continue
        gray = ImageChops.difference(a, b).convert("L")
        peak = gray.getextrema()[1]
        if peak == 0:
            continue
        big = sum(gray.point(lambda v: 255 if v > VISIBLE else 0).histogram()[255:])
        (visible if big >= MIN_PIXELS else noise).append((f, big, peak))

    print("총 %d장 · **보이는 차이 %d장** · 잡음 %d장 · 짝 없음 %d장"
          % (len(names), len(visible), len(noise), len(missing)))
    for f, big, peak in sorted(visible, key=lambda x: -x[1]):
        print("  보임  %-34s 보이는 픽셀 %7d · 최대 채널차 %d" % (f, big, peak))
    for f, big, peak in sorted(noise, key=lambda x: -x[2])[:5]:
        print("  잡음  %-34s 최대 채널차 %d" % (f, peak))
    for f in missing:
        print("  짝없음 %s" % f)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
