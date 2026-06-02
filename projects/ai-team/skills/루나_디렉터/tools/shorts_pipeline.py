"""
shorts_pipeline.py — 트렌드 기반 시티팝 YouTube Shorts 자동 파이프라인

Phase 1 (즉시 실행): 트렌드 리서치 (YouTube + Google Trends + News RSS)
Phase 2 (12:00 KST): 60초 Shorts 생성 (시티팝 음악 + 9:16 비주얼 + 트렌딩 메타데이터)
Phase 3 (14:00 KST): YouTube Shorts 업로드 (Private 예약 → 14:00 공개)

실행: python shorts_pipeline.py
"""
import os, sys, json, time, datetime, subprocess, urllib.request, urllib.parse, base64, xml.etree.ElementTree as ET

_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, ".agent")):
        break
    _root = os.path.dirname(_root)
if _root not in sys.path:
    sys.path.insert(0, _root)
sys.path.insert(0, _here)

from src.lyria_music_generator import LyriaMusicGenerator
from src.trend_analyzer import generate_music_prompt_from_title, _generate_optimized_title
from src.youtube_uploader import YouTubeUploader
from _shared.env_loader import load_env
from _shared.telegram_notifier import send_telegram_message
from _shared.ollama_client import chat as lm_chat, is_available as lm_available

KST = datetime.timezone(datetime.timedelta(hours=9))
OUTPUT_DIR    = os.path.join(_here, "output")
RESEARCH_FILE = os.path.join(OUTPUT_DIR, "shorts_research.json")
FFMPEG = next((p for p in [
    r"C:\Users\cross\AppData\Local\Microsoft\WinGet\Packages"
    r"\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe"
    r"\ffmpeg-8.1.1-full_build\bin\ffmpeg.exe"
] if os.path.exists(p)), "ffmpeg")

BANNED = ["lofi", "lo-fi", "study beats", "chill beats", "sleep music", "white noise"]

os.makedirs(OUTPUT_DIR, exist_ok=True)


# ── Phase 1: 트렌드 리서치 ─────────────────────────────────────────────────────

def _fetch_rss(url: str) -> list[str]:
    try:
        req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
        with urllib.request.urlopen(req, timeout=12) as r:
            root = ET.fromstring(r.read())
        return [e.findtext("title", "").strip() for e in root.findall(".//item") if e.findtext("title", "")]
    except Exception as e:
        print(f"  [RSS] {url[:50]} 실패: {e}")
        return []


def _fetch_yt_trending(api_key: str) -> list[str]:
    """한국 유튜브 인기 영상 상위 50개 제목 수집."""
    if not api_key:
        return []
    try:
        url = (
            "https://www.googleapis.com/youtube/v3/videos"
            f"?part=snippet&chart=mostPopular&regionCode=KR&maxResults=50&key={api_key}"
        )
        with urllib.request.urlopen(url, timeout=12) as r:
            data = json.loads(r.read())
        return [i["snippet"]["title"] for i in data.get("items", [])]
    except Exception as e:
        print(f"  [YT 트렌딩] 수집 실패: {e}")
        return []


def _fetch_yt_music_trending(api_key: str) -> list[str]:
    """한국 유튜브 인기 음악 상위 50개 제목 수집."""
    if not api_key:
        return []
    try:
        url = (
            "https://www.googleapis.com/youtube/v3/videos"
            f"?part=snippet&chart=mostPopular&regionCode=KR"
            f"&videoCategoryId=10&maxResults=50&key={api_key}"
        )
        with urllib.request.urlopen(url, timeout=12) as r:
            data = json.loads(r.read())
        return [i["snippet"]["title"] for i in data.get("items", [])]
    except Exception as e:
        print(f"  [YT 음악 트렌딩] 수집 실패: {e}")
        return []


