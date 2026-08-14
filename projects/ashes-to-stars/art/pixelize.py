"""
재와 별 — 공용 픽셀화 후처리기 (스타일 정규화기)

무엇을 푸는가:
  AI로 500장을 뽑으면 500가지 스타일이 나온다. 프롬프트로는 못 막는다.
  그래서 **스타일을 프롬프트가 아니라 코드가 강제한다** — 생성기가 무엇이든
  (Gemini·Higgsfield·블렌더 렌더·오너의 손그림) 산출물 전부를 이 파일 하나로
  통과시키면 해상도·색·외곽선이 물리적으로 같아진다.

  이 저장소는 이미 답의 절반을 갖고 있었다: gen_props.py의 후처리
  (저해상도 → 팔레트 양자화 → 1px 외곽선)가 사실 스타일 정규화기다.
  그걸 프랍 전용에서 떼어내 모든 에셋의 공용 관문으로 승격시킨 것이 이 파일이다.

⚠️ 캐릭터에는 쓰지 마라 (2026-08-14 실측으로 확인):
  이 파일을 처음 만들 때의 가설은 "캐릭터(416x297, 9126색)가 프랍(96px, 8색)과
  안 맞으니 캐릭터를 픽셀화해 맞추자"였다. **정반대였다.** 캐릭터의 9126색은
  스타일이 나빠서가 아니라 안티에일리어싱 때문이고, 그냥 축소만 해도 훌륭한
  도트가 된다. 여기에 팔레트 양자화를 걸면 색이 죽고, 강제 외곽선이 얼굴을
  뭉개고, 알파 이진화가 디테일을 갉아먹는다(비교본 /tmp/px_spike/compare.png).
  정작 품질이 떨어지는 쪽은 블렌더 절차생성 프랍이었다.
  → 통일 방향은 "캐릭터를 내리기"가 아니라 "프랍·배경을 캐릭터 수준으로 올리기".
  이 정규화기는 **AI가 새로 뽑은 프랍·타일·아이콘을 서로 맞추는 용도**로만 쓴다.

파이프라인 (순서가 중요하다):
  ① 알파 바운딩박스로 트림 — 여백 차이가 크기 불일치의 첫 번째 원인
  ② 목표 높이로 축소        — 도트 크기를 물리적으로 통일
  ③ 알파 이진화             — 픽셀아트에 반투명 가장자리는 없다 (있으면 3D 티가 난다)
  ④ 공용 팔레트로 양자화     — ★ 스타일 통일의 핵심. 팔레트는 palette.json 하나로 공유
  ⑤ 1px 외곽선              — 배경에서 분리

사용:
    python3 pixelize.py build-palette <입력...> -o palette.json   # 기준 팔레트 추출
    python3 pixelize.py apply <입력...> --height 96 --out-dir out/
"""
from __future__ import annotations

import argparse
import json
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
PALETTE_PATH = os.path.join(HERE, "palette.json")

ALPHA_CUT = 128          # 이 미만은 완전 투명 (픽셀아트는 반투명이 없다)
OUTLINE_RGB = (10, 8, 13)


# ─────────────────────────────────────────── 팔레트

def build_palette(paths: list[str], size: int = 32) -> list[list[int]]:
    """여러 이미지의 보이는 픽셀을 모아 대표색 size개를 뽑는다.

    이미 룩이 확정된 에셋(프랍·타일)에서 뽑으면, 앞으로 생성할 모든 것이
    '이미 존재하는 세계의 색'으로 강제 편입된다. 앵커를 새로 그릴 필요가 없다.
    """
    pool = []
    for p in paths:
        a = np.asarray(Image.open(p).convert("RGBA"))
        vis = a[a[..., 3] >= ALPHA_CUT][:, :3]
        if len(vis) == 0:
            continue
        # 이미지당 상한을 둬야 큰 이미지 한 장이 팔레트를 독점하지 않는다
        if len(vis) > 20000:
            idx = np.linspace(0, len(vis) - 1, 20000).astype(int)
            vis = vis[idx]
        pool.append(vis)
    if not pool:
        raise SystemExit("팔레트를 뽑을 픽셀이 없다")

    flat = np.concatenate(pool, axis=0).astype(np.uint8)
    # PIL의 median cut을 그대로 쓴다 (결정적이고 의존성이 늘지 않는다)
    strip = Image.fromarray(flat.reshape(-1, 1, 3), "RGB")
    pal_img = strip.quantize(colors=size, method=Image.Quantize.MEDIANCUT)
    raw = pal_img.getpalette()[: size * 3]
    return [raw[i:i + 3] for i in range(0, len(raw), 3)]


