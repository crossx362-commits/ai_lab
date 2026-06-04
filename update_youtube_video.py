#!/usr/bin/env python3
"""YouTube 업로드 영상 메타데이터 업데이트 (SKILL 규칙 준수)"""
import sys
import os

sys.stdout.reconfigure(encoding='utf-8', errors='replace')
sys.path.insert(0, 'projects/ai-team/skills/루나_디렉터/tools')
sys.path.insert(0, 'projects/ai-team/skills/루나_디렉터')
sys.path.insert(0, 'projects/ai-team')

from src.youtube_uploader import YouTubeUploader
from _shared.ollama_client import chat as lm_chat

video_id = 'V_TohZxaZj4'
theme = '코나테'

# 새 제목 생성 (SKILL 규칙: 한국어 중심, 고정태그 금지)
title_prompt = """
음악 테마: 코나테, 그림자와의 만남

유튜브 시티팝 음악 제목을 1개 생성해.

규칙:
- 한국어 중심 (영어 단독 제목 금지, 필요시 최소 혼용만)
- LUNA, Official, MV, Music Video 등 고정 태그 절대 금지
- 감성적이고 클릭을 유도하는 제목
- lofi, 네온, study beats 등 클리셰 금지
- 5~8단어 이내

제목만 1줄 출력:
"""

new_title = lm_chat(title_prompt, task='', max_tokens=50)
if new_title:
    new_title = new_title.strip().split('\n')[0].strip().strip('"\'')
else:
    new_title = '그림자 속 도시의 밤'

print(f'새 제목: {new_title}')

# 설명문 생성 (필수 메타데이터 블록 포함)
description = f"""🎵 {new_title}

도시의 어스름한 밤, 그림자 사이로 스며드는 시티팝 선율.
80년대 레트로 감성과 현대 K-Pop의 에너지가 만나 특별한 밤을 만듭니다.

📌 추천 상황: 심야 드라이브, 혼자만의 시간, 도심 산책, 감성 충전이 필요한 순간

🎹 Genre / Era: Japanese City Pop × K-Pop Fusion (1980s Retro)
🎸 Instruments: Synthesizer, Electric Guitar, Bass, Drums
🎙️ Vocal Style: Smooth and Melodic K-Pop Vocals
✨ Theme: 코나테, 도시의 밤, 그림자 속 감성

youtube.com/@류나-l7h

#루나 #시티팝 #코나테 #AI음악 #레트로팝 #심야드라이브"""

# 태그 생성 (최소 20개)
tags = [
    '시티팝', 'citypop', 'LUNA', '루나', '드라이브bgm',
    'AI음악', '자동생성', '뮤직비디오', '코나테',
    'kpop', 'k팝시티팝', '레트로팝', '80년대',
    '심야드라이브', '도시의밤', '감성음악', 'retro pop',
    'japanese city pop', 'k-pop fusion', '시티팝bgm',
    '드라이브음악', '밤드라이브', '감성충전'
]

print(f'태그 수: {len(tags)}개')
print()

# YouTube API로 업데이트
uploader = YouTubeUploader()
if not uploader.authenticate():
    print('❌ 인증 실패')
    sys.exit(1)

print('🔄 메타데이터 업데이트 중...')

# 제목 + 설명 + 태그 업데이트
try:
    uploader.youtube.videos().update(
        part='snippet',
        body={
            'id': video_id,
            'snippet': {
                'title': new_title,
                'description': description,
                'tags': tags,
                'categoryId': '10'
            }
        }
    ).execute()

    print('✅ 업데이트 완료!')
    print(f'제목: {new_title}')
    print(f'설명: {len(description)} 글자')
    print(f'태그: {len(tags)}개')
    print(f'링크: https://youtu.be/{video_id}')
except Exception as e:
    print(f'❌ 업데이트 실패: {e}')
