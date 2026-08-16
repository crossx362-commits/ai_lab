"""
재와 별 — 오너 제공 **투명 배경** 시트 분할 (2026-08-13)

    python split_owner_sheets.py [--apply]

이 시트들이 앞선 것들보다 나은 점:
  - **배경이 이미 투명하다.** flood fill로 배경을 지울 필요가 없고, 지우다 남는
    모눈 잔재도 없다(앞선 시트에서 격자가 스프라이트에 남아 애먹었다).
  - **라벨이 캐릭터와 안 겹친다.** 직업명은 왼쪽 열, 상태명은 맨 윗줄에만 있어
    x·y로 잘라내면 그만이다(겹친 시트는 글자가 스프라이트에 박혀 못 썼다).

  normal.png — 기본 6상태 (대기·이동2·공격2·특수·피격·사망)
  skill.png  — 이동기 4프레임 + 무적 표시

분할 원리는 단순하다: 알파가 있는 픽셀의 행/열 프로파일로 **빈 줄**을 찾아 자른다.
배경이 투명이라 이 방법이 정확하다 — 색을 추측할 필요가 없다.

정렬은 앞서 검증된 원칙 그대로: 공통 캔버스 + 가로 중앙(본체 기준)·세로 바닥,
그리고 서 있는 자세끼리 키를 맞춘다(눕는 사망은 제외).
"""
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SPR = os.path.abspath(os.path.join(HERE, "source_sheets"))
PREVIEW = os.path.abspath(os.path.join(HERE, "..", "results"))

PAD = 3
ALPHA = 8          # 이보다 진하면 내용으로 본다

# (파일, 라벨 제외 x, 라벨 제외 y위, 하단 주석 제외 y, 상태 정의)
SHEETS = {
    "normal.png": dict(
        x0=150, y0=52, y1=800,
        roles=["tank", "dps", "healer", "buffer"],
        # 열 순서대로 (상태, 프레임 수)
        states=[("idle", 1), ("walk", 2), ("attack", 2), ("special", 1), ("hurt", 1), ("death", 1)],
    ),
    "skill.png": dict(
        x0=290, y0=0, y1=843,
        roles=["tank", "dps", "ranged", "healer"],
        states=[("dash", 4), ("invuln", 1)],
    ),
}

STAND = {"idle", "walk", "attack", "hurt"}


def runs(flags, gap_min):
    """True 구간을 찾되 gap_min 미만의 끊김은 같은 구간으로 본다."""
    out, s, gap = [], None, 0
    for i, f in enumerate(flags):
        if f:
            if s is None:
                s = i
            gap = 0
        elif s is not None:
            gap += 1
            if gap >= gap_min:
                out.append((s, i - gap + 1))
                s, gap = None, 0
    if s is not None:
        out.append((s, len(flags)))
    return out


