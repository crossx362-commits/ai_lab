#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""공통 HTTP·노션 기록 모듈.

주식·코인 조사팀(행크/유나/레온/마켓데스크/소미) 전용 함수는 도메인 삭제(2026-07-08)와
함께 제거됨 — 필요해지면 git 이력에서 복구 가능. 현재 살아있는 소비처:
reports_manager.py(notion_page), notify.py/notion_publish.py(notion_report).
"""

from __future__ import annotations

import json
import os
import urllib.request


# ── HTTP ────────────────────────────────────────────────────────────────────
def _get(url: str, timeout: int = 12) -> str:
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0 (ai-team research)"})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return r.read().decode("utf-8", "replace")


def get_json(url: str, timeout: int = 12) -> dict:
    return json.loads(_get(url, timeout))


# ── 노션 기록 ───────────────────────────────────────────────────────────────
def notion_page(title: str, bullets: list[str]) -> bool:
    """NOTION_DATABASE_ID DB에 간결한 페이지(제목 + 불릿)를 만든다.
    너무 길지 않게 — 불릿은 최대 12개, 각 줄 1800자 컷."""
    key = os.getenv("NOTION_API_KEY", "").strip()
    db = os.getenv("NOTION_DATABASE_ID", "").strip()
    if not key or not db:
        return False
    headers = {
        "Authorization": f"Bearer {key}",
        "Notion-Version": "2022-06-28",
        "Content-Type": "application/json",
    }

    def _api(method: str, url: str, payload: dict | None = None):
        data = json.dumps(payload).encode("utf-8") if payload is not None else None
        req = urllib.request.Request(url, data=data, headers=headers, method=method)
        with urllib.request.urlopen(req, timeout=20) as r:
            return json.loads(r.read().decode("utf-8", "replace"))

    # title 속성 이름 동적 조회 (DB마다 다름)
    title_prop = "Name"
    try:
        meta = _api("GET", f"https://api.notion.com/v1/databases/{db}")
        for k, v in (meta.get("properties") or {}).items():
            if v.get("type") == "title":
                title_prop = k
                break
    except Exception:
        pass

    children = [
        {"object": "block", "type": "bulleted_list_item",
         "bulleted_list_item": {"rich_text": [{"type": "text", "text": {"content": (b or "")[:1800]}}]}}
        for b in bullets[:12] if (b or "").strip()
    ]
    payload = {
        "parent": {"database_id": db},
        "properties": {title_prop: {"title": [{"text": {"content": title[:200]}}]}},
        "children": children,
    }
    try:
        _api("POST", "https://api.notion.com/v1/pages", payload)
        return True
    except Exception as exc:
        print(f"  노션 기록 실패: {exc}")
        return False


def notion_report(title: str, body: str) -> str:
    """긴 텍스트 보고서를 NOTION_DATABASE_ID 페이지로 만들고 페이지 URL을 반환(실패 시 '')."""
    key = os.getenv("NOTION_API_KEY", "").strip()
    db = os.getenv("NOTION_DATABASE_ID", "").strip()
    if not key or not db:
        return ""
    headers = {
        "Authorization": f"Bearer {key}",
        "Notion-Version": "2022-06-28",
        "Content-Type": "application/json",
    }

    def _api(method: str, url: str, payload: dict | None = None):
        data = json.dumps(payload).encode("utf-8") if payload is not None else None
        req = urllib.request.Request(url, data=data, headers=headers, method=method)
        with urllib.request.urlopen(req, timeout=20) as r:
            return json.loads(r.read().decode("utf-8", "replace"))

    title_prop = "Name"
    try:
        meta = _api("GET", f"https://api.notion.com/v1/databases/{db}")
        for k, v in (meta.get("properties") or {}).items():
            if v.get("type") == "title":
                title_prop = k
                break
    except Exception:
        pass

    # 본문을 줄 단위 문단 블록으로 (한 블록 2000자 한도, 최대 100블록)
    blocks = []
    for line in (body or "").split("\n"):
        chunk = line[:1990]
        rich = [{"type": "text", "text": {"content": chunk}}] if chunk else []
        blocks.append({"object": "block", "type": "paragraph", "paragraph": {"rich_text": rich}})
        if len(blocks) >= 100:
            break
    payload = {
        "parent": {"database_id": db},
        "properties": {title_prop: {"title": [{"text": {"content": title[:200]}}]}},
        "children": blocks,
    }
    try:
        resp = _api("POST", "https://api.notion.com/v1/pages", payload)
        return resp.get("url", "") or ""
    except Exception as exc:
        print(f"  노션 보고서 기록 실패: {exc}")
        return ""
