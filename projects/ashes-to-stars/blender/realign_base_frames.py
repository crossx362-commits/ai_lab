"""
재와 별 — 기본 모션 스프라이트를 **정렬을 보존해** 다시 자른다.

    python realign_base_frames.py

왜 다시 자르나 (2026-08-13 오너 지적: "기본 애니메이션도 위치 잘 안 맞게 잘려있음"):
  1차 분할은 프레임마다 내용물 바운딩 박스로 **타이트하게** 잘랐다. 그 결과 같은
  직업인데도 idle 129×184, walk_00 132×169, walk_01 132×201 처럼 캔버스 크기가
  제각각이 됐다. 유니티 pivot은 비율(0~1)이라 캔버스가 다르면 기준점도 따라 움직인다
  → 재생하면 캐릭터가 프레임마다 위아래로 튄다.

  원본 시트는 캐릭터들의 **발밑 기준선이 행마다 거의 일정**하게 그려져 있다.
  타이트 크롭은 바로 그 정보를 버린다. 그래서 여기서는

      전 직업·전 상태 **공통 캔버스**에, **가로 중앙 · 세로 바닥** 정렬로 배치한다.

  모든 프레임의 크기와 발 위치가 같아지므로 애니메이션이 흔들리지 않고,
  PPU 하나로 전 직업 크기를 통일할 수 있다(캐릭터가 몹보다 2배 크던 문제도 여기서 잡힌다).

배경 처리는 1차 분할에서 이미 검증된 원칙을 그대로 쓴다:
  - 배경은 "어두운 색"이 아니라 **특정 색**이다. 시트 배경은 밝기 13 안팎인데
    캐릭터 외곽선은 0~4로 **더 어둡다**. 임계값 방식은 원리적으로 틀렸다.
  - 시트에 옅은 모눈 무늬가 깔려 있어 배경색이 **여러 개**다. 하나만 잡으면 격자가 남는다.
  - 투명화는 가장자리에서 번지는 방식(flood fill) — 안쪽의 검은 외곽선을 지키기 위해서다.

출력: ../unity/Assets/Resources/sprites/<role>/<role>_<state>_<nn>.png
      ⚠️ Resources 밖에 두면 Resources.Load가 못 읽어 화면에 안 나온다(실제 사고).
"""
import os
from collections import Counter, deque

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "source_sheets", "sheet3_character_design.png")
OUT = os.path.abspath(os.path.join(HERE, "..", "unity", "Assets", "Resources", "sprites"))

# 배경색 기준 채널별 허용 오차.
# 실측(2026-08-13): 셀 배경은 (10,15,18)인데 모눈 무늬가 (20,26,29)라 오차 10으로는
# **1 차이로** 배경 판정에 실패해 격자가 스프라이트에 남았다. 14로 올려 덮는다.
# 투명화는 가장자리 flood fill이라 값을 올려도 캐릭터 **안쪽** 검정은 지워지지 않는다
# (캐릭터 외곽선 (0,0,0)은 g·b 채널 차이가 15·18이라 어차피 배경으로 안 잡힌다).
BG_TOL = 14
BG_MAX = 42          # 프레임 분리용 보조 임계
GAP_MIN = 6          # 셀 안에서 이만큼 연속으로 비면 프레임 경계
PAD = 3              # 공통 캔버스 여백(px)

# 격자선 실측값(1536×1024). 자동 감지 결과와 일치한다.
#   세로선 169 299 559 866 1154 1307 / 가로선 61 294 524 748
ROWS = [("tank", 61, 294), ("dps", 294, 524), ("healer", 524, 748), ("buffer", 748, 985)]
# (상태, x0, x1, 기대 프레임 수) — 1열(169까지)은 직업 라벨이라 제외한다
COLS = [
    ("idle", 169, 299, 1),
    ("walk", 299, 559, 2),
    ("attack", 559, 866, 2),
    ("special", 866, 1154, 1),
    ("hurt", 1154, 1307, 1),
    ("death", 1307, 1536, 1),
]


def brightness(px):
    return (px[0] * 299 + px[1] * 587 + px[2] * 114) // 1000


def sample_bg(im, x0, y0, x1, y1):
    """칸 모서리에서 배경색들을 뽑는다 — 모눈 무늬 때문에 여러 개다."""
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
    for r in refs:
        if (abs(c[0] - r[0]) <= BG_TOL and abs(c[1] - r[1]) <= BG_TOL
                and abs(c[2] - r[2]) <= BG_TOL):
            return True
    return False


def col_density(im, x0, y0, x1, y1, refs):
    """열마다 '배경이 아닌 픽셀 수'. 프레임 경계를 찾는 근거가 된다."""
    p = im.load()
    return [sum(1 for y in range(y0, y1, 2) if not is_bg(p[x, y], refs)) for x in range(x0, x1)]


