#!/usr/bin/env python3
"""
reauth_youtube.py -- Luna YouTube OAuth token reissue script
A browser window will open automatically. Log in to your Google account and approve.
"""
import os
import sys
import pickle

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
if hasattr(sys.stderr, 'reconfigure'):
    sys.stderr.reconfigure(encoding='utf-8', errors='replace')

_here = os.path.dirname(os.path.abspath(__file__))
_ai_team_root = os.path.abspath(os.path.join(_here, "..", ".."))
if _ai_team_root not in sys.path:
    sys.path.insert(0, _ai_team_root)

from _shared.env import load_env
load_env()

from google_auth_oauthlib.flow import InstalledAppFlow
from googleapiclient.discovery import build

SCOPES = [
    "https://www.googleapis.com/auth/youtube",
    "https://www.googleapis.com/auth/youtube.upload",
]

# client_secret.json 우선순위: 루트 → tools 폴더
CLIENT_SECRET_CANDIDATES = [
    os.path.abspath(os.path.join(_here, "..", "..", "..", "client_secret.json")),
    os.path.abspath(os.path.join(_here, "..", "..", "skills", "루나_디렉터", "tools", "client_secret.json")),
]
TOKEN_FILE = os.path.abspath(os.path.join(
    _here, "..", "..", "skills", "루나_디렉터", "tools", "youtube_token.pickle"
))

def main():
    print("=" * 55)
    print("  [Luna] YouTube OAuth Token Reissue")
    print("=" * 55)

    client_secret = None
    for path in CLIENT_SECRET_CANDIDATES:
        if os.path.exists(path):
            client_secret = path
            print(f"  [OK] client_secret.json: {path}")
            break

    if not client_secret:
        print("  [ERROR] client_secret.json not found.")
        print("  Place the file in one of:")
        for p in CLIENT_SECRET_CANDIDATES:
            print(f"    - {p}")
        return False

    # 기존 토큰 삭제
    if os.path.exists(TOKEN_FILE):
        os.remove(TOKEN_FILE)
        print(f"  [DEL] Old token removed: {TOKEN_FILE}")

    print()
    print("  [Browser] Opening browser. Please log in with your Google account and approve...")
    print()

    try:
        flow = InstalledAppFlow.from_client_secrets_file(client_secret, SCOPES)
        credentials = flow.run_local_server(port=0)
    except Exception as e:
        print(f"  [ERROR] OAuth failed: {e}")
        return False

    # 토큰 저장
    os.makedirs(os.path.dirname(TOKEN_FILE), exist_ok=True)
    with open(TOKEN_FILE, "wb") as f:
        pickle.dump(credentials, f)
    print(f"  [OK] Token saved: {TOKEN_FILE}")

    # 연결 테스트
    try:
        yt = build("youtube", "v3", credentials=credentials)
        ch = yt.channels().list(part="snippet", mine=True).execute()
        items = ch.get("items", [])
        if items:
            ch_name = items[0]["snippet"]["title"]
            print(f"  [OK] Channel connected: {ch_name}")
        else:
            print("  [WARN] No channel found (account may not have a channel)")
    except Exception as e:
        print(f"  [WARN] Channel check failed: {e}")

    print()
    print("  [DONE] Token reissued! Luna & Gahee can now use YouTube API.")
    print("=" * 55)
    return True


if __name__ == "__main__":
    ok = main()
    sys.exit(0 if ok else 1)
