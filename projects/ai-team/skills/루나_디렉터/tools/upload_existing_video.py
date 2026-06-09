"""
upload_existing_video.py — 기존 생성된 영상을 유튜브에 업로드
"""
import os
import sys
import datetime
import json

_here = os.path.dirname(os.path.abspath(__file__))
_ai_team_root = os.path.abspath(os.path.join(_here, "..", "..", ".."))
if _ai_team_root not in sys.path:
    sys.path.insert(0, _ai_team_root)
if _here not in sys.path:
    sys.path.insert(0, _here)

from _shared.env_loader import load_env, find_project_root
from src.youtube_uploader import YouTubeUploader
from _shared.ffmpeg_utils import enhance_thumbnail
from _shared.history_recorder import record_to_history
import subprocess

# UTF-8 인코딩 설정
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
if hasattr(sys.stderr, 'reconfigure'):
    sys.stderr.reconfigure(encoding='utf-8', errors='replace')

load_env()
_root = find_project_root(_here)
KST = datetime.timezone(datetime.timedelta(hours=9))
_OUT_DIR = os.path.join(_root, "output", "luna")

FFMPEG = os.getenv("FFMPEG_PATH", "ffmpeg")
FFPROBE = os.getenv("FFPROBE_PATH", "ffprobe")

# 업로드할 영상
video_path = os.path.join(_OUT_DIR, "final_video.mp4")
title = "CPI - City Pop Invasion"
description = """🎵 달빛 아래 빛나는 도시를 담은 감성 시티팝 트랙입니다.

몽환적인 신스 사운드와 따뜻한 보컬이 어우러져 낭만적인 도시의 밤을 표현합니다.

📌 퇴근길 드라이브나 여유로운 저녁 시간에 함께하세요.

🎹 Genre / Era: Japanese City Pop × K-Pop Fusion
🎸 Instruments: Synthesizer, Electric Guitar
🎙️ Vocal Style: Vibrant, Energetic
✨ Theme: Urban night romance

youtube.com/@류나-l7h

#시티팝 #citypop #LUNA #루나 #드라이브bgm #kpop #nightdrive #synthwave #cityvibes #koreanmusic
"""

tags = [
    "시티팝", "citypop", "LUNA", "루나", "드라이브bgm", "k-pop", "city pop",
    "night drive", "synthwave", "retro pop", "korean music", "chill music",
    "urban pop", "neon nights", "80s vibes", "japanese city pop", "fusion pop",
    "drive music", "mood music", "aesthetic music", "감성음악", "밤드라이브"
]

# 예약 시간 (오늘 19:00)
now_kst = datetime.datetime.now(KST)
pub_kst = now_kst.replace(hour=19, minute=0, second=0, microsecond=0)
if pub_kst <= now_kst:
    pub_kst += datetime.timedelta(days=1)
publish_at_utc = pub_kst.astimezone(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
publish_time_kst_str = pub_kst.strftime("%Y-%m-%d %H:%M:%S")

print("=" * 60)
print("  [루나] 기존 영상 업로드")
print("=" * 60)
print(f"  - 영상: {video_path}")
print(f"  - 제목: {title}")
print(f"  - 예약: {publish_time_kst_str}")

# YouTube 업로더 초기화
uploader = YouTubeUploader()
uploader.authenticate()

# 썸네일 생성
thumb_path = os.path.join(_OUT_DIR, "upload_thumbnail.png")
print(f"🎬 썸네일 추출 중...")
try:
    subprocess.run(
        [FFMPEG, "-y", "-ss", "00:00:05", "-i", video_path, "-vframes", "1", thumb_path],
        stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True
    )
    print(f"✅ 썸네일 추출 완료")
    if enhance_thumbnail(thumb_path):
        print("✨ 썸네일 보정 완료")
except Exception as e:
    print(f"⚠️ 썸네일 생성 실패: {e}")

# 업로드
print("🎬 유튜브 업로드 시작...")
video_id = uploader.upload_video(
    video_path=video_path,
    title=title,
    description=description,
    tags=tags,
    privacy_status="private",
    publish_at=publish_at_utc,
)

if video_id:
    print(f"✅ 업로드 완료: https://youtu.be/{video_id}")

    # 썸네일 업로드
    if os.path.exists(thumb_path):
        uploader.upload_thumbnail(video_id, thumb_path)
        print("✅ 썸네일 등록 완료")

    # 재생목록 추가
    uploader.add_video_to_playlist(video_id, "도시 드라이브 시티팝")
    print("✅ 재생목록 추가 완료")

    # 히스토리 기록
    record_to_history({
        "agent": "루나",
        "status": "published",
        "uploaded_at": datetime.datetime.now(KST).isoformat(),
        "metadata": {
            "platform": "youtube",
            "video_id": video_id,
            "youtube_title": title,
            "music_prompt": "City Pop Invasion - Japanese City Pop × K-Pop Fusion",
            "video_file": os.path.basename(video_path),
            "audio_file": "",
            "publish_at": publish_at_utc,
        },
    }, caller_file=__file__)

    print(f"\n✅ 완료!")
    print(f"   제목: {title}")
    print(f"   링크: https://youtu.be/{video_id}")
    print(f"   예약: {publish_time_kst_str}")
else:
    print("❌ 업로드 실패")
