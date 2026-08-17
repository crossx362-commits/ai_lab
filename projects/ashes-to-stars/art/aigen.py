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
    "Render a single object centered in frame, no text, no border, no extra props. "
    # 아래 문장은 2026-08-14 실측 후 강화됐다. "no ground shadow" 한 마디로는
    # 그루터기·덤불 밑에 회색 지면 조각이 딸려 나왔다(지면 타일 위에 올리면
    # 사각 얼룩이 된다). 모델은 '그림자'만 금지어로 읽고 '땅'은 배경이 아니라
    # 대상의 일부로 취급한다 → 땅·흙·풀·잔디 조각을 개별로 금지해야 한다.
    "CRITICAL: the object must be cut off cleanly at its own base. Do NOT draw any "
    "ground, floor, terrain, soil, dirt patch, grass patch, snow patch, rubble scatter "
    "or cast shadow beneath or around the object. Nothing but flat magenta may touch "
    "the object's silhouette. "
    "The background must be a completely flat solid pure magenta (#FF00FF) with "
    "nothing else on it."
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


# 오너 지시(2026-08-18): **모든 이미지는 힉스필드 Nano Banana 2 + 언리미티드.**
# CLI job_type 은 `nano_banana_flash` 다. 별칭 `nano_banana_2` 는 Pro 로 붙는다
# (`higgsfield model get nano_banana_2` → nano_banana_pro, 실측 2026-08-18).
# 언리미티드 토글은 higgsfield.ai 웹에서만 동작한다(공식: CLI·MCP는 크레딧).
# 웹에서 만들 때: 모델 Nano Banana 2, Unlimited ON, 해상도 2K(언리미티드 한도).
# `--model`로 덮어쓰지 마라. Pro·Gemini·Imagine 금지.
HF_MODEL = "nano_banana_flash"
HF_RESOLUTION = "2k"
# 오너 2026-08-18: 할로우 나이트면 흑백 느낌. 상세는 STYLE_HOLLOW.md.
HOLLOW_STYLE = (
    "STYLE LOCK — Hollow Knight official sprite language, NOT generic cartoon bugs: "
    "Near-monochrome ink-and-wash. Body is VOID BLACK / charcoal. Face is a bone-white mask "
    "with EMPTY black eye-sockets (no pupils, no iris, no angry cartoon eyes). "
    "Thick smooth dark outlines + soft painterly shading inside. One accent color at most, "
    "used as a tiny glow. NO saturated green moss, NO crimson rage, NO gold shine, NO grass "
    "or ground shadow under the feet. NOT pixel art, NOT photoreal, NOT 3D, NOT chibi-cute. "
    "Read as a dark silhouette first, then a pale mask."
)
_HF_ALIAS_TO_BANANA2 = {"nano_banana_2", "nano_banana_pro", "nano_banana"}


def resolve_hf_model(name: str | None) -> str:
    n = (name or HF_MODEL).strip() or HF_MODEL
    if n in _HF_ALIAS_TO_BANANA2:
        print(f"⚠️ {n} 은 Nano Banana 2가 아니다(CLI에서 Pro). {HF_MODEL} 로 보낸다.")
        return HF_MODEL
    return n


def generate_hf(prompt: str, refs: list[str], model: str = HF_MODEL,
                resolution: str = HF_RESOLUTION, aspect: str = "1:1",
                rules: str = None) -> Image.Image:
    """Higgsfield Nano Banana 2 (`nano_banana_flash`) 경로. Pro·Gemini로 보내지 않는다."""
    import shutil
    import subprocess
    import tempfile

    cli = shutil.which("higgsfield") or "/opt/homebrew/bin/higgsfield"
    model = resolve_hf_model(model)
    resolution = resolution or HF_RESOLUTION
    print(f"   Higgsfield Nano Banana 2 ({model}) {resolution} — CLI는 언리미티드 불가, 웹은 토글 ON")
    cmd = [cli, "generate", "create", model, "--resolution", resolution,
           "--aspect-ratio", aspect,
           "--prompt", f"{HOLLOW_STYLE}\n\n{prompt}\n\n{rules or STYLE_RULES}",
           "--wait", "--wait-timeout", "10m"]
    for r in refs:
        cmd += ["--image-references", r]

    # ⚠️ 산발 실패가 실재한다(2026-08-14 실측): **완전히 같은 요청**이 1차 실패·2차 성공했다.
    #    처음엔 "강화한 프롬프트가 원인"이라 단정했는데 n=1 비교였고 틀렸다 —
    #    같은 입력을 재실행해 재현되는지부터 봐야 원인을 말할 수 있다.
    #    실패한 잡은 크레딧을 차감하지 않으므로 재시도 비용은 시간뿐이다.
    urls = []
    for attempt in range(1, 4):
        p = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", timeout=900)
        urls = [ln.strip() for ln in (p.stdout or "").splitlines() if ln.strip().startswith("http")]
        if urls:
            break
        print(f"   ↻ 재시도 {attempt}/3 — {((p.stdout or '') + (p.stderr or '')).strip()[:120]}")
    if not urls:
        raise SystemExit(f"Higgsfield 실패(3회): {(p.stdout or '') + (p.stderr or '')}"[:400])

    with urllib.request.urlopen(urls[-1], timeout=180) as r:
        data = r.read()
    tmp = tempfile.NamedTemporaryFile(suffix=".png", delete=False)
    tmp.write(data)
    tmp.close()
    return Image.open(tmp.name).convert("RGB")


