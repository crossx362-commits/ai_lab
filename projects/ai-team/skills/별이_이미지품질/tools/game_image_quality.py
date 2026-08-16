#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""재와 별 — 게임에 쓰이는 그림의 품질을 읽기 전용으로 순찰한다.

    python3 game_image_quality.py           # 보고서만
    python3 game_image_quality.py --send    # P0(필수 누락)만 알림
    python3 game_image_quality.py --self-test
"""
from __future__ import annotations

import argparse
import importlib.util
import json
import sys
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
QC = GAME / "art" / "qc.py"
SCAN = ("sprites", "FX", "props")
TINY = 24


def _load(path: Path, name: str):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def _inspect(path: Path, qc) -> list[str]:
    flags = []
    try:
        from PIL import Image
        with Image.open(path) as im:
            w, h = im.size
        if min(w, h) < TINY:
            flags.append(f"너무 작음 {w}x{h}")
    except Exception as e:
        return [f"열 수 없음 ({type(e).__name__})"]
    if qc is None:
        return flags
    try:
        info = qc.inspect(str(path))
    except Exception as e:
        flags.append(f"검수 실패 ({type(e).__name__})")
        return flags
    flags.extend(info.get("flags") or [])
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
    qc = _load(QC, "art_qc") if QC.is_file() else None
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
                flags = _inspect(p, qc)
                if flags:
                    quality.append({"path": str(p.relative_to(res)), "flags": flags})
    p1 = [q for q in quality if any("마젠타" in f or "열 수 없음" in f for f in q["flags"])]
    return {
        "scanned": scanned,
        "unused": unused,
        "missing": missing,
        "quality": quality,
        "p0": missing,
        "p1": p1,
    }


def write_report(data: dict) -> Path:
    OUT.mkdir(parents=True, exist_ok=True)
    ts = datetime.now().strftime("%Y%m%d_%H%M")
    path = OUT / f"report_{ts}.md"
    qn = len(data.get("quality") or [])
    un = len(data.get("unused") or [])
    lines = [
        f"# 이미지 품질 순찰 {ts}",
        "",
        f"스캔 {data.get('scanned', 0)}장 · 품질 표시 {qn} · 미사용/이름 {un}",
        "",
    ]
    if data.get("p0"):
        lines.append("## P0 필수 누락")
        lines.extend(f"- {x}" for x in data["p0"])
        lines.append("")
    if data.get("quality"):
        lines.append("## 품질 표시")
        for q in data["quality"][:80]:
            lines.append(f"- `{q['path']}` — {', '.join(q['flags'])}")
        lines.append("")
    if data.get("unused"):
        lines.append("## 이름 검사")
        lines.extend(f"- {x}" for x in data["unused"][:40])
        lines.append("")
    path.write_text("\n".join(lines), encoding="utf-8")
    (OUT / "latest.json").write_text(
        json.dumps({k: (v if k != "unused" else v[:40]) for k, v in data.items()
                    if k != "quality"} | {"quality_n": qn},
                   ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    return path


def self_test() -> int:
    from PIL import Image
    import tempfile
    tmp = Path(tempfile.mkdtemp())
    (tmp / "sprites").mkdir()
    tiny = tmp / "sprites" / "tiny.png"
    Image.new("RGBA", (8, 8), (255, 0, 255, 255)).save(tiny)
    data = patrol(tmp)
    ok = any("너무 작음" in f for q in data["quality"] for f in q["flags"])
    print("self-test", "PASS" if ok else "FAIL", data)
    return 0 if ok else 1


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--send", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    ap.add_argument("--once", action="store_true")
    args = ap.parse_args()
    if args.self_test:
        return self_test()
    data = patrol()
    path = write_report(data)
    print(f"스캔 {data['scanned']} · 품질 {len(data['quality'])} · 이름 {len(data['unused'])}")
    print("보고서", path)
    if args.send and data.get("p0"):
        send("별이 · 이미지 P0\n" + "\n".join(data["p0"][:8]))
    return 1 if data.get("p0") else 0


if __name__ == "__main__":
    raise SystemExit(main())
