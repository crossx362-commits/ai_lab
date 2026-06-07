import urllib.request
import xml.etree.ElementTree as ET
import json
import re
import os
import sys
import random

_here = os.path.dirname(os.path.abspath(__file__))
_ai_team_root = os.path.abspath(os.path.join(_here, "..", "..", "..", ".."))
if _ai_team_root not in sys.path:
    sys.path.insert(0, _ai_team_root)
from _shared.env_loader import find_project_root
_root = find_project_root(_here)
_RESEARCH_FILE   = os.path.join(_root, "reports", "research", "luna_research.json")
_HISTORY_FILE    = os.path.join(_root, "reports", "history", "upload_history.json")
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


def _load_used_title_words(n: int = 30) -> list[str]:
    """최근 n일 생성된 제목에서 반복 단어 추출."""
    if not os.path.exists(_TITLE_KNOWLEDGE_FILE):
        return []
    try:
        with open(_TITLE_KNOWLEDGE_FILE, "r", encoding="utf-8") as f:
            data = json.load(f)
        recent = sorted(data.keys())[-n:]
        titles = [data[k].get("generated_title", "") for k in recent if data[k].get("generated_title")]
        words = []
        for t in titles:
            words += re.findall(r"[A-Za-z가-힣]{2,}", t)
        from collections import Counter
        counts = Counter(w.lower() for w in words)
        return [w for w, c in counts.items() if c >= 2]
    except Exception:
        return []


def _load_title_knowledge() -> str:
    """SKILL.md + youtube_title_optimization.md에서 제목 생성 규칙 추출."""
    parts = []
    knowledge_dir = os.path.dirname(_TITLE_KNOWLEDGE_FILE)

    # 1. SKILL.md Section 3 — 제목 최적화 핵심 규칙
    skill_path = os.path.join(knowledge_dir, "..", "..", "SKILL.md")
    if os.path.exists(skill_path):
        m = re.search(r"### 1\. 제목 최적화 규칙.*?(?=\n### |\n## |\Z)",
                      open(skill_path, encoding="utf-8").read(), re.DOTALL)
        if m:
            parts.extend(l.strip() for l in m.group().splitlines() if l.strip())

    # 2. youtube_title_optimization.md — ## 1. 제목 생성 규칙 + ## 6. 반복 방지
    opt_path = os.path.join(knowledge_dir, "youtube_title_optimization.md")
    if os.path.exists(opt_path):
        text = open(opt_path, encoding="utf-8").read()
        for pat in [r"## 1\. 제목 생성 규칙.*?(?=\n## |\n# |\Z)",
                    r"## 6\. 반복 콘텐츠 방지.*?(?=\n## |\n# |\Z)"]:
            m = re.search(pat, text, re.DOTALL)
            if m:
                parts.extend(l.strip() for l in m.group().splitlines() if l.strip())

    return "\n".join(parts[:40])


def _load_all_used_titles() -> list[str]:
    """업로드 히스토리(upload_history.json)에서 기존 업로드된 모든 실제 영상 제목 로드."""
    if not os.path.exists(_HISTORY_FILE):
        return []
    try:
        with open(_HISTORY_FILE, "r", encoding="utf-8") as f:
            history = json.load(f)
        titles = []
        for r in history:
            title = r.get("metadata", {}).get("youtube_title", "")
            if title:
                titles.append(title.lower().strip())
        return titles
    except Exception:
        return []


def _is_similar_to_existing(title: str, existing_titles: list[str]) -> tuple[bool, str]:
    """기존 제목들과 자카드 유사도 및 부분매칭을 통해 중복/유사 제목 여부 판별."""
    title_clean = re.sub(r"[^가-힣A-Za-z0-9]", "", title.lower())
    title_words = set(re.findall(r"[A-Za-z가-힣]{2,}", title.lower()))
    
    for ext in existing_titles:
        ext_clean = re.sub(r"[^가-힣A-Za-z0-9]", "", ext)
        # 1. 완전 일치 또는 포함 관계
        if title_clean == ext_clean or title_clean in ext_clean or ext_clean in title_clean:
            return True, f"기존 제목 '{ext}'과 완전 일치 또는 포함 관계"
            
        # 2. 자카드 유사도 (50% 이상 단어 중복 시 차단)
        ext_words = set(re.findall(r"[A-Za-z가-힣]{2,}", ext))
        if title_words and ext_words:
            intersection = title_words.intersection(ext_words)
            union = title_words.union(ext_words)
            similarity = len(intersection) / len(union)
            if similarity >= 0.5:
                return True, f"기존 제목 '{ext}'과 단어 유사도 {int(similarity*100)}% 중복"
                
    return False, ""