def run_research() -> dict:
    print("=" * 60)
    print("  [Phase 1] 트렌드 리서치 시작")
    print("=" * 60)

    load_env()

    api_key = os.getenv("GEMINI_API_KEY", "")
    yt_api_key = os.getenv("YOUTUBE_API_KEY", api_key)

    results = {
        "collected_at": datetime.datetime.now(KST).isoformat(),
        "google_trends": [],
        "news_kr": [],
        "yt_trending": [],
        "yt_music": [],
    }

    print("  📡 Google 트렌드 수집 중...")
    results["google_trends"] = _fetch_rss("https://trends.google.com/trending/rss?geo=KR")[:20]
    print(f"    → {len(results['google_trends'])}개 수집")

    print("  📰 한국 뉴스 RSS 수집 중...")
    results["news_kr"] = _fetch_rss("https://news.google.com/rss?hl=ko&gl=KR&ceid=KR:ko")[:20]
    print(f"    → {len(results['news_kr'])}개 수집")

    print("  🎵 유튜브 트렌딩 수집 중...")
    results["yt_trending"] = _fetch_yt_trending(yt_api_key)
    print(f"    → {len(results['yt_trending'])}개 수집")

    print("  🎶 유튜브 음악 차트 수집 중...")
    results["yt_music"] = _fetch_yt_music_trending(yt_api_key)
    print(f"    → {len(results['yt_music'])}개 수집")

    # Ollama로 트렌드 키워드 분석 → 시티팝 결합 키워드 추출
    all_trends = results["google_trends"][:10] + results["news_kr"][:10]
    trend_keyword = _extract_citypop_keyword(all_trends, results["yt_music"][:15])
    results["selected_keyword"] = trend_keyword

    with open(RESEARCH_FILE, "w", encoding="utf-8") as f:
        json.dump(results, f, indent=2, ensure_ascii=False)

    send_telegram_message(
        f"📊 <b>[루나 숏츠]</b> 트렌드 리서치 완료\n"
        f"- 구글 트렌드: {len(results['google_trends'])}개\n"
        f"- 뉴스: {len(results['news_kr'])}개\n"
        f"- 선택 키워드: <b>{trend_keyword}</b>\n\n"
        f"⏳ 12:00에 콘텐츠 생성 시작"
    )

    print(f"\n  ✅ 리서치 완료 — 선택 키워드: {trend_keyword}")
    return results


def _extract_citypop_keyword(trends: list[str], yt_music: list[str]) -> str:
    """트렌딩 목록에서 시티팝과 결합할 최적 키워드 추출."""
    if lm_available():
        sample_trends = "\n".join(f"- {t}" for t in trends[:15])
        sample_music  = "\n".join(f"- {t}" for t in yt_music[:10])
        prompt = (
            f"[현재 한국 트렌딩]\n{sample_trends}\n\n"
            f"[유튜브 인기 음악]\n{sample_music}\n\n"
            "위 데이터를 분석해서 시티팝/K-POP 음악 숏츠와 결합하면 조회수가 높을 영어 키워드를 1개만 출력해.\n"
            "조건: 감성적이고 밤/도시/드라이브/여름/여행 느낌, lofi 금지, 단어 2~4개\n"
            "예시: Midnight Seoul Drive / Neon Tokyo Night / Summer City Cruise\n"
            "키워드만 출력 (설명 없이):"
        )
        result = lm_chat(prompt, task="", max_tokens=30)
        if result and result.strip():
            kw = result.strip().split("\n")[0].strip().strip('"').strip("'")
            if kw and not any(b in kw.lower() for b in BANNED):
                return kw

    # 폴백: 미리 정의된 감성 키워드에서 랜덤 선택
    import random
    fallbacks = [
        "Midnight Seoul Drive", "Neon City Night", "Tokyo Summer Cruise",
        "Golden Hour Drive", "Seoul Neon Romance", "City Pop Midnight",
    ]
    return random.choice(fallbacks)


# ── Phase 2: Shorts 콘텐츠 생성 ──────────────────────────────────────────────

