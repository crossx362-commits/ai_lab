import os
import sys
import json
import xml.etree.ElementTree as ET
import requests
import time
import random

# UTF-8 인코딩 설정 (이모지 출력 지원)
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    sys.stderr.reconfigure(encoding='utf-8', errors='replace')
# 아린 폴더 + 프로젝트 루트 모두 path에 추가
_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, "reports")):
        break
    _root = os.path.dirname(_root)
_OUT_DIR = os.path.join(_root, "reports", "uploads", "arin")
os.makedirs(_OUT_DIR, exist_ok=True)
if _here not in sys.path:
    sys.path.insert(0, _here)
if _root not in sys.path:
    sys.path.insert(0, _root)
from uploader import InstaUploader, load_env, ensure_token_fresh
from _shared.telegram_notifier import send_telegram_message
from prompt_crafter import craft_insta_prompt
import importlib.util as _ilu
from analysis import run_analysis_and_deepsearch
from decision import make_decision
# skills/아린_관리자/tools/ → skills/가희_검수관/tools/
_gahee_path = os.path.join(os.path.dirname(__file__), "..", "..", "가희_검수관", "tools", "content_inspector.py")
_gahee_spec = _ilu.spec_from_file_location("content_inspector", _gahee_path)
_gahee = _ilu.module_from_spec(_gahee_spec)
_gahee_spec.loader.exec_module(_gahee)
gahee_inspect_caption     = _gahee.inspect_caption
gahee_inspect_post_upload = _gahee.inspect_post_upload
# 예원 CEO approval (동적 임포트)
import importlib.util as _ilu
_approval_path = os.path.join(os.path.dirname(__file__), "..", "..", "예원_CEO", "approval.py")
_approval_spec = _ilu.spec_from_file_location("approval_yewon", _approval_path)
_approval_mod = _ilu.module_from_spec(_approval_spec)
_approval_spec.loader.exec_module(_approval_mod)
await_approval = _approval_mod.await_approval
ceo_coaching_on_rejection = _approval_mod.ceo_coaching_on_rejection
# 경수 검수관 modules
_kyungsoo_path = os.path.join(os.path.dirname(__file__), "..", "..", "경수_수사관", "tools")
_kyungsoo_spec = _ilu.spec_from_file_location("approval_kyungsoo",
    os.path.join(_kyungsoo_path, "approval_kyungsoo.py"))
_kyungsoo_mod = _ilu.module_from_spec(_kyungsoo_spec)
_kyungsoo_spec.loader.exec_module(_kyungsoo_mod)
await_kyungsoo_approval = _kyungsoo_mod.await_approval
_kyungsoo_ci_spec = _ilu.spec_from_file_location("kyungsoo_content_inspector",
    os.path.join(_kyungsoo_path, "content_inspector.py"))
_kyungsoo_ci_mod = _ilu.module_from_spec(_kyungsoo_ci_spec)
_kyungsoo_ci_spec.loader.exec_module(_kyungsoo_ci_mod)
kyungsoo_inspect_caption = _kyungsoo_ci_mod.inspect_caption
kyungsoo_inspect_youtube  = _kyungsoo_ci_mod.inspect_youtube
import subprocess
import datetime

# 캡션 금지 키워드 및 문구
_BANNED_PHRASES = [
    "AI 생성 이미지", "ai 생성", "인공지능이 만든", "인공지능으로 만든",
    "미래를 미리", "미래의 변화", "체험해보세요", "경험해보세요",
    "오늘의 AI", "오늘의 인공지능",
]
_BANNED_TOPICS = ["미래", "인공지능", "ai", "기계", "테크", "로봇", "첨단기술", "4차산업", "딥러닝", "머신러닝"]
_BANNED_POLITICAL = [
    "이재명", "정치", "선거", "국회", "대통령", "여당", "야당", "민주당", "국민의힘",
    "정당", "투표", "정권", "탄핵", "집회", "시위", "정부", "보수", "진보", "좌파", "우파",
]


def _has_banned_content(text: str) -> bool:
    """캡션에 금지 문구/주제 포함 여부 확인."""
    lower = text.lower()
    return any(p.lower() in lower for p in _BANNED_PHRASES + _BANNED_TOPICS + _BANNED_POLITICAL)


