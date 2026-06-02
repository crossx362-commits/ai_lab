"""
music_video_pipeline.py — 루나 시티팝 뮤직비디오 자율 파이프라인

흐름:
  ① 테마 선택 + 제목 생성
  ② 제목 기반 음악 프롬프트 생성 (5단 템플릿)
  ③ Lyria 3 Pro 완곡 생성 (2분 이상, clip 금지)
  ④ 5단 비주얼 생성 (Gemini → Pollinations 폴백)
  ⑤ 비주얼 + 오디오 합성 (1280x720 16:9, 쇼츠 방지)
  ⑥ 메타데이터 자동 생성 (제목+음악 내용 반영, 고유 설명)
  ⑦ 가희 사전 검수
  ⑧ YouTube 예약 업로드 (KST 19:00)
"""
import shutil
import os
import json
import datetime
import urllib.request
import urllib.parse
import base64
import random
import sys
import importlib.util
import subprocess

# 프로젝트 루트 경로 설정
_here = os.path.dirname(os.path.abspath(__file__))
_ai_team_root = os.path.abspath(os.path.join(_here, "..", "..", ".."))  # skills/루나_디렉터/tools → ai-team
if _ai_team_root not in sys.path:
    sys.path.insert(0, _ai_team_root)
if _here not in sys.path:
    sys.path.insert(0, _here)  # src.* 모듈 접근용

from _shared.env_loader import load_env, find_project_root
_root = find_project_root(_here)
from src.trend_analyzer import TrendAnalyzer, generate_music_prompt_from_title, _generate_optimized_title
from src.video_generator import VideoGenerator
from src.lyria_music_generator import LyriaMusicGenerator
from src.fallback_generators import (
    generate_music_pollinations,
    generate_video_veo,
    generate_video_ken_burns,
    generate_simple_slideshow,
)
from _shared.telegram_notifier import send_telegram_message
from _shared.resource_utils import wait_for_resources
from _shared.ollama_client import chat as _lm_chat
from src.youtube_uploader import YouTubeUploader
from src.optimal_time_analyzer import get_optimal_time_smart

# veo_video_maker 동적 임포트
_veo_spec = importlib.util.spec_from_file_location(
    "veo_video_maker", os.path.join(_here, "veo_video_maker.py")
)
_veo_mod = importlib.util.module_from_spec(_veo_spec)
_veo_spec.loader.exec_module(_veo_mod)
generate_long_take = _veo_mod.generate_long_take

# ── 상수 ──────────────────────────────────────────────────────────────────────
KST                      = datetime.timezone(datetime.timedelta(hours=9))
CHECKPOINT_FILE          = "output/music_video_checkpoint.json"
CHECKPOINT_MAX_AGE_HOURS = 36

# ffmpeg — macOS/Linux: "ffmpeg" / Windows Winget 자동 감지
FFMPEG = next(
    (p for p in [
        r"C:\Users\cross\AppData\Local\Microsoft\WinGet\Packages"
        r"\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe"
        r"\ffmpeg-8.1.1-full_build\bin\ffmpeg.exe"
    ] if os.path.exists(p)),
    "ffmpeg",
)
FFPROBE = FFMPEG.replace("ffmpeg.exe", "ffprobe.exe") if "ffmpeg.exe" in FFMPEG else "ffprobe"

# 금지 장르
_BANNED_GENRES = [
    "lofi", "lo-fi", "lo fi", "study beats", "chill beats", "sleep music", 
    "white noise", "ambient study", "focus music", "chillhop"
]
_BANNED_TITLE_WORDS = [
    "lofi", "lo-fi", "lo fi", "study beats", "chill beats", "sleep music",
    "white noise", "인공지능", "ai 생성", "미래", "로봇", "테크",
]


# ── 헬퍼 함수 ─────────────────────────────────────────────────────────────────

