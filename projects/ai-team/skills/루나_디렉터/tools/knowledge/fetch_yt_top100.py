"""
fetch_yt_top100.py -- 미국 YouTube 어제 조회수 Top 100 음악 제목 수집 스킬

[규칙]
- 하루 1회만 YouTube API를 호출하고 결과를 title_patterns.json에 캐시
- 당일 캐시가 이미 존재하면 API 미호출, 캐시 즉시 반환
- 여러 파이프라인(루나, 가희 fix_issues 등)이 같은 날 호출해도 1번만 실제 요청

[사용법]
    from knowledge.fetch_yt_top100 import get_yt_top100_titles
    titles = get_yt_top100_titles()   # list[str], 최대 100개

[직접 실행]
    python fetch_yt_top100.py          # 오늘 수집 & 캐시 갱신
    python fetch_yt_top100.py --force  # 강제 재수집 (캐시 무시)
"""
import os
import sys
import json
import datetime
import urllib.request
import urllib.parse

# ── 경로 설정 ──────────────────────────────────────────────────────────────────
_here = os.path.dirname(os.path.abspath(__file__))
_tools_dir = os.path.dirname(_here)                        # tools/
_ai_team = os.path.abspath(os.path.join(_tools_dir, "..", "..", ".."))
if _ai_team not in sys.path:
    sys.path.insert(0, _ai_team)

from _shared.env_loader import load_env, find_project_root
_root = find_project_root(_tools_dir)

# ── 캐시 파일 경로 ─────────────────────────────────────────────────────────────
_CACHE_FILE = os.path.join(_here, "title_patterns.json")

# ── 내부 함수 ──────────────────────────────────────────────────────────────────

def _today_utc() -> str:
    return datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%d")


def _yesterday_utc() -> tuple[str, str]:
    """어제 UTC 날짜의 시작/끝 ISO 8601 반환."""
    today = datetime.datetime.now(datetime.timezone.utc).date()
    yesterday = today - datetime.timedelta(days=1)
    return f"{yesterday}T00:00:00Z", f"{today}T00:00:00Z"


def _load_cache() -> dict:
    if not os.path.exists(_CACHE_FILE):
        return {}
    try:
        with open(_CACHE_FILE, "r", encoding="utf-8") as f:
            return json.load(f)
    except Exception:
        return {}


def _save_cache(cache: dict):
    os.makedirs(_here, exist_ok=True)
    # 최근 30일치만 유지
    keys = sorted(cache.keys())[-30:]
    cache = {k: cache[k] for k in keys}
    with open(_CACHE_FILE, "w", encoding="utf-8") as f:
        json.dump(cache, f, indent=2, ensure_ascii=False)


def _build_search_url(api_key: str, page_token: str = "",
                      published_after: str = "", published_before: str = "") -> str:
    """Search API URL 빌더 (1차 시도용)."""
    params = {
        "part": "snippet",
        "type": "video",
        "videoCategoryId": "10",   # Music
        "regionCode": "US",
        "order": "viewCount",
        "maxResults": "50",
        "key": api_key,
    }
    if published_after:
        params["publishedAfter"] = published_after
    if published_before:
        params["publishedBefore"] = published_before
    if page_token:
        params["pageToken"] = page_token
    return "https://www.googleapis.com/youtube/v3/search?" + urllib.parse.urlencode(params)


def _fetch_chart_top(api_key: str, n: int = 100) -> list[str]:
    """
    Videos.list?chart=mostPopular — YouTube 공식 차트 상위 음악 제목 수집.
    Search API보다 할당량 소모 적고 결과 안정적.
    """
    titles = []
    page_token = ""
    for _ in range(2):
        params = {
            "part": "snippet",
            "chart": "mostPopular",
            "videoCategoryId": "10",
            "regionCode": "US",
            "maxResults": "50",
            "key": api_key,
        }
        if page_token:
            params["pageToken"] = page_token
        url = "https://www.googleapis.com/youtube/v3/videos?" + urllib.parse.urlencode(params)
        try:
            with urllib.request.urlopen(url, timeout=15) as r:
                data = json.loads(r.read())
            titles += [item["snippet"]["title"] for item in data.get("items", [])]
            page_token = data.get("nextPageToken", "")
            if not page_token or len(titles) >= n:
                break
        except Exception as e:
            print(f"  [YT Top100] chart API 실패: {e}")
            break
    return titles[:n]


