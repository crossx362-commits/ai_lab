"""
youtube_trending_notify.py — 루나: YouTube 인기 영상 탐색 → 비서 경유 CEO 알림
조회수 높은 재미있는 영상을 발굴해 텔레그램으로 보고.
"""
import os
import sys
import json
import random
import urllib.request
import urllib.parse
import datetime

_here = os.path.dirname(os.path.abspath(__file__))
_root = os.path.abspath(os.path.join(_here, "..", "..", "..", ".."))
for _ in range(4):
    if os.path.isdir(os.path.join(_root, ".agent")):
        break
    _root = os.path.dirname(_root)
sys.path.insert(0, _root)
from _shared.env_loader import load_env as _load_env
from _shared.telegram_notifier import send_telegram_message
from _shared.ollama_client import chat as lm_chat, is_available as lm_available


def _fmt_views(n: int) -> str:
    if n >= 100_000_000:
        return f"{n / 100_000_000:.1f}억"
    if n >= 10_000:
        return f"{n // 10_000}만"
    return f"{n:,}"


def get_trending_via_api(api_key: str, max_results: int = 30) -> list[dict]:
    """YouTube Data API v3 — 한국 전체 인기 영상 조회"""
    url = (
        "https://www.googleapis.com/youtube/v3/videos"
        f"?part=snippet,statistics&chart=mostPopular"
        f"&regionCode=KR&maxResults={max_results}&key={api_key}"
    )
    try:
        with urllib.request.urlopen(url, timeout=15) as r:
            data = json.loads(r.read())
        items = data.get("items", [])
        results = []
        for item in items:
            s = item["snippet"]
            stats = item.get("statistics", {})
            views = int(stats.get("viewCount", 0))
            results.append({
                "title":   s.get("title", ""),
                "channel": s.get("channelTitle", ""),
                "url":     f"https://youtu.be/{item['id']}",
                "views":   views,
            })
        # 조회수 기준 정렬 후 상위에서 랜덤 픽
        results.sort(key=lambda x: x["views"], reverse=True)
        return results
    except Exception as e:
        print(f"  [YouTube API] 조회 실패: {e}")
        return []


def _parse_video_items(raw: str) -> list[dict]:
    """JSON 파싱 후 영상 목록 반환."""
    try:
        items = json.loads(raw.strip())
        return [
            {
                "title":     it.get("title", ""),
                "channel":   it.get("channel", ""),
                "url":       f"https://www.youtube.com/results?search_query={urllib.parse.quote(it.get('search_query', it.get('title', '')))}",
                "views":     0,
                "is_search": True,
            }
            for it in (items if isinstance(items, list) else [items])
        ]
    except Exception:
        return []


def get_trending_via_ai() -> list[dict]:
    """Ollama로 인기 영상 3개 추천."""
    categories = [
        "한국 유튜브 인기 재미있는 영상 2025",
        "Korea viral video funny 2025",
        "유튜브 쇼츠 인기 2025",
        "한국 예능 클립 인기",
    ]
    query = random.choice(categories)
    prompt = (
        f"유튜브에서 지금 조회수가 높고 재미있는 한국 영상 3개를 추천해줘. "
        f"테마: '{query}'. 반드시 JSON 배열만 반환: "
        '[{"title":"제목","channel":"채널","search_query":"검색어"}]'
    )
    if lm_available():
        raw = lm_chat(prompt, json_mode=True, max_tokens=400, temperature=0.8)
        if raw:
            items = _parse_video_items(raw)
            if items:
                print("  [루나 트렌딩] Ollama 추천 생성 완료")
                return items
    print("  [루나 트렌딩] Ollama 응답 없음 — 건너뜀")
    return []


def build_notify_message(videos: list[dict]) -> str:
    kst_now = datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=9)))
    ts = kst_now.strftime("%m/%d %H:%M")
    lines = [f"📺 <b>[루나 → 비서] YouTube 인기 영상 리포트</b> ({ts} KST)\n"]

    top = videos[:3] if len(videos) >= 3 else videos
    for i, v in enumerate(top, 1):
        views_str = f" | 조회수 {_fmt_views(v['views'])}" if v.get("views", 0) > 0 else ""
        ch_str    = f"\n   📺 {v['channel']}" if v.get("channel") else ""
        lines.append(
            f"{i}. <b>{v['title']}</b>{views_str}"
            f"{ch_str}\n   {v['url']}"
        )

    lines.append("\n비서님, CEO님께 전달 부탁드립니다. — 루나")
    return "\n".join(lines)


def run_notify():
    """인기 영상 탐색 후 비서 채널(텔레그램)로 보고."""
    _load_env()
    yt_key     = os.getenv("YOUTUBE_API_KEY", "")
    gemini_key = os.getenv("GEMINI_API_KEY", "")

    videos = []
    if yt_key:
        videos = get_trending_via_api(yt_key)
    if not videos:
        videos = get_trending_via_ai()
    if not videos:
        print("  [루나 트렌딩] 조회 실패 — 전송 건너뜀")
        return False

    msg = build_notify_message(videos)
    ok  = send_telegram_message(msg)
    print(f"  [루나 트렌딩] 비서 보고 {'완료' if ok else '실패'} ({len(videos)}개)")
    return ok


if __name__ == "__main__":
    run_notify()