def _generate_image_9x16(prompt: str, output_path: str) -> str:
    """9:16 비주얼 이미지 생성 — Gemini 1순위, Pollinations 폴백."""
    api_key = os.getenv("GEMINI_API_KEY", "")

    # 1순위: Gemini
    if api_key:
        url = (
            "https://generativelanguage.googleapis.com/v1beta/models"
            f"/gemini-3.1-flash-image-preview:generateContent?key={api_key}"
        )
        payload = json.dumps({
            "contents": [{"parts": [{"text": prompt}]}],
            "generationConfig": {"responseModalities": ["IMAGE", "TEXT"]},
        }).encode()
        try:
            req = urllib.request.Request(url, data=payload, headers={"Content-Type": "application/json"})
            with urllib.request.urlopen(req, timeout=60) as r:
                res = json.loads(r.read())
            for cand in res.get("candidates", []):
                for part in cand.get("content", {}).get("parts", []):
                    if "inlineData" in part:
                        with open(output_path, "wb") as f:
                            f.write(base64.b64decode(part["inlineData"]["data"]))
                        print(f"    [Gemini 이미지] 완료: {output_path}")
                        return output_path
        except Exception as e:
            print(f"    [Gemini 이미지] 실패: {e}")

    # 2순위: Pollinations (세로형)
    try:
        encoded = urllib.parse.quote(prompt[:400])
        url = f"https://image.pollinations.ai/prompt/{encoded}?width=720&height=1280&model=flux&nologo=true"
        req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
        with urllib.request.urlopen(req, timeout=90) as r:
            data = r.read()
        if len(data) > 5000:
            with open(output_path, "wb") as f:
                f.write(data)
            print(f"    [Pollinations 이미지] 완료: {output_path}")
            return output_path
    except Exception as e:
        print(f"    [Pollinations 이미지] 실패: {e}")

    return ""


def _create_shorts_video(image_path: str, audio_path: str, output_path: str, duration: int = 60) -> bool:
    """이미지 + 오디오 → 60초 9:16(720x1280) Shorts 영상 합성."""
    try:
        cmd = [
            FFMPEG, "-y",
            "-loop", "1", "-i", image_path,          # 이미지 루프
            "-i", audio_path,                          # 오디오
            "-vf", "scale=720:1280:force_original_aspect_ratio=decrease,"
                   "pad=720:1280:(ow-iw)/2:(oh-ih)/2:black,"
                   "setsar=1",
            "-c:v", "libx264", "-preset", "fast", "-pix_fmt", "yuv420p",
            "-c:a", "aac", "-b:a", "192k",
            "-t", str(duration),                       # 60초 고정
            "-map", "0:v:0", "-map", "1:a:0",
            "-shortest",
            output_path
        ]
        result = subprocess.run(cmd, capture_output=True, text=True, timeout=120)
        if result.returncode == 0 and os.path.exists(output_path):
            print(f"    [Shorts 영상] 완료: {output_path}")
            return True
        print(f"    [Shorts 영상] 실패: {result.stderr[-300:]}")
        return False
    except Exception as e:
        print(f"    [Shorts 영상] 예외: {e}")
        return False


