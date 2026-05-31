import urllib.request
import xml.etree.ElementTree as ET
import json
import re
import os
import random

_here = os.path.dirname(os.path.abspath(__file__))
_root = os.path.abspath(os.path.join(_here, "..", "..", "..", ".."))
for _ in range(4):
    if os.path.isdir(os.path.join(_root, ".agent")):
        break
    _root = os.path.dirname(_root)
_RESEARCH_FILE   = os.path.join(_root, ".agent", "memory", "luna_research.json")
_HISTORY_FILE    = os.path.join(_root, ".agent", "memory", "upload_history.json")
_USED_KEYWORDS_DAYS = 30  # 최근 N일 이내 사용된 키워드/제목 중복 금지

# 루나 금지 장르 (2026-05-28 사장님 지시)
BANNED_GENRES = ["lofi", "lo-fi", "lo fi", "study beats", "chill beats", "sleep music", "white noise"]

def _is_banned_genre(keyword: str) -> bool:
    kl = keyword.lower()
    return any(b in kl for b in BANNED_GENRES)

def _fetch_yt_top_titles(api_key: str, n: int = 100) -> list[str]:
    """어제 미국 유튜브 조회수 상위 음악영상 100개 제목 수집."""
    import urllib.parse, datetime
    titles = []
    page_token = ""
    # 어제 날짜 범위 (UTC)
    today = datetime.datetime.utcnow().date()
    yesterday = today - datetime.timedelta(days=1)
    published_after  = f"{yesterday}T00:00:00Z"
    published_before = f"{today}T00:00:00Z"
    for _ in range(2):
        url = (
            "https://www.googleapis.com/youtube/v3/search"
            f"?part=snippet&type=video&videoCategoryId=10"
            f"&regionCode=US&order=viewCount&maxResults=50"
            f"&publishedAfter={urllib.parse.quote(published_after)}"
            f"&publishedBefore={urllib.parse.quote(published_before)}"
            f"&key={api_key}"
        )
        if page_token:
            url += f"&pageToken={urllib.parse.quote(page_token)}"
        try:
            with urllib.request.urlopen(url, timeout=10) as r:
                data = json.loads(r.read())
            titles += [i["snippet"]["title"] for i in data.get("items", [])]
            page_token = data.get("nextPageToken", "")
            if not page_token or len(titles) >= n:
                break
        except Exception as e:
            print(f"[Warning] YouTube 미국 상위 제목 수집 실패: {e}")
            break
    return titles[:n]


_TITLE_KNOWLEDGE_FILE = os.path.join(
    os.path.dirname(_here), "knowledge", "title_patterns.json"
)

def _save_title_pattern_knowledge(patterns: dict):
    """Ollama 패턴 분석 결과를 지식 파일에 누적 저장."""
    import datetime
    try:
        existing = {}
        if os.path.exists(_TITLE_KNOWLEDGE_FILE):
            with open(_TITLE_KNOWLEDGE_FILE, "r", encoding="utf-8") as f:
                existing = json.load(f)
        existing[datetime.datetime.utcnow().strftime("%Y-%m-%d")] = patterns
        # 최근 30일치만 유지
        keys = sorted(existing.keys())[-30:]
        existing = {k: existing[k] for k in keys}
        with open(_TITLE_KNOWLEDGE_FILE, "w", encoding="utf-8") as f:
            json.dump(existing, f, indent=2, ensure_ascii=False)
    except Exception as e:
        print(f"[Warning] 제목 패턴 지식화 저장 실패: {e}")


def _generate_optimized_title(keyword: str, yt_titles: list[str]) -> str:
    """어제 미국 유튜브 상위 100개 제목 패턴을 Ollama가 분석 후 LUNA 제목 생성.
    분석 결과는 knowledge/title_patterns.json에 누적 지식화.
    """
    if yt_titles:
        try:
            import sys
            sys.path.insert(0, _root)
            from _shared.ollama_client import chat as _lm_chat
            sample = "\n".join(f"- {t}" for t in yt_titles[:50])
            prompt = (
                f"아래는 미국 유튜브에서 조회수가 가장 높은 음악 영상 제목들이야:\n\n"
                f"{sample}\n\n"
                f"이 제목들의 패턴(구조, 길이, 키워드 배치, 특수문자 사용 등)을 분석해서 "
                f"'{keyword}' 테마의 시티팝/K-POP 뮤직비디오 제목을 1개 만들어줘.\n\n"
                "조건:\n"
                "- 수집된 제목 패턴을 그대로 반영해서 자연스럽게 생성\n"
                "- 고정 공식 없음\n"
                "- lofi/lo-fi 금지\n"
                "- 제목 1줄만 출력"
            )
            result = _lm_chat(prompt, task="", max_tokens=120)
            if result and result.strip():
                title = result.strip().split("\n")[0].strip()
                _save_title_pattern_knowledge({
                    "keyword": keyword,
                    "generated_title": title,
                    "sample_count": len(yt_titles),
                    "top_5_samples": yt_titles[:5],
                })
                return title
        except Exception as e:
            print(f"[Warning] 제목 생성 실패: {e}")

    # 폴백: 키워드 그대로 반환
    return keyword


