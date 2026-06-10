"""
youtube_research.py — 루나 자가학습 모듈
매 시간 YouTube 인기 뮤직비디오 리서치 → Ollama(로컬 AI) 패턴 분석 → 지식 파일 업데이트.
Ollama 미실행 시 Gemini API로 자동 폴백.
학습된 테마는 TrendAnalyzer가 읽어 다음 비디오 생성에 반영.
"""
import os
import sys
import json
import datetime
import urllib.request
import urllib.parse
import random

_here = os.path.dirname(os.path.abspath(__file__))
_ai_team_root = os.path.abspath(os.path.join(_here, "..", "..", "..", ".."))
if _ai_team_root not in sys.path:
    sys.path.insert(0, _ai_team_root)
from _shared.env_loader import load_env as _load_env, find_project_root
from _shared.ollama_client import chat as lm_chat, is_available as lm_available
from _shared.telegram_notifier import send_telegram_message
_root = find_project_root(_here)

RESEARCH_FILE = os.path.join(_root, "reports", "research", "luna_research.json")
MAX_LEARNED_THEMES = 50


def _load_research() -> dict:
    if os.path.exists(RESEARCH_FILE):
        try:
            with open(RESEARCH_FILE, "r", encoding="utf-8") as f:
                return json.load(f)
        except Exception:
            pass
    return {"learned_themes": [], "style_insights": [], "title_insights": [], "research_count": 0}


def _save_research(data: dict):
    os.makedirs(os.path.dirname(RESEARCH_FILE), exist_ok=True)
    data["last_updated"] = datetime.datetime.now().isoformat()
    with open(RESEARCH_FILE, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2, ensure_ascii=False)


# ─── 1단계: YouTube 인기 뮤직비디오 수집 ─────────────────────────────────────

def _fetch_via_youtube_api(api_key: str) -> list[dict]:
    """YouTube Data API v3 — 한국 인기 음악 영상 (조회수/통계 포함)"""
    url = (
        "https://www.googleapis.com/youtube/v3/videos"
        f"?part=snippet,statistics&chart=mostPopular"
        f"&regionCode=KR&videoCategoryId=10&maxResults=20&key={api_key}"
    )
    try:
        with urllib.request.urlopen(url, timeout=15) as r:
            data = json.loads(r.read())
        results = []
        for item in data.get("items", []):
            s     = item["snippet"]
            stats = item.get("statistics", {})
            results.append({
                "title":      s.get("title", ""),
                "channel":    s.get("channelTitle", ""),
                "description": s.get("description", "")[:200],
                "views":      int(stats.get("viewCount", 0)),
                "likes":      int(stats.get("likeCount", 0)),
            })
        results.sort(key=lambda x: x["views"], reverse=True)
        return results[:15]
    except Exception as e:
        print(f"  [리서치] YouTube API 실패: {e}")
        return []


def _set_research_goal() -> str:
    """Ollama로 오늘의 리서치 주제/목표 설정. 실패 시 기본값 반환."""
    today = datetime.datetime.now().strftime("%Y-%m-%d (%A)")
    prompt = (
        f"루나는 AI 뮤직비디오 디렉터야. 오늘({today}) 유튜브 뮤직비디오 리서치 목표를 1가지 정해줘.\n"
        "예시: '시티팝 야경 비주얼 분석', 'K-R&B 감성 조명 기법', '90년대 레트로 애니메이션 스타일'\n"
        "딱 한 줄, 20자 이내로만 답해."
    )
    if lm_available():
        result = lm_chat(prompt, max_tokens=60, temperature=0.95)
        if result:
            return result.strip()
    return "시티팝 뮤직비디오 비주얼 트렌드"


def _call_ai(prompt: str, max_tokens: int = 1000) -> str | None:
    """Ollama로 AI 호출. 결과 텍스트 반환."""
    if lm_available():
        raw = lm_chat(prompt, json_mode=True, max_tokens=max_tokens, temperature=0.7)
        if raw:
            return raw
    print("  [리서치] Ollama 응답 없음 — 건너뜀")
    return None


