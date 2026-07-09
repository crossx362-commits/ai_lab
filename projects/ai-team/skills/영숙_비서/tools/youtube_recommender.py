"""
youtube_recommender.py — 영숙의 유튜브 추천 모듈
YOUTUBE_API_KEY 있으면 실제 트렌딩 영상, 없으면 Gemini가 추천 → YouTube 검색 URL.
"""
import os
import sys
import json
import random
import urllib.parse

_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, ".agent")):
        break
    _root = os.path.dirname(_root)
sys.path.insert(0, _root)
import urllib.request
from _shared.telegram import send
from _shared.env import load_env as _load_env
from _shared.llm import ollama as lm_chat, is_available as lm_available


# 영숙 메시지 오프닝 풀
_OPENERS = [
    "안녕하세요~ 영숙이에요 😊 오늘 이 영상 발견했는데 너무 좋더라고요!",
    "잠깐 쉬어가세요~ 영숙이 추천이에요 ✨",
    "영숙이 지금 이거 듣고 있어요 🎵 같이 들어요!",
    "오늘 기분 전환용으로 딱이에요! 영숙이 강추 💕",
    "작업하다 이 영상 발견했어요~ 공유하고 싶었어요 😄",
    "이거 완전 제 취향이에요 🎬 한번 보세요!",
]


def _get_via_youtube_api(api_key: str) -> dict | None:
    """YouTube Data API v3 — 한국 트렌딩 음악 영상"""
    cats = [
        ("10", "음악"),       # Music
        ("", "인기 영상"),    # General trending
    ]
    cat_id, cat_label = random.choice(cats)
    url = (
        "https://www.googleapis.com/youtube/v3/videos"
        f"?part=snippet&chart=mostPopular&regionCode=KR&maxResults=30"
        f"&key={api_key}"
    )
    if cat_id:
        url += f"&videoCategoryId={cat_id}"
    try:
        with urllib.request.urlopen(url, timeout=15) as r:
            data = json.loads(r.read())
        items = data.get("items", [])
        if not items:
            return None
        video = random.choice(items)
        s = video["snippet"]
        return {
            "title":   s.get("title", ""),
            "channel": s.get("channelTitle", ""),
            "url":     f"https://youtu.be/{video['id']}",
            "label":   cat_label,
        }
    except Exception as e:
        print(f"  [YouTube API] {e}")
        return None


def _get_via_ai() -> dict | None:
    """Ollama(1순위) → Gemini(2순위)로 YouTube 추천 → 검색 URL 반환"""
    moods = ["한국 인디 음악", "lo-fi chill beats", "시티팝 city pop", "K-indie 발라드",
             "일본 재즈 jazz", "어쿠스틱 기타 acoustic", "한국 여행 브이로그", "피아노 커버"]
    mood = random.choice(moods)
    prompt = (
        f"유튜브에서 '{mood}' 장르/테마로 지금 인기 있거나 좋은 영상/음악 1개를 추천해줘. "
        "반드시 JSON만 반환: "
        '{"title": "영상 또는 곡 제목", "artist_or_channel": "아티스트 또는 채널명", '
        '"search_query": "유튜브에서 검색할 한국어 또는 영어 검색어"}'
    )

    def _parse(raw: str) -> dict | None:
        try:
            rec = json.loads(raw.strip())
            query = rec.get("search_query") or rec.get("title", mood)
            return {
                "title":     rec.get("title", query),
                "channel":   rec.get("artist_or_channel", ""),
                "url":       f"https://www.youtube.com/results?search_query={urllib.parse.quote(query)}",
                "label":     mood,
                "is_search": True,
            }
        except Exception:
            return None

    # 1순위: Ollama
    if lm_available():
        raw = lm_chat(prompt, json_mode=True, max_tokens=200, temperature=0.8)
        if raw:
            result = _parse(raw)
            if result:
                return result

    print("  [영숙 AI 추천] Ollama 응답 없음 — 건너뜀")
    return None


def _format_message(video: dict) -> str:
    opener  = random.choice(_OPENERS)
    title   = video["title"]
    channel = video.get("channel", "")
    url     = video["url"]
    label   = video.get("label", "")
    is_search = video.get("is_search", False)

    ch_line = f"\n📺 {channel}" if channel else ""
    lbl_line = f"\n🏷️ {label}" if label else ""

    if is_search:
        return f"{opener}\n\n🔍 <b>{title}</b>{ch_line}{lbl_line}\n{url}"
    return f"{opener}\n\n🎬 <b>{title}</b>{ch_line}{lbl_line}\n{url}"


def send_recommendation():
    """추천 영상 선정 후 텔레그램 전송. 외부에서 직접 호출 가능."""
    _load_env()
    yt_key = os.getenv("YOUTUBE_API_KEY", "")

    video = None
    if yt_key:
        video = _get_via_youtube_api(yt_key)
    if not video:
        video = _get_via_ai()
    if not video:
        # 최후 폴백: 하드코딩 검색 URL
        queries = ["한국 인디 음악 2025", "lo-fi chill study beats", "일본 시티팝 city pop"]
        q = random.choice(queries)
        video = {
            "title": q, "channel": "",
            "url": f"https://www.youtube.com/results?search_query={urllib.parse.quote(q)}",
            "is_search": True,
        }

    msg = _format_message(video)
    ok = send(msg)
    print(f"  [영숙] 추천 전송{'완료' if ok else '실패'}: {video['title'][:40]}")
    return ok


if __name__ == "__main__":
    send_recommendation()
