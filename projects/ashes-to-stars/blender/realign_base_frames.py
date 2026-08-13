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

# 직업별 예외. 버퍼의 공격은 '연주하는 캐릭터 1명 + 오른쪽으로 퍼지는 음표'라
# 한 동작이지 2프레임이 아니다. 2로 자르면 두 번째가 **몸이 없고 음표만 있는 그림**이 된다
# (오너 지적으로 발견). 이런 건 자동 판정에 맡기지 말고 명시하는 편이 정직하다.
WANT_OVERRIDE = {("buffer", "attack"): 1}

# ── 시트4: 이동기(대시)·무적 표시 ──────────────────────────
# 격자선 실측: 가로 8/83/298/508/723/952, 세로 5/165/307/535/771/1005/1237/1530
# 맨 윗줄(8~83)은 헤더, 1·2열(~307)은 아이콘·한글 라벨이라 버린다.
#   ⚠️ 이 헤더를 안 버려서 한 행씩 밀린 적이 있다 — dps 폴더에 탱커 그림이 들어갔다.
#      그래서 아래 EXPECT_COLOR로 **결과 그림의 색까지 대조**한다.
SRC4 = os.path.join(HERE, "source_sheets", "sheet4_dash.png")
ROWS4 = [("tank", 83, 298), ("dps", 298, 508), ("ranged", 508, 723), ("healer", 723, 952)]
COLS4 = [(307, 535), (535, 771), (771, 1005), (1005, 1237)]     # dash 4프레임
INVULN4 = (1237, 1530)
# 힐/버퍼는 시트가 한 행으로 합쳐 줬다 — 같은 그림을 buffer에도 복사한다
ALSO_COPY = {"healer": "buffer"}


def brightness(px):
    return (px[0] * 299 + px[1] * 587 + px[2] * 114) // 1000


def sample_bg(im, x0, y0, x1, y1):
    """
    셀 **테두리 전체**를 훑어 배경색들을 뽑는다 — 모눈 무늬 때문에 여러 개다.

    좌상단 20×20만 보던 초기 방식은 그 구석에 이펙트가 걸린 셀에서 배경색을
    잘못 잡아, 배경이 통째로 안 지워지고 검은 사각형으로 남았다(dps hurt 실측).
    캐릭터는 셀 가운데에 있으니 **테두리는 거의 확실히 배경**이다.
    """
    p = im.load()
    c = Counter()
    for x in range(x0, x1, 2):
        for y in range(y0, y1, 2):
            c[p[x, y]] += 1
    if not c:
        return [(13, 13, 15)]

    # 밝은 색은 캐릭터다 — 흰 로브·금색 장식이 최빈에 들어도 배경으로 삼지 않는다.
    dark = [(col, n) for col, n in c.most_common(20) if brightness(col) <= 45]
    if not dark:
        return [c.most_common(1)[0][0]]
    total = sum(n for _, n in dark)
    return [col for col, n in dark if n / total >= 0.015][:10]


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

    # ⚠️ 병합은 **모든 경로에서** 해야 한다. 처음엔 골짜기 분할 뒤에만 걸었는데,
    #    버퍼 공격처럼 1차 분할에서 이미 둘로 갈린 경우가 그대로 통과해
    #    두 번째 프레임이 '음표만 있고 몸이 없는' 그림이 됐다(오너 지적으로 발견).
    frames = merge_effect_only(frames, dens, x0)

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
    return merge_effect_only([f for f in out if f[1] - f[0] >= 12], dens, x0)


def merge_effect_only(frames, dens, x0):
    """
    캐릭터가 없고 **이펙트만 있는 조각**을 앞 프레임에 흡수시킨다.

    버퍼의 공격은 '연주하는 캐릭터 1명 + 오른쪽으로 퍼지는 음표'인데,
    기대 프레임 수를 2로 두고 자르니 두 번째 조각이 **음표만 있는 그림**이 됐다(실측).
    프레임이 아니라 한 동작의 일부이므로 갈라서는 안 된다.
    내용량(불투명 픽셀 수)이 가장 큰 조각의 25%도 안 되면 캐릭터가 아니라고 본다.
    """
    if len(frames) <= 1:
        return frames
    mass = [sum(dens[f[0] - x0:f[1] - x0]) for f in frames]
    top = max(mass) if mass else 0
    out = []
    for f, m in zip(frames, mass):
        if out and top > 0 and m < top * 0.45:
            out[-1] = (out[-1][0], f[1])          # 앞 조각에 흡수
        else:
            out.append(f)
    return out


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

    # 시작점이 하나도 없으면 flood fill은 아무 일도 안 하고 **배경이 통째로 남는다**.
    # 격자선이 crop 가장자리에 걸린 셀에서 실제로 그랬다(sheet4 실측).
    # 그럴 땐 가장자리에서 가장 흔한 색을 배경으로 인정하고 다시 시작한다.
    if not q:
        edge = Counter()
        for x in range(w):
            edge[px[x, 0][:3]] += 1
            edge[px[x, h - 1][:3]] += 1
        for y in range(h):
            edge[px[0, y][:3]] += 1
            edge[px[w - 1, y][:3]] += 1
        if edge:
            refs = list(refs) + [edge.most_common(1)[0][0]]
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


