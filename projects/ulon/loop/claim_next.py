#!/usr/bin/env python3
"""바퀴 시작 전에 그록 카드를 진행 중으로 집어 제목을 찍는다."""
from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
from datetime import datetime, timezone, timedelta
from pathlib import Path

SKIP_OWNERS = {"gpt", "codex", "openai"}
SKIP_IDS = {"now", "gpt-gfx-ui"}
EDITOR_BLOCKED = {"client-rebuild", "two-client-core"}
STATUS_SKIP = (
    "에디터",
    "재빌드",
    "two_client",
    "2인",
    "사람 확인",
    "사람/",
    "Codex",
    "GPT",
    "그래픽",
    "월드맵",
    "절반",
    "동화풍",
)


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


def invent_from_status(root: Path, cards: list, loop_no: int) -> dict | None:
    title = _status_next(root, cards)
    if not title:
        return None
    slug = re.sub(r"[^a-zA-Z0-9가-힣]+", "-", title).strip("-")[:24] or "auto"
    return {
        "id": f"auto-{loop_no}-{slug}",
        "title": title,
        "difficulty": "중",
        "model": "grok-4.6",
        "status": "진행 중",
        "assigned_loop": loop_no,
        "completed_loop": None,
        "commit": "",
        "rationale": "대기 카드 없음. STATUS에서 지금 할 수 있는 항목을 루프가 집음.",
        "fail_streak": 0,
    }


def _status_next(root: Path, cards: list) -> str:
    p = root / "docs" / "STATUS.md"
    if not p.exists():
        return ""
    text = p.read_text(encoding="utf-8")
    start = text.find("## 다음 할 것")
    if start < 0:
        return ""
    rest = text[start:]
    end = rest.find("\n## ", 1)
    section = rest if end < 0 else rest[:end]
    have = {(c.get("title") or "")[:40] for c in cards}
    for line in section.splitlines():
        m = re.match(r"^\d+\.\s+(.+)$", line.strip())
        if not m:
            continue
        item = m.group(1).strip()
        if any(s in item for s in STATUS_SKIP):
            continue
        if item[:40] in have:
            continue
        return item[:80]
    # 시스템 표에서 부분/없음
    in_table = False
    for line in text.splitlines():
        if line.startswith("#") and "시스템 상태" in line:
            in_table = True
            continue
        if in_table and line.startswith("## ") and "시스템 상태" not in line:
            break
        if not in_table or not line.startswith("|"):
            continue
        cells = [c.strip() for c in line.split("|")[1:-1]]
        if len(cells) < 2 or cells[0] in {"시스템"}:
            continue
        if cells[1] not in {"부분", "없음", "미확인"}:
            continue
        name = cells[0]
        if any(s in name for s in ("그래픽", "UI", "아트", "사운드", "월드맵")):
            continue
        return name[:80]
    return ""


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
        pick = invent_from_status(root, cards, args.loop)
        if pick:
            cards.append(pick)
            data["cards"] = cards
    if not pick:
        print(args.model_mid)
        print(args.effort_mid)
        print("서버·룰 미완 항목 하나")
        asg = {
            "loop": args.loop,
            "id": "",
            "title": "서버·룰 미완 항목 하나",
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