def _validate_title(title: str, overused: set[str]) -> tuple[bool, str]:
    title_lower = title.lower()
    
    # 0. 기존 업로드 제목들과의 중복/유사성 검사
    existing_titles = _load_all_used_titles()
    is_dup, reason = _is_similar_to_existing(title, existing_titles)
    if is_dup:
        return False, reason
    
    # 한글 포함 여부 검사 (제목은 반드시 한글 위주여야 함)
    if not re.search(r"[가-힣]", title):
        return False, "한글(한국어)이 포함되어 있지 않음 (한글 제목 필수 규칙 위반)"

    # 문장형 응답이나 AI의 잡설 섞였는지 검사 (길이 및 단어 개수)
    if len(title) > 50:
        return False, "제목 길이가 너무 길어(50자 초과) 잡다한 설명이 포함된 것으로 의심됨"
        
    words_count = len(title.split())
    if words_count > 10:
        return False, f"단어 수가 너무 많아({words_count}개) 잡다한 설명이 포함된 것으로 의심됨"

    # AI의 상투적인 서술/안내식 한국어 표현 차단
    ai_talk_patterns = ["추천합니다", "생성해", "보겠습니다", "타이틀은", "제목은", "다음과 같다", "다음은", "관련된", "네!", "입니다", "합니다", "생성했습니다"]
    for pattern in ai_talk_patterns:
        if pattern in title_lower:
            return False, f"AI 잡설 패턴 '{pattern}' 포함됨"

    # 1. 고정 태그 검사
    for tag in ["luna", "official", "mv", "music video"]:
        if tag in title_lower:
            return False, f"고정 태그 '{tag}' 포함 금지 규칙 위반"
            
    # 2. 금지 장르 검사
    for bg in ["lofi", "lo-fi", "study beats", "chill beats", "sleep music", "white noise"]:
        if bg in title_lower:
            return False, f"금지 장르 키워드 '{bg}' 포함 금지 규칙 위반"
            
    # 3. 클리셰 단어 검사 (특히 neon/네온 강력 배제)
    cliches = ["neon", "네온", "네온 아래", "감성 충전", "늦은 밤 드라이브", "벚꽃 흩날리는 거리", "여름 바닷가", "조용한 밤", "편안한 휴식"]
    for cl in cliches:
        if cl in title_lower:
            return False, f"클리셰/금지 단어 '{cl}' 포함 금지 규칙 위반"
            
    # 4. 최근 중복 사용 단어 검사 (단어 단위 매칭)
    words = re.findall(r"[A-Za-z가-힣]{2,}", title_lower)
    for w in words:
        if w in overused:
            return False, f"최근 중복 사용된 단어 '{w}' 포함 금지 규칙 위반"
            
    return True, ""