def load_checkpoint() -> dict:
    if not os.path.exists(CHECKPOINT_FILE):
        return {}
    try:
        with open(CHECKPOINT_FILE, "r", encoding="utf-8") as f:
            cp = json.load(f)
        age_h = (
            datetime.datetime.now()
            - datetime.datetime.fromisoformat(cp.get("saved_at", "2000-01-01"))
        ).total_seconds() / 3600
        if age_h > CHECKPOINT_MAX_AGE_HOURS:
            os.remove(CHECKPOINT_FILE)
            return {}
        return cp
    except Exception as e:
        print(f"  [Warning] 체크포인트 로드 실패: {e}")
        return {}


def save_checkpoint(state: dict):
    state["saved_at"] = datetime.datetime.now().isoformat()
    os.makedirs("output", exist_ok=True)
    with open(CHECKPOINT_FILE, "w", encoding="utf-8") as f:
        json.dump(state, f, indent=2, ensure_ascii=False)


def clear_checkpoint():
    if os.path.exists(CHECKPOINT_FILE):
        try:
            os.remove(CHECKPOINT_FILE)
        except Exception:
            pass


def generate_visual(prompt: str, output_path: str) -> str:
    """Gemini로 비디오 기준 이미지 생성. 실패 시 빈 문자열 반환."""
    api_key = os.getenv("GEMINI_API_KEY", "")
    if not api_key:
        return ""
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
                    os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
                    with open(output_path, "wb") as f:
                        f.write(base64.b64decode(part["inlineData"]["data"]))
                    return output_path
    except Exception as e:
        print(f"  [Warning] Gemini 비주얼 생성 실패: {e}")
    return ""


def _generate_image_pollinations_fallback(prompt: str, output_path: str) -> str:
    try:
        encoded = urllib.parse.quote(prompt[:400])
        url = f"https://image.pollinations.ai/prompt/{encoded}?width=1280&height=720&model=flux&nologo=true"
        req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
        with urllib.request.urlopen(req, timeout=90) as r:
            data = r.read()
        if len(data) < 5000:
            return ""
        os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
        with open(output_path, "wb") as f:
            f.write(data)
        print(f"    [Pollinations Image] 완료: {output_path}")
        return output_path
    except Exception as e:
        print(f"    [Pollinations Image] 실패: {e}")
        return ""


def _get_local_video_duration(video_path: str) -> int:
    """ffprobe로 로컬 영상 길이(초) 반환. 실패 시 0."""
    try:
        result = subprocess.run(
            [FFPROBE,
             "-v", "error", "-show_entries", "format=duration",
             "-of", "default=noprint_wrappers=1:nokey=1", video_path],
            capture_output=True, text=True, timeout=10
        )
        return int(float(result.stdout.strip()))
    except Exception:
        return 0


def _is_duplicate_on_channel(video_path: str, uploader) -> bool:
    """로컬 영상과 채널 업로드 영상의 길이를 비교해 중복 여부 반환."""
    if not uploader.youtube:
        return False
    local_dur = _get_local_video_duration(video_path)
    if local_dur == 0:
        return False
    try:
        ch = uploader.youtube.channels().list(part="contentDetails", mine=True).execute()
        pl_id = ch["items"][0]["contentDetails"]["relatedPlaylists"]["uploads"]
        res = uploader.youtube.playlistItems().list(
            part="contentDetails", playlistId=pl_id, maxResults=50
        ).execute()
        ids = [i["contentDetails"]["videoId"] for i in res.get("items", [])]
        if not ids:
            return False
        detail = uploader.youtube.videos().list(
            part="contentDetails", id=",".join(ids)
        ).execute()
        import re as _re
        for item in detail.get("items", []):
            dur_iso = item["contentDetails"].get("duration", "PT0S")
            m = _re.match(r"PT(?:(\d+)H)?(?:(\d+)M)?(?:(\d+)S)?", dur_iso)
            if m:
                h, mi, s = (int(x or 0) for x in m.groups())
                ch_dur = h * 3600 + mi * 60 + s
                if abs(ch_dur - local_dur) <= 2:  # ±2초 오차 허용
                    print(f"  [중복 감지] 로컬={local_dur}s, 채널={ch_dur}s ({item['id']})")
                    return True
    except Exception as e:
        print(f"  [Warning] 중복 체크 실패: {e}")
    return False


