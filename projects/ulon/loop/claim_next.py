#!/usr/bin/env python3
"""바퀴 시작 전에 그록 카드를 진행 중으로 집어 제목을 찍는다."""
from __future__ import annotations

import argparse
import json
import os
import subprocess
from datetime import datetime, timezone, timedelta
from pathlib import Path

SKIP_OWNERS = {"gpt", "codex", "openai"}
SKIP_IDS = {"now", "gpt-gfx-ui"}
EDITOR_BLOCKED = {"client-rebuild", "two-client-core"}


def editor_busy() -> bool:
    try:
        out = subprocess.check_output(
            ["pgrep", "-f", "Unity.app/Contents/MacOS/Unity"],
            text=True,
            stderr=subprocess.DEVNULL,
        )
        return bool(out.strip())
    except Exception:
        return False


def grok_card(c: dict) -> bool:
    if c.get("id") in SKIP_IDS:
        return False
    raw = (c.get("model") or "").strip()
    owner = (c.get("owner") or "").strip().lower()
    if owner in SKIP_OWNERS or raw.lower() in SKIP_OWNERS:
        return False
    if raw and not raw.startswith("grok"):
        return False
    return True


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", required=True)
    ap.add_argument("--loop", type=int, required=True)
    ap.add_argument("--model-low", default="grok-4.6")
    ap.add_argument("--model-mid", default="grok-4.6")
    ap.add_argument("--model-high", default="grok-4.6")
    ap.add_argument("--effort-low", default="low")
    ap.add_argument("--effort-mid", default="medium")
    ap.add_argument("--effort-high", default="high")
    args = ap.parse_args()
    root = Path(args.root)
    board_path = root / "docs" / "board.json"
    by_diff = {"하": args.model_low, "중": args.model_mid, "상": args.model_high}
    effort = {"하": args.effort_low, "중": args.effort_mid, "상": args.effort_high}
    data = {"cards": []}
    if board_path.exists():
        try:
            data = json.loads(board_path.read_text(encoding="utf-8"))
        except Exception:
            data = {"cards": []}
    cards = data.get("cards") or []
    busy = editor_busy()
    pick = None
    for want in ("진행 중", "대기"):
        for c in cards:
            if c.get("status") != want or not grok_card(c):
                continue
            if busy and c.get("id") in EDITOR_BLOCKED:
                continue
            pick = c
            break
        if pick:
            break
    if not pick:
        print(args.model_mid)
        print(args.effort_mid)
        print("STATUS 다음 할 것 1번 — 고르면 즉시 보드를 진행 중으로")
        asg = {
            "loop": args.loop,
            "id": "",
            "title": "STATUS 다음 할 것 1번",
            "claimed": False,
        }
    else:
        pick["status"] = "진행 중"
        pick["assigned_loop"] = args.loop
        pick["model"] = pick.get("model") or args.model_mid
        d = pick.get("difficulty") or "중"
        m = (pick.get("model") or "").strip()
        model = m if m.startswith("grok") else by_diff.get(d, args.model_mid)
        title = (pick.get("title") or pick.get("id") or "").strip()
        data["updated_at"] = datetime.now(timezone(timedelta(hours=9))).strftime(
            "%Y-%m-%dT%H:%M:%S%z"
        )
        board_path.write_text(
            json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
        )
        print(model)
        print(effort.get(d, args.effort_mid))
        print(title)
        asg = {
            "loop": args.loop,
            "id": pick.get("id") or "",
            "title": title,
            "claimed": True,
        }
    out = root / "logs" / "current_assignment.json"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(asg, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
