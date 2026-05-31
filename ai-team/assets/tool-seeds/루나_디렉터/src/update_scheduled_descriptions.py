import os
import sys
import json
import re

_here_desc = os.path.dirname(os.path.abspath(__file__))
_root_desc = _here_desc
for _ in range(10):
    if os.path.exists(os.path.join(_root_desc, ".agent")):
        break
    _root_desc = os.path.dirname(_root_desc)
if _root_desc not in sys.path:
    sys.path.insert(0, _root_desc)

from _shared.env_loader import load_env
from youtube_uploader import YouTubeUploader

def extract_keyword_from_title(title: str) -> str:
    # Remove prefix "LUNA - " or "AI LEO - " or other emoji indicators
    keyword_part = title
    for prefix in ["LUNA - ", "AI LEO - ", "🎧 ", "🎵 "]:
        if keyword_part.startswith(prefix):
            keyword_part = keyword_part[len(prefix):]
            
    # Remove suffix like [Official Music Video], (80년대...), etc.
    # Take the portion before [ or (
    if " [" in keyword_part:
        keyword_part = keyword_part.split(" [")[0]
    elif "[" in keyword_part:
        keyword_part = keyword_part.split("[")[0]
    if " (" in keyword_part:
        keyword_part = keyword_part.split(" (")[0]
    elif "(" in keyword_part:
        keyword_part = keyword_part.split("(")[0]
    if " | " in keyword_part:
        keyword_part = keyword_part.split(" | ")[0]
        
    return keyword_part.strip()

