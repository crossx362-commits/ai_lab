#!/usr/bin/env python3
"""YouTube 채널의 모든 영상 메타데이터를 SKILL 규칙에 맞게 일괄 업데이트"""
import sys
import os
import time

sys.stdout.reconfigure(encoding='utf-8', errors='replace')
sys.path.insert(0, 'projects/ai-team/skills/루나_디렉터/tools')
sys.path.insert(0, 'projects/ai-team/skills/루나_디렉터')
sys.path.insert(0, 'projects/ai-team')

from src.youtube_uploader import YouTubeUploader
from _shared.ollama_client import chat as lm_chat

def get_all_uploaded_videos(uploader):
    """채널의 모든 업로드 영상 정보 가져오기"""
    try:
        # 채널 정보 가져오기
        ch = uploader.youtube.channels().list(
            part="contentDetails",
            mine=True
        ).execute()

        upload_playlist_id = ch["items"][0]["contentDetails"]["relatedPlaylists"]["uploads"]

        videos = []
        next_page_token = None

        while True:
            # 업로드 재생목록에서 영상 목록 가져오기
            playlist_response = uploader.youtube.playlistItems().list(
                part="snippet",
                playlistId=upload_playlist_id,
                maxResults=50,
                pageToken=next_page_token
            ).execute()

            for item in playlist_response.get("items", []):
                video_id = item["snippet"]["resourceId"]["videoId"]
                title = item["snippet"]["title"]
                videos.append({
                    "video_id": video_id,
                    "title": title,
                    "published_at": item["snippet"]["publishedAt"]
                })

            next_page_token = playlist_response.get("nextPageToken")
            if not next_page_token:
                break

        return videos
    except Exception as e:
        print(f"❌ 영상 목록 가져오기 실패: {e}")
        return []


def generate_new_title(old_title):
    """SKILL 규칙에 맞는 새 제목 생성"""
    prompt = f"""
기존 제목: {old_title}

위 제목을 개선하여 유튜브 시티팝 음악 제목을 1개 생성해.

규칙:
- 한국어 중심 (영어 단독 제목 금지, 필요시 최소 혼용만)
- LUNA, Official, MV, Music Video 등 고정 태그 절대 금지
- 감성적이고 클릭을 유도하는 제목
- lofi, 네온, study beats 등 클리셰 금지
- 5~8단어 이내
- 기존 제목의 감성과 테마 유지

제목만 1줄 출력:
"""

    result = lm_chat(prompt, task='', max_tokens=50)
    if result:
        new_title = result.strip().split('\n')[0].strip().strip('"\'')
        # 기본 검증
        banned = ['LUNA', 'Official', 'MV', 'Music Video', 'lofi', 'lo-fi']
        if not any(b.lower() in new_title.lower() for b in banned):
            return new_title

    # 폴백: 기존 제목에서 금지 태그만 제거
    clean_title = old_title
    for tag in ['LUNA', 'Official', 'MV', 'Music Video', '[Official]', '(Official)']:
        clean_title = clean_title.replace(tag, '').strip()
    return clean_title or old_title


def generate_description(title):
    """SKILL 규칙에 맞는 설명문 생성"""
    prompt = f"""
제목: {title}

위 제목의 시티팝 음악을 위한 YouTube 설명문을 작성해.

규칙:
- 분위기와 감성을 담은 2~3문장
- 📌 추천상황 1줄
- 필수 메타데이터 블록 포함
- 타임라인(00:00) 금지
- 해시태그 5~8개 (10개 이하)

아래 형식으로 작성:

🎵 [제목]

[감성적인 설명 2~3문장]

📌 추천 상황: [상황 나열]

🎹 Genre / Era: Japanese City Pop × K-Pop Fusion (1980s Retro)
🎸 Instruments: Synthesizer, Electric Guitar, Bass, Drums
🎙️ Vocal Style: Smooth and Melodic K-Pop Vocals
✨ Theme: [테마]

youtube.com/@류나-l7h

[해시태그 5~8개]
"""

    result = lm_chat(prompt, task='', max_tokens=400)
    if result and len(result) > 100:
        return result.strip()

    # 폴백 설명문
    return f"""🎵 {title}

80년대 시티팝 감성과 현대 K-Pop의 에너지가 만나는 특별한 순간.
도시의 밤을 수놓는 감성적인 멜로디가 당신을 기다립니다.

📌 추천 상황: 심야 드라이브, 혼자만의 시간, 감성 충전

🎹 Genre / Era: Japanese City Pop × K-Pop Fusion (1980s Retro)
🎸 Instruments: Synthesizer, Electric Guitar, Bass, Drums
🎙️ Vocal Style: Smooth and Melodic K-Pop Vocals
✨ Theme: 도시의 밤, 레트로 감성

youtube.com/@류나-l7h

#루나 #시티팝 #AI음악 #레트로팝 #심야드라이브"""


def update_video(uploader, video_id, new_title, new_description):
    """영상 메타데이터 업데이트"""
    tags = [
        '시티팝', 'citypop', 'LUNA', '루나', '드라이브bgm',
        'AI음악', '자동생성', '뮤직비디오',
        'kpop', 'k팝시티팝', '레트로팝', '80년대',
        '심야드라이브', '도시의밤', '감성음악', 'retro pop',
        'japanese city pop', 'k-pop fusion', '시티팝bgm',
        '드라이브음악', '밤드라이브', '감성충전'
    ]

    try:
        uploader.youtube.videos().update(
            part='snippet',
            body={
                'id': video_id,
                'snippet': {
                    'title': new_title,
                    'description': new_description,
                    'tags': tags,
                    'categoryId': '10'
                }
            }
        ).execute()
        return True
    except Exception as e:
        print(f"  ❌ 업데이트 실패: {e}")
        return False


def main():
    print("=" * 70)
    print("  YouTube 채널 전체 영상 메타데이터 업데이트")
    print("  SKILL 규칙 준수 (한국어 중심, 메타데이터 블록, 해시태그 10개 이하)")
    print("=" * 70)
    print()

    # YouTube 인증
    uploader = YouTubeUploader()
    if not uploader.authenticate():
        print('❌ YouTube 인증 실패')
        return

    print('✅ YouTube 인증 성공')
    print()

    # 모든 영상 목록 가져오기
    print('📋 업로드 영상 목록 가져오는 중...')
    videos = get_all_uploaded_videos(uploader)

    if not videos:
        print('❌ 영상이 없습니다.')
        return

    print(f'✅ 총 {len(videos)}개 영상 발견')
    print()

    # 각 영상 업데이트
    success_count = 0
    fail_count = 0

    for idx, video in enumerate(videos, 1):
        video_id = video['video_id']
        old_title = video['title']

        print(f"[{idx}/{len(videos)}] 처리 중...")
        print(f"  기존 제목: {old_title}")

        # 새 제목 생성
        new_title = generate_new_title(old_title)
        print(f"  새 제목: {new_title}")

        # 새 설명문 생성
        new_description = generate_description(new_title)

        # 업데이트
        if update_video(uploader, video_id, new_title, new_description):
            print(f"  ✅ 업데이트 완료: https://youtu.be/{video_id}")
            success_count += 1
        else:
            fail_count += 1

        print()

        # API 할당량 보호 (1초 대기)
        if idx < len(videos):
            time.sleep(1)

    # 결과 요약
    print("=" * 70)
    print("  업데이트 완료")
    print("=" * 70)
    print(f"✅ 성공: {success_count}개")
    print(f"❌ 실패: {fail_count}개")
    print()


if __name__ == "__main__":
    main()
