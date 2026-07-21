#!/usr/bin/env python3
"""FleetView 캐릭터 스프라이트 생성기 — 게더타운풍 24x32 픽셀아트.

`python3 gen_sprites.py` → sprites/<key>.png (8프레임 가로 시트, x3 스케일)
프레임: [0]서기A [1]눈깜빡 [2]걷기A [3]걷기중간 [4]걷기B [5]걷기중간 [6]앉기 [7]앉아타이핑
검토용 contact.png(전 캐릭터 모음)도 함께 생성.
"""
import os
from PIL import Image

W, H = 24, 32
SCALE = 3
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "sprites")

OUTLINE = (27, 21, 38, 255)
SKIN = (244, 202, 160, 255)
SKIN_D = (220, 168, 126, 255)
PANTS = (58, 51, 80, 255)
SHOE = (34, 27, 49, 255)
EYE = (38, 25, 15, 255)
MOUTH = (184, 85, 107, 255)
CHEEK = (240, 154, 142, 255)
WHITE = (255, 255, 255, 255)


def hexc(s):
    s = s.lstrip("#")
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), 255)


def tint(c, amt):
    return tuple(min(255, max(0, v + amt)) for v in c[:3]) + (255,)


CHARS = {
    "yewon":    dict(shirt="#7c4dff", hair="#5b3a1a", style="short", extra="vest"),
    "youngsuk": dict(shirt="#ff5fa2", hair="#2b2b2b", style="long",  extra=None),
    "bomi":     dict(shirt="#26c281", hair="#c9721f", style="pony",  extra=None),
    "teo":      dict(shirt="#3aa0ff", hair="#3a2f2f", style="short", extra=None),
    "baekho":   dict(shirt="#00b3a4", hair="#1c1c1c", style="short", extra="glasses"),
    "suri":     dict(shirt="#ff8c42", hair="#4a3020", style="short", extra="phones"),
    "mio":      dict(shirt="#e05fd8", hair="#7a3b8f", style="long",  extra=None),
    "namu":     dict(shirt="#6bbf59", hair="#2f5d32", style="short", extra="cap"),
}


class Px:
    def __init__(self):
        self.im = Image.new("RGBA", (W, H), (0, 0, 0, 0))

    def put(self, x, y, c):
        if 0 <= x < W and 0 <= y < H:
            self.im.putpixel((x, y), c)

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.put(x, y, c)


def outline(im):
    """실루엣 외곽 1px 아웃라인(게더 룩)."""
    src = im.load()
    solid = [[src[x, y][3] > 0 for x in range(W)] for y in range(H)]
    for y in range(H):
        for x in range(W):
            if solid[y][x]:
                continue
            near = False
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < W and 0 <= ny < H and solid[ny][nx]:
                    near = True
                    break
            if near:
                src[x, y] = OUTLINE


