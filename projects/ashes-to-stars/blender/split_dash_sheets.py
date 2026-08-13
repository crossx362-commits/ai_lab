"""
재와 별 — Dash & Invuln 스프라이트를 올바르게 분할한다.

realign_base_frames.py와 동일한 원칙:
  - 공통 캔버스에 가로 중앙 · 세로 바닥 정렬
  - 배경색 여러 개 + flood fill 투명화 (BG_TOL=14)
  - 프레임 경계는 칼럼 밀도 감지 + 골짜기 분할 폴백

대상: sheet4_dash.png (1536×1024)
  - 행 4개: 탱 / 근접딜 / 원거리딜 / 힐·버퍼
  - 열 7개: 아이콘(제외) / 텍스트(제외) / dash 5~8프레임 / invuln
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


def brightness(px):
    return (px[0] * 299 + px[1] * 587 + px[2] * 114) // 1000


def find_lines(im, axis="v"):
    """격자선 자동 감지 — 밝기 45~130 구간의 연속 선."""
    im_gray = im.convert("L")
    p = im_gray.load()
    w, h = im.size

    lines = []
    if axis == "v":  # 세로선
        for x in range(w):
            match = sum(1 for y in range(h) if 45 <= p[x, y] <= 130) / h
            if match >= 0.8:
                lines.append(x)
    else:  # 가로선
        for y in range(h):
            match = sum(1 for x in range(w) if 45 <= p[x, y] <= 130) / w
            if match >= 0.8:
                lines.append(y)

    # 연속 선들을 그룹화
    groups = []
    if lines:
        cur = [lines[0]]
        for x in lines[1:]:
            if x - cur[-1] <= 2:
                cur.append(x)
            else:
                groups.append(cur[len(cur)//2])
                cur = [x]
        groups.append(cur[len(cur)//2])
    return groups


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


def split_frames(im, x0, y0, x1, y1, want, refs):
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

    # 격자선 자동 감지
    v_lines = find_lines(im, "v")
    h_lines = find_lines(im, "h")
    print(f"격자선 감지: 세로 {v_lines}, 가로 {h_lines}")

    # 수동 조정 가능 (감지 안 되거나 조정 필요시)
    # sheet3과 동일한 구조인지 확인
    if not h_lines or len(h_lines) < 3:
        print("⚠️ 가로선 감지 실패, sheet3 값을 시도: [61, 294, 524, 748]")
        h_lines = [61, 294, 524, 748]
    if not v_lines or len(v_lines) < 5:
        print("⚠️ 세로선 감지 실패, sheet3 값을 시도: [169, 299, 559, 866, 1154, 1307]")
        v_lines = [169, 299, 559, 866, 1154, 1307]

    print(f"확정 격자선: 가로 {h_lines}, 세로 {v_lines}")

    # 행·열 정의
    # 행: (이름, y0, y1)
    rows = []
    for i in range(len(h_lines) - 1):
        row_names = ["tank", "dps", "ranged", "healer"]  # buffer는 healer와 같은 파일
        if i < len(row_names):
            rows.append((row_names[i], h_lines[i], h_lines[i + 1]))

    # 열: (이름, x0, x1, 기대 프레임 수)
    cols = [
        ("dash", v_lines[2], v_lines[5], 4),  # 5~8프레임
        ("invuln", v_lines[5], v_lines[6], 1),  # 무적 표시
    ]

    print(f"행: {rows}")
    print(f"열: {cols}")

    # 1단계: 모든 프레임 위치 조사
    plan = []
    for role, ry0, ry1 in rows:
        for state, cx0, cx1, want in cols:
            refs = sample_bg(im, cx0, ry0, cx1, ry1)
            for idx, (fx0, fx1) in enumerate(split_frames(im, cx0, ry0, cx1, ry1, want, refs)):
                bb = content_bbox(im, fx0, ry0, fx1, ry1, refs)
                if bb:
                    plan.append((role, state, idx, bb, refs))
                else:
                    print(f"⚠️ {role} {state} {idx}: 내용 없음")

    if not plan:
        raise SystemExit("프레임을 하나도 찾지 못했다 — 격자 좌표를 확인할 것")

    cw = max(b[2] - b[0] for _, _, _, b, _ in plan) + PAD * 2
    ch = max(b[3] - b[1] for _, _, _, b, _ in plan) + PAD * 2

    # 기본 모션과 같은 크기로 맞추기 (294×239)
    target_w, target_h = 294, 239
    if cw <= target_w and ch <= target_h:
        cw, ch = target_w, target_h
        print(f"공통 캔버스 기본 모션 크기로 통일: {cw}×{ch}")
    else:
        print(f"공통 캔버스 {cw}×{ch} (기본 모션 크기 초과 — 비율 유지 축소 필요)")
        if cw > target_w or ch > target_h:
            scale = min(target_w / cw, target_h / ch)
            print(f"  → 축소 비율 {scale:.2f}, 목표 크기 조정 불가능 — 원본 유지")

    print(f"  (프레임 {len(plan)}장)")

    # 2단계: 공통 캔버스에 배치
    os.makedirs(RESULTS, exist_ok=True)
    made = {}
    opaque_counts = {}

    for role, state, idx, bb, refs in plan:
        sprite = cut_transparent(im, bb, refs)
        canvas = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
        x = (cw - sprite.width) // 2
        y = ch - PAD - sprite.height
        canvas.paste(sprite, (x, y), sprite)

        d = os.path.join(OUT, role)
        os.makedirs(d, exist_ok=True)
        path = os.path.join(d, f"{role}_{state}_{idx:02d}.png")
        canvas.save(path)

        # healer와 buffer 동일 파일
        if role == "healer" and state == "invuln":
            buf_path = os.path.join(OUT, "buffer", f"buffer_{state}_{idx:02d}.png")
            os.makedirs(os.path.join(OUT, "buffer"), exist_ok=True)
            canvas.save(buf_path)
            made.setdefault("buffer", []).append(os.path.basename(buf_path))

        made.setdefault(role, []).append(os.path.basename(path))
        opaque_counts[path] = count_opaque_pixels(canvas)

    for role in sorted(made):
        print(f"  {role}: {len(made[role])}장 — {', '.join(sorted(made[role]))}")

    print(f"\n출력 {OUT}")
    print(f"결과 {RESULTS}")

    # 불투명 픽셀 수 리포트
    print("\n불투명 픽셀 수:")
    for path, count in sorted(opaque_counts.items()):
        if count > 0:
            print(f"  {os.path.basename(path)}: {count}")
        else:
            print(f"  {os.path.basename(path)}: {count} ⚠️ (빈 이미지)")

    # 검증용 확인 이미지 생성
    for role in sorted(made):
        files = sorted([os.path.join(OUT, role, f) for f in made[role]])

        # dash 4개 + invuln 1개를 가로로 붙이기
        dash_files = [f for f in files if "_dash_" in f]
        invuln_files = [f for f in files if "_invuln_" in f]

        all_imgs = []
        for f in dash_files:
            if os.path.exists(f):
                all_imgs.append(Image.open(f).convert("RGBA"))
        for f in invuln_files:
            if os.path.exists(f):
                all_imgs.append(Image.open(f).convert("RGBA"))

        if all_imgs:
            total_w = sum(img.width for img in all_imgs)
            max_h = all_imgs[0].height
            check = Image.new("RGBA", (total_w, max_h), (0, 0, 0, 0))
            x = 0
            for img in all_imgs:
                check.paste(img, (x, 0), img)
                x += img.width
            check_path = os.path.join(RESULTS, f"check_{role}_dash.png")
            check.save(check_path)
            print(f"  확인 {os.path.basename(check_path)}")


if __name__ == "__main__":
    main()