def _fetch_ad_videos_via_gemini() -> list[dict]:
    """상품 광고 영상 트렌드를 AI로 수집 (Ollama → Gemini 폴백)"""
    prompt = (
        "List 5 popular YouTube product advertisement videos (Korean brands included). "
        "Categories: beauty, food/beverage, electronics, fashion, lifestyle. "
        "Return JSON array only: "
        '[{"title":"","brand":"","product_category":"","visual_style":"","mood":""}]'
    )
    try:
        raw = _call_ai(prompt, max_tokens=1500)
        if not raw:
            return []
        items = json.loads(raw.strip())
        # 뮤직비디오 포맷에 맞게 변환
        return [
            {
                "title":       f"[AD] {it.get('title', '')}",
                "channel":     it.get("brand", ""),
                "genre":       f"Product Ad / {it.get('product_category', '')}",
                "mood":        it.get("mood", ""),
                "visual_style": it.get("visual_style", ""),
                "hook":        it.get("hook_technique", ""),
                "is_ad":       True,
            }
            for it in (items if isinstance(items, list) else [])
        ]
    except Exception as e:
        print(f"  [리서치] 광고 영상 수집 실패: {e}")
        return []


def _fetch_via_gemini_knowledge() -> list[dict]:
    """YouTube API 없을 때 AI(Ollama → Gemini) 지식 기반으로 인기 뮤직비디오 목록 생성"""
    prompt = (
        "List 5 popular YouTube music videos (Korean and Japanese: Pop, R&B, City Pop, Indie). "
        "Return JSON array only: "
        '[{"title":"","channel":"","genre":"","mood":"","visual_style":""}]'
    )
    try:
        raw = _call_ai(prompt, max_tokens=1500)
        if not raw:
            return []
        items = json.loads(raw.strip())
        return items if isinstance(items, list) else []
    except Exception as e:
        # 잘린 JSON 복구
        import re
        raw_str = locals().get("raw", "")
        matches = re.findall(r'\{[^{}]+\}', raw_str or "")
        items = []
        for m in matches:
            try:
                items.append(json.loads(m))
            except Exception:
                pass
        if items:
            print(f"  [리서치] 부분 JSON 복구 ({len(items)}개)")
            return items
        print(f"  [리서치] 뮤직비디오 목록 수집 실패: {e}")
        return []


# ─── 2단계: Gemini 패턴 분석 → 새 테마 추출 ─────────────────────────────────

_ANALYSIS_PROMPT = """Analyze these YouTube music videos and extract City Pop/J-Pop video themes:

{video_list}

Return JSON only (exactly 2 items in learned_themes, keep visual_prompt under 20 words):
{{"learned_themes":[{{"keyword":"","mood":"","visual_prompt":"","title_pattern":""}}],"style_insights":[""],"title_insights":[""]}}"""


def _analyze(videos: list[dict]) -> dict | None:
    """Ollama(로컬) → Gemini(폴백) 순으로 인기 영상 패턴 분석 및 새 테마 추출"""
    if not videos:
        return None

    video_list = "\n".join(
        f"{i+1}. {v.get('title','')} | {v.get('channel','')} | {v.get('genre','')}{v.get('mood','')} | views {v.get('views',0):,}"
        for i, v in enumerate(videos[:12])
    )
    prompt = _ANALYSIS_PROMPT.format(video_list=video_list)
    print("  [리서치] AI로 패턴 분석 중...")
    raw = _call_ai(prompt, max_tokens=2000)
    if not raw:
        print("  [리서치] 분석 실패")
        return None
    try:
        return json.loads(raw.strip())
    except Exception:
        # 잘린 JSON 복구 — 완전한 오브젝트만 추출해 learned_themes로 사용
        import re
        items = []
        for m in re.finditer(r'\{[^{}]*"keyword"[^{}]*\}', raw):
            try:
                items.append(json.loads(m.group()))
            except Exception:
                pass
        if items:
            print(f"  [리서치] 부분 JSON 복구 ({len(items)}개 테마)")
            return {"learned_themes": items, "style_insights": [], "title_insights": []}
        print("  [리서치] 분석 JSON 파싱 실패 — 건너뜀")
        return None


# ─── 3단계: 지식 파일 업데이트 ────────────────────────────────────────────────

