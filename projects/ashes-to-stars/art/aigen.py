"""
재와 별 — AI 에셋 생성기 (Gemini 이미지 + 스타일 앵커 참조)

왜 참조 이미지가 핵심인가:
  프롬프트로 "픽셀아트 스타일"이라고 말해봐야 호출할 때마다 다른 그림이 나온다.
  이 저장소엔 이미 **품질이 확정된 스타일 기준**이 있다 — 오너의 캐릭터 시트.
  그걸 매 호출에 참조 이미지로 첨부해서 "이 그림과 같은 화풍으로"라고
  시키면 스타일 편차가 근본적으로 줄어든다. 앵커를 새로 그릴 필요가 없다.

배경 처리:
  Gemini 이미지 모델은 알파 채널을 안정적으로 못 만든다. 그래서 팔레트에
  절대 없는 순수 마젠타(#FF00FF) 위에 그리게 하고 크로마키로 뺀다.

사용:
    python3 aigen.py --spec props_spike.json --out-dir out_ai/
    python3 aigen.py --spec props_spike.json --out-dir out_ai/ --only dungeon_pillar
"""
from __future__ import annotations

import argparse
import base64
import json
import mimetypes
import os
import sys
import urllib.error
import urllib.request

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
MODEL = "gemini-3-pro-image"
CHROMA = (255, 0, 255)

STYLE_RULES = (
    "Match the art style of the reference image EXACTLY: hand-drawn pixel art, "
    "same dot size, same limited earthy palette, same soft shading and thin dark "
    "outlines, same 3/4 top-down quarter view with a ~30 degree downward camera angle. "
    "Do not use a photorealistic, 3D-rendered, vector, or smooth-gradient look. "
    "Render a single object centered in frame, no ground shadow, no text, no border, "
    "no extra props. The background must be a completely flat solid pure magenta "
    "(#FF00FF) with nothing else on it."
)


def _api_key() -> str:
    sys.path.insert(0, os.path.join(ROOT, "projects", "ai-team"))
    from _shared.env import load_env  # noqa: E402

    load_env()
    k = os.getenv("GEMINI_API_KEY")
    if not k:
        raise SystemExit("GEMINI_API_KEY 없음 — .env 확인")
    return k


def _inline(path: str) -> dict:
    mime = mimetypes.guess_type(path)[0] or "image/png"
    with open(path, "rb") as f:
        return {"inline_data": {"mime_type": mime, "data": base64.b64encode(f.read()).decode()}}


def generate(prompt: str, refs: list[str], key: str, model: str = MODEL) -> Image.Image:
    parts = [_inline(p) for p in refs]
    parts.append({"text": f"{prompt}\n\n{STYLE_RULES}"})
    body = json.dumps({"contents": [{"role": "user", "parts": parts}]}).encode()

    url = f"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={key}"
    req = urllib.request.Request(url, data=body, headers={"Content-Type": "application/json"})
    try:
        res = json.load(urllib.request.urlopen(req, timeout=300))
    except urllib.error.HTTPError as e:
        raise SystemExit(f"Gemini {e.code}: {e.read().decode()[:400]}")

    for cand in res.get("candidates", []):
        for part in cand.get("content", {}).get("parts", []):
            data = part.get("inlineData") or part.get("inline_data")
            if data:
                raw = base64.b64decode(data["data"])
                tmp = os.path.join(HERE, ".raw.png")
                with open(tmp, "wb") as f:
                    f.write(raw)
                return Image.open(tmp).convert("RGB")
    raise SystemExit(f"이미지 없음: {json.dumps(res)[:400]}")


def chroma_key(img: Image.Image, tol: int = 90) -> Image.Image:
    """마젠타 배경 → 투명. 가장자리 마젠타 번짐까지 걷어낸다."""
    a = np.asarray(img).astype(np.int16)
    d = np.sqrt(((a - np.array(CHROMA)) ** 2).sum(axis=2))
    alpha = np.where(d < tol, 0, 255).astype(np.uint8)

    # 마젠타 쪽으로 물든 가장자리 픽셀은 색을 되돌린다 (보라 테두리 방지)
    rgb = a.copy()
    fringe = (alpha == 255) & (d < tol * 2.2)
    rgb[fringe, 0] = np.minimum(rgb[fringe, 0], rgb[fringe, 1] + 40)
    rgb[fringe, 2] = np.minimum(rgb[fringe, 2], rgb[fringe, 1] + 40)

    out = np.dstack([rgb.astype(np.uint8), alpha])
    im = Image.fromarray(out, "RGBA")

    ys, xs = np.where(alpha > 0)
    if len(xs):
        im = im.crop((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))
    return im


def main(argv=None):
    ap = argparse.ArgumentParser(description="재와 별 AI 에셋 생성기")
    ap.add_argument("--spec", required=True, help="에셋 명세 JSON")
    ap.add_argument("--out-dir", required=True)
    ap.add_argument("--only", help="이름에 이 문자열이 든 항목만")
    ap.add_argument("--model", default=MODEL)
    ap.add_argument("--height", type=int, default=0, help="0이면 원본 크기 유지")
    ns = ap.parse_args(argv)

    with open(ns.spec, encoding="utf-8") as f:
        spec = json.load(f)
    refs = [os.path.join(HERE, r) if not os.path.isabs(r) else r for r in spec["refs"]]
    for r in refs:
        if not os.path.exists(r):
            raise SystemExit(f"참조 이미지 없음: {r}")

    key = _api_key()
    os.makedirs(ns.out_dir, exist_ok=True)

    for item in spec["assets"]:
        name = item["name"]
        if ns.only and ns.only not in name:
            continue
        dst = os.path.join(ns.out_dir, f"{name}.png")
        if os.path.exists(dst):
            print(f"건너뜀(이미 있음) {name}")
            continue
        img = chroma_key(generate(item["prompt"], refs, key, ns.model))
        if ns.height and img.height > ns.height:
            w = max(1, round(img.width * ns.height / img.height))
            img = img.resize((w, ns.height), Image.Resampling.BOX)
        img.save(dst)
        print(f"✅ {name} {img.size[0]}x{img.size[1]} → {dst}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
