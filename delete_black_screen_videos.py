"""검은 화면 루나 영상 삭제 스크립트"""
import sys
import os

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "projects", "ai-team"))

from _shared.env_loader import load_env
load_env()

from skills.루나_디렉터.tools.src.youtube_uploader import YouTubeUploader

def main():
    # 검은 화면 영상 목록 (2026-05-27 ~ 2026-06-04 업로드, DRY-RUN 제외)
    black_screen_videos = [
        {"id": "6nFbiwKBXTs", "title": "LUNA - TOKYO NEON PERFUME"},
        {"id": "J__4KHObl0E", "title": "LUNA - NEON MOMENTUM"},
        {"id": "cJZAZ5BR8Jo", "title": "LUNA - LOCAL GLAMOUR ECHOES"},
        {"id": "dQt0-XG5aj8", "title": "City Pop/Retro Aesthetics"},
        {"id": "x6-7-V-aD6g", "title": "Artisan Gold Chocolate"},
        {"id": "TeCIjiDNhyo", "title": "Tokyo Nights: City Pop Dream"},
        {"id": "5l_UfNu-DPA", "title": "LUNA - Neon Bloom"},
        {"id": "rQmcfl2n7a8", "title": "LUNA - Stardust Bloom"},
        {"id": "iHHUMfFG-rk", "title": "LUNA - Starlight Serenade"},
        {"id": "hk205UDU3s0", "title": "하얀 비치의 꿈 (Shorts)"},
        {"id": "ZUm1RGffGvg", "title": "City Dreams, Pinky Bloom"},
    ]

    uploader = YouTubeUploader()
    if not uploader.authenticate():
        print("[ERROR] YouTube 인증 실패")
        return

    print(f"=== 검은 화면 영상 삭제 ({len(black_screen_videos)}개) ===\n")

    deleted = 0
    for video in black_screen_videos:
        vid = video["id"]
        title = video["title"]

        print(f"삭제 중: [{vid}] {title}...")

        try:
            uploader.youtube.videos().delete(id=vid).execute()
            print(f"  [OK] 삭제 완료\n")
            deleted += 1

        except Exception as e:
            print(f"  [ERROR] 삭제 실패: {e}\n")

    print(f"완료: {deleted}/{len(black_screen_videos)}개 삭제됨")

if __name__ == "__main__":
    main()
