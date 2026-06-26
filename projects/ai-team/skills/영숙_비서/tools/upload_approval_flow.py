#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Youngsuk upload approval compatibility helper.

The deleted media/upload agents are no longer part of the active ai-team roster.
This module remains so existing imports fail closed with a clear message instead
of trying to route work to removed paths.
"""

from __future__ import annotations

import sys
from typing import Any


if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")


def request_upload_approval(agent: str, platform: str, content_info: dict[str, Any] | None = None) -> dict[str, Any]:
    title = (content_info or {}).get("title") or (content_info or {}).get("caption") or "untitled"
    return {
        "approved": False,
        "stage": "inactive_upload_flow",
        "agent": agent,
        "platform": platform,
        "title": title,
        "message": "Upload approval is disabled because no active upload agent is registered.",
        "issues": ["inactive_upload_agent"],
    }


def inactive_upload_approval(content_info: dict[str, Any] | None = None) -> dict[str, Any]:
    return request_upload_approval("inactive", "unknown", content_info or {})


if __name__ == "__main__":
    print(request_upload_approval("inactive", "unknown", {"title": "compatibility check"}))