def split_frames(im, x0, y0, x1, y1, want, refs):
    """
    셀 안을 세로로 훑어 프레임을 나눈다.

    1차: 완전히 빈 열이 GAP_MIN 이상 이어지는 지점에서 자른다.
    2차(폴백): 그래도 want개가 안 나오면 **골짜기 분할**을 쓴다.
      캐릭터 사이에 잔상·먼지 이펙트가 깔려 있으면 '완전히 빈 열'이 아예 없어서
      1차가 통째로 한 덩어리로 잡는다(실제로 walk·attack이 그랬다).
      기대 프레임 수를 알고 있으므로, 균등 분할 지점 근처에서 내용이 가장 옅은
      열을 찾아 자르면 이펙트를 보존하면서 프레임만 가를 수 있다.
    """
    dens = col_density(im, x0, y0, x1, y1, refs)

    frames, run, gap = [], None, 0
    for i, d in enumerate(dens):
        if d > 0:
            if run is None:
                run = i
            gap = 0
        elif run is not None:
            gap += 1
            if gap >= GAP_MIN:
                frames.append((x0 + run, x0 + i - gap + 1))
                run, gap = None, 0
    if run is not None:
        frames.append((x0 + run, x1))
    frames = [f for f in frames if f[1] - f[0] >= 12]

    if len(frames) > want:
        frames = sorted(sorted(frames, key=lambda f: f[0] - f[1])[:want])

    if len(frames) == want or want == 1:
        return frames

    # ── 폴백: 가장 넓은 덩어리를 골짜기에서 want개로 가른다 ──
    if not frames:
        return []
    a, b = max(frames, key=lambda f: f[1] - f[0])
    span = b - a
    cuts = []
    for k in range(1, want):
        ideal = a + span * k // want
        lo, hi = max(a + 8, ideal - span // 8), min(b - 8, ideal + span // 8)
        if lo >= hi:
            cuts.append(ideal)
            continue
        cuts.append(min(range(lo, hi), key=lambda x: dens[x - x0]))

    out, prev = [], a
    for c in sorted(cuts):
        out.append((prev, c))
        prev = c
    out.append((prev, b))
    return [f for f in out if f[1] - f[0] >= 12]


def content_bbox(im, x0, y0, x1, y1, refs):
    p = im.load()
    minx, miny, maxx, maxy = x1, y1, x0, y0
    found = False
    for y in range(y0, y1):
        for x in range(x0, x1):
            if not is_bg(p[x, y], refs):
                found = True
                if x < minx: minx = x
                if x > maxx: maxx = x
                if y < miny: miny = y
                if y > maxy: maxy = y
    return (minx, miny, maxx + 1, maxy + 1) if found else None


def cut_transparent(im, box, refs):
    """box를 잘라내고 **가장자리와 이어진** 배경만 알파 0으로 만든다."""
    crop = im.crop(box).convert("RGBA")
    w, h = crop.size
    px = crop.load()

    seen = [[False] * h for _ in range(w)]
    q = deque()
    for x in range(w):
        for y in (0, h - 1):
            if not seen[x][y] and is_bg(px[x, y][:3], refs):
                seen[x][y] = True
                q.append((x, y))
    for y in range(h):
        for x in (0, w - 1):
            if not seen[x][y] and is_bg(px[x, y][:3], refs):
                seen[x][y] = True
                q.append((x, y))

    while q:
        x, y = q.popleft()
        px[x, y] = (0, 0, 0, 0)
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < w and 0 <= ny < h and not seen[nx][ny] and is_bg(px[nx, ny][:3], refs):
                seen[nx][ny] = True
                q.append((nx, ny))
    return crop


def main():
    im = Image.open(SRC).convert("RGB")

    # ── 1단계: 모든 프레임의 위치를 먼저 조사한다 (아직 자르지 않는다) ──
    # 공통 캔버스를 정하려면 전체를 다 봐야 하기 때문이다.
    plan = []
    for role, ry0, ry1 in ROWS:
        for state, cx0, cx1, want in COLS:
            refs = sample_bg(im, cx0, ry0, cx1, ry1)
            for idx, (fx0, fx1) in enumerate(split_frames(im, cx0, ry0, cx1, ry1, want, refs)):
                bb = content_bbox(im, fx0, ry0, fx1, ry1, refs)
                if bb:
                    plan.append((role, state, idx, bb, refs))

    if not plan:
        raise SystemExit("프레임을 하나도 찾지 못했다 — 격자 좌표를 확인할 것")

    cw = max(b[2] - b[0] for _, _, _, b, _ in plan) + PAD * 2
    ch = max(b[3] - b[1] for _, _, _, b, _ in plan) + PAD * 2
    print(f"공통 캔버스 {cw}×{ch}  (프레임 {len(plan)}장)")

    # ── 2단계: 공통 캔버스에 가로 중앙 · 세로 바닥으로 배치 ──
    made = {}
    for role, state, idx, bb, refs in plan:
        sprite = cut_transparent(im, bb, refs)
        canvas = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
        x = (cw - sprite.width) // 2                 # 가로: 중앙
        y = ch - PAD - sprite.height                 # 세로: 바닥 (발이 기준선에 붙는다)
        canvas.paste(sprite, (x, y), sprite)

        d = os.path.join(OUT, role)
        os.makedirs(d, exist_ok=True)
        path = os.path.join(d, f"{role}_{state}_{idx:02d}.png")
        canvas.save(path)
        made.setdefault(role, []).append(os.path.basename(path))

    for role in sorted(made):
        print(f"  {role}: {len(made[role])}장 — {', '.join(sorted(made[role]))}")
    print(f"\n출력 {OUT}")
    print("※ 모든 프레임이 같은 크기이므로 유니티에서 pivot 하나로 정렬이 맞는다")


if __name__ == "__main__":
    main()
