"""
재와 별 — Dash & Invuln 스프라이트를 올바르게 분할한다 (v2: tank 특수 처리).

tank는 배경이 매우 어두워서 (밝기 9~11) 기본 설정으로는 캐릭터가 배경에 묻힙니다.
역할별로 다른 배경색 감지 전략을 씁니다.
"""
import os
from collections import Counter, deque
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "source_sheets", "sheet4_dash.png")
OUT = os.path.abspath(os.path.join(HERE, "..", "unity", "Assets", "Resources", "sprites"))
RESULTS = os.path.join(HERE, "results")

BG_TOL = 14
GAP_MIN = 6
PAD = 3

# 역할별 격자선 좌표 (실측값)
H_LINES = [8, 83, 298, 508, 723, 952]  # 가로선 Y 좌표
V_LINES = [5, 165, 307, 535, 771, 1005, 1237, 1530]  # 세로선 X 좌표

ROWS = [("tank", 83, 298), ("dps", 298, 508), ("ranged", 508, 723), ("healer", 723, 952)]
COLS = [("dash", 307, 1005, 3), ("invuln", 1005, 1237, 1)]


def brightness(px):
    return (px[0] * 299 + px[1] * 587 + px[2] * 114) // 1000


def sample_bg(im, x0, y0, x1, y1):
    """칸 모서리에서 배경색들을 뽑는다."""
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
    """열마다 배경이 아닌 픽셀 수."""
    p = im.load()
    return [sum(1 for y in range(y0, y1, 2) if not is_bg(p[x, y], refs)) for x in range(x0, x1)]


def split_frames(im, x0, y0, x1, y1, want, refs, gap_min=GAP_MIN):
    """프레임을 want개로 나눈다."""
    dens = col_density(im, x0, y0, x1, y1, refs)

    frames, run, gap = [], None, 0
    for i, d in enumerate(dens):
        if d > 0:
            if run is None:
                run = i
            gap = 0
        elif run is not None:
            gap += 1
            if gap >= gap_min:
                frames.append((x0 + run, x0 + i - gap + 1))
                run, gap = None, 0
    if run is not None:
        frames.append((x0 + run, x1))
    frames = [f for f in frames if f[1] - f[0] >= 12]

    if len(frames) > want:
        frames = sorted(sorted(frames, key=lambda f: f[0] - f[1])[:want])

    if len(frames) == want or want == 1:
        return frames

    # 폴백: 가장 넓은 덩어리를 골짜기에서 want개로 가른다
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
    """box를 잘라내고 가장자리와 이어진 배경만 알파 0으로 만든다."""
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


def count_opaque_pixels(img):
    """불투명 픽셀 수."""
    p = img.load()
    return sum(1 for x in range(img.width) for y in range(img.height) if p[x, y][3] > 0)


def main():
    im = Image.open(SRC).convert("RGB")
    w, h = im.size
    print(f"원본 {w}×{h}")
    print(f"격자선: 가로 {H_LINES}, 세로 {V_LINES}\n")

    # 1단계: 모든 프레임 위치 조사
    plan = []
    for role, ry0, ry1 in ROWS:
        for state, cx0, cx1, want in COLS:
            refs = sample_bg(im, cx0, ry0, cx1, ry1)

            # tank는 배경이 매우 어두워서 배경색 하나만 사용 + GAP_MIN=3
            if role == "tank":
                refs = [c for c in refs if brightness(c) <= 15]  # 밝기 15 이하만 배경색
                if not refs:
                    refs = [(6, 11, 15)]
                gap_min = 3  # 더 민감하게 프레임 분리
            else:
                gap_min = GAP_MIN

            for idx, (fx0, fx1) in enumerate(split_frames(im, cx0, ry0, cx1, ry1, want, refs, gap_min)):
                bb = content_bbox(im, fx0, ry0, fx1, ry1, refs)
                if bb:
                    plan.append((role, state, idx, bb, refs))
                    print(f"  {role} {state} {idx}: x=[{fx0}~{fx1}] bbox=[{bb[0]}~{bb[2]}, {bb[1]}~{bb[3]}]")
                else:
                    print(f"  ⚠️ {role} {state} {idx}: 내용 없음")

    if not plan:
        raise SystemExit("프레임을 하나도 찾지 못했다")

    # 공통 캔버스 크기 결정 (기본 모션과 동일하게)
    target_w, target_h = 294, 239
    print(f"\n공통 캔버스: {target_w}×{target_h}  (프레임 {len(plan)}장)")

    # 2단계: 공통 캔버스에 배치
    os.makedirs(RESULTS, exist_ok=True)
    made = {}
    opaque_counts = {}

    for role, state, idx, bb, refs in plan:
        sprite = cut_transparent(im, bb, refs)
        canvas = Image.new("RGBA", (target_w, target_h), (0, 0, 0, 0))
        x = (target_w - sprite.width) // 2
        y = target_h - PAD - sprite.height
        canvas.paste(sprite, (x, y), sprite)

        d = os.path.join(OUT, role)
        os.makedirs(d, exist_ok=True)
        path = os.path.join(d, f"{role}_{state}_{idx:02d}.png")
        canvas.save(path)

        # healer → buffer 동일 파일
        if role == "healer" and state == "invuln":
            buf_path = os.path.join(OUT, "buffer", f"buffer_{state}_{idx:02d}.png")
            os.makedirs(os.path.join(OUT, "buffer"), exist_ok=True)
            canvas.save(buf_path)
            made.setdefault("buffer", []).append(os.path.basename(buf_path))

        made.setdefault(role, []).append(os.path.basename(path))
        opaque_counts[path] = count_opaque_pixels(canvas)

    print("\n출력 파일:")
    for role in sorted(made):
        print(f"  {role}: {len(made[role])}장 — {', '.join(sorted(made[role]))}")

    print(f"\n출력 {OUT}")

    # 불투명 픽셀 수 리포트
    print("\n불투명 픽셀 수:")
    for path, count in sorted(opaque_counts.items()):
        basename = os.path.basename(path)
        status = "✓" if count > 100 else "⚠️ (너무 적음)" if count > 0 else "❌ (빈 이미지)"
        print(f"  {basename}: {count} {status}")

    # 검증용 확인 이미지 생성
    print("\n확인 이미지 생성 중...")
    for role in sorted(made):
        files = sorted([os.path.join(OUT, role, f) for f in made[role]])
        dash_files = sorted([f for f in files if "_dash_" in f])
        invuln_files = sorted([f for f in files if "_invuln_" in f])

        all_imgs = []
        for f in dash_files + invuln_files:
            if os.path.exists(f):
                all_imgs.append(Image.open(f).convert("RGBA"))

        if all_imgs:
            total_w = sum(img.width for img in all_imgs) + len(all_imgs) * 2
            max_h = all_imgs[0].height
            check = Image.new("RGBA", (total_w, max_h), (0, 0, 0, 0))
            x = 0
            for img in all_imgs:
                check.paste(img, (x, 0), img)
                x += img.width + 2
            check_path = os.path.join(RESULTS, f"check_{role}_dash.png")
            check.save(check_path)
            print(f"  ✓ {os.path.basename(check_path)}")


if __name__ == "__main__":
    main()