def _record_to_history(record: dict):
    mem_path = os.path.join(_root, "reports", "history", "upload_history.json")
    try:
        history = json.load(open(mem_path, encoding="utf-8")) if os.path.exists(mem_path) else []
        history.append(record)
        with open(mem_path, "w", encoding="utf-8") as f:
            json.dump(history, f, indent=4, ensure_ascii=False)
    except Exception as e:
        print(f"  [Warning] 히스토리 기록 실패: {e}")


def _assert_not_banned(theme: dict):
    for field in [theme.get("keyword",""), theme.get("genre_era",""), theme.get("title","")]:
        for b in _BANNED_GENRES:
            if b in field.lower():
                raise ValueError(f"금지 장르: '{b}' in '{field[:40]}'")


def _auto_generate_metadata(music_prompt: str, yt_titles: list, draft_title: str = "") -> dict:
    """제목 + 음악 프롬프트 기반으로 고유한 메타데이터 생성 (Ollama)."""
    try:
        yt_sample = "\n".join(f"- {t}" for t in yt_titles[:15]) if yt_titles else "(없음)"
        prompt = (
            f"[음악 제목]\n{draft_title}\n\n"
            f"[음악 프롬프트]\n{music_prompt[:300]}\n\n"
            f"[유튜브 인기 제목 참고]\n{yt_sample}\n\n"
            "위 음악 제목과 프롬프트를 반영해 유튜브 메타데이터를 생성하라.\n\n"
            "규칙:\n"
            "- title: 트렌드 분석 기반 자연스러운 곡명. LUNA·Official·MV 등 고정 태그 일절 금지. 곡명만.\n"
            "- description: 이 곡만의 분위기·감성·스토리를 반영한 심플한 2~3문장 + 📌 추천상황 1줄\n"
            "  + youtube.com/@luna_official + 이 곡 내용 반영 해시태그 8개 이상\n"
            "  ⚠️ 다른 영상과 같은 문장 금지. 타임라인(00:00) 금지.\n"
            "- tags: 최소 20개, lofi/lo-fi 금지, 시티팝/citypop/LUNA/루나/드라이브bgm 필수\n\n"
            "JSON만 반환:\n"
            '{"title":"...","description":"...","tags":["..."]}'
        )
        raw = _lm_chat(prompt, task="", max_tokens=600, json_mode=True)
        if raw:
            data = json.loads(raw.strip())
            if data.get("title") and data.get("description") and data.get("tags"):
                print(f"  [Auto Meta] 제목: {data['title']}")
                return data
    except Exception as e:
        print(f"  [Auto Meta] 실패: {e}")
    return {}


# ── 메인 파이프라인 ───────────────────────────────────────────────────────────

