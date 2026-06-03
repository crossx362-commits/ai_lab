import os
import pickle
import json
import datetime
import sys
import requests

# ── 프로젝트 루트 탐색 ────────────────────────────────────────────────────────
_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, "reports")):
        break
    _root = os.path.dirname(_root)
_ai_team_root = os.path.join(_root, "projects", "ai-team")

sys.path.insert(0, _root)
sys.path.insert(0, _ai_team_root)
from _shared.telegram_notifier import send_telegram_message
from _shared.env_loader import load_env as _load_env
from _shared.ollama_client import chat as lm_chat, is_available as lm_available

try:
    from google_auth_oauthlib.flow import InstalledAppFlow
    from google.auth.transport.requests import Request
    from googleapiclient.discovery import build
    _GOOGLE_API_AVAILABLE = True
except ImportError:
    print("⚠️ google-api-python-client 미설치 — YouTube 수사 건너뜁니다.")
    _GOOGLE_API_AVAILABLE = False

SCOPES = [
    "https://www.googleapis.com/auth/youtube.readonly",
    "https://www.googleapis.com/auth/youtube.force-ssl",
    "https://www.googleapis.com/auth/spreadsheets",
]

CLIENT_SECRET = os.path.join(_root, ".agent", "credentials", "client_secret.json")
TOKEN_FILE = os.path.join(os.path.dirname(__file__), "token_gyeongsu.pickle")
SPREADSHEET_ID = "1XEQ8oXKISHU7Y1Kw_6-c7em5o1Afq3KcjF5mtBKvJEY"
SHEET_NAME = "블랙리스트"

# 악플 감지 키워드 (기본 필터)
HATE_KEYWORDS = [
    "죽어", "꺼져", "쓰레기", "ㅂㅅ", "병신", "찐따", "한남", "한녀",
    "보지", "자지", "시발", "씨발", "개새끼", "니애미", "느금마",
    "hate", "kill", "loser", "idiot", "stupid", "trash",
    "구독취소", "안봄", "최악", "역겨워", "토나와",
]

def authenticate():
    creds = None
    if os.path.exists(TOKEN_FILE):
        with open(TOKEN_FILE, "rb") as f:
            creds = pickle.load(f)

    if not creds or not creds.valid:
        if creds and creds.expired and creds.refresh_token:
            creds.refresh(Request())
        else:
            flow = InstalledAppFlow.from_client_secrets_file(CLIENT_SECRET, SCOPES)
            creds = flow.run_local_server(port=0)
        with open(TOKEN_FILE, "wb") as f:
            pickle.dump(creds, f)

    youtube = build("youtube", "v3", credentials=creds)
    sheets = build("sheets", "v4", credentials=creds)
    return youtube, sheets




