import os
import sys
import subprocess
import datetime

# Set python paths
_here = os.path.dirname(os.path.abspath(__file__))
# projects/ai-team/skills/데이브_주식/tools -> projects/ai-team/
AI_TEAM_ROOT = os.path.abspath(os.path.join(_here, "projects", "ai-team"))
PROJECT_ROOT = _here
sys.path.insert(0, AI_TEAM_ROOT)

from _shared.env_loader import load_env
load_env()

# Import Lyria wrapper
sys.path.insert(0, os.path.join(AI_TEAM_ROOT, "skills", "루나_디렉터", "tools"))
from src.lyria_music_generator import LyriaMusicGenerator
from _shared.ffmpeg_utils import get_ffmpeg_path

FFMPEG = get_ffmpeg_path()
IMAGE_PATH = "/Users/junholee/.gemini/antigravity-ide/brain/4fd2a4c0-c51e-4cca-9173-1271b4837bdf/swiftcart_promo_bg_1781439599101.png"
OUTPUT_DIR = os.path.join(_here, "output")
os.makedirs(OUTPUT_DIR, exist_ok=True)

def generate_promo():
    print("=== [루나] SwiftCart 30초 홍보영상 제작 개시 ===")
    
    # 1. 음악 프롬프트 구성 및 생성 (30초 클립용)
    prompt = (
        "SwiftCart Premium Pet Care brand vibe, 1980s Retro K-Pop City Pop fusion, "
        "115 BPM bright and positive energy, synthesizer chord pads, slap bass, retro drum machine, "
        "sweet female vocals singing a cheerful melody in Korean, "
        "소중한 우리 아이를 위한 건강한 선택 스위프트카트 웰니스 케어"
    )
    
    audio_path = os.path.join(OUTPUT_DIR, "swiftcart_promo_audio.mp3")
    print(f"🎵 1단계: Lyria 3로 30초 오디오 생성 시도...")
    
    track_path = None
    try:
        music_gen = LyriaMusicGenerator()
        track_path = music_gen.generate_music(prompt, output_path=audio_path, is_pro=False)
    except Exception as e:
        print(f"⚠️ Lyria API 호출 에러: {e}")
        
    if not track_path or not os.path.exists(track_path):
        print("⚠️ Lyria API 인증 실패로 인해 로컬 기존 시티팝 음원으로 폴백 진행합니다.")
        # Fallback to existing city pop tracks
        fallback_candidates = [
            os.path.join(PROJECT_ROOT, "output", "luna", "full_track.mp3"),
            os.path.join(PROJECT_ROOT, "output", "full_track.mp3"),
            os.path.join(PROJECT_ROOT, "reports", "uploads", "luna", "bgm_merged_20260603_062204.mp3")
        ]
        for candidate in fallback_candidates:
            if os.path.exists(candidate):
                track_path = candidate
                print(f"✅ 폴백 음원 선택완료: {candidate}")
                break
                
    if not track_path or not os.path.exists(track_path):
        print("❌ 오디오 생성 및 폴백 실패")
        return

        
    print(f"✅ 오디오 생성 완료: {track_path}")
    
    # 2. 비디오 합성 (30초)
    video_path = os.path.join(OUTPUT_DIR, "swiftcart_promo_video.mp4")
    print(f"🎬 2단계: FFmpeg 비디오 합성 중...")
    
    cmd = [
        FFMPEG, "-y",
        "-loop", "1", "-i", IMAGE_PATH,
        "-i", track_path,
        "-vf", "scale=1080:1080", # Scale image properly
        "-c:v", "libx264", "-preset", "fast", "-pix_fmt", "yuv420p",
        "-c:a", "aac", "-b:a", "192k",
        "-t", "30",
        "-map", "0:v:0", "-map", "1:a:0",
        "-shortest",
        video_path
    ]

    
    r = subprocess.run(cmd, capture_output=True, text=True)
    if r.returncode == 0 and os.path.exists(video_path):
        print(f"🎉 3단계: 30초 홍보영상 합성 성공!")
        print(f"📍 최종 비디오 경로: {video_path}")
    else:
        print(f"❌ 영상 합성 실패: {r.stderr}")

if __name__ == "__main__":
    generate_promo()
