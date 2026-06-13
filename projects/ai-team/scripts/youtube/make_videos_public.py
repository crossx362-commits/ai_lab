"""루나 영상들을 비공개 → 공개(unlisted)로 변경"""
import sys
import os

# AI_TEAM 경로 추가
ai_team_path = os.path.join(os.path.dirname(__file__), "projects", "ai-team")
sys.path.insert(0, ai_team_path)

from skills.루나_디렉터.tools.src.youtube_uploader import YouTubeUploader

def make_videos_public():
    uploader = YouTubeUploader()
    if not uploader.authenticate():
        print("❌ YouTube 인증 실패")
        return

    print("📋 최근 업로드된 영상 목록 조회 중...")

    # 채널의 업로드 재생목록 가져오기
    ch = uploader.youtube.channels().list(part='contentDetails', mine=True).execute()
    pl_id = ch['items'][0]['contentDetails']['relatedPlaylists']['uploads']

    # 최근 영상 10개 가져오기
    pl_res = uploader.youtube.playlistItems().list(
        part='snippet,status',
        playlistId=pl_id,
        maxResults=10
    ).execute()

    print(f"\n📺 총 {len(pl_res.get('items', []))}개 영상 발견\n")

    updated = 0
    for item in pl_res.get('items', []):
        vid = item['snippet']['resourceId']['videoId']
        title = item['snippet']['title']
        current_privacy = item.get('status', {}).get('privacyStatus', 'unknown')

        print(f"🎬 {title[:50]}...")
        print(f"   현재 상태: {current_privacy}")

        if current_privacy == 'private':
            try:
                # 영상 정보 가져오기
                video_res = uploader.youtube.videos().list(
                    part='snippet,status',
                    id=vid
                ).execute()

                if not video_res.get('items'):
                    print(f"   ⚠️  영상을 찾을 수 없음")
                    continue

                video = video_res['items'][0]
                video['status']['privacyStatus'] = 'public'  # public 또는 unlisted

                # 업데이트
                uploader.youtube.videos().update(
                    part='status',
                    body={'id': vid, 'status': video['status']}
                ).execute()

                print(f"   ✅ 공개(public)로 변경됨")
                updated += 1

            except Exception as e:
                print(f"   ❌ 변경 실패: {e}")
        else:
            print(f"   ℹ️  이미 공개 상태 (건너뜀)")

        print()

    print(f"\n{'='*60}")
    print(f"완료: {updated}개 영상 공개로 변경됨")
    print(f"{'='*60}")

if __name__ == "__main__":
    make_videos_public()
