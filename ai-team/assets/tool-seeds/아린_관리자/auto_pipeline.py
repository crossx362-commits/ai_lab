import os
import sys
import base64
import json
import urllib.parse
import xml.etree.ElementTree as ET
import requests
import time
import random
# 아린 폴더 + 프로젝트 루트 모두 path에 추가
_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, ".agent")):
        break
    _root = os.path.dirname(_root)
if _here not in sys.path:
    sys.path.insert(0, _here)
if _root not in sys.path:
    sys.path.insert(0, _root)
from uploader import InstaUploader, load_env, ensure_token_fresh
from _shared.telegram_notifier import send_telegram_message
from prompt_crafter import craft_insta_prompt
import importlib.util as _ilu
_gahee_spec = _ilu.spec_from_file_location("content_inspector",
    os.path.join(os.path.dirname(__file__), "..", "가희_검수관", "content_inspector.py"))
_gahee = _ilu.module_from_spec(_gahee_spec)
_gahee_spec.loader.exec_module(_gahee)
gahee_inspect_caption     = _gahee.inspect_caption
gahee_inspect_post_upload = _gahee.inspect_post_upload
import subprocess
import datetime

# 캡션 금지 키워드 및 문구
_BANNED_PHRASES = [
    "AI 생성 이미지", "ai 생성", "인공지능이 만든", "인공지능으로 만든",
    "미래를 미리", "미래의 변화", "체험해보세요", "경험해보세요",
    "오늘의 AI", "오늘의 인공지능",
]
_BANNED_TOPICS = ["미래", "인공지능", "ai", "기계", "테크", "로봇", "첨단기술", "4차산업", "딥러닝", "머신러닝"]


def _has_banned_content(text: str) -> bool:
    """캡션에 금지 문구/주제 포함 여부 확인."""
    lower = text.lower()
    return any(p.lower() in lower for p in _BANNED_PHRASES + _BANNED_TOPICS)


def _clean_caption(caption: str) -> str | None:
    """금지 문구가 있으면 None 반환 (재생성 필요 신호)."""
    if _has_banned_content(caption):
        return None
    return caption


def _record_to_history(record: dict):
    """통합 에이전트 메모리(.agent/memory/upload_history.json)에 레코드 추가."""
    # sys.path[0]에 이미 프로젝트 루트가 있음 (_shared import 시 삽입됨)
    root = _root
    mem_path = os.path.join(root, ".agent", "memory", "upload_history.json")
    try:
        history = json.load(open(mem_path, "r", encoding="utf-8")) if os.path.exists(mem_path) else []
        history.append(record)
        with open(mem_path, "w", encoding="utf-8") as f:
            json.dump(history, f, indent=4, ensure_ascii=False)
    except Exception as e:
        print(f"  [Warning] 히스토리 기록 실패: {e}")

# ─── Gemini API 설정 ───────────────────────────────────────────────
GEMINI_API_KEY = os.getenv("GEMINI_API_KEY", "")
GEMINI_IMAGE_MODEL = "gemini-2.5-flash-image"
GEMINI_IMAGE_URL = (
    f"https://generativelanguage.googleapis.com/v1beta/models/"
    f"{GEMINI_IMAGE_MODEL}:generateContent?key={{key}}"
)

