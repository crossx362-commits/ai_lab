"""검은 화면 루나 영상을 비공개 처리하는 스크립트 (삭제 대신)"""
import sys
import os

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "projects", "ai-team"))

from _shared.env_loader import load_env
load_env()

from skills.루나_디렉터.tools.src.youtube_uploader import YouTubeUploader

def main():
    uploader = YouTubeUploader()
    if not uploader.authenticate():
        print("[ERROR] YouTube 인증 실패")
        return

    # 채널의 모든 영상 가져오기
    ch = uploader.youtube.channels().list(part='contentDetails', mine=True).execute()
    pl_id = ch['items'][0]['contentDetails']['relatedPlaylists']['uploads']

    pl_res = uploader.youtube.playlistItems().list(
        part='snippet,status',
        playlistId=pl_id,
        maxResults=50
    ).execute()

    print("=== 루나 채널 영상 관리 ===\n")
    print("현재 공개 상태 영상:\n")

    public_count = 0
    for item in pl_res.get('items', []):
        vid = item['snippet']['resourceId']['videoId']
        title = item['snippet']['title']
        privacy = item.get('status', {}).get('privacyStatus', 'unknown')

        if privacy == 'public':
            public_count += 1
            print(f"{public_count}. [{vid}] {title[:60]}")

    print(f"\n총 {public_count}개의 공개 영상 확인됨")
    print("\n앞으로는 문제 영상을 삭제하지 않고 '비공개(private)'로 처리합니다.")
    print("비공개 처리 시 복구가 가능하며, 나중에 다시 공개할 수 있습니다.\n")

if __name__ == "__main__":
    main()