def _generate_metadata(title: str, keyword: str, music_prompt: str, yt_titles: list[str]) -> dict:
    """선정된 제목 + 음악 프롬프트 기반 Shorts 메타데이터 생성. 고정 공식 없음."""
    if lm_available():
        sample = "\n".join(f"- {t}" for t in yt_titles[:15])
        prompt = (
            f"[영상 제목] {title}\n"
            f"[트렌딩 키워드] {keyword}\n"
            f"[음악 프롬프트] {music_prompt[:200]}\n"
            f"[유튜브 인기 제목 참고]\n{sample}\n\n"
            "YouTube Shorts 메타데이터를 생성하라.\n\n"
            "규칙:\n"
            "- title: 위 [영상 제목]을 그대로 사용. 단, #Shorts 태그가 없으면 끝에 추가.\n"
            "- description: 곡의 분위기·감성 2문장 + #Shorts #시티팝 #citypop + youtube.com/@luna_official\n"
            "  ⚠️ 타임라인 금지. 각 영상마다 고유한 문장.\n"
            "- tags: 최소 15개, #Shorts/#쇼츠 포함, lofi 금지, citypop/시티팝/드라이브bgm 필수\n"
            "  트렌딩 키워드도 tags에 포함.\n\n"
            'JSON만 반환: {"title":"...","description":"...","tags":["..."]}'
        )
        raw = lm_chat(prompt, task="", max_tokens=600, json_mode=True)
        if raw:
            try:
                data = json.loads(raw.strip())
                if data.get("title") and data.get("description") and data.get("tags"):
                    return data
            except Exception:
                pass

    # 폴백: 선정된 제목 그대로 사용
    final_title = title if "#Shorts" in title else f"{title} #Shorts"
    return {
        "title": final_title,
        "description": (
            f"✨ {keyword} — 도시의 밤을 물들이는 시티팝.\n"
            f"감성 가득한 60초 숏츠.\n\n"
            f"🎵 youtube.com/@luna_official\n\n"
            f"#Shorts #쇼츠 #시티팝 #citypop #드라이브BGM #{keyword.replace(' ','')}"
        ),
        "tags": [
            "Shorts", "쇼츠", "시티팝", "citypop", "드라이브BGM",
            "city pop", "K-Pop", "케이팝", "음악", keyword, "BGM", "야간드라이브",
        ],
    }


