"""
재와 별 — 오너 제공 스프라이트 시트를 프레임 단위로 분할

    python 시트_분할.py

무엇을 하는가:
  오너가 준 시트(격자에 캐릭터가 배치된 한 장)를 읽어
  ① 격자선을 자동 감지해 셀로 나누고
  ② 셀 안에서 배경(거의 검정)이 아닌 덩어리를 찾아 개별 프레임으로 잘라
  ③ 투명 배경 PNG로 저장한다.

왜 자동 감지인가:
  좌표를 손으로 박으면 시트가 조금만 달라져도 전부 어긋난다.
  격자선과 배경색은 규칙적이므로 코드가 찾게 하는 편이 안전하다.

출력: ../unity/Assets/_Game/Art/Sprites/<직업>/<직업>_<상태>_<번호>.png
"""
import os
import sys
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "원본시트")
OUT = os.path.abspath(os.path.join(HERE, "..", "unity", "Assets", "_Game", "Art", "Sprites"))

# 배경은 "어두운 색"이 아니라 **특정 색**이다.
# 실측(2026-08-13): 시트 배경 = 밝기 13 안팎의 짙은 회색.
# 반면 캐릭터 외곽선은 밝기 0~4로 **배경보다 더 어둡다** —
# 그래서 "어두우면 배경"이라는 임계값 방식은 원리적으로 틀렸고,
# 실제로 캐릭터가 조각조각 부서졌다. 배경색과 직접 비교해야 한다.
BG_TOL = 10          # 배경색 기준 채널별 허용 오차
BG_MAX = 42          # (격자선 판정 등 보조 용도로만 남김)
LINE_MIN_RATIO = 0.80  # 한 줄의 80% 이상이 격자선 색이면 격자선
GAP_MIN = 6          # 셀 안에서 이만큼 연속으로 비면 프레임 경계
PAD = 2              # 잘라낸 뒤 남길 여백(px)

SHEETS = {
    "시트3_캐릭터디자인.png": {          # 실제 내용: 기본형 6상태 모션표
        "직업": ["탱커", "딜러", "힐러", "버퍼"],
        "상태": ["대기", "이동", "공격", "특수", "피격", "사망"],
        # 칸마다 프레임이 몇 개인지 시트를 보고 적는다.
        # 간격 자동 감지는 공격(칼 궤적이 두 포즈를 잇는다)에서 1개로 뭉치고
        # 사망(떨어진 무기가 떨어져 있다)에서 2개로 갈렸다 — 눈으로 센 값이 정확하다.
        "프레임수": {"대기": 1, "이동": 2, "공격": 2, "특수": 1, "피격": 1, "사망": 1},
    },
}


def brightness(px):
    return (px[0] * 299 + px[1] * 587 + px[2] * 114) // 1000