def scan_instagram_forensics(sheets, max_media=5) -> dict:
    """인스타그램 최근 게시물 댓글 및 좋아요 수사"""
    _load_env()
    account_id = os.getenv("INSTAGRAM_ACCOUNT_ID")
    token = os.getenv("INSTAGRAM_ACCESS_TOKEN")
    
    result_data = {
        "scanned_media_count": 0,
        "total_likes": 0,
        "total_comments_scanned": 0,
        "hate_count": 0
    }
    
    if not account_id or not token:
        print("⚠️ [인스타 수사] INSTAGRAM_ACCOUNT_ID 또는 INSTAGRAM_ACCESS_TOKEN이 없어 스킵합니다.")
        return result_data

    print(f"\n🔍 인스타그램 계정 [{account_id}] 수사 개시!")
    
    # 1. 최근 미디어 목록 조회
    url = f"https://graph.instagram.com/v23.0/{account_id}/media"
    params = {
        "fields": "id,caption,media_type,like_count,comments_count",
        "access_token": token,
        "limit": max_media
    }
    
    try:
        res = requests.get(url, params=params).json()
        if "error" in res:
            print(f"  ❌ 인스타 미디어 조회 에러: {res['error'].get('message')}")
            return result_data
        
        media_list = res.get("data", [])
        result_data["scanned_media_count"] = len(media_list)
        
        hate_rows = []
        
        for media in media_list:
            media_id = media.get("id")
            caption = media.get("caption", "(내용 없음)")
            likes = int(media.get("like_count", 0))
            comments_count = int(media.get("comments_count", 0))
            
            result_data["total_likes"] += likes
            print(f"\n📸 게시물 [{caption[:30]}...] 댓글 스캔 중... (좋아요: {likes}개, 댓글: {comments_count}개)")
            
            if comments_count == 0:
                continue
                
            # 2. 미디어별 댓글 목록 조회
            comments_url = f"https://graph.instagram.com/v23.0/{media_id}/comments"
            c_params = {
                "fields": "id,text,username,timestamp",
                "access_token": token
            }
            c_res = requests.get(comments_url, params=c_params).json()
            if "error" in c_res:
                print(f"  ⚠️ 댓글 조회 실패: {c_res['error'].get('message')}")
                continue
                
            for comment in c_res.get("data", []):
                text = comment.get("text", "")
                author_name = comment.get("username", "unknown")
                result_data["total_comments_scanned"] += 1
                
                if is_hate(text):
                    summary = text[:80] + ("..." if len(text) > 80 else "")
                    row = [
                        datetime.datetime.now().strftime("%Y-%m-%d %H:%M"),
                        comment.get("id", "unknown"),
                        author_name,
                        summary,
                        f"https://instagram.com/p/{media_id}",
                        f"[Instagram] {caption[:40]}"
                    ]
                    hate_rows.append(row)
                    print(f"  🚨 인스타 악플 감지: {author_name} — {summary[:40]}...")
                    
        result_data["hate_count"] = len(hate_rows)
        print(f"\n📊 인스타 총 {result_data['total_comments_scanned']}개 댓글 스캔 완료 | 악플 {len(hate_rows)}건 적발")
        
        if hate_rows:
            ensure_sheet(sheets)
            sheets.spreadsheets().values().append(
                spreadsheetId=SPREADSHEET_ID,
                range=f"{SHEET_NAME}!A2",
                valueInputOption="RAW",
                insertDataOption="INSERT_ROWS",
                body={"values": hate_rows},
            ).execute()
            print(f"📋 인스타그램 악플 시트 박제 완료! ({len(hate_rows)}건)")
            
    except Exception as e:
        print(f"  ❌ 인스타 수사 진행 중 에러 발생: {e}")
        
    return result_data


def ensure_sheet(sheets):
    """블랙리스트 시트가 없으면 생성하고 헤더를 추가합니다."""
    meta = sheets.spreadsheets().get(spreadsheetId=SPREADSHEET_ID).execute()
    existing = [s["properties"]["title"] for s in meta["sheets"]]

    if SHEET_NAME not in existing:
        sheets.spreadsheets().batchUpdate(
            spreadsheetId=SPREADSHEET_ID,
            body={"requests": [{"addSheet": {"properties": {"title": SHEET_NAME}}}]},
        ).execute()
        sheets.spreadsheets().values().update(
            spreadsheetId=SPREADSHEET_ID,
            range=f"{SHEET_NAME}!A1",
            valueInputOption="RAW",
            body={"values": [["날짜", "악플러 ID", "악플러 닉네임", "내용 요약", "원본 링크", "영상 제목"]]},
        ).execute()
        print(f"✅ '{SHEET_NAME}' 시트 생성 완료")


def is_hate(text: str) -> bool:
    """Ollama 1순위 → 키워드 폴백으로 악플 판별."""
    # 1순위: Ollama AI 판별
    try:
        prompt = (
            f"다음 댓글이 악성 댓글(혐오, 욕설, 명예훼손, 심각한 비난)인지 판단해.\n"
            f"댓글: {text[:200]}\n"
            "오직 'YES' 또는 'NO'만 대답해. 다른 말 금지."
        )
        result = lm_chat(prompt, max_tokens=50, temperature=0.1) if lm_available() else None
        if result and result.strip().upper().startswith("YES"):
            return True
        if result and result.strip().upper().startswith("NO"):
            return False
    except Exception:
        pass
    # 폴백: 키워드 매칭
    t = text.lower()
    return any(kw in t for kw in HATE_KEYWORDS)