def run_generation(research: dict) -> dict:
    print("=" * 60)
    print("  [Phase 2] Shorts 콘텐츠 생성 시작")
    print("=" * 60)

    keyword     = research.get("selected_keyword", "Midnight Seoul Drive")
    yt_titles   = research.get("yt_music", []) + research.get("yt_trending", [])
    music_gen   = LyriaMusicGenerator()

    send_telegram_message(f"🎬 <b>[루나 숏츠]</b> 콘텐츠 생성 시작\n키워드: <b>{keyword}</b>")

    # ① 제목 선정 (키워드 → 제목) — 지식파일 확정 흐름: 제목→음악프롬프트
    title = _generate_optimized_title(keyword, yt_titles)
    print(f"  [제목 선정] {title}")

    # ② 제목 기반 음악 프롬프트 생성 (generate_music_prompt_from_title 사용)
    music_prompt = generate_music_prompt_from_title(title, keyword)
    print(f"  [음악 프롬프트] {music_prompt[:80]}...")

    # ② 60초 음악 생성 (Lyria)
    audio_path = os.path.join(OUTPUT_DIR, "shorts_track.mp3")
    print("  🎵 60초 시티팝 음악 생성 중 (Lyria 3 Pro)...")
    result = music_gen.generate_music(music_prompt, output_path=audio_path, is_pro=True)
    if not result or not os.path.exists(result):
        send_telegram_message("⚠️ [루나 숏츠] Lyria 실패 — 파이프라인 중단")
        return {}
    print(f"  ✅ 음악 생성 완료: {result}")

    # ③ 9:16 비주얼 생성
    visual_prompt = (
        f"Cinematic vertical 9:16 city pop aesthetic, {keyword.lower()}, "
        "neon-lit urban street at night, retro 80s Japanese city vibes, "
        "warm pink and purple tones, rain reflections on asphalt, "
        "vintage car, dreamy bokeh lights, lofi city atmosphere, "
        "phone wallpaper composition, no text, no watermark"
    )
    image_path = os.path.join(OUTPUT_DIR, "shorts_visual.jpg")
    print("  🖼️  9:16 비주얼 생성 중...")
    img = _generate_image_9x16(visual_prompt, image_path)
    if not img:
        send_telegram_message("⚠️ [루나 숏츠] 비주얼 생성 실패 — 파이프라인 중단")
        return {}

    # ④ 60초 Shorts 영상 합성
    video_path = os.path.join(OUTPUT_DIR, "shorts_final.mp4")
    print("  🎬 60초 Shorts 영상 합성 중 (720x1280)...")
    if not _create_shorts_video(img, result, video_path, duration=60):
        send_telegram_message("⚠️ [루나 숏츠] 영상 합성 실패 — 파이프라인 중단")
        return {}

    # ⑤ 썸네일 추출 (5초 지점)
    thumb_path = os.path.join(OUTPUT_DIR, "shorts_thumb.jpg")
    try:
        subprocess.run(
            [FFMPEG, "-y", "-ss", "5", "-i", video_path, "-vframes", "1", thumb_path],
            capture_output=True, check=True, timeout=30
        )
        # PIL 보정
        try:
            from PIL import Image, ImageEnhance
            img_obj = Image.open(thumb_path)
            img_obj = ImageEnhance.Color(img_obj).enhance(1.4)
            img_obj = ImageEnhance.Contrast(img_obj).enhance(1.15)
            img_obj.save(thumb_path)
        except Exception:
            pass
        print(f"  ✅ 썸네일 추출 완료")
    except Exception as e:
        print(f"  ⚠️ 썸네일 추출 실패: {e}")
        thumb_path = ""

    # ⑥ 메타데이터 생성
    print("  📝 Shorts 메타데이터 생성 중...")
    meta = _generate_metadata(title, keyword, music_prompt, yt_titles)
    print(f"  ✅ 제목: {meta['title']}")

    content = {
        "video_path":  video_path,
        "thumb_path":  thumb_path,
        "keyword":     keyword,
        "music_prompt": music_prompt,
        "meta":        meta,
    }

    # 중간 파일 저장
    state_path = os.path.join(OUTPUT_DIR, "shorts_state.json")
    with open(state_path, "w", encoding="utf-8") as f:
        json.dump({k: v for k, v in content.items() if k != "meta"} | {"meta": meta}, f,
                  indent=2, ensure_ascii=False)

    send_telegram_message(
        f"✅ <b>[루나 숏츠]</b> 콘텐츠 생성 완료!\n"
        f"- 제목: {meta['title']}\n"
        f"⏳ 14:00에 업로드 예정"
    )
    return content


# ── Phase 3: YouTube 업로드 ───────────────────────────────────────────────────

