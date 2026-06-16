"""
content_inspector.py — 경수(수사관) 인스타/유튜브 콘텐츠 검수 모듈
Instagram 포스팅 캡션 및 YouTube 메타데이터를 검수합니다.
"""
import os
import sys
import re

# _shared 경로 추가
_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, "reports")):
        break
    _root = os.path.dirname(_root)
_shared = os.path.join(_root, "_shared")
if _shared not in sys.path:
    sys.path.insert(0, _shared)
if _root not in sys.path:
    sys.path.insert(0, _root)

# 금지 키워드 목록
_BANNED_PHRASES = [
    "AI 생성", "인공지능", "머신러닝", "딥러닝", "4차산업",
    "이재명", "윤석열", "정치", "선거", "국회",
    "도박", "불법", "욕설", "스팸",
]
_MIN_CAPTION_LEN = 10
_MAX_CAPTION_LEN = 2200
_MAX_HASHTAGS = 30
_MIN_HASHTAGS = 2
_MIN_YT_TITLE_LEN = 5
_MAX_YT_TITLE_LEN = 100
_MIN_YT_DESC_LEN = 20
_MAX_YT_DESC_LEN = 5000


def inspect_caption(caption: str) -> dict:
    """
    인스타그램 캡션 검수.
    반환: {"pass": bool, "issues": [str], "score": int}
    """
    issues = []
    score = 100

    if not caption or len(caption.strip()) < _MIN_CAPTION_LEN:
        issues.append(f"캡션이 너무 짧습니다 (최소 {_MIN_CAPTION_LEN}자)")
        score -= 30

    if len(caption) > _MAX_CAPTION_LEN:
        issues.append(f"캡션이 너무 깁니다 (최대 {_MAX_CAPTION_LEN}자)")
        score -= 20

    lower = caption.lower()
    for phrase in _BANNED_PHRASES:
        if phrase.lower() in lower:
            issues.append(f"금지 키워드 포함: '{phrase}'")
            score -= 25

    hashtags = re.findall(r"#\w+", caption)
    if len(hashtags) > _MAX_HASHTAGS:
        issues.append(f"해시태그 과다 ({len(hashtags)}개, 최대 {_MAX_HASHTAGS}개)")
        score -= 15
    if len(hashtags) < _MIN_HASHTAGS:
        issues.append(f"해시태그 부족 ({len(hashtags)}개, 최소 {_MIN_HASHTAGS}개)")
        score -= 10

    score = max(0, score)
    return {"pass": len(issues) == 0, "issues": issues, "score": score}


def inspect_youtube(title: str, description: str, tags: list) -> dict:
    """
    YouTube 메타데이터 검수.
    반환: {"pass": bool, "issues": [str], "score": int}
    """
    issues = []
    score = 100

    # 제목 검수
    if not title or len(title.strip()) < _MIN_YT_TITLE_LEN:
        issues.append(f"유튜브 제목이 너무 짧습니다 (최소 {_MIN_YT_TITLE_LEN}자)")
        score -= 30
    if len(title) > _MAX_YT_TITLE_LEN:
        issues.append(f"유튜브 제목이 너무 깁니다 (최대 {_MAX_YT_TITLE_LEN}자)")
        score -= 15

    lower_title = title.lower()
    for phrase in _BANNED_PHRASES:
        if phrase.lower() in lower_title:
            issues.append(f"제목에 금지 키워드 포함: '{phrase}'")
            score -= 25

    # 설명 검수
    if not description or len(description.strip()) < _MIN_YT_DESC_LEN:
        issues.append(f"유튜브 설명이 너무 짧습니다 (최소 {_MIN_YT_DESC_LEN}자)")
        score -= 20
    if len(description) > _MAX_YT_DESC_LEN:
        issues.append(f"유튜브 설명이 너무 깁니다 (최대 {_MAX_YT_DESC_LEN}자)")
        score -= 10

    lower_desc = description.lower()
    for phrase in _BANNED_PHRASES:
        if phrase.lower() in lower_desc:
            issues.append(f"설명에 금지 키워드 포함: '{phrase}'")
            score -= 15

    # 태그 검수
    if not tags or len(tags) == 0:
        issues.append("태그가 없습니다 (최소 1개 이상 필요)")
        score -= 10
    if len(tags) > 500:
        issues.append(f"태그 총 글자수 초과 (500자 이내 권장)")
        score -= 5

    score = max(0, score)
    return {"pass": len(issues) == 0, "issues": issues, "score": score}


def inspect_post_upload(post_id: str) -> dict:
    """
    업로드 이후 사후 검수 (현재는 기본 검사만 수행).
    반환: {"pass": bool, "issues": [str]}
    """
    issues = []
    if not post_id:
        issues.append("포스트 ID가 없습니다")
    return {"pass": len(issues) == 0, "issues": issues}