def load_palette(path: str = PALETTE_PATH) -> np.ndarray:
    with open(path, encoding="utf-8") as f:
        return np.array(json.load(f)["colors"], dtype=np.int16)


# ─────────────────────────────────────────── 후처리 단계

def _trim(img: Image.Image) -> Image.Image:
    a = np.asarray(img)
    mask = a[..., 3] >= ALPHA_CUT
    if not mask.any():
        return img
    ys, xs = np.where(mask)
    return img.crop((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))


def _downscale(img: Image.Image, height: int) -> Image.Image:
    if img.height <= height:
        return img
    w = max(1, round(img.width * height / img.height))
    # BOX = 면적 평균. LANCZOS는 링잉으로 없던 색을 만들어 양자화를 어지럽힌다.
    return img.resize((w, height), Image.Resampling.BOX)


def _quantize(img: Image.Image, palette: np.ndarray) -> Image.Image:
    a = np.asarray(img).astype(np.int16)
    rgb, alpha = a[..., :3], a[..., 3]
    solid = alpha >= ALPHA_CUT

    # 각 픽셀 → 팔레트에서 가장 가까운 색 (제곱거리, 결정적)
    d = ((rgb[:, :, None, :] - palette[None, None, :, :]) ** 2).sum(axis=3)
    out_rgb = palette[d.argmin(axis=2)].astype(np.uint8)

    out = np.zeros_like(a, dtype=np.uint8)
    out[..., :3] = out_rgb
    out[..., 3] = np.where(solid, 255, 0)     # 알파 이진화
    out[~solid, :3] = 0
    return Image.fromarray(out, "RGBA")


def _outline(img: Image.Image) -> Image.Image:
    a = np.asarray(img).copy()
    solid = a[..., 3] == 255
    # 상하좌우 한 칸이라도 비어 있으면 가장자리 픽셀
    pad = np.pad(solid, 1, constant_values=False)
    edge = solid & ~(
        pad[:-2, 1:-1] & pad[2:, 1:-1] & pad[1:-1, :-2] & pad[1:-1, 2:]
    )
    a[edge, :3] = OUTLINE_RGB
    return Image.fromarray(a, "RGBA")


def normalize(path: str, height: int, palette: np.ndarray, outline: bool = True) -> Image.Image:
    img = Image.open(path).convert("RGBA")
    img = _trim(img)
    img = _downscale(img, height)
    img = _quantize(img, palette)
    if outline:
        img = _outline(img)
    return img


# ─────────────────────────────────────────── CLI

def main(argv=None):
    ap = argparse.ArgumentParser(description="재와 별 공용 픽셀화 후처리기")
    sub = ap.add_subparsers(dest="cmd", required=True)

    b = sub.add_parser("build-palette", help="기준 팔레트 추출")
    b.add_argument("inputs", nargs="+")
    b.add_argument("-o", "--out", default=PALETTE_PATH)
    b.add_argument("--size", type=int, default=32)

    a = sub.add_parser("apply", help="후처리 적용")
    a.add_argument("inputs", nargs="+")
    a.add_argument("--height", type=int, required=True)
    a.add_argument("--out-dir", required=True)
    a.add_argument("--palette", default=PALETTE_PATH)
    a.add_argument("--no-outline", action="store_true")

    ns = ap.parse_args(argv)

    if ns.cmd == "build-palette":
        colors = build_palette(ns.inputs, ns.size)
        with open(ns.out, "w", encoding="utf-8") as f:
            json.dump({"size": len(colors), "colors": colors}, f, indent=1)
        print(f"팔레트 {len(colors)}색 → {ns.out}")
        return 0

    pal = load_palette(ns.palette)
    os.makedirs(ns.out_dir, exist_ok=True)
    for p in ns.inputs:
        out = normalize(p, ns.height, pal, outline=not ns.no_outline)
        dst = os.path.join(ns.out_dir, os.path.basename(p))
        out.save(dst)
        print(f"{os.path.basename(p)} → {out.size[0]}x{out.size[1]}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