def get_trends():
    """KR·US·JP 구글 트렌드 + 카테고리 큐레이션으로 20개+ 후보 수집."""
    print("🔍 트렌드 수집 중 (KR·US·JP + 카테고리)...")
    all_trends: list[str] = []
    headers = {"User-Agent": "Mozilla/5.0"}

    for geo in ["KR", "US", "JP"]:
        try:
            res = requests.get(
                f"https://trends.google.com/trending/rss?geo={geo}",
                headers=headers, timeout=10
            )
            if res.status_code == 200:
                root_el = ET.fromstring(res.text)
                for item in root_el.findall(".//item"):
                    t = item.find("title")
                    if t is not None and t.text:
                        all_trends.append(t.text)
        except Exception as e:
            print(f"  ⚠️ {geo} 트렌드 로드 실패: {e}")

    # 카테고리 큐레이션 — 시각적으로 풍부한 주제 풀
    curated = [
        # 자연·계절
        "여름 해변 황혼 노을", "이슬 맺힌 새벽 숲속", "장마 후 청명한 하늘",
        "산 정상 구름바다 일출", "논밭 물안개 이른 아침",
        # 도시·라이프스타일
        "도심 카페 창가 빗소리", "골목길 벽화 빛 반사", "야경 네온 거리 산책",
        "루프탑 야외 다이닝", "시장 골목 로컬 음식",
        # 감성·여행
        "제주 오름 풍경 초록", "경주 고궁 야간 조명", "부산 감천문화마을",
        "유럽 꽃골목 봄 여행", "일본 후지산 벚꽃",
        # 음식·소품
        "수박 빙수 여름 디저트", "티라미수 카페 분위기", "와인 한 잔 저녁 감성",
        # 동물·인물
        "강아지 해변 뛰어다니기", "고양이 창문 햇살", "아이 꽃밭 뛰어노는 순간",
    ]
    all_trends.extend(curated)

    # 중복 제거
    seen: set = set()
    unique = []
    for t in all_trends:
        if t not in seen:
            seen.add(t)
            unique.append(t)

    if unique:
        print(f"✅ 트렌드 수집 완료: 총 {len(unique)}개 후보")
        return unique

    # 최후 fallback
    return ["여름 해변 황혼 노을", "도심 카페 창가 빗소리", "강아지 해변 뛰어다니기"]


def _extract_visual_keywords(trends: list[str]) -> dict:
    """상위 트렌드에서 시각적 키워드 추출 — Ollama 우선, 실패 시 규칙 기반."""
    from _shared.ollama_client import chat as lm_chat, is_available as lm_available
    sample = "\n".join(f"- {t}" for t in trends[:12])

    if lm_available():
        prompt = (
            f"아래 인스타그램 트렌드 목록에서 시각적으로 가장 풍부한 키워드를 추출해줘.\n\n"
            f"트렌드:\n{sample}\n\n"
            "결과를 JSON으로만 반환:\n"
            '{"mood":"감성 형용사 2개","scene":"장면/배경 묘사","color":"주요 색감 2개",'
            '"subject":"주요 피사체","style":"사진 스타일","season":"계절/시간대","topic":"선정된 트렌드 1개"}'
        )
        try:
            raw = lm_chat(prompt, max_tokens=200, temperature=0.5, json_mode=True)
            if raw:
                return json.loads(raw.strip())
        except Exception:
            pass

    # 규칙 기반 폴백
    topic = trends[0] if trends else "여름 감성"
    return {
        "mood": "몽환적, 감성적", "scene": topic, "color": "골든, 소프트",
        "subject": "자연 풍경", "style": "DSLR 사진", "season": "여름", "topic": topic
    }


def _generate_full_content(selected_trend: str, keywords: dict) -> dict:
    """키워드 기반으로 이미지 프롬프트·제목·디스크립션·태그 한 번에 생성."""
    from _shared.ollama_client import chat as lm_chat, is_available as lm_available
    optimal_time = analyze_optimal_time(selected_trend)

    mood    = keywords.get("mood", "감성적")
    scene   = keywords.get("scene", selected_trend)
    color   = keywords.get("color", "따뜻한")
    subject = keywords.get("subject", "자연")
    style   = keywords.get("style", "DSLR")
    season  = keywords.get("season", "여름")

    if lm_available():
        prompt = (
            f"트렌드: {selected_trend} / 무드: {mood} / 배경: {scene} / 계절: {season}\n\n"
            "위 내용으로 인스타 포스팅용 JSON을 만들어줘.\n"
            "image_prompt: 영어, Gemini Imagen용, DSLR 스타일, 150자 이내.\n"
            "title+description: 진짜 사람이 폰으로 찍고 올린 것처럼 짧고 자연스러운 한국어. "
            "딱딱한 문어체 금지, 친구한테 말하듯. 이모지 1개. AI·기술·인공지능 단어 금지.\n"
            "hashtags: 한국어 8개 배열. AI·기술·미래 금지.\n"
            "JSON만 반환:\n"
            '{"image_prompt":"...","title":"...","description":"...","hashtags":["#..."]}'
        )
        try:
            raw = lm_chat(prompt, max_tokens=400, temperature=0.7, json_mode=True)
            if raw:
                data = json.loads(raw.strip())
                caption = (
                    f"{data.get('title','')}\n\n"
                    f"{data.get('description','')}\n\n"
                    + " ".join(data.get("hashtags", []))
                )
                return {
                    "image_prompt": data.get("image_prompt", scene),
                    "caption":      caption,
                    "best_time":    optimal_time,
                }
        except Exception as e:
            print(f"  ⚠️ 콘텐츠 생성 실패 ({e}), 템플릿 사용")

    # 템플릿 폴백
    caption = (
        f"{mood.split(',')[0]} 한 순간 🌿\n\n"
        f"{scene}의 {color} 빛이 마음을 물들입니다.\n"
        f"오늘 하루도 이런 풍경 하나 마음에 담아요.\n\n"
        f"#감성 #{subject.replace(' ','')} #힐링 #자연 #일상 #풍경 #감성사진 #사진스타그램"
    )
    return {
        "image_prompt": (
            f"photorealistic DSLR photo, {style}, {scene}, {color} tones, "
            f"{mood} atmosphere, {season}, soft bokeh, high detail, 8K"
        ),
        "caption":   caption,
        "best_time": optimal_time,
    }

