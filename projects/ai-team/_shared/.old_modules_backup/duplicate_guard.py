"""
duplicate_guard.py — 중앙 중복 감지 모듈

에이전트는 자체 중복 체크를 하지 않고 이 모듈을 import해서 사용한다.
"""
import os
import json
import datetime
import difflib

# ── 공통 경로 탐색 ────────────────────────────────────────────────────────────
_here = os.path.dirname(os.path.abspath(__file__))
try:
    from env_loader import find_project_root
except ImportError:
    from _shared.env_loader import find_project_root

_root = find_project_root(_here)

_HISTORY_FILE   = os.path.join(_root, "reports", "history", "upload_history.json")
_BLOG_MEM_FILE  = os.path.join(_root, "reports", "history", "sukja_blog.json")


# ══════════════════════════════════════════════════════════════════════════════
# 공통 히스토리 읽기
# ══════════════════════════════════════════════════════════════════════════════

def _load_history(days: int, agent: str | None = None) -> list[dict]:
    """upload_history.json에서 최근 N일 레코드를 반환. agent 지정 시 필터."""
    if not os.path.exists(_HISTORY_FILE):
        return []
    try:
        cutoff = (datetime.datetime.now() - datetime.timedelta(days=days)).isoformat()
        history: list[dict] = json.load(open(_HISTORY_FILE, encoding="utf-8"))
        records = [r for r in history if r.get("uploaded_at", "") >= cutoff]
        if agent:
            records = [r for r in records if r.get("agent") == agent]
        return records
    except Exception:
        return []


# ══════════════════════════════════════════════════════════════════════════════

def get_recent_blog_titles(days: int = 30) -> list[str]:
    titles: list[str] = []
    if os.path.exists(_BLOG_MEM_FILE):
        try:
            cutoff = (datetime.datetime.now() - datetime.timedelta(days=days)).isoformat()
            data: list[dict] = json.load(open(_BLOG_MEM_FILE, encoding="utf-8"))
            for entry in data:
                if entry.get("date", "") >= cutoff[:10]:
                    t = entry.get("title", "")
                    if t:
                        titles.append(t)
        except Exception:
            pass
    return titles


def is_blog_title_used(title: str, threshold: float = 0.70,
                       days: int = 30) -> tuple[bool, float]:
    """블로그 제목이 최근 N일 내 발행 제목과 유사한지."""
    recent = get_recent_blog_titles(days)
    tl = title.lower()
    max_ratio = 0.0
    for prev in recent:
        ratio = difflib.SequenceMatcher(None, tl, prev.lower()).ratio()
        if ratio > max_ratio:
            max_ratio = ratio
    return max_ratio >= threshold, max_ratio