def chroma_key(img: Image.Image, tol: int = 120) -> Image.Image:
    """마젠타 배경 → 투명 + 디스필.

    거리 임계값만으로는 부족하다(2026-08-14 실측): 첫 판은 기둥 좌우 실루엣에
    자주색 테두리가 그대로 남았다. 배경이 반투명하게 섞인 안티에일리어싱
    가장자리는 '마젠타에 가깝진 않지만 마젠타 쪽으로 물든' 상태라 거리
    테스트를 통과해버리기 때문이다.
    → 알파는 거리로 자르되, **색은 별도로 디스필**한다. 마젠타는 R·B가 높고
      G가 낮은 색이므로, G보다 과하게 높은 R·B를 끌어내리면 원래 색이 돌아온다.
    """
    a = np.asarray(img).astype(np.int16)
    d = np.sqrt(((a - np.array(CHROMA)) ** 2).sum(axis=2))
    alpha = np.where(d < tol, 0, 255).astype(np.uint8)

    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    # 마젠타 오염도 = R·B 중 작은 쪽이 G를 얼마나 넘어서는가
    spill = np.minimum(r, b) - g
    hit = (alpha == 255) & (spill > 0)
    cap = g + np.maximum(0, (spill * 0.25).astype(np.int16))  # G 기준으로 되돌린다
    rgb = a.copy()
    rgb[..., 0] = np.where(hit, np.minimum(r, cap), r)
    rgb[..., 2] = np.where(hit, np.minimum(b, cap), b)

    out = np.dstack([np.clip(rgb, 0, 255).astype(np.uint8), alpha])
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
    ap.add_argument("--model", default="")
    ap.add_argument("--backend", choices=["gemini", "higgsfield"], default="higgsfield")
    ap.add_argument("--resolution", default=HF_RESOLUTION, help="Nano Banana 2 기본 2k")
    ap.add_argument("--height", type=int, default=0, help="0이면 원본 크기 유지")
    ns = ap.parse_args(argv)
    if ns.backend == "gemini":
        raise SystemExit("오너 지시(2026-08-18): Gemini 금지. 힉스필드 Nano Banana 2만.")

    with open(ns.spec, encoding="utf-8") as f:
        spec = json.load(f)
    refs = [os.path.join(HERE, r) if not os.path.isabs(r) else r for r in spec["refs"]]
    for r in refs:
        if not os.path.exists(r):
            raise SystemExit(f"참조 이미지 없음: {r}")

    key = _api_key() if ns.backend == "gemini" else None
    os.makedirs(ns.out_dir, exist_ok=True)

    for item in spec["assets"]:
        name = item["name"]
        if ns.only and ns.only not in name:
            continue
        dst = os.path.join(ns.out_dir, f"{name}.png")
        if os.path.exists(dst):
            print(f"건너뜀(이미 있음) {name}")
            continue
        # 항목이 자기 규칙을 갖고 있으면 그걸 쓴다 — 시트 생성처럼 "단일 오브젝트 중앙"
        # 규칙이 정면으로 방해가 되는 경우가 있다. spec의 "rules_ref"는 spec 최상단
        # "rulesets"에서 찾는다(프롬프트가 두 곳으로 갈라지지 않게 파일 안에 묶어 둔다).
        rules = spec.get("rulesets", {}).get(item.get("rules_ref", ""), None)
        aspect = item.get("aspect", "1:1")
        if ns.backend == "gemini":
            raw = generate(item["prompt"], refs, key, ns.model or MODEL)
        else:
            raw = generate_hf(item["prompt"], refs, ns.model or HF_MODEL,
                              resolution=ns.resolution, aspect=aspect, rules=rules)
        img = chroma_key(raw) if not item.get("keep_bg") else raw.convert("RGBA")
        if ns.height and img.height > ns.height:
            w = max(1, round(img.width * ns.height / img.height))
            img = img.resize((w, ns.height), Image.Resampling.BOX)
        img.save(dst)
        print(f"✅ {name} {img.size[0]}x{img.size[1]} → {dst}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
