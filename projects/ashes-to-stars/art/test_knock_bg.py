#!/usr/bin/env python3
"""knock_bg 네거티브 컨트롤 — 마젠타/흰/회색/라벨, 가면은 남겨야 한다."""
from __future__ import annotations

import unittest

import numpy as np
from PIL import Image, ImageDraw

import knock_bg


def _solid(bg, size=80):
    return Image.new("RGBA", (size, size), bg)


def _bug(bg, size=80, face=(245, 245, 240, 255)):
    """검은 외곽선 + 흰 가면. 흰 배경을 통째로 지우면 가면이 사라진다."""
    im = Image.new("RGBA", (size, size), bg)
    d = ImageDraw.Draw(im)
    d.ellipse((20, 18, 60, 62), fill=(20, 20, 22, 255), outline=(8, 8, 8, 255), width=3)
    d.ellipse((30, 24, 50, 46), fill=face)
    d.ellipse((34, 30, 38, 36), fill=(5, 5, 5, 255))
    d.ellipse((42, 30, 46, 36), fill=(5, 5, 5, 255))
    return im


def _face_alive(im: Image.Image) -> bool:
    a = np.asarray(im.convert("RGBA"))
    rgb, al = a[..., :3], a[..., 3]
    # 가면: 밝은 불투명
    pale = (al > 200) & (rgb.min(axis=2) > 200)
    return int(pale.sum()) >= 40


def _bg_gone(im: Image.Image, color, tol=40) -> bool:
    """모서리가 투명하면 배경이 빠진 것이다. 가면은 뼈색이라 색거리로 보면 안 된다."""
    a = np.asarray(im.convert("RGBA"))
    corners = np.concatenate([
        a[:5, :5, 3].ravel(), a[:5, -5:, 3].ravel(),
        a[-5:, :5, 3].ravel(), a[-5:, -5:, 3].ravel(),
    ])
    return float((corners > 128).mean()) < 0.05


class KnockBgTests(unittest.TestCase):
    def test_magenta_bg(self):
        im = knock_bg.apply(_bug((255, 0, 255, 255)))
        self.assertTrue(_face_alive(im), "마젠타 키 후 가면이 살아야 한다")
        self.assertTrue(_bg_gone(im, (255, 0, 255)), "마젠타가 남아 있다")

    def test_white_bg_keeps_mask(self):
        im = knock_bg.apply(_bug((255, 255, 255, 255)))
        self.assertTrue(_face_alive(im), "흰 배경을 지울 때 가면을 같이 파면 안 된다")
        self.assertTrue(_bg_gone(im, (255, 255, 255)), "흰 배경이 남아 있다")

    def test_gray_bg(self):
        im = knock_bg.apply(_bug((170, 170, 171, 255)))
        self.assertTrue(_face_alive(im))
        self.assertTrue(_bg_gone(im, (170, 170, 171)))

    def test_label_dropped(self):
        im = _bug((255, 0, 255, 255))
        d = ImageDraw.Draw(im)
        d.text((8, 66), "WALK L", fill=(10, 10, 10, 255))
        out = knock_bg.apply(im)
        a = np.asarray(out)
        # 라벨은 하단 작은 덩어리 — 가면(상단)은 남고 하단 글자 줄은 거의 없어야 한다
        bottom = a[64:, :, 3] > 128
        self.assertLess(int(bottom.sum()), 20, "라벨이 남았다")
        self.assertTrue(_face_alive(out))

    def test_no_magenta_on_white_is_not_noop(self):
        raw = _bug((255, 255, 255, 255))
        # 옛 chroma_key만 쓰면 흰 배경은 100% 불투명으로 남는다
        keyed = knock_bg._chroma_mask(np.asarray(raw.convert("RGB")))
        self.assertLess(float(keyed.mean()), 0.01, "흰 장은 마젠타 키가 안 먹어야 한다(전제)")
        out = knock_bg.apply(raw)
        self.assertTrue(_bg_gone(out, (255, 255, 255)))

    def test_white_card_dropped_crescent_kept(self):
        im = _bug((0, 0, 0, 0), size=96)
        # 투명 캔버스 + 가면 벌레 + 왼쪽 흰 카드(사고) + 오른쪽 초승달(공격 FX)
        d = ImageDraw.Draw(im)
        for y in range(20, 70):
            for x in range(0, 22):
                d.point((x, y), fill=(230, 230, 230, 255) if (x // 6 + y // 6) % 2 == 0 else (50, 50, 50, 255))
        d.pieslice((70, 20, 94, 70), 200, 340, fill=(240, 240, 240, 255))
        out = knock_bg.apply(im)
        a = np.asarray(out)
        left = a[20:70, 2:22, 3] > 128
        right = a[20:70, 70:94, 3] > 128
        self.assertLess(int(left.sum()), 30, "흰 카드가 남았다")
        self.assertGreater(int(right.sum()), 40, "초승달 베기를 같이 지우면 안 된다")
        self.assertTrue(_face_alive(out))

    def test_strip_gray_checker_keeps_blue(self):
        im = Image.new("RGBA", (40, 40), (0, 0, 0, 0))
        d = ImageDraw.Draw(im)
        d.rectangle((0, 0, 12, 39), fill=(230, 230, 230, 255))
        d.ellipse((18, 8, 36, 32), fill=(40, 60, 140, 255))
        out = knock_bg.strip_gray_checker(im)
        a = np.asarray(out)
        self.assertLess(int((a[:, :12, 3] > 128).sum()), 10)
        self.assertGreater(int((a[8:32, 18:36, 3] > 128).sum()), 80)

    def test_checkerboard_bg(self):
        im = _bug((204, 204, 204, 255), size=96)
        px = im.load()
        for y in range(96):
            for x in range(96):
                if (x // 8 + y // 8) % 2 and px[x, y][0] > 180:
                    px[x, y] = (80, 80, 80, 255)
        out = knock_bg.apply(im)
        self.assertTrue(_face_alive(out), "바둑판을 지울 때 가면이 살아야 한다")
        self.assertTrue(_bg_gone(out, (204, 204, 204)))


if __name__ == "__main__":
    unittest.main()