def generate_description_from_keyword(keyword: str):
    kw_lower = keyword.lower()
    
    # Default instrument session / vocals / theme
    genre = "Japanese City Pop"
    instruments = "Slap bass, electric piano (DX7), brass synthesizer, vintage drum machine"
    vocals = "Sweet female lead vocals, clean voice"
    theme_topic = "Late night drive, urban city lights romance"
    
    if "espresso" in kw_lower or "morning" in kw_lower:
        mood_desc = "따뜻한 아침 햇살이 비치는 창가, 갓 추출한 에스프레소의 향긋함과 함께 설레는 하루를 시작하는 기분.\n80s 감성을 담은 류나의 모닝 시티팝 음악입니다."
        rec_situations = "아침 커피 타임 / 등교 및 출근길 / 아침 음악 / 독서 시간"
        genre_tag = "Soft Jazzy City Pop / Acoustic Synth"
        hashtags = "#시티팝 #citypop #모닝음악 #커피음악 #류나 #80s #jpop #감성음악 #힐링음악 #출근길음악"
    elif "rose" in kw_lower or "skincare" in kw_lower or "dewy" in kw_lower:
        mood_desc = "장미 꽃잎에 맺힌 촉촉한 이슬처럼, 하루의 피로를 부드럽게 씻어내고 나만의 휴식을 취하는 기분.\n80s 감성을 담은 류나의 실키 앰비언트 시티팝 음악입니다."
        rec_situations = "휴식 및 스킨케어 / 요가 및 명상 / 샤워 시간 / 조용한 밤"
        genre_tag = "Dreamy Ambient City Pop / Relaxing Chillwave"
        hashtags = "#시티팝 #citypop #릴랙싱음악 #스킨케어 #류나 #80s #ambient #감성음악 #힐링음악 #휴식"
    elif "perfume" in kw_lower or "shibuya" in kw_lower or "midnight" in kw_lower or "neon" in kw_lower:
        mood_desc = "은은하게 퍼지는 향수 향기와 함께 화려한 네온빛 도시 야경 속을 질주하는 기분.\n80s 감성을 담은 류나의 센슈얼 심야 시티팝 음악입니다."
        rec_situations = "심야 드라이브 / 밤거리 산책 / 세련된 무드 연출 / 퇴근길"
        genre_tag = "Sensual Late-Night City Pop / R&B Synth"
        hashtags = "#시티팝 #citypop #밤드라이브 #퇴근길음악 #류나 #80s #nocturnal #감성음악 #새벽감성 #도시야경"
    elif "chocolate" in kw_lower or "sweet" in kw_lower:
        mood_desc = "달콤하게 녹아내리는 수제 초콜릿처럼, 노을빛 아래 연인과 사랑을 속삭이는 기분.\n80s 감성을 담은 류나의 스위트 R&B 시티팝 음악입니다."
        rec_situations = "연인과의 데이트 / 노을빛 산책 / 디저트 타임 / 달콤한 휴식"
        genre_tag = "Groovy City Pop R&B / Sweet Retro Soul"
        hashtags = "#시티팝 #citypop #데이트음악 #달콤한노래 #류나 #80s #rnb #감성음악 #로맨틱시티팝 #디저트음악"
    elif "water" in kw_lower or "glacial" in kw_lower or "beach" in kw_lower or "disco" in kw_lower:
        mood_desc = "무더운 뜨거운 태양 아래, 가슴 속을 얼릴 듯 짜릿하고 시원한 탄산 음료 한 모금의 기분.\n80s 감성을 담은 류나의 서머 댄스 시티팝 음악입니다."
        rec_situations = "여름 휴가길 / 드라이브 / 홈파티 / 리프레시 타임"
        genre_tag = "Breezy Summer City Pop / Energetic Disco Synth"
        hashtags = "#시티팝 #citypop #여름시티팝 #드라이브음악 #류나 #80s #disco #청량한음악 #신나는음악 #여가길"
    elif "spring" in kw_lower or "cherry" in kw_lower or "bloom" in kw_lower or "봄" in kw_lower:
        mood_desc = "벚꽃이 눈부시게 흩날리는 봄날 오후, 창문을 열고 바람을 느끼며 드라이브하고 싶은 기분.\n80s 감성을 담은 류나의 봄 시티팝 음악입니다."
        rec_situations = "봄날 드라이브 / 봄 나들이 / 기분 전환 / 설레는 날"
        genre_tag = "Spring City Pop / J-Pop / 80s Synth"
        hashtags = "#시티팝 #citypop #봄음악 #드라이브음악 #류나 #80s #jpop #감성음악 #봄시티팝 #설레는음악"
    else:
        # Fallback keyword-specific dynamic description
        clean_word = keyword.replace("Retro ", "").replace("Japanese ", "").replace("City Pop", "").strip()
        if not clean_word:
            clean_word = "City Night"
        mood_desc = f"도시의 네온사인 불빛 사이로 '{clean_word}'의 짙은 여운이 어리는 밤, 감각적인 비트와 함께 감성을 채워주는 기분.\n80s 감성을 담은 류나의 '{clean_word}' 테마 시티팝 음악입니다."
        rec_situations = f"야간 드라이브 / 밤거리 산책 / 나만의 휴식 시간 / 감성 충전이 필요할 때"
        genre_tag = "Nostalgic Retro City Pop / 80s Synthwave"
        
        safe_kw = re.sub(r'[^a-zA-Z0-9가-힣]', '', clean_word).lower()
        hashtags = f"#시티팝 #citypop #류나 #80s #retro #감성음악 #밤드라이브 #{safe_kw}음악"
        
    return mood_desc, rec_situations, genre_tag, hashtags, genre, instruments, vocals, theme_topic