def _fetch_from_api(api_key: str, n: int = 100) -> list[str]:
    """
    YouTube 조회수 Top 100 음악 제목 수집.
    1차: Videos.list chart=mostPopular (공식 차트, 할당량 최소)
    2차 폴백: Search API 날짜 필터 없이 US 음악 viewCount 순
    """
    # ── 1차: 공식 차트 ──────────────────────────────────────────────────────
    titles = _fetch_chart_top(api_key, n)
    if titles:
        print(f"  [YT Top100] 공식 차트 수집 완료: {len(titles)}개")
        return titles

    # ── 2차 폴백: Search API (날짜 필터 없이) ─────────────────────────────
    print("  [YT Top100] 차트 결과 없음 — Search API 폴백...")
    page_token = ""
    for _ in range(2):
        url = _build_search_url(api_key, page_token)
        try:
            with urllib.request.urlopen(url, timeout=15) as r:
                data = json.loads(r.read())
            titles += [item["snippet"]["title"] for item in data.get("items", [])]
            page_token = data.get("nextPageToken", "")
            if not page_token or len(titles) >= n:
                break
        except Exception as e:
            print(f"  [YT Top100] Search API 폴백 실패: {e}")
            break

    return titles[:n]




# ── 공개 인터페이스 ────────────────────────────────────────────────────────────