def find_lines(im, axis):
    """격자선 위치를 찾는다. axis=0이면 세로선(x좌표), 1이면 가로선(y좌표)."""
    w, h = im.size
    p = im.load()
    lines = []
    n = w if axis == 0 else h
    m = h if axis == 0 else w
    for i in range(n):
        lit = 0
        for j in range(0, m, 3):                     # 3px 간격 표본
            px = p[i, j] if axis == 0 else p[j, i]
            b = brightness(px)
            if 45 <= b <= 130:                       # 격자선은 배경보다 밝고 캐릭터보다 어둡다
                lit += 1
        if lit / (m / 3) >= LINE_MIN_RATIO:
            lines.append(i)
    # 연속된 좌표를 하나로 뭉친다
    merged = []
    for v in lines:
        if merged and v - merged[-1][-1] <= 2:
            merged[-1].append(v)
        else:
            merged.append([v])
    return [sum(g) // len(g) for g in merged]


def sample_bg(im, x0, y0, x1, y1):
    """
    칸 모서리에서 배경색들을 뽑는다 — **여러 개**다.
    시트에는 옅은 격자 무늬(모눈종이)가 깔려 있어 배경이 두 가지 색이다.
    하나만 잡으면 격자가 스프라이트에 남는다(실제로 남았다).
    """
    from collections import Counter
    p = im.load()
    c = Counter()
    for x in range(x0, min(x0 + 20, x1)):
        for y in range(y0, min(y0 + 20, y1)):
            c[p[x, y]] += 1
    if not c:
        return [(13, 13, 15)]
    total = sum(c.values())
    return [col for col, n in c.most_common(6) if n / total >= 0.03]


def is_bg(c, refs):
    if isinstance(refs, tuple):
        refs = [refs]
    for r in refs:
        if (abs(c[0] - r[0]) <= BG_TOL and
                abs(c[1] - r[1]) <= BG_TOL and
                abs(c[2] - r[2]) <= BG_TOL):
            return True
    return False


def content_bbox(im, x0, y0, x1, y1, ref=None):
    """영역 안에서 배경이 아닌 픽셀의 경계 상자"""
    p = im.load()
    if ref is None:
        ref = sample_bg(im, x0, y0, x1, y1)
    minx, miny, maxx, maxy = x1, y1, x0, y0
    found = False
    for y in range(y0, y1):
        for x in range(x0, x1):
            if not is_bg(p[x, y], ref):
                found = True
                if x < minx: minx = x
                if x > maxx: maxx = x
                if y < miny: miny = y
                if y > maxy: maxy = y
    return (minx, miny, maxx, maxy) if found else None


def split_frames(im, x0, y0, x1, y1):
    """셀 안을 세로 방향으로 훑어 빈 열이 이어지는 지점에서 프레임을 나눈다"""
    p = im.load()
    cols = []
    for x in range(x0, x1):
        has = any(brightness(p[x, y]) > BG_MAX for y in range(y0, y1, 2))
        cols.append(has)

    frames, run_start = [], None
    gap = 0
    for i, has in enumerate(cols):
        if has:
            if run_start is None:
                run_start = i
            gap = 0
        else:
            if run_start is not None:
                gap += 1
                if gap >= GAP_MIN:
                    frames.append((x0 + run_start, x0 + i - gap + 1))
                    run_start = None
                    gap = 0
    if run_start is not None:
        frames.append((x0 + run_start, x1))
    return [f for f in frames if f[1] - f[0] >= 12]      # 너무 좁은 조각은 노이즈


def make_transparent(crop):
    """
    배경만 알파 0으로 만든다 — **가장자리에서 번지는(flood fill) 방식**.

    처음엔 "어두우면 배경"이라는 임계값으로 지웠는데, 픽셀아트는 캐릭터에
    **검은 외곽선과 그림자**가 있어서 그것까지 같이 날아갔다(실제로 스프라이트가
    조각조각 부서져 보였다). 배경은 가장자리와 이어져 있다는 성질을 쓰면
    안쪽의 검은 픽셀은 건드리지 않는다.
    """
    crop = crop.convert("RGBA")
    px = crop.load()
    w, h = crop.size
    refs = sample_bg(crop.convert("RGB"), 0, 0, w, h)
    if px[0, 0][:3] not in refs:
        refs.append(px[0, 0][:3])

    seen = bytearray(w * h)
    stack = []
    for x in range(w):
        for y in (0, h - 1):
            if is_bg(px[x, y][:3], refs):
                stack.append((x, y))
    for y in range(h):
        for x in (0, w - 1):
            if is_bg(px[x, y][:3], refs):
                stack.append((x, y))

    while stack:
        x, y = stack.pop()
        i = y * w + x
        if seen[i]:
            continue
        seen[i] = 1
        px[x, y] = (0, 0, 0, 0)
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < w and 0 <= ny < h and not seen[ny * w + nx]:
                if is_bg(px[nx, ny][:3], refs):
                    stack.append((nx, ny))
    return crop


def process(path, spec):
    im = Image.open(path).convert("RGB")
    w, h = im.size
    print(f"\n[시트] {os.path.basename(path)}  {w}x{h}")

    xs = find_lines(im, 0)
    ys = find_lines(im, 1)
    print(f"  격자선: 세로 {len(xs)}개 / 가로 {len(ys)}개")

    직업들, 상태들 = spec["직업"], spec["상태"]

    # 격자선은 **내부 경계**만 잡힌다. 바깥 테두리를 더해야 칸이 완성된다.
    #   열: 라벨 | 대기 | 이동 | ... | 사망  → 7칸이면 내부선 6개 + 양 끝 2개
    xs = [0] + xs + [w]
    ys = [0] + ys + [h]
    need_cols = len(상태들) + 2          # 라벨 열 + 상태 열들 + 오른쪽 끝
    need_rows = len(직업들) + 2          # 헤더 행 + 직업 행들 + 아래 끝
    if len(xs) < need_cols or len(ys) < need_rows:
        print(f"  ✗ 격자 감지 실패 — 행 경계 {len(ys)}/{need_rows}, 열 경계 {len(xs)}/{need_cols}")
        return 0

    # 첫 칸(헤더 행·라벨 열)은 건너뛴다
    xs = xs[1:]
    ys = ys[1:]

    total = 0
    for r, job in enumerate(직업들):
        y0, y1 = ys[r] + 2, ys[r + 1] - 2
        d = os.path.join(OUT, job)
        os.makedirs(d, exist_ok=True)
        for c, state in enumerate(상태들):
            x0, x1 = xs[c] + 2, xs[c + 1] - 2
            if x1 <= x0 or y1 <= y0:
                continue
            n = spec["프레임수"].get(state, 1)
            cell = content_bbox(im, x0, y0, x1, y1)
            if cell is None:
                continue
            cx0, cy0, cx1, cy1 = cell
            span = (cx1 - cx0 + 1) / n
            for k in range(n):
                sx0 = int(cx0 + span * k)
                sx1 = int(cx0 + span * (k + 1))
                bb = content_bbox(im, sx0, cy0, min(sx1 + 1, x1), cy1 + 1)
                if bb is None:
                    continue
                bx0, by0, bx1, by1 = bb
                crop = im.crop((max(0, bx0 - PAD), max(0, by0 - PAD),
                                min(w, bx1 + PAD + 1), min(h, by1 + PAD + 1)))
                out = os.path.join(d, f"{job}_{state}_{k:02d}.png")
                make_transparent(crop).save(out)
                total += 1
        print(f"  {job}: 저장 완료")
    return total


def main():
    if not os.path.isdir(SRC):
        print(f"원본시트 폴더가 없다: {SRC}")
        return 1
    grand = 0
    for name, spec in SHEETS.items():
        p = os.path.join(SRC, name)
        if not os.path.isfile(p):
            print(f"건너뜀(없음): {name}")
            continue
        grand += process(p, spec)
    print(f"\n[분할] 총 {grand}장 → {OUT}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