def fetch_instagram_hacks():
    return "1. Start with an ultra-strong scroll-stopping hook. 2. Ask a question at the end to double comments. 3. Use 5-7 highly relevant niche hashtags."

def analyze_optimal_time(trend_topic):
    """Calculates the absolute optimal time to post based on weekday and content style."""
    now = datetime.datetime.now()
    weekday = now.weekday()
    if weekday >= 5: # Saturday or Sunday
        times = ["13:45", "14:30", "15:15", "16:00"]
    else:
        times = ["11:30", "12:15", "18:00", "18:45", "19:15"]
    
    base_time = random.choice(times)
    hour, minute = map(int, base_time.split(":"))
    minute = (minute + random.randint(-5, 5)) % 60
    optimal_time = f"{hour:02d}:{minute:02d}"
    print(f"⏰ 오늘 트렌드 '{trend_topic}'의 알고리즘 최적 업로드 분석 시간: {optimal_time}")
    return optimal_time

def update_ics_calendar(trend_topic, post_date, post_time):
    """Appends or creates a daily post event to the instagram_posting_schedule.ics file."""
    ics_path = "instagram_posting_schedule.ics"
    event_uid = f"arin-insta-post-{int(time.time())}@auto.uploader"
    
    formatted_start = f"{post_date}T{post_time.replace(':', '')}00"
    # End event 1 hour later
    hour_val, min_val = map(int, post_time.split(":"))
    formatted_end = f"{post_date}T{(hour_val + 1):02d}{min_val:02d}00"
    
    event_block = (
        "BEGIN:VEVENT\n"
        f"UID:{event_uid}\n"
        f"DTSTAMP:{datetime.datetime.utcnow().strftime('%Y%m%dT%H%M%SZ')}\n"
        f"DTSTART;TZID=Asia/Seoul:{formatted_start}\n"
        f"DTEND;TZID=Asia/Seoul:{formatted_end}\n"
        f"SUMMARY:아린 인스타 자동 포스팅 - {trend_topic}\n"
        f"DESCRIPTION:알고리즘 최적화 본문 및 AI 아트워크 자동 업로드 시간 ({post_time})\\n트렌드: {trend_topic}\n"
        "STATUS:CONFIRMED\n"
        "SEQUENCE:0\n"
        "END:VEVENT\n"
    )
    
    if os.path.exists(ics_path):
        try:
            with open(ics_path, "r", encoding="utf-8") as f:
                lines = f.read()
            if "END:VCALENDAR" in lines:
                new_content = lines.replace("END:VCALENDAR", event_block + "END:VCALENDAR")
                with open(ics_path, "w", encoding="utf-8") as f:
                    f.write(new_content)
                print(f"📅 캘린더에 포스팅 일정이 추가되었습니다! ({post_date} {post_time})")
                return
        except Exception as e:
            print(f"⚠️ 기존 캘린더 파일 로드 실패 ({e}), 신규 생성합니다.")
            
    new_ics = (
        "BEGIN:VCALENDAR\n"
        "VERSION:2.0\n"
        "PRODID:-//Arin Auto Uploader//Instagram Posting Schedule//KO\n"
        "CALSCALE:GREGORIAN\n"
        "METHOD:PUBLISH\n"
        + event_block +
        "END:VCALENDAR\n"
    )
    with open(ics_path, "w", encoding="utf-8") as f:
        f.write(new_ics)
    print(f"📅 새 캘린더 파일이 생성되고 일정이 추가되었습니다! ({post_date} {post_time})")