def _clean_caption(caption: str) -> str | None:
    """금지 문구가 있으면 None 반환 (재생성 필요 신호)."""
    if _has_banned_content(caption):
        return None
    return caption


def _record_to_history(record: dict):
    """통합 에이전트 메모리(reports/history/upload_history.json)에 레코드 추가."""
    # sys.path[0]에 이미 프로젝트 루트가 있음 (_shared import 시 삽입됨)
    root = _root
    mem_path = os.path.join(root, "reports", "history", "upload_history.json")
    try:
        history = json.load(open(mem_path, "r", encoding="utf-8")) if os.path.exists(mem_path) else []
        history.append(record)
        with open(mem_path, "w", encoding="utf-8") as f:
            json.dump(history, f, indent=4, ensure_ascii=False)
    except Exception as e:
        print(f"  [Warning] 히스토리 기록 실패: {e}")

GEMINI_API_KEY = os.getenv("GEMINI_API_KEY", "")
# 나노바나나2 = Imagen 4.0 generate-001 (최신 고품질)
GEMINI_IMAGE_MODEL = "imagen-4.0-generate-001"

def _generate_image_gemini(prompt) -> bytes | None:
    """나노바나나2 (Imagen 3.0 generate-002) API 호출 - 실사풍 고퀄리티."""
    if not GEMINI_API_KEY:
        print("  ⚠️ GEMINI_API_KEY 미설정 — Imagen 건너뜁니다.")
        return None

    enhanced_prompt = (
        f"{prompt}, "
        "photorealistic, ultra high quality, professional photography, "
        "DSLR camera, sharp focus, natural lighting, cinematic composition, "
        "8K resolution, detailed textures, hyperrealistic, "
        "award-winning photography style"
    )

    payload = {
        "instances": [{"prompt": enhanced_prompt}],
        "parameters": {
            "sampleCount": 1,
            "aspectRatio": "1:1",
            "safetyFilterLevel": "block_some",
            "personGeneration": "allow_adult"
        }
    }
    headers = {"Content-Type": "application/json"}

    # v1beta → v1 순서로 시도
    for api_ver in ("v1beta", "v1"):
        url = (
            f"https://generativelanguage.googleapis.com/{api_ver}/models/"
            f"{GEMINI_IMAGE_MODEL}:predict?key={GEMINI_API_KEY}"
        )
        try:
            print(f"🍋 나노바나나2 ({GEMINI_IMAGE_MODEL}, {api_ver}) 호출 중...")
            res = requests.post(url, headers=headers,
                                data=json.dumps(payload), timeout=60)
            if res.status_code == 200:
                import base64
                predictions = res.json().get("predictions", [])
                if predictions:
                    b64 = predictions[0].get("bytesBase64Encoded", "")
                    if b64:
                        return base64.b64decode(b64)
                print(f"  ⚠️ Imagen 응답 형식 이상: {res.text[:100]}")
                return None
            elif res.status_code == 404:
                print(f"  ⚠️ {api_ver} 404 — 다음 버전 시도...")
                continue
            else:
                print(f"  ⚠️ Imagen API 에러 ({api_ver}, {res.status_code}): {res.text[:150]}")
                return None
        except Exception as e:
            print(f"  ❌ Imagen 예외 ({api_ver}): {e}")

    return None


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
            "  ⚠️ 동일 단어(예: 감성, 오늘, 하루 등 2글자 이상의 의미 있는 단어)를 본문 내에 2회 이상 중복해서 반복 사용하는 것을 엄격히 금지합니다.\n"
            "hashtags: 한국어 8개 배열. AI·기술·미래 금지.\n"
            "JSON만 반환:\n"
            '{"image_prompt":"...","title":"...","description":"...","hashtags":["#..."]}'
        )
        for attempt in range(3):
            try:
                raw = lm_chat(prompt, max_tokens=400, temperature=0.7, json_mode=True)
                if raw:
                    data = json.loads(raw.strip())
                    title = data.get('title','')
                    desc = data.get('description','')
                    full_text = f"{title} {desc}"
                    import re
                    words = re.findall(r'[a-zA-Z가-힣]{2,}', full_text.lower())
                    stop_words = {"있는", "합니다", "한다", "그리고", "에서", "으로", "이다", "하고", "했다", "하는", "추천"}
                    counts = {}
                    repeated = []
                    for w in words:
                        if w in stop_words:
                            continue
                        counts[w] = counts.get(w, 0) + 1
                        if counts[w] > 1:
                            repeated.append(w)
                    if not repeated:
                        caption = (
                            f"{title}\n\n"
                            f"{desc}\n\n"
                            + " ".join(data.get("hashtags", []))
                        )
                        return {
                            "image_prompt": data.get("image_prompt", scene),
                            "caption":      caption,
                            "best_time":    optimal_time,
                        }
                    else:
                        print(f"  ⚠️ [중복 단어 발견] 재생성 시도 {attempt+1}: {list(set(repeated))}")
                        prompt += f"\n⚠️ 피드백: 이전 문장에서 {list(set(repeated))} 단어들이 2회 이상 중복되어 나타났습니다. 이 단어들의 반복 사용을 피하고 다른 다채로운 표현으로 대체해 주세요."
            except Exception as e:
                print(f"  ⚠️ 콘텐츠 생성 시도 실패: {e}")

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

