#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Evaluate published upload history into reward/punishment learning logs.

This tool is retained for scheduled compatibility. It no longer references
removed media agents; it only processes records already present in
`reports/history/upload_history.json`.
"""

from __future__ import annotations

import json
import os
import pickle
import sys
from datetime import datetime
from pathlib import Path
from typing import Any


if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")


HERE = Path(__file__).resolve()
AI_TEAM_ROOT = HERE.parents[3]
PROJECT_ROOT = AI_TEAM_ROOT.parents[1]
if str(AI_TEAM_ROOT) not in sys.path:
    sys.path.insert(0, str(AI_TEAM_ROOT))

from _shared.env import load_env


MEM_FILE = str(PROJECT_ROOT / "reports" / "history" / "upload_history.json")
REWARD_DIR = str(PROJECT_ROOT / "reports" / "learning" / "reward")
PUNISH_DIR = str(PROJECT_ROOT / "reports" / "learning" / "punishment")
TOKEN_FILE = str(HERE / "youtube_token.pickle")
VIEWS_THRESHOLD = 10_000


def _get_youtube():
    """Return a YouTube client from the local OAuth token, or None."""
    if not os.path.exists(TOKEN_FILE):
        return None
    try:
        from google.auth.transport.requests import Request
        from googleapiclient.discovery import build

        with open(TOKEN_FILE, "rb") as file:
            creds = pickle.load(file)
        if getattr(creds, "expired", False) and getattr(creds, "refresh_token", None):
            creds.refresh(Request())
            with open(TOKEN_FILE, "wb") as file:
                pickle.dump(creds, file)
        return build("youtube", "v3", credentials=creds)
    except Exception as exc:
        print(f"[Warning] YouTube client unavailable: {exc}")
        return None


def _fetch_stats(youtube, video_id: str) -> dict[str, int]:
    if not youtube or not video_id or video_id.startswith("DRY-RUN"):
        return {"views": 0, "likes": 0, "comments": 0}
    try:
        response = youtube.videos().list(part="statistics", id=video_id).execute()
        stats = (response.get("items") or [{}])[0].get("statistics", {})
        return {
            "views": int(stats.get("viewCount", 0)),
            "likes": int(stats.get("likeCount", 0)),
            "comments": int(stats.get("commentCount", 0)),
        }
    except Exception as exc:
        print(f"[Warning] Failed to fetch stats for {video_id}: {exc}")
        return {"views": 0, "likes": 0, "comments": 0}


def _append_jsonl(path: str, payload: dict[str, Any]) -> None:
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    with open(path, "a", encoding="utf-8") as file:
        file.write(json.dumps(payload, ensure_ascii=False) + "\n")


def auto_evaluate_performance() -> None:
    load_env(str(PROJECT_ROOT))
    if not os.path.exists(MEM_FILE):
        print(f"[Info] Upload history not found: {MEM_FILE}")
        return

    os.makedirs(REWARD_DIR, exist_ok=True)
    os.makedirs(PUNISH_DIR, exist_ok=True)

    with open(MEM_FILE, "r", encoding="utf-8") as file:
        history = json.load(file)

    youtube = _get_youtube()
    reward_count = 0
    punish_count = 0
    changed = False

    for record in history:
        if record.get("status") != "published":
            continue

        meta = record.get("metadata", {})
        platform = meta.get("platform", "youtube")
        video_id = meta.get("video_id") or meta.get("post_id", "")
        title = meta.get("youtube_title") or meta.get("title") or meta.get("caption") or "untitled"
        stats = _fetch_stats(youtube, video_id) if platform != "instagram" else {"views": 0, "likes": 0, "comments": 0}

        summary = {
            "agent": record.get("agent", "unknown"),
            "platform": platform,
            "title": title,
            "video_id": video_id,
            "views": stats["views"],
            "likes": stats["likes"],
            "comments": stats["comments"],
            "feedback_date": datetime.now().strftime("%Y-%m-%d %H:%M"),
        }

        if platform == "instagram" or stats["views"] >= VIEWS_THRESHOLD:
            summary["conclusion"] = "Keep as a positive learning example."
            _append_jsonl(os.path.join(REWARD_DIR, "success_log.jsonl"), summary)
            reward_count += 1
            label = "REWARD"
        else:
            summary["conclusion"] = "Below threshold; review title, hook, and audience fit."
            _append_jsonl(os.path.join(PUNISH_DIR, "fail_log.jsonl"), summary)
            punish_count += 1
            label = "PUNISH"

        print(f"{label} | [{summary['agent']}/{platform}] {title[:40]} | {stats['views']:,} views")
        record["status"] = "evaluated"
        changed = True

    if changed:
        with open(MEM_FILE, "w", encoding="utf-8") as file:
            json.dump(history, file, indent=2, ensure_ascii=False)
        print(f"Evaluation complete. REWARD: {reward_count}, PUNISH: {punish_count}")
    else:
        print("No published records to evaluate.")


if __name__ == "__main__":
    auto_evaluate_performance()