def run_pipeline(publish_hhmm: str = None):
    """publish_hhmm: 'HH:MM' 형식 KST 시간. 없으면 기존 로직(오늘/내일 19:00) 사용."""
    load_env()
    os.makedirs("output", exist_ok=True)

    print("=" * 60)
    print("  [루나] AI 음악 & 비디오 통합 파이프라인 기동")
    print("=" * 60)

    cp = load_checkpoint()
    if cp:
        print(f"  - [Checkpoint] 재개: 단계='{cp.get('step')}', 저장={cp.get('saved_at','?')}")
    # else:
    #     send_telegram_message("🎬 루나: K-POP × 시티팝 뮤직비디오 파이프라인 기동합니다.")  # 중복 방지: telegram_bot.py에서 전송

    analyzer  = TrendAnalyzer()
    generator = VideoGenerator()
    music_gen = LyriaMusicGenerator()
    uploader  = YouTubeUploader()
    uploader.authenticate()

    db_path = "output/uploaded_music.json"
    uploaded_files = []
    if os.path.exists(db_path):
        try:
            uploaded_files = json.load(open(db_path, encoding="utf-8"))
        except Exception:
            pass

    # ── 예약 시간 (매일 최적 시간 자동 분석) ────────────────────────────────────
    if cp.get("publish_at_utc"):
        publish_at_utc       = cp["publish_at_utc"]
        publish_time_kst_str = cp.get("publish_time_kst_str", publish_at_utc)
    else:
        now_kst = datetime.datetime.now(KST)
        if publish_hhmm:
            # 수동 지정 시간
            h, m = int(publish_hhmm[:2]), int(publish_hhmm[3:])
            pub_kst = now_kst.replace(hour=h, minute=m, second=0, microsecond=0)
            if pub_kst <= now_kst:
                pub_kst += datetime.timedelta(days=1)
        else:
            # 자동 최적 시간 분석 (YouTube Analytics + Ollama)
            print("  📊 최적 업로드 시간 분석 중...")
            optimal_time_str = get_optimal_time_smart(uploader.youtube)
            h, m = int(optimal_time_str[:2]), int(optimal_time_str[3:])
            pub_kst = now_kst.replace(hour=h, minute=m, second=0, microsecond=0)
            if pub_kst <= now_kst:
                pub_kst += datetime.timedelta(days=1)
        publish_at_utc       = pub_kst.astimezone(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
        publish_time_kst_str = pub_kst.strftime("%Y-%m-%d %H:%M:%S")

    print(f"  - 예약 시간(KST): {publish_time_kst_str}")

    # ── ① 테마 선택 ────────────────────────────────────────────────────────────
    if cp.get("theme"):
        theme = cp["theme"]
        print(f"  - [Checkpoint] 테마 복원: {theme['keyword']}")
        send_telegram_message(f"🎬 [루나] 이전 작업을 복원했습니다. 테마: {theme['keyword']}")
    else:
        for _attempt in range(5):
            theme = analyzer.select_best_theme(idx=random.randint(1, 100))
            try:
                _assert_not_banned(theme)
                break
            except ValueError as e:
                print(f"  ⚠️ [금지 장르] {e} — 재선정 중...")
        else:
            send_telegram_message("⚠️ [루나] 금지 장르 회피 실패 — 기본 테마 강제 적용")
            theme["keyword"]   = "K-Pop City Pop Night Drive"
            theme["genre_era"] = "K-Pop × Japanese City Pop Fusion (80s Retro)"
        cp = {"step": "theme", "theme": theme,
              "publish_at_utc": publish_at_utc, "publish_time_kst_str": publish_time_kst_str}
        save_checkpoint(cp)
        send_telegram_message(f"🎬 [루나] 1단계: 오늘의 테마를 '{theme['keyword']}'로 결정했습니다.")

    # ── ① 제목 선정 (키워드 → 유튜브 최적화 제목) ────────────────────────────────
    yt_titles_for_title = theme.get("_yt_top_titles", [])
    title = _generate_optimized_title(theme["keyword"], yt_titles_for_title) or theme["title"]
    print(f"  - 테마: {theme['keyword']}")
    print(f"  - 제목: {title}")

    # ── ②③ 음악 + 비주얼 생성 ─────────────────────────────────────────────────
    video_path = "output/final_video.mp4"
    audio_path = ""
    full_music_prompt = cp.get("full_music_prompt", "")  # 체크포인트에서 복원 시도

    if (cp.get("step") == "video"
            and cp.get("video_path") and os.path.exists(cp["video_path"])
            and cp.get("audio_path")):
        video_path        = cp["video_path"]
        audio_path        = cp["audio_path"]
        full_music_prompt = cp.get("full_music_prompt", full_music_prompt)
        print(f"  - [Checkpoint] 비디오/오디오 복원: {video_path}")
        send_telegram_message(f"🎬 [루나] 2단계: 기존 생성 완료된 비디오/오디오를 복원하여 합성 및 검수 단계로 진입합니다.")
    else:
        print("🎬 [루나] 완곡 1트랙 + 5단 비주얼 시퀀스 시작...")
        send_telegram_message(f"🎬 [루나] 2단계: 음악 및 이미지/비디오 생성 작업을 시작합니다.\n곡명: {title}")
        order        = ["intro", "verse", "chorus", "bridge", "outro"]
        parts_images = {}
        parts_videos = {}

        # ② 제목 기반 음악 프롬프트 생성 (5단 템플릿, 매번 새로)
        full_music_prompt = generate_music_prompt_from_title(title, theme.get("keyword", ""))
        print(f"  [음악 프롬프트] {full_music_prompt[:100]}...")

        # ③ Lyria 3 Pro 완곡 생성
        send_telegram_message(f"🎵 [루나] 음원 생성 중 (lyria-3-pro-preview)...")
        full_audio_path = os.path.join("output", "full_track.mp3")
        print(f"\n🎵 [완곡 생성 — lyria-3-pro-preview]")
        full_track = music_gen.generate_music(full_music_prompt, output_path=full_audio_path, is_pro=True)
        if not full_track or not os.path.exists(full_track):
            print("⚠️ Lyria 실패 → Pollinations.ai 폴백...")
            full_track = generate_music_pollinations(full_music_prompt, full_audio_path)
        if not full_track or not os.path.exists(full_track):
            print("❌ 완곡 생성 실패 — 무음으로 진행.")
            full_track = ""

        # [전진 배치 검수] 비디오 합성 전 음원 길이 체크 (120초 미만 차단)
        if full_track:
            try:
                probe_cmd = [FFPROBE, "-v", "error", "-show_entries", "format=duration", "-of", "default=noprint_wrappers=1:nokey=1", full_track]
                track_dur = float(subprocess.check_output(probe_cmd).decode().strip())
                if track_dur < 120:
                    print(f"⚠️  [Early Check] 음원 길이 미달({track_dur:.1f}s). 비주얼 합성을 중단하고 재시도를 권장합니다.")
                    send_telegram_message(f"📢 <b>[루나]</b> 음원 길이 미달({track_dur:.1f}s) 감지. 리소스 절약을 위해 비주얼 합성을 중단합니다.")
                    clear_checkpoint()
                    return
            except Exception as e:
                print(f"  [Warning] 초기 길이 확인 실패: {e}")

        # ④ 5단 비주얼 생성
        send_telegram_message(f"🖼️ [루나] 5단 비주얼 시퀀스 생성 시작...")
        for part_name in order:
            visual_prompt = theme["visual_parts"][part_name]
            print(f"\n🖼️  [{part_name.upper()} 비주얼 생성 중...]")
            send_telegram_message(f"🖼️ [루나] 비주얼 '{part_name}' 생성 중...")

            img_path = os.path.join("output", f"visual_{part_name}.png")
            result = generate_visual(visual_prompt, img_path)
            if not result:
                print(f"⚠️ Gemini 실패 → Pollinations 시도...")
                result = _generate_image_pollinations_fallback(visual_prompt, img_path)
            part_image_path = result if (result and os.path.exists(result)) else None
            parts_images[part_name] = part_image_path

            vid_path  = os.path.join("output", f"video_{part_name}.mp4")
            video_ok  = False
            # Veo 사용 명시 요청 없을 시 호출 금지
            # is_ad = (theme.get("is_ad") or "[AD]" in theme.get("title","")
            #          or any(k in theme["keyword"].lower()
            #                 for k in ["skincare","perfume","espresso","chocolate","glacial water"]))
            # 
            # if is_ad:
            #     api_key = os.getenv("GEMINI_API_KEY","")
            #     if api_key:
            #         r = generate_video_veo(visual_prompt, vid_path, api_key)
            #         if r and os.path.exists(r):
            #             video_ok = True

            if not video_ok and part_image_path:
                ok = generator.generate_video(part_image_path, full_track, vid_path, duration=None)
                if ok and os.path.exists(vid_path):
                    video_ok = True

            if not video_ok and part_image_path:
                r = generate_video_ken_burns(part_image_path, full_track, vid_path, FFMPEG)
                if r and os.path.exists(r):
                    video_ok = True

            if video_ok:
                parts_videos[part_name] = vid_path
            else:
                print(f"❌ [{part_name.upper()}] 비디오 생성 실패 — 파트 제외.")

        # ⑤ 비주얼 concat + 오디오 합성 (1280x720 16:9 강제)
        successful_parts = [p for p in order if p in parts_videos]
        if not successful_parts:
            send_telegram_message("❌ 루나: 모든 파트 실패 — 파이프라인 중단.")
            return

        send_telegram_message("🎬 [루나] 3단계: 생성된 비주얼 소스 합성을 위한 FFmpeg 병합 작업을 시작합니다.")
        ts = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
        list_file = "output/video_list.txt"
        with open(list_file, "w", encoding="utf-8") as lf:
            for p in successful_parts:
                lf.write(f"file '{os.path.basename(parts_videos[p])}'\n")

        silent_video = "output/visual_only.mp4"
        try:
            # [병목 관리] 무거운 렌더링 전 시스템 자원 체크
            wait_for_resources(task_name="비주얼 병합(FFmpeg)")
            
            print("🎬 비주얼 병합 (1280x720 16:9)...")
            subprocess.run(
                [FFMPEG, "-y", "-f", "concat", "-safe", "0", "-i", list_file, "-an",
                 "-vf", "scale=1280:720,setsar=1", "-c:v", "libx264", "-pix_fmt", "yuv420p", silent_video],
                stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True
            )
            print(f"✅ 비주얼 병합 완료")
        except Exception as e:
            print(f"⚠️ concat 실패 ({e}) → 슬라이드쇼 폴백...")
            imgs = [parts_images[p] for p in successful_parts if parts_images.get(p)]
            if not generate_simple_slideshow(imgs, [full_track] if full_track else [], video_path, FFMPEG):
                send_telegram_message("❌ 루나: 비디오 병합 완전 실패.")
                return
            silent_video = video_path

        if full_track and os.path.exists(full_track) and silent_video != video_path:
            try:
                # [병목 관리] 오디오 합성 전 체크
                wait_for_resources(task_name="오디오 믹싱(FFmpeg)")
                print("🎬 비주얼과 오디오 합성 중 (루핑 적용)...")
                ok = generator.merge_video_audio(silent_video, full_track, video_path)
                if ok:
                    print(f"✅ 오디오 합성 완료: {video_path}")
                else:
                    raise RuntimeError("merge_video_audio returned False")
            except Exception as e:
                print(f"⚠️ 오디오 합성 실패 ({e}) — 무음 영상 사용")
                shutil.copy(silent_video, video_path)
        elif silent_video != video_path:
            shutil.copy(silent_video, video_path)

        print(f"✅ 최종 영상 완성: {video_path}")

        merged_path = os.path.join("output", f"bgm_merged_{ts}.mp3")
        if full_track and os.path.exists(full_track):
            shutil.copy(full_track, merged_path)
            audio_path = merged_path
        else:
            audio_path = ""

        # 임시 파일 정리
        for pf in ([p for p in parts_images.values() if p]
                   + list(parts_videos.values())
                   + [list_file, silent_video if silent_video != video_path else ""]):
            if pf and os.path.exists(pf):
                try:
                    os.remove(pf)
                except Exception:
                    pass

        cp["step"]             = "video"
        cp["audio_path"]       = audio_path
        cp["video_path"]       = video_path
        cp["full_music_prompt"] = full_music_prompt  # 체크포인트에 저장
        save_checkpoint(cp)

    # ── ⑥ 메타데이터 자동 생성 ────────────────────────────────────────────────
    # 폴백 설명 (Auto Meta 실패 시)
    description = (
        f"🎵 {title}\n\n"
        f"🎵 장르: {theme.get('genre_era','Japanese City Pop')}\n"
        f"⚡ 무드: {theme.get('tempo_mood','Nostalgic, upbeat')}\n"
        f"🎹 악기: {theme.get('instruments','Electric piano, synth bass')}\n"
        f"🎤 스타일: {theme.get('vocal_style','Female vocals')}\n\n"
        f"🎵 youtube.com/@luna_official"
    )
    tags = theme.get("tags", [theme["keyword"], "city pop", "시티팝", "드라이브bgm", "루나", "LUNA"])

    yt_titles = theme.get("_yt_top_titles", [])
    auto_meta = _auto_generate_metadata(full_music_prompt, yt_titles, draft_title=title)
    if auto_meta:
        title       = auto_meta.get("title", title)
        description = auto_meta.get("description", description)
        tags        = auto_meta.get("tags", tags)

    # ── ⑦ 가희 사전 검수 ──────────────────────────────────────────────────────
    issues = [w for w in _BANNED_TITLE_WORDS if w in title.lower()]
    if issues:
        send_telegram_message(
            f"🚨 <b>[가희]</b> 업로드 중단 — 금지 키워드: {issues}\n제목: {title}"
        )
        print(f"  [가희] 업로드 차단: {issues}")
        return

    # ⑦-2 영상 길이 최종 검수 (120초 미만 금지)
    try:
        probe_cmd = [FFPROBE, "-v", "error", "-show_entries", "format=duration", "-of", "default=noprint_wrappers=1:nokey=1", video_path]
        final_duration = float(subprocess.check_output(probe_cmd).decode().strip())
        if final_duration < 120:
            send_telegram_message(f"🚨 <b>[가희]</b> 업로드 중단 — 영상 길이가 너무 짧음 ({final_duration:.1f}초).")
            print(f"  [가희] 업로드 차단: 너무 짧은 영상 ({final_duration}s)")
            return
    except Exception as e:
        print(f"  [Warning] 최종 길이 확인 실패: {e}")

    print(f"  [가희] 사전 검수 통과 ✅ — {title[:60]}")

    # ── ⑧ YouTube 업로드 ──────────────────────────────────────────────────────
    # 썸네일 추출 및 보정
    thumb_path = "output/best_scene_thumbnail.png"
    print(f"🎬 썸네일 추출 중 (5초 지점)...")
    try:
        subprocess.run(
            [FFMPEG, "-y", "-ss", "00:00:05", "-i", video_path, "-vframes", "1", thumb_path],
            stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True
        )
        print(f"✅ 썸네일 추출 완료")
        try:
            from PIL import Image, ImageEnhance
            img = Image.open(thumb_path)
            img = ImageEnhance.Color(img).enhance(1.3)
            img = ImageEnhance.Contrast(img).enhance(1.2)
            img.save(thumb_path)
            print("✨ 썸네일 채도/대비 보정 완료")
        except Exception as e:
            print(f"⚠️ 썸네일 보정 실패: {e}")
        image_path = thumb_path
    except Exception as e:
        print(f"⚠️ 썸네일 추출 실패: {e}")
        image_path = thumb_path

    if _is_duplicate_on_channel(video_path, uploader):
        send_telegram_message(f"⚠️ 루나: 채널에 동일 영상 존재 — 업로드 중단.\n제목: {title}")
        print(f"  [중복 차단] 채널에 동일 길이 영상 존재 — 업로드 건너뜀.")
        clear_checkpoint()
        return

    send_telegram_message(f"🎬 [루나] 4단계: 가희 검수 통과 완료. 유튜브 업로드를 시작합니다.")
    video_id = uploader.upload_video(
        video_path=video_path, title=title, description=description,
        tags=tags, privacy_status="private", publish_at=publish_at_utc,
    )

    if video_id:
        send_telegram_message(f"🎬 [루나] 유튜브 영상 업로드 완료. 썸네일 등록 및 재생목록 추가 중...")
        uploader.upload_thumbnail(video_id, image_path)
        uploader.add_video_to_playlist(video_id, theme.get("playlist_title", "도시 드라이브 시티팝"))

        # 비디오 파일명 기록 (audio 없어도 반드시 저장)
        uploaded_files.append(os.path.basename(video_path))
        if audio_path:
            uploaded_files.append(os.path.basename(audio_path))
        with open(db_path, "w", encoding="utf-8") as f:
            json.dump(uploaded_files, f, indent=2, ensure_ascii=False)

        _record_to_history({
            "agent": "루나", "status": "published",
            "uploaded_at": datetime.datetime.now(KST).isoformat(),
            "metadata": {
                "platform": "youtube", "video_id": video_id,
                "youtube_title": title,
                "music_prompt": full_music_prompt,
                "video_file": os.path.basename(video_path),
                "audio_file": os.path.basename(audio_path) if audio_path else "",
                "publish_at": publish_at_utc,
            },
        })
        clear_checkpoint()

        msg = (f"✅ 루나: 뮤직비디오 업로드 완료!\n"
               f"- 제목: {title}\n"
               f"- 링크: https://youtu.be/{video_id}\n"
               f"- 예약(KST): {publish_time_kst_str}")
        # send_telegram_message(msg)  # 중복 방지: telegram_bot.py에서 전송
        print(f"\n{msg}")

        # 가희 사후 검수 (업로드 후 메타데이터 확인) + 실패 시 자동 수정
        try:
            import importlib.util as _ilu
            _spec = _ilu.spec_from_file_location("content_inspector",
                os.path.join(_root, "projects", "ai-team", "skills", "가희_검수관", "tools", "content_inspector.py"))
            _ci = _ilu.module_from_spec(_spec); _spec.loader.exec_module(_ci)
            post_check = _ci.inspect_video(video_id, mode="NEW_UPLOAD")
            status = post_check.get("status", "PASS")
            if status != "PASS":
                violations = post_check.get("violations", [])
                send_telegram_message(
                    f"⚠️ <b>[가희]</b> 루나 사후 검수 이상 ({status})\n"
                    f"영상: https://youtu.be/{video_id}\n"
                    f"위반: {violations}"
                )
                print(f"  [가희] 사후 검수: {status} — {violations}")
                # 자동 수정: 제목·설명 재생성 후 YouTube API로 업데이트
                try:
                    fixed_meta = _auto_generate_metadata(
                        full_music_prompt, theme.get("_yt_top_titles", []) or [], draft_title=title
                    )
                    fixed_title = fixed_meta.get("title", title)
                    _ci._update_yt_metadata(uploader.youtube, video_id, fixed_title)
                    # 수정 후 재검수
                    re_check = _ci.inspect_video(video_id, mode="NEW_UPLOAD")
                    re_status = re_check.get("status", "PASS")
                    if re_status == "PASS":
                        send_telegram_message(f"✅ <b>[가희]</b> 루나 재검수 통과 — 제목 수정 완료\n새 제목: {fixed_title}")
                        print(f"  [가희] 재검수 통과 ✅ — {fixed_title}")
                    else:
                        send_telegram_message(f"🚨 <b>[가희]</b> 루나 재검수도 실패 — 수동 확인 필요\nhttps://youtu.be/{video_id}")
                except Exception as fix_err:
                    print(f"  [가희] 자동 수정 실패: {fix_err}")
            else:
                print(f"  [가희] 사후 검수 통과 ✅")
        except Exception as e:
            print(f"  [가희] 사후 검수 호출 실패: {e}")
    else:
        send_telegram_message(f"❌ 루나: YouTube 업로드 실패 — {title}")

    print("=" * 60)
    print("  [루나] 파이프라인 실행 완료")
    print("=" * 60)


if __name__ == "__main__":
    import io, argparse
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser()
    ap.add_argument("--publish-at", dest="publish_at", default=None,
                    help="KST 업로드 시간 HH:MM (예: 19:30)")
    args = ap.parse_args()
    run_pipeline(publish_hhmm=args.publish_at)