def _generate_image_pollinations(prompt):
    """Stable Diffusion via Hugging Face로 이미지 생성 (Pollinations 폴백 대체)."""
    # 1순위: Hugging Face Inference API (stabilityai/stable-diffusion-2-1)
    hf_token = os.getenv("HF_API_TOKEN", "")
    if hf_token:
        try:
            hf_url = "https://api-inference.huggingface.co/models/stabilityai/stable-diffusion-2-1"
            print(f"\U0001f338 HuggingFace SD2.1 이미지 생성 중...")
            res = requests.post(
                hf_url,
                headers={"Authorization": f"Bearer {hf_token}"},
                json={"inputs": prompt[:500]},
                timeout=90
            )
            if res.status_code == 200 and res.content:
                return res.content
            print(f"  ⚠️ HuggingFace 응답 이상: {res.status_code}")
        except Exception as e:
            print(f"  ⚠️ HuggingFace 실패: {e}")

    # 2순위: Picsum (placeholder 이미지 실외 없음) → Lorem Picsum 1024x1024 랜덤 고화질
    seed = random.randint(1, 999)
    try:
        fallback_url = f"https://picsum.photos/seed/{seed}/1024/1024"
        print(f"\U0001f338 Picsum 폴백 이미지 사용 (seed={seed})...")
        res = requests.get(fallback_url, timeout=30)
        res.raise_for_status()
        return res.content
    except Exception as e:
        raise RuntimeError(f"이미지 생성 전체 실패: {e}")

