"""
duplicate_guard.py — 가희(콘텐츠 검수관) 소유 중앙 중복 감지 모듈

모든 에이전트(아린·숙자·루나)의 중복 감지 로직이 이곳에 집중됨.
에이전트는 자체 중복 체크를 하지 않고 이 모듈을 import해서 사용한다.

소유: 가희 (assets/tool-seeds/가희_검수관/)
유지보수: 가희 SKILL.md Section 5 참조
"""
import os
import re
import json
import datetime
import difflib

# ── 공통 경로 탐색 ────────────────────────────────────────────────────────────
_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, ".agent")):
        break
    _root = os.path.dirname(_root)

_HISTORY_FILE   = os.path.join(_root, ".agent", "memory", "upload_history.json")
_BLOG_MEM_FILE  = os.path.join(_root, ".agent", "memory", "sukja_blog.json")


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
# 루나 — YouTube 키워드·제목 중복
# ══════════════════════════════════════════════════════════════════════════════

def get_used_yt_keywords(days: int = 30) -> set[str]:
    """루나가 최근 N일간 사용한 YouTube 제목·키워드 소문자 set 반환."""
    used: set[str] = set()
    for r in _load_history(days, agent=None):
        if r.get("agent") not in ("루나", "루나_디렉터"):
            continue
        meta  = r.get("metadata", {})
        title = meta.get("youtube_title", "")
        prompt = meta.get("music_prompt", "")
        if title:
            used.add(title.lower())
            m = re.search(r"LUNA\s*-\s*(.+?)\s*[\[\(]", title)
            if m:
                used.add(m.group(1).strip().lower())
        if prompt:
            used.add(prompt[:40].lower())
    return used


def is_yt_keyword_used(keyword: str, days: int = 30) -> bool:
    """해당 YouTube 키워드가 최근 N일 내 이미 사용됐는지 확인."""
    kl = keyword.lower()
    used = get_used_yt_keywords(days)
    return kl in used or any(kl in u for u in used)


# ══════════════════════════════════════════════════════════════════════════════
# 아린 — Instagram 트렌드·프롬프트·캡션·해시태그 중복
# ══════════════════════════════════════════════════════════════════════════════

def get_used_insta_trends(days: int = 7) -> set[str]:
    """아린이 최근 N일간 사용한 트렌드 주제 set 반환."""
    used: set[str] = set()
    for r in _load_history(days, agent="아린"):
        meta = r.get("metadata", {})
        for key in ("trend", "topic", "keyword"):
            val = meta.get(key, "")
            if val:
                used.add(val.lower())
        # 캡션에서 제목 추출 시도
        caption = r.get("caption", "") or meta.get("caption", "")
        if caption:
            used.add(caption[:30].lower())
    return used


def is_insta_trend_used(topic: str, days: int = 7) -> bool:
    """해당 트렌드 주제가 최근 N일 내 이미 사용됐는지."""
    return topic.lower() in get_used_insta_trends(days)


def get_recent_insta_prompts(days: int = 14) -> list[str]:
    """아린이 최근 N일간 사용한 이미지 프롬프트 목록 반환."""
    prompts: list[str] = []
    for r in _load_history(days, agent="아린"):
        meta = r.get("metadata", {})
        p = meta.get("image_prompt", "") or meta.get("prompt", "")
        if p:
            prompts.append(p)
    return prompts


def is_prompt_similar(new_prompt: str, threshold: float = 0.60,
                      days: int = 14) -> tuple[bool, float, str]:
    """새 이미지 프롬프트가 최근 유사 프롬프트와 threshold 이상 유사한지."""
    recent = get_recent_insta_prompts(days)
    new_lower = new_prompt.lower()
    max_ratio, matched = 0.0, ""
    for prev in recent:
        ratio = difflib.SequenceMatcher(None, new_lower, prev.lower()).ratio()
        if ratio > max_ratio:
            max_ratio, matched = ratio, prev
    return max_ratio >= threshold, max_ratio, matched


def get_recent_insta_captions(days: int = 14) -> list[str]:
    """아린이 최근 N일간 게시한 캡션 목록 반환."""
    captions: list[str] = []
    for r in _load_history(days, agent="아린"):
        meta = r.get("metadata", {})
        cap = (r.get("caption", "") or
               meta.get("caption", "") or
               meta.get("full_caption", ""))
        if cap:
            captions.append(cap)
    return captions


def is_caption_duplicate(new_caption: str, threshold: float = 0.70,
                         days: int = 14) -> tuple[bool, float]:
    """캡션이 최근 N일 내 게시물과 threshold 이상 유사한지."""
    recent = get_recent_insta_captions(days)
    new_lower = new_caption.lower()
    max_ratio = 0.0
    for prev in recent:
        ratio = difflib.SequenceMatcher(None, new_lower, prev.lower()).ratio()
        if ratio > max_ratio:
            max_ratio = ratio
    return max_ratio >= threshold, max_ratio


def is_hashtag_duplicate(new_caption: str, overlap_threshold: float = 0.80,
                         days: int = 7) -> tuple[bool, float]:
    """해시태그 세트가 최근 N일 내 게시물과 overlap_threshold 이상 겹치는지."""
    new_tags = set(re.findall(r"#\w+", new_caption.lower()))
    if not new_tags:
        return False, 0.0
    captions = get_recent_insta_captions(days)
    max_overlap = 0.0
    for cap in captions:
        prev_tags = set(re.findall(r"#\w+", cap.lower()))
        if not prev_tags:
            continue
        overlap = len(new_tags & prev_tags) / len(new_tags | prev_tags)
        if overlap > max_overlap:
            max_overlap = overlap
    return max_overlap >= overlap_threshold, max_overlap


# ══════════════════════════════════════════════════════════════════════════════
# 숙자 — Blog 제목 중복
# ══════════════════════════════════════════════════════════════════════════════

def get_recent_blog_titles(days: int = 30) -> list[str]:
    """숙자가 최근 N일간 발행한 블로그 제목 목록 반환."""
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