def scan_comments(youtube, sheets, max_videos=5):
    # 내 채널 정보
    ch = youtube.channels().list(part="snippet,contentDetails", mine=True).execute()
    ch_title = ch["items"][0]["snippet"]["title"]
    uploads_pl = ch["items"][0]["contentDetails"]["relatedPlaylists"]["uploads"]
    print(f"🔍 채널 [{ch_title}] 댓글 수사 개시!")

    # 최근 영상 목록
    pl = youtube.playlistItems().list(
        part="contentDetails,snippet", playlistId=uploads_pl, maxResults=max_videos
    ).execute()

    hate_rows = []
    total_scanned = 0

    for item in pl["items"]:
        vid_id = item["contentDetails"]["videoId"]
        vid_title = item["snippet"]["title"]
        vid_url = f"https://youtu.be/{vid_id}"
        print(f"\n📹 [{vid_title[:40]}] 댓글 스캔 중...")

        try:
            comments_resp = youtube.commentThreads().list(
                part="snippet", videoId=vid_id, maxResults=100, order="time"
            ).execute()
        except Exception as e:
            print(f"  ⚠️ 댓글 조회 실패: {e}")
            continue

        for c in comments_resp.get("items", []):
            top = c["snippet"]["topLevelComment"]["snippet"]
            text = top.get("textOriginal", "")
            author_id = top.get("authorChannelId", {}).get("value", "unknown")
            author_name = top.get("authorDisplayName", "알 수 없음")
            total_scanned += 1

            if is_hate(text):
                summary = text[:80] + ("..." if len(text) > 80 else "")
                row = [
                    datetime.datetime.now().strftime("%Y-%m-%d %H:%M"),
                    author_id,
                    author_name,
                    summary,
                    vid_url,
                    vid_title[:50],
                ]
                hate_rows.append(row)
                print(f"  🚨 악플 감지: {author_name} — {summary[:40]}...")

    print(f"\n📊 총 {total_scanned}개 댓글 스캔 완료 | 악플 {len(hate_rows)}건 적발")

    if hate_rows:
        ensure_sheet(sheets)
        sheets.spreadsheets().values().append(
            spreadsheetId=SPREADSHEET_ID,
            range=f"{SHEET_NAME}!A2",
            valueInputOption="RAW",
            insertDataOption="INSERT_ROWS",
            body={"values": hate_rows},
        ).execute()
        print(f"📋 구글 시트 박제 완료! ({len(hate_rows)}건)")
        print(f"🔗 https://docs.google.com/spreadsheets/d/{SPREADSHEET_ID}")
    else:
        print("✅ 악플 없음. 채널이 건강합니다!")

    return len(hate_rows)


def main(max_videos=5):
    _load_env()
    print("👮 경수 사이버수사대 — 악플 및 채널 포렌식 시작!\n")
    
    youtube, sheets = None, None
    yt_count = 0
    
    try:
        if _GOOGLE_API_AVAILABLE:
            youtube, sheets = authenticate()
            # 1. YouTube 수사
            yt_count = scan_comments(youtube, sheets, max_videos=max_videos)
        else:
            print("⚠️ [YouTube 수사 스킵] google-api-python-client 미설치")
    except Exception as e:
        print(f"⚠️ [YouTube 수사 스킵] 구글 API 인증 실패 또는 누락: {e}")
        sheets = None
    
    # 2. Instagram 수사
    insta_info = scan_instagram_forensics(sheets, max_media=max_videos)
    
    # 3. 통합 결과 보고서 작성 및 발송
    report_lines = [
        "👮 [경수 → 비서] 사이버수사대 일일 수사 보고 완료!",
        "",
    ]
    
    if youtube:
        report_lines.extend([
            "📺 YouTube 수사 현황",
            f"- 스캔 대상 영상: 최근 {max_videos}개",
            f"- 적발된 악플러: {yt_count}명 (구글 시트 박제 완료)",
            ""
        ])
    else:
        report_lines.extend([
            "📺 YouTube 수사 현황",
            "- ⚠️ 구글 API 인증 정보 없음 (스캔 건너뜀)",
            ""
        ])
        
    report_lines.extend([
        "📸 Instagram 수사 현황",
        f"- 스캔 대상 포스팅: 최근 {insta_info['scanned_media_count']}개",
        f"- 스캔된 총 댓글: {insta_info['total_comments_scanned']}개",
        f"- 누적 좋아요 수: {insta_info['total_likes']}개",
        f"- 적발된 악플러: {insta_info['hate_count']}명 (구글 시트 박제 완료)",
        ""
    ])
    
    if yt_count > 0 or insta_info['hate_count'] > 0:
        report_lines.append("🚨 경고: 악성 유저 박제 처리를 마쳤으니 시트 확인 부탁드립니다.")
    else:
        report_lines.append("✅ 오늘도 채널 전체가 건강하고 클린합니다. 이상 무!")
        
    report_lines.append(f"\n🔗 수사 데이터 시트: https://docs.google.com/spreadsheets/d/{SPREADSHEET_ID}")
    
    send_telegram_message("\n".join(report_lines))


if __name__ == "__main__":
    import io
    import sys
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding="utf-8", errors="replace")
    main()