def _generate_image_gemini(prompt):
    """Calls Gemini 3.1 Flash Image Preview API and returns raw image bytes."""
    api_url = GEMINI_IMAGE_URL.format(key=GEMINI_API_KEY)
    payload = {
        "contents": [
            {
                "parts": [
                    {"text": prompt}
                ]
            }
        ],
        "generationConfig": {
            "responseModalities": ["IMAGE", "TEXT"]
        }
    }
    headers = {"Content-Type": "application/json"}
    res = requests.post(api_url, headers=headers, json=payload, timeout=60)
    res.raise_for_status()
    data = res.json()

    # Extract the first image part from the response
    for candidate in data.get("candidates", []):
        for part in candidate.get("content", {}).get("parts", []):
            if "inlineData" in part:
                img_bytes = base64.b64decode(part["inlineData"]["data"])
                return img_bytes
    raise ValueError(f"Gemini 응답에서 이미지 데이터를 찾지 못했습니다. 응답: {json.dumps(data)[:200]}")


def _generate_image_pollinations(prompt):
    """Pollinations.ai로 이미지 생성 후 bytes 반환 (나노바나나 429 폴백)."""
    seed = random.randint(1, 999999)
    url = (
        f"https://image.pollinations.ai/prompt/{urllib.parse.quote(prompt)}"
        f"?width=1024&height=1024&model=flux&nologo=true&seed={seed}"
    )
    print(f"🌸 Pollinations.ai 폴백으로 이미지 생성 중... (seed={seed})")
    res = requests.get(url, timeout=60)
    res.raise_for_status()
    return res.content


def generate_and_upload_image(prompt):
    """Generates an image using Gemini 3.1 Flash Image Preview and uploads it to Catbox.moe for a static public URL."""
    print(f"🎨 Gemini AI로 이미지 생성 중 (프롬프트: {prompt[:80]}...)")
    temp_filename = "temp_generated.jpg"

    try:
        img_bytes = _generate_image_gemini(prompt)
        with open(temp_filename, "wb") as f:
            f.write(img_bytes)
        print(f"✅ Gemini 이미지 생성 완료! ({len(img_bytes):,} bytes)")

        print("📤 이미지를 퍼블릭 서버에 업로드 중...")
        # Upload to Catbox.moe
        with open(temp_filename, "rb") as f:
            res = requests.post(
                "https://catbox.moe/user/api.php",
                data={"reqtype": "fileupload"},
                files={"fileToUpload": f},
                timeout=15
            )

        if os.path.exists(temp_filename):
            os.remove(temp_filename)

        if res.status_code == 200 and "https://" in res.text:
            public_url = res.text.strip()
            print(f"✅ 이미지 업로드 성공! URL: {public_url}")
            return public_url
        else:
            print(f"⚠️ Catbox 업로드 응답 이상: {res.status_code} / {res.text[:100]}")
    except Exception as e:
        print(f"❌ 이미지 생성 또는 업로드 중 실패: {e}")
        if os.path.exists(temp_filename):
            os.remove(temp_filename)
    return None