def biggest_blob(a):
    """가장 큰 연결 덩어리(캐릭터 본체)의 bbox. 이펙트가 붙어도 몸 기준으로 정렬하려는 것."""
    h, w = a.shape
    seen = np.zeros_like(a, dtype=bool)
    best = None
    best_n = 0
    for sy in range(0, h, 2):
        for sx in range(0, w, 2):
            if not a[sy, sx] or seen[sy, sx]:
                continue
            stack = [(sy, sx)]
            seen[sy, sx] = True
            cells = []
            while stack:
                y, x = stack.pop()
                cells.append((y, x))
                for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < h and 0 <= nx < w and a[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        stack.append((ny, nx))
            if len(cells) > best_n:
                best_n = len(cells)
                ys = [c[0] for c in cells]; xs = [c[1] for c in cells]
                best = (min(xs), min(ys), max(xs) + 1, max(ys) + 1)
    return best


def dechecker(im):
    """
    '투명해 보이는' 체커보드 배경을 실제 투명으로 바꾼다.

    ⚠️ 이 시트들은 알파가 **전부 255**다(실측). 체커보드는 뷰어가 투명을 표시한 게 아니라
    이미지에 **그려져 있는 회색 격자**다 — 스크린샷으로 저장된 흔적이다.
    그래서 알파로 내용을 찾으려 하면 시트 전체가 한 덩어리로 잡힌다(실제로 그랬다).

    체커는 무채색 두 톤이 번갈아 나온다. 그 두 톤만 배경으로 보고,
    **가장자리에서 번지는 방식**으로 지운다 — 캐릭터 안쪽의 비슷한 회색을 지키기 위해서다.
    """
    a = np.asarray(im).astype(int)
    r, g, b = a[:, :, 0], a[:, :, 1], a[:, :, 2]
    gray = (np.abs(r - g) <= 5) & (np.abs(g - b) <= 5)

    # 체커 두 톤을 **대역**으로 잡는다. 최빈값 ±3으로는 안 지워졌다 —
    # 실측하니 값이 (59~64), (88~95)처럼 흩어져 있다(스크린샷 압축 탓).
    vals, counts = np.unique(r[gray], return_counts=True)
    order = vals[np.argsort(-counts)]
    tone1 = int(order[0])
    tone2 = next((int(v) for v in order if abs(int(v) - tone1) > 12), tone1 + 30)

    bg = np.zeros(r.shape, dtype=bool)
    for t in (tone1, tone2):
        bg |= gray & (np.abs(r - t) <= 7)

    out = np.asarray(im).copy()
    out[:, :, 3] = np.where(bg, 0, 255)
    return Image.fromarray(out, "RGBA")


def collect(name, cfg):
    im = dechecker(Image.open(os.path.join(SPR, name)).convert("RGBA"))
    im.save(os.path.join(PREVIEW, f"dechecked_{name}"))
    a = np.asarray(im)[:, :, 3] > ALPHA
    x0, y0, y1 = cfg["x0"], cfg["y0"], cfg["y1"]
    sub = a[y0:y1, x0:]

    rows = runs(sub.any(axis=1).tolist(), 14)
    if len(rows) != len(cfg["roles"]):
        print(f"  ⚠️ {name}: 행 {len(rows)}개 (기대 {len(cfg['roles'])}) → {rows}")

    got = []
    for (ry0, ry1), role in zip(rows, cfg["roles"]):
        band = sub[ry0:ry1, :]
        cols = [c for c in runs(band.any(axis=0).tolist(), 12) if c[1] - c[0] >= 12]

        want = sum(n for _, n in cfg["states"])
        if len(cols) != want:
            print(f"  ⚠️ {name} {role}: 열 {len(cols)}개 (기대 {want})")

        i = 0
        for state, n in cfg["states"]:
            for k in range(n):
                if i >= len(cols):
                    break
                cx0, cx1 = cols[i]; i += 1
                box = (x0 + cx0, y0 + ry0, x0 + cx1, y0 + ry1)
                crop = im.crop(box)
                aa = np.asarray(crop)[:, :, 3] > ALPHA
                if not aa.any():
                    continue
                body = biggest_blob(aa)
                ys = np.where(aa.any(axis=1))[0]; xs = np.where(aa.any(axis=0))[0]
                kept = (int(xs[0]), int(ys[0]), int(xs[-1]) + 1, int(ys[-1]) + 1)
                got.append([role, state, k, crop, body or kept, kept])
    return got


def normalize(items):
    """서 있는 자세끼리 키를 맞춘다. 눕는 사망·이펙트 프레임은 평균 배율."""
    ref = {r: b[3] - b[1] for r, s, k, im, b, kp in items if s == "idle"}
    acc = {}
    for r, s, k, im, b, kp in items:
        h = b[3] - b[1]
        if s in STAND and h > 0 and r in ref:
            acc.setdefault(r, []).append(ref[r] / h)
    avg = {r: sum(v) / len(v) for r, v in acc.items()}

    out = []
    for r, s, k, im, b, kp in items:
        h = b[3] - b[1]
        sc = (ref[r] / h) if (s in STAND and r in ref and h > 0) else avg.get(r, 1.0)
        sc = max(0.55, min(1.8, sc))
        if abs(sc - 1) > 0.02:
            im = im.resize((max(1, round(im.width * sc)), max(1, round(im.height * sc))), Image.NEAREST)
            b = tuple(int(round(v * sc)) for v in b)
            kp = tuple(int(round(v * sc)) for v in kp)
        out.append([r, s, k, im, b, kp])
    return out


def main():
    apply_it = "--apply" in sys.argv
    items = []
    for name, cfg in SHEETS.items():
        print(f"[{name}]")
        items += collect(name, cfg)
    print(f"총 {len(items)}장")

    items = normalize(items)

    left = max(b[0] - kp[0] + (b[2] - b[0]) // 2 for _, _, _, _, b, kp in items)
    right = max(kp[2] - b[2] + (b[2] - b[0]) // 2 for _, _, _, _, b, kp in items)
    cw = (max(left, right) + PAD) * 2
    ch = max(kp[3] - kp[1] for _, _, _, _, _, kp in items) + PAD * 2
    print(f"공통 캔버스 {cw}×{ch}")

    made = {}
    os.makedirs(PREVIEW, exist_ok=True)
    canvases = []
    for role, state, k, im, body, kept in items:
        cv = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
        x = int(round(cw / 2 - (body[0] + body[2]) / 2))
        y = ch - PAD - kept[3]
        cv.paste(im, (x, y), im)
        canvases.append((role, state, k, cv))
        made.setdefault(role, []).append(f"{state}_{k:02d}")

    per = 13
    tw, th = int(cw * 0.42), int(ch * 0.42)
    sheet = Image.new("RGBA", (tw * per, th * ((len(canvases) + per - 1) // per)), (45, 48, 56, 255))
    for i, (_, _, _, cv) in enumerate(canvases):
        sheet.paste(cv.resize((tw, th), Image.NEAREST), ((i % per) * tw, (i // per) * th))
    sheet.save(os.path.join(PREVIEW, "check_owner_sheets.png"))
    print("미리보기 → results/check_owner_sheets.png")

    for r in sorted(made):
        print(f"  {r}: {len(made[r])}장")

    if not apply_it:
        print("\n(미리보기만 했다. 실제 교체는 --apply)")
        return

    for role, state, k, cv in canvases:
        d = os.path.join(SPR, role)
        os.makedirs(d, exist_ok=True)
        cv.save(os.path.join(d, f"{role}_{state}_{k:02d}.png"))
    print(f"\n{len(canvases)}장 교체 완료")


if __name__ == "__main__":
    main()
