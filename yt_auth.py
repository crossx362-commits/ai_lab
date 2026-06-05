# -*- coding: utf-8 -*-
import os
import sys
import pickle
from google_auth_oauthlib.flow import InstalledAppFlow
from googleapiclient.discovery import build

# UTF-8 인코딩 강제
if sys.platform == 'win32':
    import codecs
    sys.stdout = codecs.getwriter('utf-8')(sys.stdout.buffer, 'strict')
    sys.stderr = codecs.getwriter('utf-8')(sys.stderr.buffer, 'strict')

scopes = [
    "https://www.googleapis.com/auth/youtube",
    "https://www.googleapis.com/auth/youtube.upload",
]

client_secrets = "D:/ai_lab/client_secret.json"
token_file = "D:/ai_lab/projects/ai-team/skills/루나_디렉터/tools/youtube_token.pickle"

try:
    flow = InstalledAppFlow.from_client_secrets_file(client_secrets, scopes)
    credentials = flow.run_local_server(port=0)
    
    with open(token_file, "wb") as token:
        pickle.dump(credentials, token)
    
    youtube = build("youtube", "v3", credentials=credentials)
    print("SUCCESS: YouTube OAuth authentication completed")
except Exception as e:
    print(f"ERROR: {e}")