def _merge_research(existing: dict, new_data: dict) -> dict:
    """기존 지식 파일에 새 인사이트 병합 (중복 제거, 오래된 것 제거)"""
    now_str = datetime.datetime.now().isoformat()

    # 새 테마에 타임스탬프 추가
    new_themes = new_data.get("learned_themes", [])
    for t in new_themes:
        t["created_at"] = now_str

    # 기존 + 새 테마 합치고 초과분 제거 (최신 유지)
    all_themes = existing.get("learned_themes", []) + new_themes
    # 중복 keyword 제거 (최신 우선)
    seen = set()
    deduped = []
    for t in reversed(all_themes):
        kw = t.get("keyword", "").lower()
        if kw not in seen:
            seen.add(kw)
            deduped.append(t)
    deduped.reverse()
    existing["learned_themes"] = deduped[-MAX_LEARNED_THEMES:]

    # 인사이트 병합 (최대 10개 유지)
    for key in ("style_insights", "title_insights"):
        combined = list(dict.fromkeys(
            existing.get(key, []) + new_data.get(key, [])
        ))
        existing[key] = combined[-10:]

    existing["research_count"] = existing.get("research_count", 0) + 1
    return existing


# ─── 메인 실행 ────────────────────────────────────────────────────────────────

def run_research() -> bool:
    """1회 리서치 사이클: 목표 설정 → 수집 → 분석 → 저장. 성공 시 True 반환."""
    _load_env()
    gemini_key = os.getenv("GEMINI_API_KEY", "")
    yt_key     = os.getenv("YOUTUBE_API_KEY", "")

    if not gemini_key:
        print("  [루나 리서치] GEMINI_API_KEY 없음 — 건너뜀")
        return False

    # 0단계: Ollama로 오늘의 리서치 목표 설정
    goal = _set_research_goal()
    print(f"  [루나 리서치] 오늘의 목표: {goal}")

    print("  [루나 리서치] YouTube 인기 뮤직비디오 + 광고 영상 수집 중...")

    # 1. 뮤직비디오 목록 수집
    videos = []
    if yt_key:
        videos = _fetch_via_youtube_api(yt_key)
    if not videos:
        videos = _fetch_via_gemini_knowledge()

    # 상품 광고 영상 추가 (매 리서치마다 포함)
    ad_videos = _fetch_ad_videos_via_gemini()
    if ad_videos:
        # 광고 영상과 뮤직비디오를 랜덤 비율로 혼합 (광고 30~50%)
        ad_count = max(3, int(len(ad_videos) * random.uniform(0.3, 0.5)))
        videos = videos + random.sample(ad_videos, min(ad_count, len(ad_videos)))
        print(f"  [루나 리서치] 광고 영상 {ad_count}개 추가 포함")

    if not videos:
        print("  [루나 리서치] 영상 목록 수집 실패 — Ollama 미응답")
        send_telegram_message("⚠️ [루나] 리서치 실패 — Ollama 응답 없음. 올라마 실행 상태 확인 필요.")
        return False

    print(f"  [루나 리서치] {len(videos)}개 영상 수집 완료 → AI 분석 시작 (목표: {goal})...")

    # 2. AI 패턴 분석 (목표 기반)
    analysis = _analyze(videos)
    if not analysis:
        print("  [루나 리서치] Gemini 분석 실패")
        return False

    new_themes = analysis.get("learned_themes", [])
    print(f"  [루나 리서치] {len(new_themes)}개 새 테마 추출 완료")

    # 3. 지식 파일 저장
    existing = _load_research()
    merged   = _merge_research(existing, analysis)
    _save_research(merged)

    total = len(merged["learned_themes"])
    count = merged["research_count"]
    print(f"  [루나 리서치] 저장 완료 — 누적 테마 {total}개, 총 리서치 {count}회")

    # 텔레그램 보고서 발송
    themes_str = ", ".join(t.get("keyword", "") for t in new_themes if t.get("keyword"))
    msg = (
        f"🎬 [루나 → 비서] YouTube 음악 트렌드 자가 학습 완료!\n\n"
        f"🎯 오늘의 리서치 목표: {goal}\n"
        f"💡 새 학습 테마: {themes_str}\n"
        f"📊 총 리서치 횟수: {count}회 | 누적 학습 테마: {total}개\n"
        f"분석 내용이 다음 뮤직비디오 생성에 반영됩니다!"
    )
    # 지식 공유 시스템에 등록
    from _shared.knowledge_base import store_knowledge
    report = (
        f"🎯 오늘의 리서치 목표: {goal}\n"
        f"💡 새 학습 테마: {themes_str}\n"
        f"📊 총 리서치 횟수: {count}회 | 누적 학습 테마: {total}개\n"
    )
    try:
        store_knowledge('루나', 'YouTube Music/Video Trend', report, ['Trend', 'YouTube'])
    except Exception as e:
        print(f"  [루나 리서치] 지식 저장 실패: {e}")
        
    send_telegram_message(msg)

    return True


if __name__ == "__main__":
    import io
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
    run_research()