def generate_caption_from_image(img_bytes: bytes) -> str:
    """생성된 이미지를 Gemini Vision으로 직접 분석하여
    이미지 내용에 기반한 감성적 한국어 인스타 캡션을 자동 생성합니다."""
    print("👁️ Gemini Vision으로 이미지 분석 및 캡션 작성 중...")
    try:
        api_url = (
            "https://generativelanguage.googleapis.com/v1beta/models/"
            f"gemini-2.5-flash:generateContent?key={GEMINI_API_KEY}"
        )
        img_b64 = base64.b64encode(img_bytes).decode()
        prompt = (
            "이 사진 보고 인스타 캡션 써줘. "
            "진짜 사람이 폰으로 찍고 올린 것처럼 자연스럽게. "
            "딱딱한 문어체 금지, 평소 친구한테 말하듯이. "
            "짧고 감성적으로 1~2문장. 이모지 1개. "
            "마지막에 해시태그 6~8개. "
            "캡션만 출력, 다른 말 없이."
        )
        payload = {
            "contents": [{
                "parts": [
                    {"text": prompt},
                    {"inline_data": {"mime_type": "image/jpeg", "data": img_b64}}
                ]
            }]
        }
        res = requests.post(api_url, json=payload, timeout=30)
        res.raise_for_status()
        caption = res.json()["candidates"][0]["content"]["parts"][0]["text"]
        print("✅ Gemini Vision 캡션 생성 완료!")
        return caption.strip()
    except Exception as e:
        print(f"⚠️ Vision 캡션 실패 ({e}), 템플릿 캡션으로 대체합니다.")
        return None


def git_sync():
    print("📤 Git 동기화 진행 중...")
    try:
        git_root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
        subprocess.run(["git", "add", "."], cwd=git_root, check=True)
        status = subprocess.run(["git", "status", "--porcelain"], cwd=git_root, capture_output=True, text=True)
        if status.stdout.strip():
            timestamp = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
            subprocess.run(["git", "commit", "-m", f"Auto-sync: Arin pipeline executed at {timestamp}"], cwd=git_root, check=True)
            subprocess.run(["git", "push", "origin", "main"], cwd=git_root, check=True)
            print("✅ Git 동기화 완료!")
            send_telegram_message("💾 [Git 동기화 완료] 아린 에이전트 소스 및 설정이 GitHub에 백업되었습니다.")
        else:
            print("ℹ️ 변경된 파일이 없어 Git 동기화를 건너뜁니다.")
    except Exception as e:
        print(f"❌ Git 동기화 실패: {e}")
        send_telegram_message(f"⚠️ [Git 동기화 실패] 오류 발생: {e}")

# ── 중복 감지 — 가희 duplicate_guard 위임 ─────────────────────────────────────
from _shared.duplicate_guard import (
    get_used_insta_trends     as _guard_trends,
    get_recent_insta_prompts  as _guard_prompts,
    get_recent_insta_captions as _guard_captions,
    is_caption_duplicate      as _guard_cap_dup,
    is_hashtag_duplicate      as _guard_tag_dup,
)

def _get_recent_trend_topics(days: int = 7) -> set:
    return _guard_trends(days)

def _get_recent_image_prompts(days: int = 14) -> list:
    return _guard_prompts(days)

def _is_similar_prompt(new_prompt: str, recent_prompts: list, threshold: float = 0.60) -> tuple:
    import difflib
    pool = recent_prompts if recent_prompts else _guard_prompts(days=14)
    nl, max_ratio, matched = new_prompt.lower(), 0.0, ""
    for prev in pool:
        r = difflib.SequenceMatcher(None, nl, prev.lower()).ratio()
        if r > max_ratio:
            max_ratio, matched = r, prev
    return max_ratio >= threshold, max_ratio, matched

def _get_recent_captions(days: int = 14) -> list:
    return _guard_captions(days)

def _is_caption_duplicate(new_caption: str, threshold: float = 0.70) -> tuple:
    return _guard_cap_dup(new_caption, threshold=threshold)

def _is_hashtag_duplicate(new_caption: str, overlap_threshold: float = 0.80) -> tuple:
    return _guard_tag_dup(new_caption, overlap_threshold=overlap_threshold)


