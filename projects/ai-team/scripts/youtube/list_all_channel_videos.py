import os
import sys
import pickle
from google.auth.transport.requests import Request
from googleapiclient.discovery import build

# UTF-8 출력 강제 및 에러 대체
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
if hasattr(sys.stderr, 'reconfigure'):
    sys.stderr.reconfigure(encoding='utf-8', errors='replace')

_here = os.path.dirname(os.path.abspath(__file__))
token_file = os.path.join(_here, "projects", "ai-team", "skills", "루나_디렉터", "tools", "youtube_token.pickle")

if not os.path.exists(token_file):
    print("Token file not found:", token_file)
    sys.exit(1)

with open(token_file, "rb") as f:
    creds = pickle.load(f)

if creds.expired and creds.refresh_token:
    creds.refresh(Request())

youtube = build("youtube", "v3", credentials=creds)

try:
    ch = youtube.channels().list(part="contentDetails", mine=True).execute()
    pl_id = ch["items"][0]["contentDetails"]["relatedPlaylists"]["uploads"]
    
    videos = []
    page_token = None
    while True:
        kwargs = {
            "part": "snippet",
            "playlistId": pl_id,
            "maxResults": 50
        }
        if page_token:
            kwargs["pageToken"] = page_token
            
        pl_res = youtube.playlistItems().list(**kwargs).execute()
        
        for item in pl_res.get("items", []):
            vid = item["snippet"]["resourceId"]["videoId"]
            title = item["snippet"]["title"]
            videos.append({"id": vid, "title": title})
            
        page_token = pl_res.get("nextPageToken")
        if not page_token:
            break
            
    print("=== Found Videos ===")
    for v in videos:
        # 안전한 출력을 위해 이모지나 특수문자 에러 방지
        safe_title = v["title"].encode('utf-8', errors='ignore').decode('utf-8')
        print(f"ID: {v['id']} | Title: {safe_title}")
except Exception as e:
    print("Error:", e)