def update_ics_calendar(trend_topic, post_date, post_time):
    """Appends or creates a daily post event to the instagram_posting_schedule.ics file."""
    # 절대경로 고정: 실행 위치에 무관하게 항상 tools/ 폴더에 저장
    ics_path = os.path.join(_here, "instagram_posting_schedule.ics")
    event_uid = f"arin-insta-post-{int(time.time())}@auto.uploader"
    
    formatted_start = f"{post_date}T{post_time.replace(':', '')}00"
    # End event 1 hour later
    hour_val, min_val = map(int, post_time.split(":"))
    formatted_end = f"{post_date}T{(hour_val + 1):02d}{min_val:02d}00"
    
    event_block = (
        "BEGIN:VEVENT\n"
        f"UID:{event_uid}\n"
        f"DTSTAMP:{datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ')}\n"
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




def generate_and_upload_image(prompt):
    """Pollinations으로 이미지 생성 후 Catbox.moe 업로드."""
    print(f"🎨 Pollinations으로 이미지 생성 중 (프롬프트: {prompt[:80]}...)")
    temp_filename = os.path.join(_OUT_DIR, "temp_generated.jpg")

    try:
        img_bytes = _generate_image_pollinations(prompt)
        with open(temp_filename, "wb") as f:
            f.write(img_bytes)
        print(f"✅ 이미지 생성 완료! ({len(img_bytes):,} bytes)")

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


def _clean_vision_caption(text: str) -> str | None:
    """Vision 모델이 구조화 포맷으로 응답했을 경우 순수 캡션만 추출."""
    import re
    # "1. 사진 느낌 설명:" 등 번호+제목 포맷 감지
    if re.search(r"^\s*\d+\.", text, re.MULTILINE):
        # 해시태그 줄 추출 후 재조합
        hashtags = " ".join(re.findall(r"#\S+", text))
        # 첫 번째 설명 문장 추출 (번호/레이블 제거)
        lines = [l.strip() for l in text.splitlines() if l.strip()]
        desc_lines = [re.sub(r"^\d+\.\s*[^:：]+[:：]\s*", "", l) for l in lines
                      if not re.match(r"^\d+\.", l) and not l.startswith("#")]
        body = " ".join(desc_lines[:2]).strip()
        if not body or not hashtags:
            return None
        # 이모지가 없으면 기본 추가
        emoji = re.search(r"[\U0001F300-\U0001FFFF]", body)
        if not emoji:
            body += " 🌿"
        return f"{body}\n\n{hashtags}"
    return text


def generate_caption_from_image(img_bytes: bytes) -> tuple[str | None, str | None]:
    """이미지 분석 → (캡션, alt_text) 동시 반환. Ollama 우선, 실패 시 Gemini Vision 폴백.
    alt_text: 인스타 Explore 탭 검색 인덱싱용 150자 이내 이미지 묘사 (영어).
    """
    caption_prompt = (
        "아래 사진을 보고 인스타그램용 두 가지를 JSON으로만 반환해줘.\n\n"
        "1. caption: 진짜 사람이 폰으로 찍어 올린 것처럼 자연스러운 한국어 캡션.\n"
        "   - 1~2문장 + 이모지 1개 + 마지막 줄 해시태그 6~8개\n"
        "   - 번호/레이블/설명 형식 절대 금지\n"
        "2. alt_text: 인스타그램 검색 인덱싱용 영어 이미지 묘사. 150자 이내, 시각적 요소 중심.\n\n"
        '{"caption": "...", "alt_text": "..."}'
    )
    plain_prompt = (
        "아래 사진을 보고 인스타그램 캡션을 작성해줘.\n"
        "- 진짜 사람이 폰으로 찍어 올린 것처럼 자연스럽고 짧게\n"
        "- 1~2문장 + 이모지 1개 + 마지막 줄 해시태그 6~8개\n"
        "- 번호/레이블 절대 금지. 캡션 텍스트만 출력."
    )

    def _parse_result(raw: str) -> tuple[str | None, str | None]:
        """JSON 또는 plain text 응답에서 caption, alt_text 추출. 실패 시 None 반환."""
        import json as _json, re as _re
        raw = raw.strip()
        # 마크다운 코드블록 제거
        raw = _re.sub(r"^```(?:json)?\s*\n?", "", raw)
        raw = _re.sub(r"\n?```\s*$", "", raw).strip()
        try:
            data = _json.loads(raw)
            if not isinstance(data, dict):
                return None, None
            cap = _clean_vision_caption(data.get("caption", "") or "")
            alt = (data.get("alt_text") or "")[:150] or None
            if not cap or len(cap.strip()) < 10:
                return None, alt
            return cap, alt
        except Exception:
            # JSON 파싱 실패 시 raw 반환하지 않음 — 원본 기획 캡션 사용
            return None, None

    # 1순위: Gemini Vision (JSON 응답 안정적)
    try:
        from _shared.gemini_client import vision as gemini_vision
        print("👁️ Gemini Vision으로 이미지 분석 중...")
        result = gemini_vision(img_bytes, caption_prompt, max_tokens=400)
        if result:
            cap, alt = _parse_result(result)
            if cap:
                print(f"✅ Gemini Vision 완료! alt_text: {'있음' if alt else '없음'}")
                return cap, alt
    except Exception as e:
        print(f"  [Gemini Vision] 실패: {e}")

    # 2순위: Ollama Vision 폴백
    try:
        from _shared.ollama_client import chat_vision, is_available
        if is_available():
            print("👁️ Ollama Vision으로 이미지 분석 중...")
            result = chat_vision(plain_prompt, img_bytes, max_tokens=300)
            if result:
                cleaned = _clean_vision_caption(result.strip())
                if cleaned:
                    print("✅ Ollama Vision 캡션 생성 완료!")
                    return cleaned, None
    except Exception as e:
        print(f"  [Ollama Vision] 실패: {e}")

    return None, None


def git_sync():
    print("📤 Git 동기화 진행 중...")
    try:
        git_root = _root
        env = os.environ.copy()
        env["GIT_TERMINAL_PROMPT"] = "0"
        subprocess.run(["git", "add", "."], cwd=git_root, check=True)
        status = subprocess.run(["git", "status", "--porcelain"], cwd=git_root, capture_output=True, text=True)
        if status.stdout.strip():
            timestamp = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
            subprocess.run(["git", "commit", "-m", f"Auto-sync: Arin pipeline executed at {timestamp}"], cwd=git_root, check=True)
        # 브랜치명 동적 감지
        branch_result = subprocess.run(["git", "rev-parse", "--abbrev-ref", "HEAD"], cwd=git_root, capture_output=True, text=True)
        current_branch = branch_result.stdout.strip() if branch_result.returncode == 0 else "master"
        # remote에 먼저 커밋이 있을 경우 pull --rebase 후 push
        subprocess.run(["git", "pull", "--rebase", "origin", current_branch], cwd=git_root, check=True, env=env)
        subprocess.run(["git", "push", "origin", current_branch], cwd=git_root, check=True, env=env)
        print("✅ Git 동기화 완료!")
        send_telegram_message("💾 [Git 동기화 완료] 아린 에이전트 소스 및 설정이 GitHub에 백업되었습니다.")
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
    selected_trend = random.choice(fresh_trends)
    print(f"✅ 선택된 트렌드: {selected_trend}")
    send_telegram_message(f"📸 [아린] 1단계: 오늘의 구글 트렌드 분석 완료. 타겟 키워드 '{selected_trend}' 선정 완료.")

    # 2. 상위 트렌드에서 시각적 키워드 추출
    print("🔑 키워드 추출 중...")
    keywords = _extract_visual_keywords(fresh_trends[:12])
    keywords["topic"] = selected_trend
    print(f"  키워드: {keywords}")

    # 3. 키워드 기반 이미지 프롬프트·제목·디스크립션·태그 생성
    post_data = _generate_full_content(selected_trend, keywords)

    # ---------------------------------------------------
    # 2a. 분석·딥서치 및 의사결정
    # ---------------------------------------------------
    # 실행 중인 리포트 경로 (예시) – 필요에 따라 조정
    report_path = os.path.join(_root, "reports", "research", "sample_research_report.md")
    analysis_result = run_analysis_and_deepsearch(report_path)
    decision = make_decision(analysis_result)
    # 승인 요청 – Ye-won(CEO)에게 전달
    if not await_approval(decision):
        print("❌ 승인 거부 또는 타임아웃 – 파이프라인 중단.")
        send_telegram_message("🚨 아린 인스타: 승인 거부로 포스팅 중단.")
        sys.exit(0)
    
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
        send_telegram_message(f"📸 [아린] 2단계: 기획 수립 완료 (업로드 시간: {post_data['best_time']}). 드라이 런으로 종료합니다.")
        return

    send_telegram_message(f"📸 [아린] 2단계: 포스팅 기획 완료. 업로드 시간 {post_data['best_time']} 매핑. 이미지 생성을 시작합니다.")
        
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
    temp_filename = os.path.join(_OUT_DIR, "temp_generated.jpg")
    img_bytes = None
    image_url = None
    try:
        # 1순위: 나노바나나2 (Imagen 3.0) 생성 시도
        img_bytes = _generate_image_gemini(crafted_prompt)
        
        # 2순위 폴백: Pollinations.ai
        if not img_bytes:
            print("  ⚠️ 나노바나나2 생성 실패로 Pollinations.ai 폴백을 진행합니다.")
            img_bytes = _generate_image_pollinations(crafted_prompt)
            print(f"✅ Pollinations.ai 이미지 생성 완료! ({len(img_bytes):,} bytes)")
        else:
            print(f"✅ 나노바나나2 (Imagen 3.0) 이미지 생성 완료! ({len(img_bytes):,} bytes)")

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

    # 5. Gemini Vision으로 이미지 보고 캡션 + alt_text 작성
    vision_caption, vision_alt_text = generate_caption_from_image(img_bytes) if img_bytes else (None, None)
    if vision_caption and _has_banned_content(vision_caption):
        print("⚠️ Vision 캡션에 금지 문구 감지 — 템플릿 캡션으로 대체합니다.")
        vision_caption = None
    # Vision 캡션이 너무 짧으면 원본 기획 캡션 사용 (JSON 파싱 실패 등 방어)
    if vision_caption and len(vision_caption.strip()) < 20:
        print(f"⚠️ Vision 캡션 너무 짧음 ({len(vision_caption.strip())}자) — 원본 캡션 사용")
        vision_caption = None
    final_caption = vision_caption if vision_caption else post_data["caption"]
    final_alt_text = vision_alt_text or ""

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
        """가희 지적 사항을 Ollama로 직접 수정한 새 캡션 반환 (최소 50자 보장)."""
        if not lm_available():
            return bad_caption
        issues_str = ", ".join(issues)
        # 원본 기획 캡션을 베이스로 수정 (짧은 bad_caption 사용 금지)
        base = bad_caption if len(bad_caption) >= 30 else post_data.get("caption", bad_caption)
        prompt = (
            f"인스타그램 캡션을 수정해줘.\n"
            f"문제점: {issues_str}\n"
            f"원본:\n{base}\n\n"
            "규칙:\n"
            "- 반드시 50자 이상 작성\n"
            "- 진짜 사람이 쓴 것처럼 자연스러운 한국어\n"
            "- 이모지 1~2개 포함\n"
            "- 해시태그 5~8개 마지막 줄에\n"
            "- AI·인공지능·테크·로봇 금지\n"
            "- 캡션 텍스트만 출력 (설명 없이)"
        )
        result = lm_chat(prompt, task="", max_tokens=400, temperature=0.8)
        if result and len(result.strip()) >= 20:
            return result.strip()
        # Ollama도 짧게 반환 시 원본 기획 캡션으로 복귀
        return post_data.get("caption", bad_caption)

    uploader = InstaUploader(account_id, access_token)
    post_id = None
    MAX_RETRIES = 15

    for attempt in range(1, MAX_RETRIES + 1):
        # 가희 사전 검수
        send_telegram_message(f"📸 [아린] 3단계: 가희(Inspector) 사전 검수 진행 중 (시도 {attempt}/{MAX_RETRIES})...")
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
        send_telegram_message(f"📸 [아린] 사전 검수 통과 완료. 인스타그램 업로드를 시도합니다.")

        # 업로드
        print(f"  📤 업로드 중 (시도 {attempt})...")
        post_id = uploader.upload_image(image_url, final_caption, alt_text=final_alt_text)
        if not post_id:
            print(f"  ❌ 업로드 실패")
            send_telegram_message(f"🚨 [아린] 인스타그램 이미지 발행 실패 (시도 {attempt}/{MAX_RETRIES})")
            break

        # 가희 사후 검수
        send_telegram_message(f"📸 [아린] 4단계: 가희(Inspector) 사후 검수 진행 중...")
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
            send_telegram_message(f"📸 [아린] 사후 검수 최종 통과 완료.")
        break  # 검수 통과 or 최대 재시도 도달

    # 업로드 성공 후 처리 (루프 밖)
    if post_id:
        print(f"🎉 성공적으로 자동 포스팅이 완료되었습니다! (ID: {post_id})")
        send_telegram_message(f"📸 [아린] 5단계: 금일 자동 인스타그램 포스팅 발행 완료!\n- 주제: {selected_trend}\n- 업로드 타임: {post_data['best_time']}\n- ID: {post_id}")
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

    # 아린은 인스타그램 전담 — YouTube 영상 생성 없음 (사장님 지시)

    # 깃 동기화
    git_sync()


if __name__ == "__main__":
    is_dry = "--dry-run" in sys.argv
    try:
        main(dry_run=is_dry)
    except Exception as _e:
        try:
            from _shared.agent_council import convene_from_exception
            convene_from_exception(_e, context_file=__file__, caller_agent="아린_관리자")
        except Exception as _ce:
            print(f"[Council] 회의 소집 실패: {_ce}")
        raise
