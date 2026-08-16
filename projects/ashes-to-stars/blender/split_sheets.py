"""
재와 별 — 오너 제공 스프라이트 시트를 프레임 단위로 분할

    python split_sheets.py

무엇을 하는가:
  오너가 준 시트(격자에 캐릭터가 배치된 한 장)를 읽어
  ① 격자선을 자동 감지해 셀로 나누고
  ② 셀 안에서 배경(거의 검정)이 아닌 덩어리를 찾아 개별 프레임으로 잘라
  ③ 투명 배경 PNG로 저장한다.

왜 자동 감지인가:
  좌표를 손으로 박으면 시트가 조금만 달라져도 전부 어긋난다.
  격자선과 배경색은 규칙적이므로 코드가 찾게 하는 편이 안전하다.

출력: ../unity/Assets/Resources/sprites/<직업>/<직업>_<상태>_<번호>.png
  ⚠️ Assets/_Game/Art/Sprites 는 Resources.Load가 못 읽는 옛 함정. 쓰지 마라.
"""
import os
import sys
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "source_sheets")
# 스프라이트는 unity/Assets/Resources/sprites/ 아래에만 저장된다.
# Resources.Load(path)는 Assets/Resources/ 밖을 못 읽어서,
# 예전 경로(Assets/_Game/Art/Sprites)의 스프라이트들은 로드되지 않았다.
OUT = os.path.abspath(os.path.join(HERE, "..", "unity", "Assets", "Resources", "sprites"))

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
    "sheet3_character_design.png": {
        # 레거시 한글 명칭 — 기존 32장 보존용. 재생성 금지.
        "직업": ["탱커", "딜러", "힐러", "버퍼"],
        "상태": ["대기", "이동", "공격", "특수", "피격", "사망"],
        "프레임수": {"대기": 1, "이동": 2, "공격": 2, "특수": 1, "피격": 1, "사망": 1},
    },
    "sheet4_dash.png": {
        # 2026-08-13 오너 제공. 이동기 동작(Dash, Invuln) 시각화.
        # 시트 구조:
        #   행: 탱, 근접딜, 원거리딜, 힐/버퍼 (4개)
        #   열: 아이콘(건너뜀), 텍스트(건너뜀), 프레임1~4, 무적이펙트 (7개, 맨 앞 2개 제외하면 5개)
        # skip 이후의 상대 인덱스:
        #   0: 프레임1  1: 프레임2  2: 프레임3  3: 프레임4  4: 무적이펙트
        "role": ["tank", "dps", "ranged", "healer"],
        "frame_cols": [0, 1, 2, 3],  # skip 이후 열 인덱스 (프레임 4개)
        "invuln_col": 4,  # skip 이후 열 인덱스 (무적 1개)
        "skip_rows": 1,  # 첫 행(헤더) 건너뜀
        "skip_cols": 2,  # 첫 2열(아이콘, 텍스트) 건너뜀
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


def process_sheet4(path, spec):
    """
    sheet4_dash.png 처리.

    중요: 한 직업군(행)의 모든 프레임은 같은 크기여야 한다.
    그렇지 않으면 유니티에서 pivot 고정 시 프레임마다 캐릭터가 튄다.

    절차:
    1. 각 셀의 내용물 바운딩 박스 구하기
    2. 행별로 최대 폭·높이 찾기 → 공통 캔버스 크기 정하기
    3. 각 프레임을 공통 캔버스에 배치:
       - 가로: 내용물 중심을 캔버스 중앙에
       - 세로: 내용물 바닥을 캔버스 바닥에 (발이 땅에 붙어 보이게)
    """
    im = Image.open(path).convert("RGB")
    w, h = im.size
    print(f"\n[시트] {os.path.basename(path)}  {w}x{h}")

    xs = find_lines(im, 0)
    ys = find_lines(im, 1)
    print(f"  격자선: 세로 {len(xs)}개 / 가로 {len(ys)}개")

    roles = spec["role"]
    frame_cols = spec["frame_cols"]
    invuln_col = spec["invuln_col"]
    skip_rows = spec["skip_rows"]
    skip_cols = spec["skip_cols"]

    xs = [0] + xs + [w]
    ys = [0] + ys + [h]

    need_cols = len(frame_cols) + 1 + skip_cols + 1
    need_rows = len(roles) + skip_rows + 1
    if len(xs) < need_cols or len(ys) < need_rows:
        print(f"  ✗ 격자 감지 실패 — 행 경계 {len(ys)}/{need_rows}, 열 경계 {len(xs)}/{need_cols}")
        return 0

    total = 0
    for r, role in enumerate(roles):
        y0, y1 = ys[skip_rows + r] + 2, ys[skip_rows + r + 1] - 2

        # 단계 1: 이 행의 모든 dash 프레임 바운딩 박스 수집
        dash_bboxes = []
        for col_idx in frame_cols:
            x0, x1 = xs[skip_cols + col_idx] + 2, xs[skip_cols + col_idx + 1] - 2
            if x1 > x0 and y1 > y0:
                cell = content_bbox(im, x0, y0, x1, y1)
                if cell is not None:
                    dash_bboxes.append(cell)
                else:
                    dash_bboxes.append(None)
            else:
                dash_bboxes.append(None)

        # 단계 2: Dash 캔버스 크기 결정 (모든 dash 프레임이 들어가는 최소 크기)
        if any(b is not None for b in dash_bboxes):
            dash_widths = [(b[2] - b[0] + 1) for b in dash_bboxes if b is not None]
            dash_heights = [(b[3] - b[1] + 1) for b in dash_bboxes if b is not None]
            canvas_w = max(dash_widths) + PAD * 2
            canvas_h = max(dash_heights) + PAD * 2
        else:
            canvas_w, canvas_h = 64, 64  # 폴백

        d = os.path.join(OUT, role)
        os.makedirs(d, exist_ok=True)

        # 단계 3: 각 dash 프레임을 캔버스에 배치해 저장
        for frame_idx, (col_idx, bbox) in enumerate(zip(frame_cols, dash_bboxes)):
            if bbox is None:
                continue

            cx0, cy0, cx1, cy1 = bbox
            content_w = cx1 - cx0 + 1
            content_h = cy1 - cy0 + 1

            # 캔버스 안에서 프레임 배치
            # 세로: 바닥 정렬 (cy1이 canvas_h - PAD와 같아야 함)
            canvas = Image.new("RGBA", (canvas_w, canvas_h), (0, 0, 0, 0))
            crop = im.crop((max(0, cx0 - PAD), max(0, cy0 - PAD),
                            min(w, cx1 + PAD + 1), min(h, cy1 + PAD + 1)))
            crop = make_transparent(crop)

            # 캔버스에 페이스트할 위치 계산
            # 가로: 중심 정렬
            paste_x = (canvas_w - crop.width) // 2
            # 세로: 바닥 정렬
            paste_y = canvas_h - crop.height

            canvas.paste(crop, (paste_x, paste_y), crop)

            out = os.path.join(d, f"{role}_dash_{frame_idx:02d}.png")
            canvas.save(out)
            total += 1

        # 단계 4: Invuln 프레임 (dash와는 별도 크기 그룹)
        col_idx = invuln_col
        x0, x1 = xs[skip_cols + col_idx] + 2, xs[skip_cols + col_idx + 1] - 2
        if x1 > x0 and y1 > y0:
            cell = content_bbox(im, x0, y0, x1, y1)
            if cell is not None:
                cx0, cy0, cx1, cy1 = cell
                content_w = cx1 - cx0 + 1
                content_h = cy1 - cy0 + 1

                # invuln용 캔버스 (타이트하게, 여유 2px)
                invuln_w = content_w + PAD * 2
                invuln_h = content_h + PAD * 2
                canvas = Image.new("RGBA", (invuln_w, invuln_h), (0, 0, 0, 0))

                crop = im.crop((max(0, cx0 - PAD), max(0, cy0 - PAD),
                                min(w, cx1 + PAD + 1), min(h, cy1 + PAD + 1)))
                crop = make_transparent(crop)

                # invuln도 바닥 정렬 (발밑이 같은 비율에 오도록)
                paste_x = (invuln_w - crop.width) // 2
                paste_y = invuln_h - crop.height

                canvas.paste(crop, (paste_x, paste_y), crop)

                out = os.path.join(d, f"{role}_invuln_00.png")
                canvas.save(out)
                total += 1

        print(f"  {role}: dash {canvas_w}x{canvas_h} (4장) + invuln (1장)")

    # Buffer는 healer의 sheet4 파일만 복사 (dash + invuln, 레거시 제외)
    healer_dir = os.path.join(OUT, "healer")
    buffer_dir = os.path.join(OUT, "buffer")
    if os.path.isdir(healer_dir):
        os.makedirs(buffer_dir, exist_ok=True)
        import shutil
        for fname in os.listdir(healer_dir):
            # sheet4에서 생성된 파일만: healer_dash_* 또는 healer_invuln_*
            if fname.startswith("healer_") and ("_dash_" in fname or "_invuln_" in fname):
                src = os.path.join(healer_dir, fname)
                new_fname = fname.replace("healer_", "buffer_")
                dst = os.path.join(buffer_dir, new_fname)
                shutil.copy2(src, dst)
        print(f"  buffer: healer sheet4 파일 복사 완료 (5장)")
        total += 5

    return total


def main():
    if not os.path.isdir(SRC):
        print(f"원본시트 폴더가 없다: {SRC}")
        return 1

    # 이번 세션에서는 sheet4만 처리 (기존 32장 보존)
    target = "sheet4_dash.png"

    grand = 0
    if target in SHEETS:
        p = os.path.join(SRC, target)
        if os.path.isfile(p):
            spec = SHEETS[target]
            if "role" in spec:  # sheet4 형식 판별
                grand += process_sheet4(p, spec)
            else:
                grand += process(p, spec)
        else:
            print(f"파일이 없다: {target}")
    else:
        print(f"SHEETS에 정의되지 않음: {target}")

    print(f"\n[분할] 총 {grand}장 → {OUT}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