def _build_music_prompt(genre_era: str, mood: str, instruments: str,
                         vocal_style: str, lyrics_theme: str) -> str:
    """표준 음악 생성 프롬프트 템플릿: 장르/시대 + 템포/무드 + 악기 + 보컬 + 가사/주제."""
    return f"{genre_era}, {mood}, {instruments}, {vocal_style}, {lyrics_theme}"


def generate_music_prompt_from_keyword(keyword: str) -> str:
    """키워드 기반으로 음악 생성 프롬프트를 먼저 생성 (제목보다 먼저).

    흐름: 키워드 → 음악 프롬프트 → 음악 생성 → 프롬프트 기반 제목 생성
    (제목이 음악 내용을 반영하도록 음악 프롬프트를 먼저 확정)

    지식 파일 규칙:
    - 템플릿: [장르/시대] + [템포/무드] + [특정악기] + [보컬스타일] + [가사/주제]
    - 선호: 일본 시티팝×케이팝 퓨전, 감성 힙합·R&B·Pop
    - 금지: lofi, lo-fi, study beats, chill beats
    - BPM 110 이상, 신나고 에너제틱하게
    """
    try:
        import sys as _sys
        _sys.path.insert(0, _root)
        from _shared.ollama_client import chat as _lm
        prompt = (
            f"음악 키워드/테마: '{keyword}'\n\n"
            "아래 5단 템플릿으로 영어 음악 생성 프롬프트를 1개 작성해. 프롬프트 1줄만 출력.\n\n"
            "템플릿: [장르/시대], [템포/무드], [특정악기], [보컬스타일], [가사/주제]\n\n"
            "규칙:\n"
            "- 선호 장르: 일본 시티팝×케이팝 퓨전, 감성 힙합·R&B·Pop\n"
            "- 금지: lofi, lo-fi, study beats, chill beats\n"
            "- BPM 110 이상, 신나고 에너제틱하게\n"
            "- 키워드 감성을 가사/주제에 반드시 반영"
        )
        result = _lm(prompt, task='', max_tokens=200)
        if result and result.strip() and len(result.strip()) > 30:
            cleaned = result.strip().split('\n')[0].strip()
            if not any(b in cleaned.lower() for b in ['lofi', 'lo-fi', 'study beats', 'chill beats']):
                return cleaned
    except Exception as e:
        print(f"  [음악 프롬프트 생성] 실패: {e}")
    return (
        f"J-Pop City Pop × K-Pop Fusion (1980s Retro), "
        f"Medium-fast tempo 118 BPM energetic groove, "
        f"DX7 electric piano + punchy kick drum + slap bass + brass synth, "
        f"Powerful K-pop female vocals with city pop smoothness, "
        f"{keyword} — neon city lights and midnight drive"
    )


# 하위 호환성 유지 (기존 코드에서 호출 시)
def generate_music_prompt_from_title(title: str, keyword: str) -> str:
    """제목을 기반으로 음악 내용(프롬프트)을 정의.

    흐름: 제목 확정 → 제목 감성/무드 분석 → 5단 음악 프롬프트 생성
    제목에 담긴 분위기, 키워드, 감성이 음악 내용에 직접 반영됨.
    """
    try:
        import sys as _sys
        _sys.path.insert(0, _root)
        from _shared.ollama_client import chat as _lm
        prompt = (
            f"유튜브 음악 영상 제목: '{title}'\n\n"
            "위 제목의 분위기·키워드·감성을 분석해서 아래 5단 템플릿으로 "
            "영어 음악 생성 프롬프트를 1개 작성해. 프롬프트 1줄만 출력.\n\n"
            "템플릿: [장르/시대], [템포/무드], [특정악기], [보컬스타일], [가사/주제]\n\n"
            "규칙:\n"
            "- 선호 장르: 일본 시티팝×케이팝 퓨전, 감성 힙합·R&B·Pop\n"
            "- 금지: lofi, lo-fi, study beats, chill beats\n"
            "- BPM 110 이상, 신나고 에너제틱하게\n"
            "- 제목의 분위기·감성을 가사/주제에 그대로 반영할 것"
        )
        result = _lm(prompt, task='', max_tokens=200)
        if result and result.strip() and len(result.strip()) > 30:
            cleaned = result.strip().split('\n')[0].strip()
            if not any(b in cleaned.lower() for b in ['lofi', 'lo-fi', 'study beats', 'chill beats']):
                print(f"  [음악 프롬프트←제목] {cleaned[:80]}...")
                return cleaned
    except Exception as e:
        print(f"  [음악 프롬프트 생성] 실패: {e} → keyword 폴백")
    return generate_music_prompt_from_keyword(keyword or title)


def _load_used_keywords(days: int = _USED_KEYWORDS_DAYS) -> set:
    """최근 N일간 사용된 루나 키워드 set — 가희 duplicate_guard 위임."""
    from _shared.duplicate_guard import get_used_yt_keywords as _guard
    return _guard(days)