def main(dry_run=False):
    load_env()

    # 토큰 만료 확인 및 자동 갱신 (코다리 의존 없이 자체 처리)
    access_token = ensure_token_fresh()
    account_id   = os.getenv("INSTAGRAM_ACCOUNT_ID")

    if not account_id or not access_token:
        print("❌ 에러: .env 파일에서 계정 정보를 읽을 수 없습니다.")
        sys.exit(1)

    # send_telegram_message("🌸 아린 인스타: 금일 포스팅 제작 및 자동 업로드 파이프라인을 기동합니다.")  # 중복 방지: telegram_bot.py에서 전송

    # 1. 트렌드 수집 (KR·US·JP + 카테고리 큐레이션)
    trends = get_trends()

    # 최근 7일 사용 + 금지 키워드 제외
    used_topics = _get_recent_trend_topics(days=7)
    fresh_trends = [t for t in trends if t not in used_topics and not _has_banned_content(t)]
    if not fresh_trends:
        fresh_trends = [t for t in trends if not _has_banned_content(t)]
    if not fresh_trends:
        fresh_trends = trends[:1]
    selected_trend = fresh_trends[0]
    print(f"✅ 선택된 트렌드: {selected_trend}")

    # 2. 상위 트렌드에서 시각적 키워드 추출
    print("🔑 키워드 추출 중...")
    keywords = _extract_visual_keywords(fresh_trends[:12])
    keywords["topic"] = selected_trend
    print(f"  키워드: {keywords}")

    # 3. 키워드 기반 이미지 프롬프트·제목·디스크립션·태그 생성
    post_data = _generate_full_content(selected_trend, keywords)
    
    print("\n========================================")
    print("📋 생성된 포스팅 기획 정보")
    print(f"▪️ 타겟 트렌드: {selected_trend}")
    print(f"▪️ 최적 업로드 타임: {post_data['best_time']}")
    print(f"▪️ AI 이미지 프롬프트: {post_data['image_prompt']}")
    print(f"▪️ 피드 본문:\n{post_data['caption']}")
    print("========================================\n")
    
    # Update Calendar (.ics)
    current_date = datetime.datetime.now().strftime("%Y%m%d")
    update_ics_calendar(selected_trend, current_date, post_data['best_time'])
    
    if dry_run:
        print("🧪 드라이 런 모드입니다. 실제 업로드는 수행하지 않고 종료합니다.")
        # 드라이런인 경우라도 변경된 calendar 파일 저장을 위해 git_sync를 수행
        git_sync()
        return
        
    # 3. 프롬프트 크래프터로 Gemini 최적화 프롬프트 고도화
    raw_prompt = post_data['image_prompt']
    crafted_prompt = craft_insta_prompt(raw_prompt)
    print(f"🎯 크래프팅된 최종 프롬프트:\n{crafted_prompt[:200]}...\n")

    # 🔍 유사 이미지 검수: 최근 14일 프롬프트와 비교
    recent_prompts = _get_recent_image_prompts(days=14)
    is_similar, ratio, matched = _is_similar_prompt(crafted_prompt, recent_prompts, threshold=0.60)
    if is_similar:
        print(f"⚠️ 유사 이미지 감지 (유사도 {ratio:.0%}) — 프롬프트를 변형합니다.")
        print(f"   기존 유사 프롬프트: {matched[:80]}...")
        # 프롬프트 다양성 강제 변형: 계절·시간대·구도 접두어 순환
        import hashlib
        variation_seeds = [
            "Aerial bird's-eye view, ", "Close-up macro shot, ",
            "Twilight blue hour, ", "Foggy misty morning, ",
            "Vivid summer midday, ", "Snowy winter scene, ",
        ]
        idx = int(hashlib.md5(crafted_prompt.encode()).hexdigest(), 16) % len(variation_seeds)
        crafted_prompt = variation_seeds[idx] + crafted_prompt
        print(f"   변형된 프롬프트: {crafted_prompt[:120]}...")
    else:
        print(f"✅ 유사 이미지 없음 (최고 유사도 {ratio:.0%}) — 진행합니다.")

    # 4. 이미지 생성 (img_bytes 보관 — Vision 캡션에 재사용)
    temp_filename = "temp_generated.jpg"
    img_bytes = None
    image_url = None
    try:
        try:
            img_bytes = _generate_image_gemini(crafted_prompt)
            print(f"✅ 나노바나나 이미지 생성 완료! ({len(img_bytes):,} bytes)")
        except Exception as gemini_err:
            print(f"⚠️ 나노바나나 실패 ({gemini_err}), Pollinations.ai 폴백 시도...")
            img_bytes = _generate_image_pollinations(crafted_prompt)
            print(f"✅ Pollinations.ai 이미지 생성 완료! ({len(img_bytes):,} bytes)")

        with open(temp_filename, "wb") as f:
            f.write(img_bytes)
        print("📤 이미지를 퍼블릭 서버에 업로드 중...")
        with open(temp_filename, "rb") as f:
            catbox_res = requests.post(
                "https://catbox.moe/user/api.php",
                data={"reqtype": "fileupload"},
                files={"fileToUpload": f},
                timeout=15
            )
        if os.path.exists(temp_filename):
            os.remove(temp_filename)
        if catbox_res.status_code == 200 and "https://" in catbox_res.text:
            image_url = catbox_res.text.strip()
            print(f"✅ 이미지 업로드 성공! URL: {image_url}")
        else:
            print(f"⚠️ Catbox 업로드 실패: {catbox_res.text[:100]}")
            send_telegram_message(f"❌ 아린 인스타: 이미지 호스팅 실패")
            sys.exit(1)
    except Exception as e:
        print(f"❌ 이미지 생성/업로드 실패: {e}")
        if os.path.exists(temp_filename):
            os.remove(temp_filename)
        send_telegram_message(f"❌ 아린 인스타: 이미지 실패 - {e}")
        sys.exit(1)

    # 5. Gemini Vision으로 이미지 보고 캡션 작성
    vision_caption = generate_caption_from_image(img_bytes) if img_bytes else None
    # 금지 문구 포함 시 템플릿 캡션으로 대체
    if vision_caption and _has_banned_content(vision_caption):
        print("⚠️ Vision 캡션에 금지 문구 감지 — 템플릿 캡션으로 대체합니다.")
        vision_caption = None
    final_caption = vision_caption if vision_caption else post_data["caption"]

    # ── 캡션 중복 체크 ──────────────────────────────────────────────────────
    cap_dup, cap_ratio = _is_caption_duplicate(final_caption)
    if cap_dup:
        print(f"⚠️ 캡션 유사도 {cap_ratio:.0%} 감지 → Ollama로 새 캡션 생성")
        from _shared.ollama_client import chat as lm_chat, is_available as lm_available
        if lm_available():
            regen = lm_chat(
                f"다음 인스타 캡션을 다른 표현으로 바꿔줘. "
                f"진짜 사람이 쓴 것처럼 짧고 자연스럽게, 친구한테 말하듯. "
                f"이모지·해시태그 유지. 캡션만 출력:\n{final_caption}",
                max_tokens=300, temperature=0.9
            )
            if regen:
                final_caption = regen.strip()
                print(f"  ✅ 캡션 재생성 완료")
    else:
        print(f"✅ 캡션 중복 없음 (최고 유사도 {cap_ratio:.0%})")

    # ── 해시태그 중복 체크 ──────────────────────────────────────────────────
    tag_dup, tag_overlap = _is_hashtag_duplicate(final_caption)
    if tag_dup:
        print(f"⚠️ 해시태그 {tag_overlap:.0%} 겹침 감지 → 일부 태그 교체")
        import re, hashlib
        alt_tags = ["#감성사진", "#일상공유", "#데일리", "#분위기", "#힐링",
                    "#소확행", "#좋아요", "#팔로우", "#인스타그램", "#사진스타그램"]
        existing = re.findall(r"#\w+", final_caption)
        # 마지막 2개 태그를 대안 태그로 교체
        for i, old_tag in enumerate(existing[-2:]):
            new_tag = alt_tags[(int(hashlib.md5(old_tag.encode()).hexdigest(), 16) + i + 1) % len(alt_tags)]
            final_caption = final_caption.replace(old_tag, new_tag, 1)
        print(f"  ✅ 해시태그 일부 교체 완료")
    else:
        print(f"✅ 해시태그 중복 없음 (겹침 {tag_overlap:.0%})")

    print(f"\n📝 최종 캡션:\n{final_caption}\n")

    # 6. 가희 검수 + 업로드 루프 (사전→업로드→사후, 실패 시 수정 재업)
    from _shared.ollama_client import chat as lm_chat, is_available as lm_available

    def _fix_caption_ollama(bad_caption: str, issues: list) -> str:
        """가희 지적 사항을 Ollama로 수정한 새 캡션 반환."""
        if not lm_available():
            return bad_caption
        issues_str = ", ".join(issues)
        prompt = (
            f"이 인스타 캡션 고쳐줘. 문제: {issues_str}\n"
            f"원본: {bad_caption}\n\n"
            "진짜 사람이 쓴 것처럼 짧고 자연스럽게. AI·인공지능·미래·테크 금지. "
            "해시태그 5~8개. 캡션만 출력."
        )
        result = lm_chat(prompt, task="", max_tokens=300, temperature=0.9)
        return result.strip() if (result and result.strip()) else bad_caption

    uploader = InstaUploader(account_id, access_token)
    post_id = None
    MAX_RETRIES = 3

    for attempt in range(1, MAX_RETRIES + 1):
        # 가희 사전 검수
        pre_check = gahee_inspect_caption(final_caption)
        if not pre_check["pass"]:
            issues_str = ", ".join(pre_check["issues"])
            print(f"🚨 [가희] 사전 검수 실패 (시도 {attempt}): {issues_str}")
            if attempt == MAX_RETRIES:
                send_telegram_message(f"🚨 <b>[가희]</b> 사전 검수 {MAX_RETRIES}회 실패 — 업로드 중단\n{issues_str}")
                return
            print(f"  ✍️  Ollama로 캡션 수정 중...")
            final_caption = _fix_caption_ollama(final_caption, pre_check["issues"])
            print(f"  수정된 캡션 (앞 80자): {final_caption[:80]}")
            continue
        print(f"  ✅ [가희] 사전 검수 통과 (시도 {attempt})")

        # 업로드
        print(f"  📤 업로드 중 (시도 {attempt})...")
        post_id = uploader.upload_image(image_url, final_caption)
        if not post_id:
            print(f"  ❌ 업로드 실패")
            break

        # 가희 사후 검수
        post_check = gahee_inspect_post_upload(post_id)
        if not post_check["pass"]:
            issues_str = ", ".join(post_check["issues"])
            print(f"⚠️ [가희] 사후 검수 이상 (시도 {attempt}): {issues_str}")
            send_telegram_message(f"⚠️ <b>[가희]</b> 사후 검수 이상 (시도 {attempt})\nID: {post_id}\n사유: {issues_str}")
            if attempt < MAX_RETRIES:
                # 삭제 후 캡션 수정해서 재업
                token_env = os.getenv("INSTAGRAM_ACCESS_TOKEN", "")
                del_r = requests.delete(f"https://graph.instagram.com/v23.0/{post_id}",
                                    params={"access_token": token_env}, timeout=10).json()
                if "error" not in del_r:
                    print(f"  🗑️  기존 포스팅 삭제 완료")
                else:
                    print(f"  ⚠️  삭제 불가 (API 제한) — 수정 캡션으로 재업")
                final_caption = _fix_caption_ollama(final_caption, post_check["issues"])
                post_id = None
                continue
            else:
                send_telegram_message(f"🚨 <b>[가희]</b> 사후 검수 {MAX_RETRIES}회 실패 — 수동 확인 필요\nID: {post_id}")
        else:
            print(f"  ✅ [가희] 사후 검수 통과")
        break  # 검수 통과 or 최대 재시도 도달

    # 업로드 성공 후 처리 (루프 밖)
    if post_id:
        print(f"🎉 성공적으로 자동 포스팅이 완료되었습니다! (ID: {post_id})")
        # send_telegram_message(f"✅ 아린 인스타: 금일 자동 포스팅 발행 완료!\n- 트렌드 주제: {selected_trend}\n- 업로드 타임: {post_data['best_time']}\n- 포스팅 ID: {post_id}\n- 이미지: {image_url}")  # 중복 방지: telegram_bot.py에서 전송
        _record_to_history({
            "agent": "아린",
            "status": "published",
            "uploaded_at": datetime.datetime.now().isoformat(),
            "metadata": {
                "platform": "instagram",
                "post_id": post_id,
                "caption": final_caption,
                "image_url": image_url,
                "trend_topic": selected_trend,
                "image_prompt": crafted_prompt,
            },
        })
    else:
        print("❌ 자동 포스팅에 실패했습니다.")
        # send_telegram_message(f"❌ 아린 인스타: 인스타그램 업로드 API 오류로 포스팅 실패\n- 트렌드 주제: {selected_trend}")  # 중복 방지: telegram_bot.py에서 전송

    # 깃 동기화
    git_sync()

if __name__ == "__main__":
    is_dry = "--dry-run" in sys.argv
    main(dry_run=is_dry)