def clean_and_measure(sprite, min_blob=40):
    """
    투명화하고 남은 **고립된 작은 조각**을 지우고, 캐릭터 본체의 위치를 돌려준다.

    왜 필요한가 (2026-08-13 오너 지적 "센터 안 맞잖아"):
      flood fill로 배경을 지워도 모눈 무늬 잔재가 점·선으로 남는다. 그 조각들이
      바운딩 박스를 부풀려서 '내용물 중심'이 캐릭터 중심이 아니게 되고,
      결과적으로 캐릭터가 캔버스 한쪽으로 치우친다.

      허용오차를 더 올려 지우는 건 위험하다 — 잔재 (24,30,34)는 배경 (10,15,18)과
      채널차 14~16인데 캐릭터 외곽선 (0,0,0)도 채널차 10~18이라 같이 날아간다.
      그래서 색이 아니라 **크기**로 거른다.

    반환: (정리된 이미지, 본체 bbox, 전체 bbox)
      정렬은 **본체 bbox** 기준으로 해야 검 궤적 같은 이펙트가 붙어도 몸이 안 흔들린다.
    """
    w, h = sprite.size
    px = sprite.load()
    seen = [[False] * h for _ in range(w)]
    blobs = []

    for sx in range(w):
        for sy in range(h):
            if seen[sx][sy] or px[sx, sy][3] == 0:
                continue
            q, cells = deque([(sx, sy)]), []
            seen[sx][sy] = True
            while q:
                x, y = q.popleft()
                cells.append((x, y))
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < w and 0 <= ny < h and not seen[nx][ny] and px[nx, ny][3] > 0:
                        seen[nx][ny] = True
                        q.append((nx, ny))
            blobs.append(cells)

    if not blobs:
        return sprite, (0, 0, w, h), (0, 0, w, h)

    body = max(blobs, key=len)
    for cells in blobs:
        if cells is body or len(cells) >= min_blob:
            continue
        for x, y in cells:                      # 노이즈 조각 제거
            px[x, y] = (0, 0, 0, 0)

    def bbox(cs):
        xs = [c[0] for c in cs]; ys = [c[1] for c in cs]
        return (min(xs), min(ys), max(xs) + 1, max(ys) + 1)

    kept = [c for b in blobs if b is body or len(b) >= min_blob for c in b]
    return sprite, bbox(body), bbox(kept)


# 서 있는 자세들 — 이것끼리는 **키가 같아야** 한다.
# 제외 이유:
#   death  누워 있다 — 높이로 맞추면 거대해진다
#   special 이펙트가 캐릭터보다 크다
#   dash   돌진·구르기는 몸을 낮추는 동작이라 **낮은 게 정상**이다.
#          여기 넣었더니 억지로 키워서 오히려 부자연스러웠다(실측 후 제외).
STAND_STATES = {"idle", "walk", "attack", "hurt"}


def normalize_height(cut):
    """
    프레임마다 다른 캐릭터 키를 idle 기준으로 통일한다.

    왜 필요한가 (2026-08-13 오너 지적 "캐릭터 스프라이트 애니 높이 안맞음"):
      공통 캔버스에 발밑을 맞춰도 **원본 그림 자체가 프레임마다 캐릭터를 다른 크기로**
      그려놨다(생성 이미지라 일관성이 없다). 유니티에서 직업당 배율 하나로 묶어도
      그 차이는 그대로 남아 재생하면 키가 들쭉날쭉해 보인다.

    눕는 자세(death)·이펙트 프레임(special·invuln)은 높이로 맞추면 안 되므로
    그 직업의 **서 있는 프레임 평균 배율**을 대신 쓴다.
    """
    ref = {}
    for role, state, idx, sprite, body, kept in cut:
        if state == "idle":
            ref[role] = body[3] - body[1]

    # 직업별 평균 배율 (눕는 자세에 쓸 값)
    acc = {}
    for role, state, idx, sprite, body, kept in cut:
        h = body[3] - body[1]
        if state in STAND_STATES and h > 0 and role in ref:
            acc.setdefault(role, []).append(ref[role] / h)
    avg = {r: sum(v) / len(v) for r, v in acc.items()}

    out = []
    for role, state, idx, sprite, body, kept in cut:
        h = body[3] - body[1]
        if role not in ref or h <= 0:
            out.append((role, state, idx, sprite, body, kept))
            continue

        s = (ref[role] / h) if state in STAND_STATES else avg.get(role, 1.0)
        s = max(0.55, min(1.8, s))              # 튀는 값이 캐릭터를 망가뜨리지 않게
        if abs(s - 1.0) < 0.02:
            out.append((role, state, idx, sprite, body, kept))
            continue

        # 픽셀아트라 NEAREST — 부드럽게 늘리면 도트가 뭉개진다
        nw, nh = max(1, round(sprite.width * s)), max(1, round(sprite.height * s))
        sprite = sprite.resize((nw, nh), Image.NEAREST)
        body = tuple(int(round(v * s)) for v in body)
        kept = tuple(int(round(v * s)) for v in kept)
        out.append((role, state, idx, sprite, body, kept))
    return out