def _generate_optimized_title(keyword: str, yt_titles: list[str]) -> str:
    """Ollama로 LUNA 뮤직비디오 제목 생성.
    - Top100 패턴 분석(fetch_yt_top100.get_title_pattern_analysis) 결과를 참고하여 알고리즘적으로 분석된 구조로 생성
    - title_patterns.json 반복 단어 자동 금지 및 클리셰 필터링 적용
    - 결과는 지식 파일에 누적 저장
    """
    try:
        import sys
        sys.path.insert(0, _root)
        from _shared.ollama_client import chat as _lm_chat, is_available as _lm_available
        if not _lm_available():
            return keyword

        overused = _load_used_title_words()
        overused_set = set(w.lower() for w in overused)
        
        avoid_clause = (
            f"- 아래 단어들은 최근 제목에서 반복 사용됐으므로 반드시 제외: {', '.join(overused)}\n"
            if overused else ""
        )
        knowledge = _load_title_knowledge()
        knowledge_block = f"\n[제목 최적화 지식 — 반드시 준수]\n{knowledge}\n" if knowledge else ""

        # Top100 패턴 분석 결과 가져오기
        pattern_hint = "유튜브 트렌드 제목 참고 없이 창의적으로"
        try:
            _luna_tools = os.path.join(_root, "projects", "ai-team", "skills", "루나_디렉터", "tools")
            if _luna_tools not in sys.path:
                sys.path.insert(0, _luna_tools)
            from knowledge.fetch_yt_top100 import get_title_pattern_analysis
            analysis = get_title_pattern_analysis()
            if analysis and "prompt_hint" in analysis:
                pattern_hint = f"미국 YouTube Top100 패턴 참고: {analysis['prompt_hint']}"
        except Exception as pe:
            print(f"[Warning] 패턴 분석 로드 실패: {pe}")

        context = f"아래 분석된 최신 유튜브 트렌드 스타일을 반영해서: {pattern_hint}\n"

        feedback_msg = ""
        for attempt in range(5):
            prompt = (
                f"{context}'{keyword}' 테마의 시티팝/K-POP 뮤직비디오 제목을 1개 만들어줘.\n"
                f"{knowledge_block}\n"
                "조건:\n"
                "- 고정 공식 없음. 분석된 Top100 패턴의 구조, 길이, 언어 비율, 구분자 스타일 등을 적극 차용하여 구조화할 것\n"
                "- LUNA, Official, MV, Music Video 등 고정 태그 삽입 절대 금지 (채널명이므로)\n"
                "- 반드시 감성적인 한국어(한글) 위주로 작성 (영어 단독 제목 절대 금지, 필요 시 영문 고유명사나 피처링 명칭만 최소 혼용 허용)\n"
                "- lofi/lo-fi/study beats/chill beats/sleep music 금지\n"
                "- 클리셰 단어 절대 금지: 'neon', '네온', '네온 아래', '감성 충전', '늦은 밤 드라이브', '벚꽃 흩날리는 거리', '여름 바닷가', '조용한 밤', '편안한 휴식'\n"
                f"{avoid_clause}"
                f"{feedback_msg}"
                "- 제목 1줄만 출력 (설명이나 다른 텍스트는 일체 제외)"
            )
            result = _lm_chat(prompt, task="", max_tokens=120)
            if result and result.strip():
                title = result.strip().split("\n")[0].strip()
                title = re.sub(r'^["\'“]+|["\'”]+$', '', title).strip()
                
                is_valid, reason = _validate_title(title, overused_set)
                if is_valid:
                    _save_title_pattern_knowledge({
                        "keyword": keyword,
                        "generated_title": title,
                        "sample_count": len(yt_titles),
                        "top_5_samples": yt_titles[:5],
                    })
                    return title
                else:
                    print(f"[Warning] 제목 검증 실패 (시도 {attempt+1}/5): {reason} -> '{title}'")
                    feedback_msg = f"\n[이전 생성 실패 피드백]\n- 직전에 생성했던 '{title}'은 '{reason}'으로 인해 거절되었습니다. 동일한 실수를 반복하지 말고 완전히 새로운 단어와 구조로 작성하십시오.\n"
            
    except Exception as e:
        print(f"[Warning] 제목 생성 실패: {e}")

    # 폴백: 스킬 규칙 기반 조합 (네온/클리셰 배제)
    kw_upper = keyword.upper()[:30]
    return f"{kw_upper}"


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
            "아래 6단 구조로 영어 음악 생성 프롬프트를 1개 작성해. 프롬프트 1줄만 출력.\n\n"
            "구조: [키워드 연계 콘셉트], [장르/시대], [템포/무드], [주요악기], [보컬스타일], [주제/가사]\n\n"
            "규칙:\n"
            "- 1순위: Korean Female Hip-Hop × R&B (여성 힙합·알앤비, 95~130 BPM)\n"
            "- 2순위: K-Pop Girl Group (걸그룹 팝·댄스, 115~150 BPM)\n"
            "- 3순위: Korean R&B Pop (감성 알앤비·팝, 80~110 BPM)\n"
            "- 보컬: 강인하고 자신감 넘치는 한국 여성 보컬, 랩·훅 혼합\n"
            "- 가사/주제(lyrics_theme)는 반드시 한국어로 작성\n"
            "- 금지: lofi, lo-fi, study beats, chill beats, sleep music, citypop, city pop"
        )
        result = _lm(prompt, task='', max_tokens=200)
        if result and result.strip() and len(result.strip()) > 30:
            cleaned = result.strip().split('\n')[0].strip()
            if not any(b in cleaned.lower() for b in ['lofi', 'lo-fi', 'study beats', 'chill beats']):
                return cleaned
    except Exception as e:
        print(f"  [음악 프롬프트 생성] 실패: {e}")
    return (
        f"Korean Female Hip-Hop × R&B, "
        f"Confident energetic 100 BPM groove with punchy 808 bass, "
        f"Piano chops + trap hi-hats + thick bass line + synth lead, "
        f"강인하고 자신감 넘치는 한국 여성 래퍼·보컬, 랩 버스 + 감성 훅, "
        f"{keyword}"
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
            f"확정된 유튜브 영상 제목: '{title}'\n\n"
            "위 제목의 핵심 콘셉트와 무드를 프롬프트 첫머리에 연계하여, "
            "아래 구조로 영어 음악 생성 프롬프트를 1개 작성해. 프롬프트 1줄만 출력.\n\n"
            "구조: [제목 콘셉트 연계], [장르/시대], [템포/무드], [주요악기], [보컬스타일], [주제/가사]\n\n"
            "규칙:\n"
            "- 1순위 장르: Korean Female Hip-Hop × R&B (여성 힙합·알앤비)\n"
            "- 2순위 장르: K-Pop Girl Group (걸그룹 팝·댄스)\n"
            "- 3순위 장르: Korean R&B Pop (감성 알앤비)\n"
            "- 보컬: 강인·자신감·매력적인 한국 여성 보컬, 랩+훅 구성\n"
            "- 가사/주제(lyrics_theme)는 반드시 한국어로 작성\n"
            "- 금지: lofi, lo-fi, study beats, chill beats, sleep music, city pop, citypop"
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
            # ── 여성 힙합 × R&B × 걸그룹 테마 (2026-06-05 변경) ──────────────
            {
                "keyword": "Korean Girl Group Bop",
                "mood": "fierce, confident, addictive, empowering",
                "style": "K-pop girl group stage, holographic outfit, urban Seoul backdrop, dynamic dance formation",
                "genre_era": "K-Pop Girl Group Dance Pop (2020s)",
                "tempo_mood": "Fast tempo (130 BPM), fierce, high-energy, addictive hook-driven girl group anthem",
                "instruments": "Hard-hitting 808 kick, trap hi-hats, synth lead, punchy claps, thick bass drop",
                "vocal_style": "Powerful Korean girl group vocals, sharp rap verse + melodic chorus, tight harmonies",
                "lyrics_theme": "자신감 넘치는 걸그룹의 무대, 당당하고 빛나는 여성의 에너지, 멈출 수 없는 열정"
            },
            {
                "keyword": "Korean Female Hip-Hop",
                "mood": "bold, swag, cool, authentic",
                "style": "Korean female rapper in urban streetwear, Seoul underground hip-hop vibe, city graffiti backdrop",
                "genre_era": "Korean Female Hip-Hop (Contemporary)",
                "tempo_mood": "Mid-tempo (98 BPM), swag-filled, authentic, hard-hitting Korean hip-hop flow",
                "instruments": "Boom-bap drums, deep 808 bass, jazz-sampled piano chops, vinyl scratch, brass stabs",
                "vocal_style": "Confident Korean female rapper, hard-hitting rap verses, melodic hook with attitude",
                "lyrics_theme": "나만의 길을 가는 강한 여성, 한국 힙합 문화, 자유와 자신감의 가사"
            },
            {
                "keyword": "Korean R&B Girl Vibe",
                "mood": "sensual, emotional, smooth, soulful",
                "style": "Korean R&B singer in soft-lit studio, urban Seoul night, moody atmospheric visuals",
                "genre_era": "Korean R&B × Soul Pop (Modern)",
                "tempo_mood": "Mid-tempo (90 BPM), smooth, soulful, emotionally rich Korean R&B groove",
                "instruments": "Rhodes piano, warm bass guitar, soft trap kick, lush string pads, muted guitar",
                "vocal_style": "Silky smooth Korean female R&B vocals, emotional runs and ad-libs, breathy whisper-to-belt",
                "lyrics_theme": "감성적인 밤, 사랑과 자아를 탐구하는 한국 알앤비, 내 마음 깊은 곳의 이야기"
            },
            {
                "keyword": "K-Pop Girl Crush",
                "mood": "fierce, cool, charismatic, unstoppable",
                "style": "girl crush concept, dark urban aesthetic, powerful stage presence, Seoul city night",
                "genre_era": "K-Pop Girl Crush Hip-Hop Pop (2020s)",
                "tempo_mood": "Fast tempo (120 BPM), charismatic, fierce, girl crush energy with hip-hop edge",
                "instruments": "Heavy bass, electric guitar riff, punchy snare, synth brass, trap percussion",
                "vocal_style": "Sharp powerful Korean female vocals, rap-vocal hybrid, fierce delivery with melodic chorus",
                "lyrics_theme": "걸크러시 에너지, 두려움 없는 자신감, 세상을 지배하는 강한 여성의 이야기"
            },
            {
                "keyword": "Korean Ballad R&B",
                "mood": "emotional, heartfelt, powerful, healing",
                "style": "emotional ballad atmosphere, soft Seoul evening, intimate singer-songwriter vibe",
                "genre_era": "Korean R&B Ballad (Contemporary)",
                "tempo_mood": "Slow-medium tempo (75 BPM), deeply emotional, heartfelt Korean R&B ballad",
                "instruments": "Grand piano, warm cello, soft bass, brushed drums, ambient synth pad",
                "vocal_style": "Powerful emotional Korean female belting, raw vulnerability, soaring climax",
                "lyrics_theme": "도쿄 심야 드라이브, 네온 가로등 아래 스러지는 여름 로맨스"
            },
            {
                "keyword": "Midnight Tokyo City Pop Drive",
                "mood": "groovy, melancholic, nocturnal",
                "style": "rainy night in Shibuya retro aesthetic, nostalgic vaporwave vibe",
                "genre_era": "Japanese City Pop (Late 1980s)",
                "tempo_mood": "Slow-medium tempo (100 BPM), groovy, melancholic, nocturnal, smooth",
                "instruments": "Jazzy guitar chords, warm sub-bass, nostalgic synth pads, saxophone solos",
                "vocal_style": "Soft whispery male lead vocals, smooth vocal echo effect",
                "lyrics_theme": "빗속 자정 드라이브, 텅 빈 도시 거리, 어제를 그리워하는 외로운 감성"
            },
            {
                "keyword": "Sparkling City Pop Dance",
                "mood": "sparkling, cheerful, groovy",
                "style": "retro resort beach disco night, colorful pastel neon lights",
                "genre_era": "Japanese City Pop / Disco-Funk (1980s)",
                "tempo_mood": "Fast-tempo (120 BPM), sparkling, cheerful, highly groovy, energetic",
                "instruments": "Funky rhythm guitar scratch, bright brass chords, driving disco drums, funky slap bass",
                "vocal_style": "Passionate bright female lead vocals, upbeat group choruses",
                "lyrics_theme": "해변가 반짝이는 댄스 플로어, 열대의 밤바람, 끝없는 주말 축제"
            },
            {
                "keyword": "Golden Morning Espresso",
                "mood": "warm, premium, nostalgic morning",
                "style": "elegant premium coffee cup on a marble table, warm golden morning light streaming in, retro anime cafe interior",
                "genre_era": "Soft Jazzy City Pop (Mid 1980s)",
                "tempo_mood": "Medium-slow tempo (95 BPM), warm, cozy, premium, nostalgic morning vibe",
                "instruments": "Electric marimba synth, DX7 Rhodes piano, acoustic double bass, vintage hi-hats",
                "vocal_style": "Clear passionate female lead vocals, mellow hums",
                "lyrics_theme": "황금빛 아침 첫 번째 따뜻한 커피 한 잔, 달콤한 꿈을 쫓는 설레는 하루"
            },
            {
                "keyword": "Dewy Rose Skincare",
                "mood": "pure, luxury, silky smooth",
                "style": "elegant cosmetic skincare serum glass bottle on a clean white marble surface, dewy rose water droplets, soft vaporwave aesthetic",
                "genre_era": "Dreamy Ambient City Pop (Late 1980s)",
                "tempo_mood": "Slow tempo (85 BPM), pure, silky, luxury, soft, relaxing",
                "instruments": "Warm analog synthesizer pads, delicate chimes, smooth fretless bass, echo snare",
                "vocal_style": "Soft whispering female backing vocals, dreamy harmony",
                "lyrics_theme": "부드러운 실크 감촉, 장미 꽃잎 위 신선한 아침 이슬, 순수한 럭셔리 루틴"
            },
            {
                "keyword": "Tokyo Neon Perfume",
                "mood": "nocturnal, dramatic, high-end",
                "style": "luxury perfume bottle on a reflective glass surface, neon night skyline of Tokyo bokeh background, cinematic dramatic retro lighting",
                "genre_era": "Sensual Late-Night City Pop (1980s)",
                "tempo_mood": "Slow-medium tempo (105 BPM), nocturnal, dramatic, sensual, high-end",
                "instruments": "Mellow saxophone solo, slap bass highlights, warm Rhodes chords, gated synth drums",
                "vocal_style": "Sensual deep female lead vocals, jazzy backing echoes",
                "lyrics_theme": "도시 불빛의 향기, 시부야 밤바람 속 신비로운 향수"
            },
            {
                "keyword": "Artisan Gold Chocolate",
                "mood": "rich, chocolatey, golden romantic",
                "style": "artisan chocolate pieces falling onto dark marble, golden rich brown tones, retro premium packaging, warm retro studio lighting",
                "genre_era": "Groovy City Pop R&B (Late 1980s)",
                "tempo_mood": "Medium tempo (110 BPM), rich, sweet, highly groovy, romantic",
                "instruments": "Funky rhythm guitar scratching, electric bass groove, warm brass hits, vintage synth clavinet",
                "vocal_style": "Passionate bright male lead vocals, upbeat group choruses",
                "lyrics_theme": "사랑에 녹아드는 달콤한 초콜릿, 황금빛 노을 아래 로맨스"
            },
            {
                "keyword": "Pure Glacial Water",
                "mood": "glacial clear, highly refreshing",
                "style": "premium crystal clear water bottle surrounded by ice and fresh mint leaves, pure sky blue background, refreshing retro poster aesthetic",
                "genre_era": "Breezy Summer City Pop (1980s)",
                "tempo_mood": "Fast-tempo (125 BPM), highly refreshing, breezy, energetic, sparkling",
                "instruments": "Bright digital keyboard, driving disco bass guitar, snappy snare, crystal chimes",
                "vocal_style": "Cheerful energetic female lead vocals, bright high notes",
                "lyrics_theme": "맑은 산바람, 뜨거운 여름 오후 시원한 물빛 속 청량한 해방감"
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

        # ── 유튜브 상위 제목 수집 (하루 1회 캐시 보장) ────────────────────────
        yt_top_titles: list[str] = []
        try:
            _top100_path = os.path.join(os.path.dirname(_here), "knowledge")
            if _top100_path not in sys.path:
                sys.path.insert(0, _top100_path)
            from fetch_yt_top100 import get_yt_top100_titles
            yt_top_titles = get_yt_top100_titles()
            if yt_top_titles:
                print(f"[Info] 유튜브 상위 {len(yt_top_titles)}개 제목 로드 (캐시/신규 페치 자동)")
        except Exception as _e:
            print(f"[Warning] fetch_yt_top100 스킬 로드 실패: {_e}")

        # ── 최근 사용된 키워드 로드 (중복 방지) ──────────────────────────────
        used_keywords = _load_used_keywords()
        if used_keywords:
            print(f"[Info] 🚫 최근 {_USED_KEYWORDS_DAYS}일 사용된 키워드 {len(used_keywords)}개 제외")

        def _is_used(kw: str) -> bool:
            return kw.lower() in used_keywords or any(kw.lower() in u for u in used_keywords)

        # ── 학습 테마 우선 사용 (70% 확률, 학습 테마가 있을 때) ──────────────
        # 사용된 키워드 + neon 과다 편중 제외 후 선택
        _OVERUSED = ["neon", "bloom", "stardust", "starlight", "city dream"]
        fresh_learned = [
            t for t in learned
            if not _is_used(t.get("keyword", ""))
            and not any(ov in t.get("keyword", "").lower() for ov in _OVERUSED)
        ]
        if not fresh_learned:
            # 과다 편중 필터 없이 재시도
            fresh_learned = [t for t in learned if not _is_used(t.get("keyword", ""))]
        if not fresh_learned and learned:
            print("[Info] ⚠️ 모든 학습 테마가 최근 사용됨 — 기본 테마 폴백")
        if fresh_learned and random.random() < 0.7:
            picked = random.choice(fresh_learned)
            keyword        = picked.get("keyword", "City Night Drive")
            genre_era      = picked.get("genre_era", "Japanese City Pop (1980s Retro)")
            vocal_style    = picked.get("vocal_style", "Sweet and smooth female lead vocals, jazzy backing harmonies")
            core_topic     = picked.get("music_theme") or picked.get("lyrics_theme", "도쿄 심야 드라이브, 네온 불빛 아래 설레는 밤")
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
                core_topic = fallback.get("lyrics_theme", "서울 네온 야간 드라이브, 케이팝과 시티팝의 만남")
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
        clean_kw = re.sub(r'[^a-zA-Z0-9\s]', '', keyword).strip().upper()
        if not clean_kw:
            clean_kw = "CITY NIGHTS"

        title = _generate_optimized_title(keyword, yt_top_titles)

        meta = self.build_metadata_for_keyword(keyword, title, yt_top_titles)
        description = meta["description"]
        tags = meta["tags"]

        # VEO 비주얼 및 이미지 프롬프트 (학습 비주얼이 있으면 chorus에 반영)
        style = f"a luxurious retro 80s anime visual showing '{keyword}', neon glowing signs, Tokyo midnight street, pastel tone sunset beach, highly aesthetic"
        prompt = learned_visual if learned_visual else (
            f"A beautiful cinematic retro visualizer background showing '{keyword}', "
            f"glowing pastel neon colors, retro 1980s anime style, detail-rich, Ghibli atmosphere, night neon drive mood"
        )

        # 동적 5단 비주얼 프롬프트 생성 (제목 및 가사/테마 연계)
        visual_parts = {}
        try:
            from _shared.ollama_client import chat as _lm_chat, is_available as _lm_available
            has_llm = _lm_available()
        except Exception:
            has_llm = False

        if has_llm:
            try:
                vis_prompt = (
                    f"확정된 음악 제목: '{title}'\n"
                    f"음악 장르/시대: '{genre_era}'\n"
                    f"무드: '{mood}'\n"
                    f"가사 주제/스토리: '{core_topic}'\n\n"
                    "위 정보를 반영하여, 유튜브 뮤직비디오의 흐름에 맞춰 생성될 5단 구성(intro, verse, chorus, bridge, outro)의 이미지 생성용 영어 프롬프트를 JSON 형식으로 작성해줘.\n"
                    "조건:\n"
                    "- 각 프롬프트는 80년대 레트로 애니메이션 스타일(1980s retro anime style, Ghibli atmosphere, aesthetic, detail-rich)을 기본 무드로 삼아 작성할 것\n"
                    "- 각 프롬프트는 음악의 제목, 무드, 가사 스토리 흐름에 완벽히 정렬되고 시각적으로 조화를 이루어야 함 (단순히 동일한 도쿄 거리나 스포츠카의 무의미한 반복 금지)\n"
                    "- intro: 곡의 도입부 분위기를 나타내는 차분하고 서정적인 배경이나 오브젝트 묘사\n"
                    "- verse: 전개되는 가사와 무드에 어울리는 풍경이나 상황 묘사\n"
                    "- chorus: 타이틀과 핵심 감성이 극대화되는 가장 화려하고 감동적인 메인 시각 묘사\n"
                    "- bridge: 노래의 성찰적이거나 전환되는 분위기에 어울리는 디테일하고 고요한 실내/소품/배경 묘사\n"
                    "- outro: 여운을 남기며 새벽, 밤하늘, 또는 조용한 풍경으로 마무리되는 연출\n"
                    "반드시 JSON 형식으로만 응답:\n"
                    '{"intro":"...","verse":"...","chorus":"...","bridge":"...","outro":"..."}'
                )
                vis_raw = _lm_chat(vis_prompt, task="", max_tokens=1000, json_mode=True)
                if vis_raw:
                    vis_data = json.loads(vis_raw.strip())
                    if all(k in vis_data for k in ["intro", "verse", "chorus", "bridge", "outro"]):
                        visual_parts = vis_data
                        print("  [TrendAnalyzer] ✅ 제목 및 음악 연계형 동적 비주얼 프롬프트 생성 완료")
            except Exception as e:
                print(f"  [Warning] 동적 비주얼 프롬프트 생성 실패: {e}")

        # 폴백 (기존 템플릿 사용하되 키워드 및 제목 무드 결합)
        if not visual_parts:
            visual_parts = {
                "intro": f"A beautiful cinematic retro visualizer background, slow calm night, establishing shot of Tokyo skyline with twinkling stars, soft glowing streetlights, pastel retro 1980s anime style, detail-rich, Ghibli atmosphere, inspired by '{keyword}', matching the title '{title}'",
                "verse": f"A classic sportscar driving smoothly on a rainy night in Shibuya, neon signs reflecting on the wet street, retro 1980s anime style, cozy melancholic atmosphere, detail-rich, inspired by '{keyword}', matching the title '{title}'",
                "chorus": learned_visual if learned_visual else f"A stunning pastel sunset beach view, warm orange and pink horizon, gentle retro waves washing the shore, palm trees silhouette, sparkling neon pastel lights, nostalgic 1980s anime visual, inspired by '{keyword}', matching the title '{title}'",
                "bridge": f"Interior of a cozy warm retro Tokyo cafe, a coffee cup on the wooden table, warm steam rising, window view of neon city lights bokeh in the background, highly aesthetic, retro anime style, inspired by '{keyword}', matching the title '{title}'",
                "outro": f"Early morning dawn sunrise over the city, soft blue and golden sky, empty clean street, fading neon signs, quiet and peaceful exit, retro 1980s anime style, Ghibli atmosphere, inspired by '{keyword}', matching the title '{title}'"
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

    def build_metadata_for_keyword(self, keyword: str, title: str, yt_top_titles: list[str]) -> dict:
        """지식 가이드라인에 맞춘 설명문(Description) 및 태그(Tags) 동적 생성 (루나 브랜딩 제거 및 중복 원천 차단)."""
        try:
            from _shared.ollama_client import chat as _lm_chat, is_available as _lm_available
            has_llm = _lm_available()
        except Exception:
            has_llm = False

        mood_desc = ""
        rec_situations = ""
        hashtags = ""
        kw_lower = keyword.lower()

        # 1. 설명글(mood_desc) 동적 생성 (LLM 활용하여 중복 전면 제거)
        if has_llm:
            try:
                desc_prompt = (
                    f"유튜브 음악 영상 제목: '{title}'\n"
                    f"음악 테마 키워드: '{keyword}'\n\n"
                    "위 정보를 바탕으로 이 80년대 시티팝/K-POP 음악이 전달하는 도시적인 야경, 청춘, 자유로움, 혹은 로맨틱한 정서를 한 편의 이야기처럼 묘사하는 감성적인 한국어 소개글을 2~3문장 내외로 작성해줘.\n"
                    "조건:\n"
                    "- 반드시 한국어로 작성\n"
                    "- 기존의 상투적인 클리셰('네온 아래', '감성 충전', '늦은 밤 드라이브', '편안한 휴식') 절대 사용 금지\n"
                    "- 다른 영상들과 표현이 겹치지 않게 완전히 새롭고 창의적인 문장으로 작성\n"
                    "- 부연 설명 없이 소개 본문 문장만 출력"
                )
                result = _lm_chat(desc_prompt, task="", max_tokens=150)
                if result and result.strip():
                    mood_desc = result.strip().split("\n")[0].strip()
            except Exception as e:
                print(f"[Warning] 동적 설명글 생성 실패: {e}")

        # LLM 실패 시에만 최소한의 폴백 적용
        if not mood_desc:
            clean_word = keyword.replace("Retro ", "").replace("Japanese ", "").replace("City Pop", "").strip()
            mood_desc = f"도시의 아늑한 불빛 사이로 아른거리는 '{clean_word}'의 여운을 담은 시간. 80s 시티팝의 부드러운 선율과 감성적인 멜로디가 녹아든 음악입니다."

        # 2. 추천 상황(rec_situations) 동적 생성
        if has_llm:
            try:
                sit_prompt = (
                    f"음악 테마: '{keyword}', 분위기: '{mood_desc}'\n\n"
                    "이 음악의 분위기와 가장 잘 어울리는 추천 상황 4가지를 쉼표(,)로 구분해서 한글로 적어줘.\n"
                    "예시: 아침 커피 타임, 밤거리 산책, 나만의 휴식, 홈파티\n"
                    "조건: 쉼표로 구분된 단어 4개만 딱 출력"
                )
                result = _lm_chat(sit_prompt, task="", max_tokens=60)
                if result and result.strip():
                    rec_situations = result.strip().replace("\n", " ")
            except Exception:
                pass
        if not rec_situations:
            rec_situations = "드라이브, 밤거리 산책, 나만의 휴식, 작업실 BGM"

        # 3. 해시태그(hashtags) 동적 생성
        if has_llm:
            try:
                hash_prompt = (
                    f"음악 테마: '{keyword}'\n\n"
                    "이 시티팝/K-POP 음악 영상에 달 해시태그 8개를 공백으로 구분해서 작성해줘.\n"
                    "조건:\n"
                    "- '#루나', '#luna', '#류나' 등 특정 브랜딩 절대 포함 금지\n"
                    "- 예시: #시티팝 #citypop #감성음악 #밤드라이브\n"
                    "- 해시태그 8개만 공백 구분하여 한 줄로 딱 출력"
                )
                result = _lm_chat(hash_prompt, task="", max_tokens=80)
                if result and result.strip():
                    hashtags = result.strip().replace("\n", " ")
            except Exception:
                pass
        if not hashtags:
            hashtags = "#시티팝 #citypop #80s #retro #감성음악 #밤드라이브"

        # 디스크립션 형식: 생성된 음악에 대한 설명에 초점을 맞추도록 단순화 및 최적화
        description = (
            f"🌟 {title}\n\n"
            f"{mood_desc}\n\n"
            f"🎹 Genre / Era: Japanese City Pop (1980s Retro)\n"
            f"🎸 Instruments: DX7 Rhodes, slap bass, analog synth, drum machine\n"
            f"🎙️ Vocal Style: Sweet and smooth female lead vocals, jazzy backing harmonies\n"
            f"✨ Theme: {keyword} 감성 시티팝 음악\n\n"
            f"{hashtags}"
        )

        # 키워드별 맞춤형 SEO 태그 (LUNA, 루나, AI LUNA 제거)
        base_tags = [
            "시티팝", "city pop", "citypop", "드라이브 음악",
        ]
        if keyword.lower() not in [t.lower() for t in base_tags]:
            base_tags.append(keyword.lower())
            
        if "espresso" in kw_lower or "morning" in kw_lower:
            base_tags.extend(["모닝 시티팝", "아침 BGM", "coffee bgm"])
        elif "rose" in kw_lower or "skincare" in kw_lower or "dewy" in kw_lower:
            base_tags.extend(["릴랙싱 음악", "힐링 시티팝", "dreamy city pop"])
        elif "perfume" in kw_lower or "shibuya" in kw_lower or "midnight" in kw_lower or "neon" in kw_lower:
            base_tags.extend(["심야 드라이브", "밤드라이브 BGM", "night drive"])
        elif "chocolate" in kw_lower or "sweet" in kw_lower:
            base_tags.extend(["로맨틱 시티팝", "sweet pop", "romantic bgm"])
        elif "water" in kw_lower or "glacial" in kw_lower or "beach" in kw_lower or "disco" in kw_lower:
            base_tags.extend(["여름 시티팝", "summer city pop", "disco synth"])
        elif "spring" in kw_lower or "cherry" in kw_lower or "bloom" in kw_lower or "봄" in kw_lower:
            base_tags.extend(["봄 시티팝", "봄 음악", "spring drive"])
        else:
            clean_word = keyword.replace("Retro ", "").replace("Japanese ", "").replace("City Pop", "").strip()
            if clean_word:
                base_tags.extend([clean_word.lower(), f"{clean_word.lower()} 음악"])
            base_tags.extend(["밤드라이브 BGM", "작업용 BGM"])
            
        # 태그 중복 제거 후 최대 10개로 제한
        tags = list(dict.fromkeys([t.strip() for t in base_tags if t.strip()]))[:10]

        return {"description": description, "tags": tags}



if __name__ == "__main__":
    import io
    import sys
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
    analyzer = TrendAnalyzer()
    theme = analyzer.select_best_theme()
    print("선정된 테마 정보:")
    print(json.dumps(theme, indent=4, ensure_ascii=False))


