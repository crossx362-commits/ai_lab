#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""재와 별 — 게임에 쓰이는 그림을 딥서치·검수·수정한다.

    python3 game_image_quality.py           # 검수 + 수정
    python3 game_image_quality.py --dry     # 보고서만
    python3 game_image_quality.py --send    # 못 고친 P0·크로마만 알림
    python3 game_image_quality.py --self-test
"""
from __future__ import annotations

import argparse
import importlib.util
import json
import re
import shutil
import sys
import urllib.error
import urllib.request
from datetime import datetime
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[4]
sys.path.insert(0, str(ROOT / "projects" / "ai-team"))

from _shared.env import load_env  # noqa: E402
from _shared.telegram import send  # noqa: E402

load_env(str(ROOT))

GAME = ROOT / "projects" / "ashes-to-stars"
RES = GAME / "unity" / "Assets" / "Resources"
OUT = ROOT / "output" / "qa" / "ashes-to-stars" / "image_quality"
NAMES = HERE.parents[2] / "마루_게임개발" / "tools" / "game_asset_names.py"
SCAN = ("sprites", "FX", "props", "ui")
TINY = 24
# 진짜 크로마키 잔상만. qc.py의 min(R,B)-G>25 는 자주색 몹·소환진을 오탐한다.
CHROMA = (255, 0, 255)
CHROMA_DIST = 60
SEMI = (16, 239)
NO_AUTO = {"FX", "ui"}
# 딥서치 실패 시에도 쓰는 기준(2026-08-16 실측 + Derek Yu·chroma despill)
BAKED = [
    "픽셀아트 알파는 0 또는 255(하드 매트). 중간값은 3D 티·진흙.",
    "크로마 잔상은 #FF00FF 근처만. 자주색 본체는 정체성이다.",
    "이미 들어간 프레임은 자르지 않는다 — 캔버스가 바뀌면 애니메이션이 튄다.",
    "아틀라스·FX 글로우는 자동으로 안 건드린다.",
]
SEARCH_Q = (
    "pixel art sprite hard matte binary alpha chroma key magenta spill",
    "2D game sprite magenta fringe cleanup without destroying purple pixels",
)
SEARCH_PAGES = (
    "https://www.derekyu.com/makegames/pixelart.html",
    "https://benmcewan.com/blog/understanding-despill-algorithms",
)


def _load(path: Path, name: str):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def _np_img(path: Path):
    import numpy as np
    from PIL import Image
    with Image.open(path) as im:
        arr = np.asarray(im.convert("RGBA")).copy()
    return arr


def _metrics(arr) -> dict:
    import numpy as np
    rgb = arr[..., :3].astype(int)
    al = arr[..., 3].astype(int)
    h, w = arr.shape[:2]
    vis = al > 128
    n = int(vis.sum())
    if n == 0:
        return {"w": w, "h": h, "empty": True, "chroma": 0.0, "semi": 0.0, "vis": 0}
    d = np.sqrt(((rgb - np.array(CHROMA)) ** 2).sum(-1))
    hard = (rgb[..., 1] < 40) & (np.minimum(rgb[..., 0], rgb[..., 2]) > 200)
    chroma = float(((d < CHROMA_DIST) | hard)[vis].mean() * 100)
    semi = int(((al > SEMI[0]) & (al < SEMI[1])).sum())
    solid = int((al >= SEMI[1]).sum())
    return {
        "w": w, "h": h, "empty": False, "chroma": round(chroma, 2),
        "semi": round(semi / max(1, semi + solid) * 100, 1), "vis": n,
    }


def _folder_of(rel: str) -> str:
    return rel.split("/", 1)[0]


def inspect_file(path: Path) -> tuple[list[str], dict]:
    try:
        m = _metrics(_np_img(path))
    except Exception as e:
        return [f"열 수 없음 ({type(e).__name__})"], {}
    flags = []
    if m.get("empty"):
        flags.append("빈 이미지")
        return flags, m
    if min(m["w"], m["h"]) < TINY:
        flags.append(f"너무 작음 {m['w']}x{m['h']}")
    if m["chroma"] > 0.2:
        flags.append(f"크로마 잔상 {m['chroma']:.1f}%")
    if _folder_of(path.name if "/" not in str(path) else "") != "skip":
        pass
    return flags, m


def _inspect_rel(path: Path, rel: str) -> list[str]:
    flags, m = inspect_file(path)
    folder = _folder_of(rel)
    if m and not m.get("empty") and folder not in NO_AUTO and m.get("semi", 0) > 12:
        flags.append(f"반투명 가장자리 {m['semi']:.1f}%")
    return flags


def name_problems(names, res: Path) -> tuple[list[str], list[str]]:
    missing, unused = [], []
    if names is None:
        return missing, unused
    try:
        exp = names.expected()
    except Exception:
        exp = {}
    for folder, files in exp.items():
        d = res / folder
        have = {p.name for p in d.glob("*.png")} if d.is_dir() else set()
        miss = [n for n in files if n not in have]
        if miss:
            missing.append(f"누락 {folder}: {len(miss)} — {', '.join(miss[:5])}")
    try:
        unused = names.unused_resource_problems(res)
    except Exception:
        unused = []
    return missing, unused


def patrol(res: Path | None = None) -> dict:
    res = res or RES
    names = _load(NAMES, "game_asset_names") if NAMES.is_file() else None
    missing, unused = name_problems(names, res)
    quality = []
    scanned = 0
    if res.is_dir():
        for folder in SCAN:
            d = res / folder
            if not d.is_dir():
                continue
            for p in sorted(d.rglob("*.png")):
                scanned += 1
                rel = str(p.relative_to(res))
                flags = _inspect_rel(p, rel)
                if flags:
                    quality.append({"path": rel, "flags": flags})
    leftover = [q for q in quality if any("크로마" in f or "열 수 없음" in f for f in q["flags"])]
    return {
        "scanned": scanned,
        "unused": unused,
        "missing": missing,
        "quality": quality,
        "p0": missing,
        "p1": leftover,
    }


def can_fix(rel: str, flags: list[str]) -> bool:
    if _folder_of(rel) in NO_AUTO:
        return False
    return any(f.startswith("크로마 잔상") or f.startswith("반투명") for f in flags)


def apply_fix(path: Path, rel: str) -> str | None:
    import numpy as np
    from PIL import Image
    before = _np_img(path)
    prev = _metrics(before)
    arr = before.copy()
    rgb = arr[..., :3].astype(int)
    al = arr[..., 3]
    vis = al > 128
    d = np.sqrt(((rgb - np.array(CHROMA)) ** 2).sum(-1))
    hard = (rgb[..., 1] < 40) & (np.minimum(rgb[..., 0], rgb[..., 2]) > 200)
    chroma = vis & ((d < CHROMA_DIST) | hard)
    if int(chroma.sum()):
        al = al.copy()
        al[chroma] = 0
        arr[..., 3] = al
    folder = _folder_of(rel)
    if folder not in NO_AUTO:
        al = arr[..., 3]
        if _metrics(arr)["semi"] > 12:
            snapped = al.copy()
            snapped[al < 128] = 0
            snapped[al >= 128] = 255
            arr[..., 3] = snapped
    after = _metrics(arr)
    if after.get("empty") and not prev.get("empty"):
        return None
    if after["vis"] < prev["vis"] * 0.92:
        return None
    better = after["chroma"] < prev["chroma"] - 0.05 or after["semi"] < prev["semi"] - 0.5
    if not better:
        return None
    bak = OUT / "backup" / rel
    bak.parent.mkdir(parents=True, exist_ok=True)
    if not bak.exists():
        shutil.copy2(path, bak)
    Image.fromarray(arr, "RGBA").save(path)
    return f"크로마 {prev['chroma']}→{after['chroma']} · 반투명 {prev['semi']}→{after['semi']}"


def fix_all(res: Path, quality: list[dict]) -> list[dict]:
    done = []
    for q in quality:
        rel = q["path"]
        if not can_fix(rel, q["flags"]):
            continue
        path = res / rel
        if not path.is_file():
            continue
        note = apply_fix(path, rel)
        if note:
            done.append({"path": rel, "fix": note})
    return done


def _fetch(url: str, timeout: float = 8.0) -> str:
    req = urllib.request.Request(
        url, headers={"User-Agent": "ByeoliImageQA/1.0 (local art patrol)"})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        raw = r.read(80_000)
    return raw.decode("utf-8", errors="replace")


def deep_search() -> dict:
    """매 실행 웹에서 기준을 다시 확인한다. 실패해도 내장 기준으로 수정을 진행한다."""
    notes = list(BAKED)
    hits = []
    try:
        q = urllib.request.quote(SEARCH_Q[0])
        html = _fetch(f"https://html.duckduckgo.com/html/?q={q}")
        titles = re.findall(r'class="result__a"[^>]*>(.*?)</a>', html, re.I | re.S)
        clean = [re.sub(r"<[^>]+>", "", t).strip() for t in titles]
        hits.extend(t for t in clean if t)
        hits = hits[:5]
    except (urllib.error.URLError, TimeoutError, OSError, ValueError) as e:
        hits.append(f"(검색 실패: {type(e).__name__})")
    for url in SEARCH_PAGES:
        try:
            text = re.sub(r"<[^>]+>", " ", _fetch(url))
            text = re.sub(r"\s+", " ", text)
            if re.search(r"pixel|chroma|despill|alpha|matte", text, re.I):
                hits.append(f"확인 {url}")
        except (urllib.error.URLError, TimeoutError, OSError, ValueError):
            continue
    return {"criteria": notes, "hits": hits, "at": datetime.now().isoformat(timespec="minutes")}


def write_report(data: dict, research: dict, fixed: list[dict]) -> Path:
    OUT.mkdir(parents=True, exist_ok=True)
    ts = datetime.now().strftime("%Y%m%d_%H%M")
    path = OUT / f"report_{ts}.md"
    qn = len(data.get("quality") or [])
    lines = [
        f"# 이미지 품질 {ts}",
        "",
        f"스캔 {data.get('scanned', 0)}장 · 표시 {qn} · 고침 {len(fixed)} · 이름 {len(data.get('unused') or [])}",
        "",
        "## 딥서치 기준",
        *(f"- {c}" for c in (research.get("criteria") or BAKED)),
        "",
    ]
    if research.get("hits"):
        lines.append("검색")
        lines.extend(f"- {h}" for h in research["hits"][:8])
        lines.append("")
    if fixed:
        lines.append("## 수정")
        lines.extend(f"- `{x['path']}` — {x['fix']}" for x in fixed[:80])
        lines.append("")
    if data.get("p0"):
        lines.append("## P0 필수 누락")
        lines.extend(f"- {x}" for x in data["p0"])
        lines.append("")
    leftover = data.get("quality") or []
    if leftover:
        lines.append("## 남은 표시")
        for q in leftover[:80]:
            lines.append(f"- `{q['path']}` — {', '.join(q['flags'])}")
        lines.append("")
    path.write_text("\n".join(lines), encoding="utf-8")
    payload = {
        "scanned": data.get("scanned", 0),
        "quality_n": qn,
        "fixed_n": len(fixed),
        "p0": data.get("p0") or [],
        "p1": data.get("p1") or [],
        "research": research,
        "fixed": fixed[:40],
    }
    (OUT / "latest.json").write_text(
        json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    (OUT / "research.json").write_text(
        json.dumps(research, ensure_ascii=False, indent=2), encoding="utf-8")
    return path


def self_test() -> int:
    from PIL import Image
    import numpy as np
    import tempfile
    tmp = Path(tempfile.mkdtemp())
    (tmp / "sprites").mkdir()
    Image.new("RGBA", (8, 8), (255, 0, 255, 255)).save(tmp / "sprites" / "tiny.png")
    body = np.zeros((64, 64, 4), np.uint8)
    body[16:48, 16:48] = (180, 60, 160, 255)  # 자주색 본체 — 지우면 안 됨
    body[16:48, 16] = (255, 0, 255, 255)       # 크로마 테두리
    Image.fromarray(body, "RGBA").save(tmp / "sprites" / "mob.png")
    data = patrol(tmp)
    flags = {q["path"]: q["flags"] for q in data["quality"]}
    tiny_ok = any("너무 작음" in f for f in flags.get("sprites/tiny.png", []))
    chroma_ok = any("크로마" in f for f in flags.get("sprites/mob.png", []))
    purple_ok = not any("크로마" in f for f in flags.get("sprites/tiny.png", []) if "너무" in f)
    # tiny is solid chroma so it may also say 크로마 — that's fine
    before = _np_img(tmp / "sprites" / "mob.png")
    note = apply_fix(tmp / "sprites" / "mob.png", "sprites/mob.png")
    after = _np_img(tmp / "sprites" / "mob.png")
    fringe_gone = int((after[16:48, 16, 3] == 0).sum()) >= 20
    body_kept = int((after[24:40, 24:40, 3] > 128).sum()) > 200
    research = deep_search() if False else {"criteria": BAKED, "hits": []}
    ok = tiny_ok and chroma_ok and note and fringe_gone and body_kept
    print("self-test", "PASS" if ok else "FAIL",
          {"tiny": tiny_ok, "chroma": chroma_ok, "fix": note,
           "fringe_gone": fringe_gone, "body_kept": body_kept,
           "flags": flags, "research_baked": bool(research["criteria"])})
    return 0 if ok else 1


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--send", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    ap.add_argument("--once", action="store_true")
    ap.add_argument("--dry", action="store_true", help="수정하지 않고 보고만")
    ap.add_argument("--no-search", action="store_true")
    args = ap.parse_args()
    if args.self_test:
        return self_test()
    research = {"criteria": BAKED, "hits": ["(이번 실행 검색 생략)"]} if args.no_search else deep_search()
    data = patrol()
    fixed = [] if args.dry else fix_all(RES, data.get("quality") or [])
    if fixed:
        data = patrol()
    path = write_report(data, research, fixed)
    print(f"스캔 {data['scanned']} · 표시 {len(data['quality'])} · 고침 {len(fixed)}")
    print("보고서", path)
    leftover = []
    leftover.extend(data.get("p0") or [])
    leftover.extend(
        f"{q['path']} {', '.join(q['flags'])}"
        for q in (data.get("p1") or [])[:6])
    if args.send and leftover:
        send("별이 · 못 고친 이미지\n" + "\n".join(leftover[:8]))
    return 1 if data.get("p0") else 0


if __name__ == "__main__":
    raise SystemExit(main())