def main():
    im = Image.open(SRC).convert("RGB")

    # ── 1단계: 모든 프레임의 위치를 먼저 조사한다 (아직 자르지 않는다) ──
    # 공통 캔버스를 정하려면 전체를 다 봐야 하기 때문이다.
    plan = []
    for role, ry0, ry1 in ROWS:
        for state, cx0, cx1, want_default in COLS:
            want = WANT_OVERRIDE.get((role, state), want_default)
            refs = sample_bg(im, cx0, ry0, cx1, ry1)
            found = []
            for fx0, fx1 in split_frames(im, cx0, ry0, cx1, ry1, want, refs):
                bb = content_bbox(im, fx0, ry0, fx1, ry1, refs)
                if bb:
                    found.append(bb)
            if not found:
                print(f"  ⚠️ {role}_{state}: 내용을 못 찾음")
                continue
            # 이펙트 조각을 병합하면 프레임 수가 기대보다 적어질 수 있다.
            # 파일이 비면 런타임이 단색으로 대체해 화면에 빨간 사각형이 뜨므로,
            # 마지막 프레임을 복제해 채운다 — 동작이 멈춰 보일 뿐 깨지지는 않는다.
            # 파일 개수는 예외와 무관하게 want_default를 채운다 —
            # 런타임이 attack_01을 찾다 실패하면 단색 사각형으로 대체되기 때문이다.
            while len(found) < want_default:
                found.append(found[-1])
            for idx, bb in enumerate(found[:want_default]):
                plan.append((role, state, idx, bb, refs, im))

    # ── 시트4(이동기)도 **같은 계획에 합친다** ──
    # 따로 돌리면 공통 캔버스가 달라져(294 vs 382) 유니티에서 같은 PPU를 써도
    # 대시 프레임만 크기가 어긋난다. 실제로 그렇게 어긋났었다.
    im4 = Image.open(SRC4).convert("RGB")
    for role, ry0, ry1 in ROWS4:
        cells = [(f"dash", i, cx0, cx1) for i, (cx0, cx1) in enumerate(COLS4)]
        cells.append(("invuln", 0, INVULN4[0], INVULN4[1]))
        for state, idx, cx0, cx1 in cells:
            # 격자선을 crop에 들이지 않으려고 셀 안쪽으로 물려 자른다.
            # 격자선이 가장자리에 걸리면 flood fill 시작점이 없어 배경이 통째로 남는다.
            ix0, iy0, ix1, iy1 = cx0 + 3, ry0 + 3, cx1 - 3, ry1 - 3
            refs = sample_bg(im4, ix0, iy0, ix1, iy1)
            bb = content_bbox(im4, ix0, iy0, ix1, iy1, refs)
            if not bb:
                print(f"  ⚠️ {role}_{state}_{idx}: 내용 없음")
                continue
            plan.append((role, state, idx, bb, refs, im4))
            if role in ALSO_COPY:
                plan.append((ALSO_COPY[role], state, idx, bb, refs, im4))

    if not plan:
        raise SystemExit("프레임을 하나도 찾지 못했다 — 격자 좌표를 확인할 것")

    # ── 2단계: 잘라내고 노이즈를 걸러 **본체 위치**를 잰다 ──
    cut = []
    for role, state, idx, bb, refs, src in plan:
        sprite, body, kept = clean_and_measure(cut_transparent(src, bb, refs))
        cut.append((role, state, idx, sprite, body, kept))

    cut = normalize_height(cut)

    # 캔버스는 '노이즈를 뺀 실제 내용'이 다 들어갈 크기로 잡는다.
    # 본체 중심을 캔버스 중앙에 두므로, 본체 밖으로 뻗은 이펙트가 잘리지 않도록
    # 좌우로 필요한 만큼(최대치)을 양쪽에 확보한다.
    left = max(bd[0] - kp[0] + (bd[2] - bd[0]) // 2 for _, _, _, _, bd, kp in cut)
    right = max(kp[2] - bd[2] + (bd[2] - bd[0]) // 2 for _, _, _, _, bd, kp in cut)
    cw = (max(left, right) + PAD) * 2
    ch = max(kp[3] - kp[1] for _, _, _, _, _, kp in cut) + PAD * 2
    print(f"공통 캔버스 {cw}×{ch}  (프레임 {len(cut)}장)")

    # ── 3단계: 본체 가로 중앙 · 내용 바닥 정렬로 배치 ──
    made = {}
    for role, state, idx, sprite, body, kept in cut:
        canvas = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
        body_cx = (body[0] + body[2]) / 2.0
        x = int(round(cw / 2.0 - body_cx))           # 가로: **본체 중심**을 캔버스 중앙에
        y = ch - PAD - kept[3]                       # 세로: 내용 바닥을 기준선에
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