def main():
    load_env()
    
    # 프로젝트 루트 동적 검색하여 새 토큰 파일 경로 지정
    here = os.path.dirname(os.path.abspath(__file__))
    root = here
    for _ in range(6):
        if os.path.isdir(os.path.join(root, ".agent")):
            break
        root = os.path.dirname(root)
    token_file = os.path.join(root, ".agent", "credentials", "youtube_token_update.pickle")
    
    uploader = YouTubeUploader(token_file=token_file)
    uploader.scopes = [
        "https://www.googleapis.com/auth/youtube",
        "https://www.googleapis.com/auth/youtube.upload",
        "https://www.googleapis.com/auth/youtube.readonly"
    ]
    
    if not uploader.authenticate():
        print("❌ [Error] 유튜브 API 인증에 실패했습니다.")
        sys.exit(1)
        
    youtube = uploader.youtube
    
    # 1. 로컬 업로드 기록에서 비디오 ID 읽어오기
    video_ids = []
    
    history_path = os.path.join(root, ".agent", "memory", "upload_history.json")
    if os.path.exists(history_path):
        try:
            with open(history_path, "r", encoding="utf-8") as f:
                history = json.load(f)
                for item in history:
                    vid = item.get("metadata", {}).get("video_id")
                    if vid and vid not in video_ids:
                        video_ids.append(vid)
        except Exception as e:
            print(f"⚠️ [Warning] 로컬 히스토리 로드 실패: {e}")

    print(f"🔍 [Info] 로컬 기록에서 발견한 Video ID: {video_ids}")
    
    # 2. 채널의 최근 50개 업로드 목록 가져오기
    try:
        channels_response = youtube.channels().list(
            mine=True,
            part="contentDetails"
        ).execute()
        
        for channel in channels_response.get("items", []):
            uploads_list_id = channel["contentDetails"]["relatedPlaylists"]["uploads"]
            print(f"🔍 [Info] 업로드 플레이리스트 ID: {uploads_list_id}")
            
            playlist_items_response = youtube.playlistItems().list(
                playlistId=uploads_list_id,
                part="snippet",
                maxResults=50
            ).execute()
            
            for item in playlist_items_response.get("items", []):
                vid = item["snippet"]["resourceId"]["videoId"]
                if vid not in video_ids:
                    video_ids.append(vid)
    except Exception as e:
        print(f"⚠️ [Warning] 최근 업로드 리스트 조회 실패: {e}")

    if not video_ids:
        print("ℹ️ [Info] 업데이트할 동영상이 감지되지 않았습니다.")
        return

    print(f"🚀 [Info] 총 {len(video_ids)}개의 동영상을 업데이트합니다: {video_ids}")

    for vid in video_ids:
        try:
            # 동영상 정보 가져오기
            videos_response = youtube.videos().list(
                id=vid,
                part="snippet,status"
            ).execute()
            
            items = videos_response.get("items", [])
            if not items:
                print(f"⚠️ [Warning] 동영상 {vid}를 찾을 수 없습니다.")
                continue
                
            video = items[0]
            snippet = video["snippet"]
            status = video["status"]
            title = snippet["title"]
            
            # 예약 혹은 공개 상태 체크
            privacy = status.get("privacyStatus")
            is_scheduled = "publishAt" in status
            
            print(f"\n──────────────────────────────────────────────────")
            print(f"🎬 제목: {title} (ID: {vid}, 상태: {privacy})")
            
            # 제목에서 키워드 추출
            keyword = extract_keyword_from_title(title)
            print(f"🔑 추출된 키워드: '{keyword}'")
            
            # 새로운 포맷의 메타데이터 생성
            mood_desc, rec_situations, genre_tag, hashtags, genre, instruments, vocals, theme_topic = generate_description_from_keyword(keyword)
            
            # 최종 설명란 조립
            new_description = (
                f"🎵 LUNA - {keyword}\n\n"
                f"{mood_desc}\n\n"
                f"📌 추천 상황: {rec_situations}\n\n"
                f"🎹 장르/시대: {genre_tag}\n"
                f"🎸 세션 악기: {instruments}\n"
                f"🎙️ 보컬 스타일: {vocals}\n"
                f"✨ 곡의 테마: {theme_topic}\n\n"
                f"{hashtags}"
            )
            
            print("📝 [Generated Description Preview]")
            print(new_description)
            
            # 유튜브 비디오 스니펫 및 태그 업데이트
            base_tags = ["시티팝", "city pop", "LUNA", "루나", "AI LUNA", "AI 음악", "80s retro", "citypop BGM", "감성 음악"]
            extracted_tags = [h.replace("#", "") for h in hashtags.split(" ") if h.startswith("#")]
            for t in extracted_tags:
                if t not in base_tags:
                    base_tags.append(t)
            
            snippet["tags"] = base_tags
            snippet["description"] = new_description
            
            update_body = {
                "id": vid,
                "snippet": snippet
            }
            if "categoryId" not in snippet:
                snippet["categoryId"] = "10" # Music
                
            youtube.videos().update(
                part="snippet",
                body=update_body
            ).execute()
            
            print(f"✅ [Success] Video ID: {vid} 업데이트 완료!")
            
        except Exception as e:
            print(f"❌ [Error] Video ID: {vid} 업데이트 실패: {e}")

if __name__ == "__main__":
    import io
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding="utf-8", errors="replace")
    main()