def run_upload(content: dict) -> bool:
    print("=" * 60)
    print("  [Phase 3] YouTube Shorts 업로드 시작")
    print("=" * 60)
    video_path = content.get("video_path", "")
    thumb_path = content.get("thumb_path", "")
    meta       = content.get("meta", {})

    # 길이 검증 — Shorts는 60초 이하만 허용
    import subprocess
    if video_path and os.path.exists(video_path):
        try:
            probe = subprocess.check_output(
                ["ffmpeg", "-v", "error", "-show_entries", "format=duration",
                 "-of", "default=noprint_wrappers=1:nokey=1", video_path]
            ).decode().strip()
            dur = float(probe)
            if dur > 60:
                print(f"  🚫 Shorts 길이 초과({dur:.1f}s > 60s) — 업로드 차단")
                from _shared.telegram_notifier import send_telegram_message as _tm
                _tm(f"🚫 [루나 shorts] {dur:.1f}초 — 60초 초과로 업로드 차단됨")
                return False
        except Exception:
            pass

    if not video_path or not os.path.exists(video_path):
        print("  ❌ 업로드할 영상 없음")
        return False

    # 14:00 KST public 예약
    now_kst     = datetime.datetime.now(KST)
    pub_kst     = now_kst.replace(hour=14, minute=0, second=0, microsecond=0)
    if pub_kst <= now_kst:
        pub_kst += datetime.timedelta(days=1)
    publish_at_utc = pub_kst.astimezone(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")

    uploader = YouTubeUploader()
    uploader.authenticate()

    video_id = uploader.upload_video(
        video_path=video_path,
        title=meta.get("title", "LUNA - MIDNIGHT DRIVE #Shorts"),
        description=meta.get("description", "#Shorts #시티팝 youtube.com/@luna_official"),
        tags=meta.get("tags", ["Shorts", "시티팝", "LUNA"]),
        privacy_status="private",
        publish_at=publish_at_utc,
    )

    if video_id:
        if thumb_path and os.path.exists(thumb_path):
            uploader.upload_thumbnail(video_id, thumb_path)
        uploader.add_video_to_playlist(video_id, "시티팝 숏츠")

        # 히스토리 기록
        history_path = os.path.join(_root, ".agent", "memory", "upload_history.json")
        try:
            history = json.load(open(history_path, encoding="utf-8")) if os.path.exists(history_path) else []
            history.append({
                "agent": "루나", "status": "published",
                "uploaded_at": datetime.datetime.now(KST).isoformat(),
                "metadata": {
                    "platform": "youtube",
                    "video_id": video_id,
                    "youtube_title": meta.get("title"),
                    "music_prompt": content.get("music_prompt", ""),
                    "video_file": os.path.basename(video_path),
                    "publish_at": publish_at_utc,
                    "format": "shorts_9x16",
                },
            })
            with open(history_path, "w", encoding="utf-8") as f:
                json.dump(history, f, indent=4, ensure_ascii=False)
        except Exception as e:
            print(f"  [Warning] 히스토리 기록 실패: {e}")

        msg = (
            f"✅ <b>[루나 숏츠]</b> 업로드 완료!\n"
            f"- 제목: {meta.get('title')}\n"
            f"- 링크: https://youtu.be/{video_id}\n"
            f"- 공개 예약: 14:00 KST ({pub_kst.strftime('%Y-%m-%d')})"
        )
        send_telegram_message(msg)
        print(f"\n{msg}")
        return True
    else:
        send_telegram_message("❌ [루나 숏츠] YouTube 업로드 실패")
        return False


# ── 대기 헬퍼 ────────────────────────────────────────────────────────────────

def _wait_until(hour: int, minute: int = 0, label: str = ""):
    now = datetime.datetime.now(KST)
    target = now.replace(hour=hour, minute=minute, second=0, microsecond=0)
    if target <= now:
        return  # 이미 지났으면 바로 실행
    wait_sec = (target - now).total_seconds()
    print(f"\n  ⏳ {label} 까지 {wait_sec/60:.0f}분 대기 중... ({target.strftime('%H:%M KST')})")
    time.sleep(wait_sec)


# ── 메인 ─────────────────────────────────────────────────────────────────────

def main():
    print("\n" + "=" * 60)
    print("  [루나] 트렌딩 시티팝 YouTube Shorts 파이프라인")
    print("=" * 60)

    send_telegram_message("🚀 <b>[루나 숏츠 파이프라인]</b> 시작!\nPhase 1: 트렌드 리서치 중...")

    # Phase 1: 즉시 리서치
    research = run_research()
    if not research:
        print("  ❌ 리서치 실패 — 중단")
        return

    # Phase 2: 12:00 KST 이후 생성
    _wait_until(12, 0, "12:00 KST 콘텐츠 생성")
    content = run_generation(research)
    if not content:
        print("  ❌ 콘텐츠 생성 실패 — 중단")
        return

    # Phase 3: 14:00 KST 업로드 (생성 완료 후 바로 업로드 → YouTube가 14:00에 공개)
    _wait_until(14, 0, "14:00 KST 업로드")
    run_upload(content)

    print("\n" + "=" * 60)
    print("  [루나] 숏츠 파이프라인 완료")
    print("=" * 60)


if __name__ == "__main__":
    main()