def draw_frame(cfg, kind):
    p = Px()
    shirt = hexc(cfg["shirt"])
    shirt_d = tint(shirt, -36)
    shirt_l = tint(shirt, 26)
    hair = hexc(cfg["hair"])
    hair_l = tint(hair, 36)
    style, extra = cfg["style"], cfg["extra"]

    seated = kind in ("sit", "type")
    dy = 6 if seated else (-1 if kind == "pass" else 0)

    def R(x0, y0, x1, y1, c):
        p.rect(x0, y0 + dy, x1, y1 + dy, c)

    def PT(x, y, c):
        p.put(x, y + dy, c)

    # ---------- 다리 ----------
    if seated:
        R(7, 23, 15, 24, PANTS)  # 무릎/랩 (의자에 앉음, 발 숨김)
    elif kind == "wA":           # 왼발 디딤, 오른발 들림
        R(8, 24, 10, 29, PANTS); R(8, 30, 10, 31, SHOE)
        R(13, 24, 15, 26, PANTS); R(13, 27, 15, 28, SHOE)
    elif kind == "wB":           # 반대
        R(8, 24, 10, 26, PANTS); R(8, 27, 10, 28, SHOE)
        R(13, 24, 15, 29, PANTS); R(13, 30, 15, 31, SHOE)
    else:                        # 서기/걷기중간
        R(8, 24, 10, 29, PANTS); R(8, 30, 10, 31, SHOE)
        R(13, 24, 15, 29, PANTS); R(13, 30, 15, 31, SHOE)

    # ---------- 몸통 ----------
    torso_end = 22 if seated else 23
    R(7, 16, 16, torso_end, shirt)
    R(8, 17, 15, 17, shirt_l)            # 하이라이트
    R(7, torso_end, 16, torso_end, shirt_d)  # 밑단
    R(10, 16, 13, 16, shirt_d)           # 카라

    # ---------- 팔 ----------
    if kind == "wA":    # 왼팔 뒤로(짧게), 오른팔 앞으로(길게)
        R(5, 16, 6, 19, shirt); R(5, 20, 6, 20, SKIN)
        R(17, 16, 18, 21, shirt); R(17, 22, 18, 22, SKIN)
    elif kind == "wB":
        R(5, 16, 6, 21, shirt); R(5, 22, 6, 22, SKIN)
        R(17, 16, 18, 19, shirt); R(17, 20, 18, 20, SKIN)
    elif kind == "sit":  # 팔 옆으로 내림
        R(5, 16, 6, 20, shirt); R(5, 21, 6, 21, SKIN)
        R(17, 16, 18, 20, shirt); R(17, 21, 18, 21, SKIN)
    elif kind == "type":  # 팔 앞으로 — 키보드 위 손
        R(5, 16, 6, 18, shirt); R(17, 16, 18, 18, shirt)
        R(6, 19, 8, 19, shirt); R(15, 19, 17, 19, shirt)
        R(8, 20, 9, 20, SKIN); R(14, 20, 15, 20, SKIN)
    else:               # 중립
        R(5, 16, 6, 21, shirt); R(5, 22, 6, 22, SKIN)
        R(17, 16, 18, 21, shirt); R(17, 22, 18, 22, SKIN)

    # ---------- 머리 ----------
    R(6, 5, 17, 15, SKIN)
    R(6, 15, 17, 15, SKIN_D)             # 턱 음영
    # 머리카락
    if extra == "cap":
        capc = hexc("#3e7d46"); brim = hexc("#2f5d32")
        R(5, 2, 18, 7, capc)
        R(7, 3, 15, 3, tint(capc, 30))
        R(4, 8, 19, 8, brim)             # 챙
        R(5, 9, 18, 9, hair)             # 모자 밑 머리 한 줄
    else:
        R(5, 2, 18, 8, hair)
        R(7, 3, 15, 3, hair_l)           # 윤기
        R(5, 9, 6, 11, hair); R(17, 9, 18, 11, hair)  # 구레나룻
    if style == "long":
        R(4, 9, 5, 19, hair); R(18, 9, 19, 19, hair)
        PT(4, 10, hair_l); PT(19, 10, hair_l)
    elif style == "pony":
        R(19, 6, 21, 14, hair)
        R(19, 9, 21, 9, tint(hair, -40))  # 머리끈
        PT(20, 15, hair)

    # ---------- 얼굴 ----------
    if extra == "glasses":
        # 안경: 밝은 렌즈 + 어두운 테 + 눈동자
        g = (24, 38, 50, 255); lens = (198, 226, 246, 255)
        if kind == "blink":
            for x0 in (9, 14):
                R(x0, 10, x0 + 1, 11, lens)
                R(x0, 11, x0 + 1, 11, SKIN_D)
        else:
            R(9, 10, 10, 11, lens); R(14, 10, 15, 11, lens)
            PT(9, 11, EYE); PT(14, 11, EYE)
        for x0, x1 in ((8, 11), (13, 16)):
            R(x0, 9, x1, 9, g); R(x0, 12, x1, 12, g)
            R(x0, 10, x0, 11, g); R(x1, 10, x1, 11, g)
        PT(12, 10, g)
    elif kind == "blink":
        R(9, 11, 10, 11, SKIN_D); R(14, 11, 15, 11, SKIN_D)
    else:
        R(9, 10, 10, 11, EYE); R(14, 10, 15, 11, EYE)
        PT(10, 10, WHITE); PT(15, 10, WHITE)
    PT(8, 13, CHEEK); PT(16, 13, CHEEK)
    R(11, 14, 13, 14, MOUTH)

    # ---------- 액세서리 ----------
    if extra == "vest":
        vest = tint(shirt, -60)
        R(7, 16, 8, torso_end, vest); R(15, 16, 16, torso_end, vest)
        gold = (255, 214, 107, 255)
        R(11, 17, 12, 19, gold); PT(11, 16, gold); PT(12, 16, gold)
    elif extra == "phones":
        d = (47, 39, 56, 255)
        R(6, 1, 17, 2, d)
        R(4, 9, 5, 12, d); R(18, 9, 19, 12, d)
        PT(4, 10, tint(d, 40)); PT(19, 10, tint(d, 40))

    outline(p.im)
    return p.im


FRAME_KINDS = ["idle", "blink", "wA", "pass", "wB", "pass", "sit", "type"]


def main():
    os.makedirs(OUT, exist_ok=True)
    sheets = {}
    for key, cfg in CHARS.items():
        sheet = Image.new("RGBA", (W * len(FRAME_KINDS), H), (0, 0, 0, 0))
        for i, kind in enumerate(FRAME_KINDS):
            sheet.paste(draw_frame(cfg, kind), (W * i, 0))
        big = sheet.resize((sheet.width * SCALE, sheet.height * SCALE), Image.NEAREST)
        big.save(os.path.join(OUT, f"{key}.png"))
        sheets[key] = big
        print(f"✓ sprites/{key}.png  ({big.width}x{big.height})")

    # 검토용 콘택트 시트
    gap = 6
    cw = max(s.width for s in sheets.values())
    ch = sum(s.height + gap for s in sheets.values())
    contact = Image.new("RGBA", (cw, ch), (26, 22, 34, 255))
    y = 0
    for key, s in sheets.items():
        contact.paste(s, (0, y), s)
        y += s.height + gap
    contact.save(os.path.join(OUT, "contact.png"))
    print(f"✓ sprites/contact.png ({cw}x{ch})")


if __name__ == "__main__":
    main()