class TrendAnalyzer:
    """
    데이터 분석 및 유튜브 인기 키워드를 추적하여 오늘의 음악 비주얼 프롬프트를 설계합니다.
    """
    def __init__(self):
        # 기본 fallback 테마 (장르/시대, 템포/무드, 특정 악기, 보컬 스타일, 가사/주제 정의)
        # 음악 테마를 주로 일본 시티팝 무드(1980s Retro Japanese City Pop) 위주로 변경
        self.default_themes = [
            # ── K-POP × 시티팝 융합 테마 (2026-05-28 추가) ──────────────────
            {
                "keyword": "K-Pop City Pop Night Drive",
                "mood": "stylish, energetic, nostalgic, urban chic",
                "style": "Seoul neon night skyline, K-pop idol silhouette, retro 80s Tokyo street, holographic glow",
                "genre_era": "K-Pop × Japanese City Pop Fusion (80s Retro)",
                "tempo_mood": "Medium-fast tempo (118 BPM), stylish, energetic, smooth K-pop production with city pop warmth",
                "instruments": "DX7 electric piano, punchy K-pop kick drum, slap bass, analog brass synth, twinkling glockenspiel",
                "vocal_style": "Clear powerful K-pop female lead vocals, tight harmonic backing, call-and-response ad-libs",
                "lyrics_theme": "Midnight Seoul drive, K-pop star in city lights, blending Korean soul with Tokyo retro romance"
            },
            {
                "keyword": "Seoul Neon City Pop",
                "mood": "glamorous, bold, retro-futuristic, addictive",
                "style": "Gangnam district neon signs, idol aesthetic, pastel city pop sunset, retro anime vibe",
                "genre_era": "K-Pop × City Pop Synthwave (Late 80s Fusion)",
                "tempo_mood": "Fast tempo (122 BPM), glamorous, bold hook-driven, city pop groove with K-pop precision",
                "instruments": "Bright synth lead, K-pop snare punch, groovy slap bass, DX7 pads, shimmering cymbals",
                "vocal_style": "Confident powerful lead vocals with airy falsetto, group harmony chorus, K-pop precision",
                "lyrics_theme": "Shining under Seoul neon lights, idol dreams meeting city pop nostalgia, unstoppable night"
            },
            {
                "keyword": "K-Wave Retro Romance",
                "mood": "dreamy, romantic, soft-powerful, melodic",
                "style": "cherry blossom in Seoul alley, soft city lights, K-drama aesthetic meets 80s Tokyo",
                "genre_era": "K-Pop Ballad × Soft City Pop (80s Inspired)",
                "tempo_mood": "Medium-slow tempo (100 BPM), dreamy, deeply melodic, emotional K-pop balladry with city pop warmth",
                "instruments": "Warm Rhodes piano, soft slap bass, orchestra strings, light snare brush, airy synth pad",
                "vocal_style": "Emotional, breathy K-pop female lead, soaring high notes, gentle harmonies",
                "lyrics_theme": "A timeless romance across Seoul and Tokyo, longing for yesterday under city lights"
            },
            {
                "keyword": "K-Pop Disco Funk City",
                "mood": "playful, energetic, groovy, feel-good",
                "style": "retro disco hall with K-pop dancers, flashy mirror ball, pastel neon city backdrop",
                "genre_era": "K-Pop × City Pop Disco Funk (80s)",
                "tempo_mood": "Fast tempo (128 BPM), highly groovy, feel-good, K-pop energy with disco city pop flair",
                "instruments": "Funky rhythm guitar, bass groove, bright brass hits, K-pop drum kit, wah synth",
                "vocal_style": "Bright playful lead vocals, catchy ad-libs, strong group chorus chant",
                "lyrics_theme": "Neon disco night, K-pop groove meets city pop dance floor, unstoppable feel-good energy"
            },
            {
                "keyword": "Retro Japanese City Pop",
                "mood": "nostalgic, upbeat, breezy, romantic",
                "style": "80s Tokyo city neon lights street, retro anime style",
                "genre_era": "Japanese City Pop (1980s Retro)",
                "tempo_mood": "Medium-tempo (115 BPM), nostalgic, upbeat, breezy, romantic",
                "instruments": "Slap bass guitar, electric piano (DX7), brass synthesizer section, vintage drum machine",
                "vocal_style": "Sweet and smooth female lead vocals, jazzy backing harmonies",
                "lyrics_theme": "Late night drive in Tokyo, neon street lights, fading summer romance"
            },
            {
                "keyword": "Midnight Tokyo City Pop Drive",
                "mood": "groovy, melancholic, nocturnal",
                "style": "rainy night in Shibuya retro aesthetic, nostalgic vaporwave vibe",
                "genre_era": "Japanese City Pop (Late 1980s)",
                "tempo_mood": "Slow-medium tempo (100 BPM), groovy, melancholic, nocturnal, smooth",
                "instruments": "Jazzy guitar chords, warm sub-bass, nostalgic synth pads, saxophone solos",
                "vocal_style": "Soft whispery male lead vocals, smooth vocal echo effect",
                "lyrics_theme": "Rainy midnight drive, lonely city streets, nostalgia for yesterday"
            },
            {
                "keyword": "Sparkling City Pop Dance",
                "mood": "sparkling, cheerful, groovy",
                "style": "retro resort beach disco night, colorful pastel neon lights",
                "genre_era": "Japanese City Pop / Disco-Funk (1980s)",
                "tempo_mood": "Fast-tempo (120 BPM), sparkling, cheerful, highly groovy, energetic",
                "instruments": "Funky rhythm guitar scratch, bright brass chords, driving disco drums, funky slap bass",
                "vocal_style": "Passionate bright female lead vocals, upbeat group choruses",
                "lyrics_theme": "Sparkling dance floor by the beach, tropical night breeze, endless weekend celebration"
            },
            {
                "keyword": "Golden Morning Espresso",
                "mood": "warm, premium, nostalgic morning",
                "style": "elegant premium coffee cup on a marble table, warm golden morning light streaming in, retro anime cafe interior",
                "genre_era": "Soft Jazzy City Pop (Mid 1980s)",
                "tempo_mood": "Medium-slow tempo (95 BPM), warm, cozy, premium, nostalgic morning vibe",
                "instruments": "Electric marimba synth, DX7 Rhodes piano, acoustic double bass, vintage hi-hats",
                "vocal_style": "Clear passionate female lead vocals, mellow hums",
                "lyrics_theme": "First warm coffee cup on a golden morning, chasing sweet dreams"
            },
            {
                "keyword": "Dewy Rose Skincare",
                "mood": "pure, luxury, silky smooth",
                "style": "elegant cosmetic skincare serum glass bottle on a clean white marble surface, dewy rose water droplets, soft vaporwave aesthetic",
                "genre_era": "Dreamy Ambient City Pop (Late 1980s)",
                "tempo_mood": "Slow tempo (85 BPM), pure, silky, luxury, soft, relaxing",
                "instruments": "Warm analog synthesizer pads, delicate chimes, smooth fretless bass, echo snare",
                "vocal_style": "Soft whispering female backing vocals, dreamy harmony",
                "lyrics_theme": "Silky smooth touch, fresh morning dew on rose petals, pure luxury routine"
            },
            {
                "keyword": "Tokyo Neon Perfume",
                "mood": "nocturnal, dramatic, high-end",
                "style": "luxury perfume bottle on a reflective glass surface, neon night skyline of Tokyo bokeh background, cinematic dramatic retro lighting",
                "genre_era": "Sensual Late-Night City Pop (1980s)",
                "tempo_mood": "Slow-medium tempo (105 BPM), nocturnal, dramatic, sensual, high-end",
                "instruments": "Mellow saxophone solo, slap bass highlights, warm Rhodes chords, gated synth drums",
                "vocal_style": "Sensual deep female lead vocals, jazzy backing echoes",
                "lyrics_theme": "Scent of city lights, mysterious perfume in Shibuya night breeze"
            },
            {
                "keyword": "Artisan Gold Chocolate",
                "mood": "rich, chocolatey, golden romantic",
                "style": "artisan chocolate pieces falling onto dark marble, golden rich brown tones, retro premium packaging, warm retro studio lighting",
                "genre_era": "Groovy City Pop R&B (Late 1980s)",
                "tempo_mood": "Medium tempo (110 BPM), rich, sweet, highly groovy, romantic",
                "instruments": "Funky rhythm guitar scratching, electric bass groove, warm brass hits, vintage synth clavinet",
                "vocal_style": "Passionate bright male lead vocals, upbeat group choruses",
                "lyrics_theme": "Sweet chocolate melting in love, golden sunset romance"
            },
            {
                "keyword": "Pure Glacial Water",
                "mood": "glacial clear, highly refreshing",
                "style": "premium crystal clear water bottle surrounded by ice and fresh mint leaves, pure sky blue background, refreshing retro poster aesthetic",
                "genre_era": "Breezy Summer City Pop (1980s)",
                "tempo_mood": "Fast-tempo (125 BPM), highly refreshing, breezy, energetic, sparkling",
                "instruments": "Bright digital keyboard, driving disco bass guitar, snappy snare, crystal chimes",
                "vocal_style": "Cheerful energetic female lead vocals, bright high notes",
                "lyrics_theme": "Pure mountain breeze, refreshing ice splash on a hot summer afternoon"
            }
        ]

    def load_learned_themes(self) -> list:
        """luna_research.json에서 학습된 테마 로드"""
        if not os.path.exists(_RESEARCH_FILE):
            return []
        try:
            with open(_RESEARCH_FILE, "r", encoding="utf-8") as f:
                data = json.load(f)
            return data.get("learned_themes", [])
        except Exception:
            return []

    def load_style_insights(self) -> list:
        """학습된 비주얼/제목 인사이트 로드"""
        if not os.path.exists(_RESEARCH_FILE):
            return []
        try:
            with open(_RESEARCH_FILE, "r", encoding="utf-8") as f:
                data = json.load(f)
            return data.get("style_insights", []) + data.get("title_insights", [])
        except Exception:
            return []

    def fetch_google_trends(self) -> list:
        """
        구글 트렌드 RSS 피드에서 인기 키워드 수집 시도
        """
        try:
            url = "https://trends.google.com/trending/rss?geo=KR"
            req = urllib.request.Request(url, headers={'User-Agent': 'Mozilla/5.0'})
            with urllib.request.urlopen(req, timeout=5) as response:
                xml_data = response.read()
            
            root = ET.fromstring(xml_data)
            trends = []
            for item in root.findall('.//item'):
                title = item.find('title')
                if title is not None and title.text:
                    trends.append(title.text.strip())
            return trends
        except Exception as e:
            # 실패 시 빈 리스트 리턴
            print(f"[Warning] 구글 트렌드 RSS 스캔 실패: {e}")
            return []

    def select_best_theme(self, idx: int = 1) -> dict:
        """
        트렌드 데이터 기반 최종 음악 테마 선정.
        ① 유튜브 상위 100개 제목 수집 → 알고리즘 최적화 제목 생성
        ② 표준 음악 프롬프트 템플릿(장르/시대+템포/무드+악기+보컬+가사) 적용
        ③ lofi 계열 금지
        """
        trends = self.fetch_google_trends()
        learned = self.load_learned_themes()

        # ── 유튜브 상위 제목 수집 (알고리즘 최적화용) ────────────────────────
        yt_api_key = os.getenv("YOUTUBE_API_KEY", "")
        yt_top_titles: list[str] = []
        if yt_api_key:
            yt_top_titles = _fetch_yt_top_titles(yt_api_key, n=100)
            if yt_top_titles:
                print(f"[Info] 📊 유튜브 상위 {len(yt_top_titles)}개 제목 수집 완료")

        # ── 최근 사용된 키워드 로드 (중복 방지) ──────────────────────────────
        used_keywords = _load_used_keywords()
        if used_keywords:
            print(f"[Info] 🚫 최근 {_USED_KEYWORDS_DAYS}일 사용된 키워드 {len(used_keywords)}개 제외")

        def _is_used(kw: str) -> bool:
            return kw.lower() in used_keywords or any(kw.lower() in u for u in used_keywords)

        # ── 학습 테마 우선 사용 (70% 확률, 학습 테마가 있을 때) ──────────────
        # 사용된 키워드 제외 후 선택
        fresh_learned = [t for t in learned if not _is_used(t.get("keyword", ""))]
        if not fresh_learned and learned:
            print("[Info] ⚠️ 모든 학습 테마가 최근 사용됨 — 기본 테마 폴백")
        if fresh_learned and random.random() < 0.7:
            picked = random.choice(fresh_learned)
            keyword        = picked.get("keyword", "City Night Drive")
            genre_era      = picked.get("genre_era", "Japanese City Pop (1980s Retro)")
            vocal_style    = picked.get("vocal_style", "Sweet and smooth female lead vocals, jazzy backing harmonies")
            core_topic     = picked.get("music_theme") or picked.get("lyrics_theme", "Late night drive in Tokyo")
            base_instruments = picked.get("instruments", "Slap bass, DX7 piano, brass synthesizer, drum machine")
            mood           = picked.get("mood", "nostalgic, upbeat, breezy, romantic")
            learned_visual = picked.get("visual_prompt", "")
            print(f"[Info] 🧠 학습 테마 적용: '{keyword}' (총 {len(learned)}개 학습됨)")

        # ── 구글 트렌드 기반 (사용된 것, 금지 장르 제외) ────────────────────
        elif trends:
            fresh_trends = [t for t in trends if not _is_used(t) and not _is_banned_genre(t)]
            top_trend = fresh_trends[(idx - 1) % len(fresh_trends)] if fresh_trends else None
            if top_trend is None:
                # 트렌드가 모두 금지/사용됨 → 기본 테마로
                fresh_defaults = [t for t in self.default_themes if not _is_used(t.get("keyword", ""))]
                pool = fresh_defaults if fresh_defaults else self.default_themes
                fallback = pool[(idx - 1) % len(pool)]
                keyword = fallback["keyword"]
                genre_era = fallback.get("genre_era", "K-Pop × Japanese City Pop Fusion (80s Retro)")
                vocal_style = fallback.get("vocal_style", "Clear powerful K-pop female lead vocals")
                core_topic = fallback.get("lyrics_theme", "Seoul neon night drive, K-pop meets city pop")
                base_instruments = fallback.get("instruments", "DX7 piano, slap bass, analog brass")
                mood = fallback.get("mood", "stylish, energetic, nostalgic")
                learned_visual = ""
            else:
                print(f"[Info] 최신 트렌드 키워드 발견 (비디오 #{idx}): {top_trend}")
                keyword        = top_trend
                genre_era      = "K-Pop × Japanese City Pop Fusion (80s Trend Edition)"
                vocal_style    = "Clear powerful K-pop female lead vocals, city pop warmth"
                core_topic     = f"Seoul meets Tokyo — K-pop soul and city pop nostalgia inspired by '{top_trend}'"
                base_instruments = "DX7 electric piano, K-pop drum kit, slap bass, analog brass synth"
                mood           = "stylish, energetic, nostalgic, urban"
                learned_visual = ""

        # ── 기본 테마 폴백 (사용된 것 제외) ─────────────────────────────────
        else:
            fresh_defaults = [t for t in self.default_themes if not _is_used(t.get("keyword", ""))]
            pool = fresh_defaults if fresh_defaults else self.default_themes
            fallback = pool[(idx - 1) % len(pool)]
            keyword        = fallback["keyword"]
            genre_era      = fallback.get("genre_era", "Japanese City Pop (1980s Retro)")
            vocal_style    = fallback.get("vocal_style", "Sweet and smooth female lead vocals, jazzy backing harmonies")
            core_topic     = fallback.get("lyrics_theme", "Late night drive in Tokyo, neon street lights")
            base_instruments = "Slap bass, DX7 piano, brass synthesizer, drum machine"
            mood           = fallback.get("mood", "nostalgic, upbeat, breezy, romantic")
            learned_visual = ""

        # 기승전결 (Intro -> Verse -> Chorus -> Bridge -> Outro) 5단 프롬프트 생성 (잔잔하고 감성적인 Mellow/Calm City Pop 템포 적용)
        parts = {
            # 표준 템플릿: 장르/시대 + 템포/무드 + 특정악기 + 보컬스타일 + 가사/주제
            "intro": _build_music_prompt(genre_era, "Very slow calm ambient build-up 80 BPM, cozy night atmosphere", "soft nostalgic synth pads, delicate chimes", "whispery quiet hums", f"opening scene of {core_topic}"),
            "verse": _build_music_prompt(genre_era, "Slow mellow tempo 90 BPM, smooth groove", f"warm DX7 Rhodes piano, gentle sub-bass, {base_instruments}", vocal_style, core_topic),
            "chorus": _build_music_prompt(genre_era, "Melodic emotional climax 115 BPM, energetic hook", f"punchy kick drum, brass synth highlights, {base_instruments}", f"powerful {vocal_style}, soaring harmonies", core_topic),
            "bridge": _build_music_prompt(genre_era, "Reflective half-tempo, introspective mood", "warm saxophone solo, acoustic guitar chords, dreamy ambient pad", "soft backing hums, whisper vocals", f"deeper emotion of {core_topic}"),
            "outro": _build_music_prompt(genre_era, "Gentle peaceful fade-out mood", "mellow fretless bass, fading warm digital piano keys", f"sweet final whispering {vocal_style}", "fading into cool quiet night breeze")
        }


        # 유튜브 100대 음악 분석 알고리즘 최적화 타이틀 생성
        # 제목 형식: 🎵 [장르/무드] | [컨셉 설명]  (참고: EIKeuvi0g5E 스타일)
        clean_kw = re.sub(r'[^a-zA-Z0-9\s]', '', keyword).strip().upper()
        if not clean_kw:
            clean_kw = "CITY NIGHTS"

        # 유튜브 상위 100개 분석 → 지식 파일 공식 기반 제목 생성 (항상 적용)
        # 공식: LUNA - [곡명 영문 대문자] [Official Music Video] (한글 감성 설명)
        title = _generate_optimized_title(keyword, yt_top_titles)
        
        # 테마 감성 설명구 매칭 (음악별 개별화 및 플레이리스트가 아닌 '음악'으로 변경)
        kw_lower = keyword.lower()
        if "espresso" in kw_lower or "morning" in kw_lower:
            mood_desc = "따뜻한 아침 햇살이 비치는 창가, 갓 추출한 에스프레소의 향긋함과 함께 설레는 하루를 시작하는 기분.\n80s 감성을 담은 류나의 모닝 시티팝 음악입니다."
            rec_situations = "아침 커피 타임 / 등교 및 출근길 / 아침 음악 / 독서 시간"
            hashtags = "#시티팝 #citypop #모닝음악 #커피음악 #류나 #80s #jpop #감성음악 #힐링음악 #출근길음악"
        elif "rose" in kw_lower or "skincare" in kw_lower or "dewy" in kw_lower:
            mood_desc = "장미 꽃잎에 맺힌 촉촉한 이슬처럼, 하루의 피로를 부드럽게 씻어내고 나만의 휴식을 취하는 기분.\n80s 감성을 담은 류나의 실키 앰비언트 시티팝 음악입니다."
            rec_situations = "휴식 및 스킨케어 / 요가 및 명상 / 샤워 시간 / 조용한 밤"
            hashtags = "#시티팝 #citypop #릴랙싱음악 #스킨케어 #류나 #80s #ambient #감성음악 #힐링음악 #휴식"
        elif "perfume" in kw_lower or "shibuya" in kw_lower or "midnight" in kw_lower or "neon" in kw_lower:
            mood_desc = "은은하게 퍼지는 향수 향기와 함께 화려한 네온빛 도시 야경 속을 질주하는 기분.\n80s 감성을 담은 류나의 센슈얼 심야 시티팝 음악입니다."
            rec_situations = "심야 드라이브 / 밤거리 산책 / 세련된 무드 연출 / 퇴근길"
            hashtags = "#시티팝 #citypop #밤드라이브 #퇴근길음악 #류나 #80s #nocturnal #감성음악 #새벽감성 #도시야경"
        elif "chocolate" in kw_lower or "sweet" in kw_lower:
            mood_desc = "달콤하게 녹아내리는 수제 초콜릿처럼, 노을빛 아래 연인과 사랑을 속삭이는 기분.\n80s 감성을 담은 류나의 스위트 R&B 시티팝 음악입니다."
            rec_situations = "연인과의 데이트 / 노을빛 산책 / 디저트 타임 / 달콤한 휴식"
            hashtags = "#시티팝 #citypop #데이트음악 #달콤한노래 #류나 #80s #rnb #감성음악 #로맨틱시티팝 #디저트음악"
        elif "water" in kw_lower or "glacial" in kw_lower or "beach" in kw_lower or "disco" in kw_lower:
            mood_desc = "무더운 뜨거운 태양 아래, 가슴 속을 얼릴 듯 짜릿하고 시원한 탄산 음료 한 모금의 기분.\n80s 감성을 담은 류나의 서머 댄스 시티팝 음악입니다."
            rec_situations = "여름 휴가길 / 드라이브 / 홈파티 / 리프레시 타임"
            hashtags = "#시티팝 #citypop #여름시티팝 #드라이브음악 #류나 #80s #disco #청량한음악 #신나는음악 #여가길"
        elif "spring" in kw_lower or "cherry" in kw_lower or "bloom" in kw_lower or "봄" in kw_lower:
            mood_desc = "벚꽃이 눈부시게 흩날리는 봄날 오후, 창문을 열고 바람을 느끼며 드라이브하고 싶은 기분.\n80s 감성을 담은 류나의 봄 시티팝 음악입니다."
            rec_situations = "봄날 드라이브 / 봄 나들이 / 기분 전환 / 설레는 날"
            hashtags = "#시티팝 #citypop #봄음악 #드라이브음악 #류나 #80s #jpop #감성음악 #봄시티팝 #설레는음악"
        else: # 키워드 기반 동적 무드 설명구 매칭 (음악에 맞게 자동 커스텀)
            clean_word = keyword.replace("Retro ", "").replace("Japanese ", "").replace("City Pop", "").strip()
            if not clean_word:
                clean_word = "City Night"
            mood_desc = f"도시의 네온사인 불빛 사이로 '{clean_word}'의 짙은 여운이 어리는 밤, 감각적인 비트와 함께 감성을 채워주는 기분.\n80s 감성을 담은 류나의 '{clean_word}' 테마 시티팝 음악입니다."
            rec_situations = f"야간 드라이브 / 밤거리 산책 / 나만의 휴식 시간 / 감성 충전이 필요할 때"
            
            safe_kw = re.sub(r'[^a-zA-Z0-9가-힣]', '', clean_word).lower()
            hashtags = f"#시티팝 #citypop #류나 #80s #retro #감성음악 #밤드라이브 #{safe_kw}음악"

        # 디스크립션 형식: 참고 영상(EIKeuvi0g5E) 스타일 — 짧은 감성 소개 + 채널 링크 + 해시태그
        description = (
            f"{mood_desc}\n\n"
            f"📌 추천 상황: {rec_situations}\n\n"
            f"🎵 More from LUNA: youtube.com/@luna_official\n\n"
            f"{hashtags}"
        )




        
        # 키워드별 맞춤형 SEO 태그 — 참고 영상(EIKeuvi0g5E) 스타일 기준
        base_tags = [
            "시티팝", "시티팝 bgm", "일본 시티팝", "일본시티팝무드음악",
            "city pop", "citypop", "citypop BGM", "japanese city pop", "retro city pop",
            "LUNA", "루나", "AI LUNA", "AI 음악",
            "80s", "80s retro", "80s japanese",
            "감성 음악", "감성음악", "드라이브 bgm", "드라이브 음악",
        ]
        if keyword.lower() not in [t.lower() for t in base_tags]:
            base_tags.append(keyword.lower())
        if clean_kw.lower() not in [t.lower() for t in base_tags]:
            base_tags.append(clean_kw.lower())
            
        if "espresso" in kw_lower or "morning" in kw_lower:
            base_tags.extend(["모닝 시티팝", "커피 음악", "아침 BGM", "출근길 음악", "coffee bgm", "morning city pop"])
        elif "rose" in kw_lower or "skincare" in kw_lower or "dewy" in kw_lower:
            base_tags.extend(["릴랙싱 음악", "스킨케어 bgm", "조용한 음악", "힐링 시티팝", "relaxing bgm", "dreamy city pop"])
        elif "perfume" in kw_lower or "shibuya" in kw_lower or "midnight" in kw_lower or "neon" in kw_lower:
            base_tags.extend(["심야 드라이브", "밤드라이브 BGM", "퇴근길 음악", "새벽감성 시티팝", "night drive", "nocturnal bgm"])
        elif "chocolate" in kw_lower or "sweet" in kw_lower:
            base_tags.extend(["데이트 음악", "달콤한 노래", "로맨틱 시티팝", "디저트 음악", "sweet pop", "romantic bgm"])
        elif "water" in kw_lower or "glacial" in kw_lower or "beach" in kw_lower or "disco" in kw_lower:
            base_tags.extend(["여름 시티팝", "드라이브 음악", "청량한 BGM", "신나는 시티팝", "summer city pop", "disco synth"])
        elif "spring" in kw_lower or "cherry" in kw_lower or "bloom" in kw_lower or "봄" in kw_lower:
            base_tags.extend(["봄 시티팝", "봄 음악", "벚꽃 드라이브", "설레는 노래", "spring drive", "cherry blossom bgm"])
        else:
            clean_word = keyword.replace("Retro ", "").replace("Japanese ", "").replace("City Pop", "").strip()
            if clean_word:
                base_tags.extend([clean_word.lower(), f"{clean_word.lower()} bgm", f"{clean_word.lower()} 음악"])
            base_tags.extend(["밤드라이브 BGM", "감성 시티팝 BGM", "작업용 BGM"])
            
        tags = list(dict.fromkeys([t.strip() for t in base_tags if t.strip()]))

        # VEO 비주얼 및 이미지 프롬프트 (학습 비주얼이 있으면 chorus에 반영)
        style = f"a luxurious retro 80s anime visual showing '{keyword}', neon glowing signs, Tokyo midnight street, pastel tone sunset beach, highly aesthetic"
        prompt = learned_visual if learned_visual else (
            f"A beautiful cinematic retro visualizer background showing '{keyword}', "
            f"glowing pastel neon colors, retro 1980s anime style, detail-rich, Ghibli atmosphere, night neon drive mood"
        )

        # 기승전결 5단 테마 맞춤 비주얼 프롬프트
        visual_parts = {
            "intro": f"A beautiful cinematic retro visualizer background, slow calm night, establishing shot of Tokyo skyline with twinkling stars, soft glowing streetlights, pastel retro 1980s anime style, detail-rich, Ghibli atmosphere, inspired by '{keyword}'",
            "verse": f"A classic sportscar driving smoothly on a rainy night in Shibuya, neon signs reflecting on the wet street, retro 1980s anime style, cozy melancholic atmosphere, detail-rich, inspired by '{keyword}'",
            "chorus": learned_visual if learned_visual else f"A stunning pastel sunset beach view, warm orange and pink horizon, gentle retro waves washing the shore, palm trees silhouette, sparkling neon pastel lights, nostalgic 1980s anime visual, inspired by '{keyword}'",
            "bridge": f"Interior of a cozy warm retro Tokyo cafe, a coffee cup on the wooden table, warm steam rising, window view of neon city lights bokeh in the background, highly aesthetic, retro anime style, inspired by '{keyword}'",
            "outro": f"Early morning dawn sunrise over the city, soft blue and golden sky, empty clean street, fading neon signs, quiet and peaceful exit, retro 1980s anime style, Ghibli atmosphere, inspired by '{keyword}'"
        }

        # 플레이리스트 자동 매핑 (무드별 분류)
        kw_l = keyword.lower()
        if any(w in kw_l for w in ["k-pop", "kpop", "k wave", "seoul", "disco funk"]):
            playlist_title = "K-POP × 시티팝 퓨전"
        elif any(w in kw_l for w in ["midnight", "neon", "perfume", "nocturnal"]):
            playlist_title = "심야 드라이브 시티팝"
        elif any(w in kw_l for w in ["morning", "espresso", "rose", "cozy"]):
            playlist_title = "감성 아침 시티팝"
        else:
            playlist_title = "도시 드라이브 시티팝"

        return {
            "keyword": keyword,
            "title": title,
            "mood": mood,
            "style": style,
            "prompt": prompt,
            "music_parts": parts,
            "visual_parts": visual_parts,
            "description": description,
            "tags": tags,
            "genre_era": genre_era,
            "tempo_mood": mood,
            "instruments": base_instruments,
            "vocal_style": vocal_style,
            "lyrics_theme": core_topic,
            "playlist_title": playlist_title,
            "_yt_top_titles": yt_top_titles,   # 파이프라인 메타데이터 자동생성용
        }



if __name__ == "__main__":
    import io
    import sys
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
    analyzer = TrendAnalyzer()
    theme = analyzer.select_best_theme()
    print("선정된 테마 정보:")
    print(json.dumps(theme, indent=4, ensure_ascii=False))