def get_yt_top100_titles(force: bool = False) -> list[str]:
    """
    어제 미국 YouTube 조회수 Top 100 음악 영상 제목 반환.

    - 당일 캐시가 있으면 API 호출 없이 즉시 반환 (하루 1회 보장)
    - force=True 이면 캐시 무시하고 강제 재수집
    """
    load_env()
    today_key = _today_utc()

    # ── 캐시 확인 ──────────────────────────────────────────────────────────────
    cache = _load_cache()
    if not force and today_key in cache:
        cached_titles = cache[today_key].get("us_top_titles", [])
        if cached_titles:
            print(f"  [YT Top100] 캐시 사용 ({today_key}, {len(cached_titles)}개) — API 미호출")
            return cached_titles

    # ── API 호출 ───────────────────────────────────────────────────────────────
    api_key = os.getenv("YOUTUBE_API_KEY", "")
    if not api_key:
        print("  [YT Top100] YOUTUBE_API_KEY 없음 — 빈 리스트 반환")
        return []

    print(f"  [YT Top100] API 호출 중 (어제 날짜: {_yesterday_utc()[0][:10]})...")
    titles = _fetch_from_api(api_key, n=100)

    if not titles:
        print("  [YT Top100] 수집 결과 없음")
        return []

    # ── 캐시 저장 ──────────────────────────────────────────────────────────────
    existing_entry = cache.get(today_key, {})
    existing_entry["us_top_titles"] = titles
    existing_entry["fetched_at"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    existing_entry["count"] = len(titles)
    cache[today_key] = existing_entry
    _save_cache(cache)

    print(f"  [YT Top100] 수집 완료: {len(titles)}개 → {_CACHE_FILE}")
    return titles


def analyze_title_patterns(titles: list[str]) -> dict:
    """
    Top100 제목 목록에서 구조적 패턴을 알고리즘으로 추출.

    반환값 (dict):
      avg_len          — 평균 글자 수
      median_words     — 중간 단어 수
      separator_style  — 가장 많이 쓰인 구분자 ('|', '-', ':', '/')
      emoji_rate       — 이모지 포함 비율 (0.0~1.0)
      lang_mix         — 언어 혼합 패턴 ('ko_only', 'en_only', 'mixed')
      top_formats      — 상위 구조 패턴 예시 3개
      bracket_rate     — [] 괄호 포함 비율
      paren_rate       — () 괄호 포함 비율
      prompt_hint      — LLM에게 전달할 간결한 패턴 설명 (1~2문장)
    """
    import re
    import unicodedata

    if not titles:
        return {"prompt_hint": "패턴 데이터 없음 — 자유 생성"}

    def has_emoji(s: str) -> bool:
        return any(unicodedata.category(c) in ("So", "Sk") for c in s)

    def lang_class(s: str) -> str:
        has_ko = bool(re.search(r"[가-힣]", s))
        has_en = bool(re.search(r"[A-Za-z]", s))
        if has_ko and has_en:
            return "mixed"
        if has_ko:
            return "ko_only"
        return "en_only"

    lens   = [len(t) for t in titles]
    words  = [len(t.split()) for t in titles]
    emojis = [has_emoji(t) for t in titles]
    langs  = [lang_class(t) for t in titles]

    sep_counts = {"|": 0, "-": 0, ":": 0, "/": 0, "×": 0}
    bracket_count = sum(1 for t in titles if "[" in t or "]" in t)
    paren_count   = sum(1 for t in titles if "(" in t or ")" in t)

    for t in titles:
        for sep in sep_counts:
            if sep in t:
                sep_counts[sep] += 1

    dominant_sep = max(sep_counts, key=sep_counts.get) if any(sep_counts.values()) else ""

    # 언어 혼합 분포
    lang_dist = {k: langs.count(k) for k in ("ko_only", "en_only", "mixed")}
    dominant_lang = max(lang_dist, key=lang_dist.get)

    avg_len      = round(sum(lens) / len(lens))
    median_words = sorted(words)[len(words) // 2]
    emoji_rate   = round(sum(emojis) / len(emojis), 2)
    bracket_rate = round(bracket_count / len(titles), 2)
    paren_rate   = round(paren_count  / len(titles), 2)

    # 대표 형식 예시 (짧은 것 상위 3개)
    short_titles = sorted(titles, key=len)[:3]

    # LLM 힌트 문장 생성
    sep_hint = f"구분자는 '{dominant_sep}'을 자주 사용" if dominant_sep else "구분자 없이 단어 나열"
    lang_hint = {
        "ko_only": "한국어 단독",
        "en_only": "영어 단독",
        "mixed":   "한국어+영어 혼합"
    }[dominant_lang]
    emoji_hint = f"이모지 사용률 {int(emoji_rate*100)}%" if emoji_rate > 0 else "이모지 없음"
    bracket_hint = f"[대괄호] {int(bracket_rate*100)}% 사용" if bracket_rate > 0.1 else ""

    prompt_hint = (
        f"Top100 패턴: 평균 {avg_len}자/{median_words}단어, "
        f"{lang_hint}, {sep_hint}, {emoji_hint}"
        + (f", {bracket_hint}" if bracket_hint else "")
        + f". 예시: {' / '.join(short_titles[:2])}"
    )

    return {
        "avg_len":        avg_len,
        "median_words":   median_words,
        "separator":      dominant_sep,
        "emoji_rate":     emoji_rate,
        "lang_mix":       dominant_lang,
        "top_formats":    short_titles,
        "bracket_rate":   bracket_rate,
        "paren_rate":     paren_rate,
        "prompt_hint":    prompt_hint,
    }


def get_title_pattern_analysis(force: bool = False) -> dict:
    """
    당일 Top100 타이틀 패턴 분석 결과 반환 (캐시 우선).
    titles 수집 → analyze_title_patterns() → 결과 캐시 저장.
    """
    load_env()
    today_key = _today_utc()

    cache = _load_cache()
    if not force and today_key in cache:
        analysis = cache[today_key].get("pattern_analysis")
        if analysis:
            print(f"  [YT Top100] 패턴 분석 캐시 사용 ({today_key})")
            return analysis

    titles = get_yt_top100_titles(force=force)
    if not titles:
        return {"prompt_hint": "Top100 데이터 없음 — 루나 기본 규칙으로 생성"}

    analysis = analyze_title_patterns(titles)

    # 캐시 업데이트
    existing_entry = cache.get(today_key, {})
    existing_entry["pattern_analysis"] = analysis
    cache[today_key] = existing_entry
    _save_cache(cache)

    print(f"  [YT Top100] 패턴 분석 완료: {analysis['prompt_hint'][:60]}...")
    return analysis


# ── CLI ───────────────────────────────────────────────────────────────────────

if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")

    force = "--force" in sys.argv
    titles = get_yt_top100_titles(force=force)

    print(f"\n=== 어제 미국 YouTube Top {len(titles)}개 ===")
    for i, t in enumerate(titles[:20], 1):
        print(f"  {i:3d}. {t}")
    if len(titles) > 20:
        print(f"  ... (총 {len(titles)}개)")

    if titles:
        analysis = analyze_title_patterns(titles)
        print(f"\n=== 패턴 분석 ===")
        print(f"  평균 길이: {analysis['avg_len']}자 / {analysis['median_words']}단어")
        print(f"  언어 혼합: {analysis['lang_mix']}")
        print(f"  주요 구분자: '{analysis['separator']}'")
        print(f"  이모지 비율: {int(analysis['emoji_rate']*100)}%")
        print(f"  힌트: {analysis['prompt_hint']}")
