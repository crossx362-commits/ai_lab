#!/usr/bin/env python
# -*- coding: utf-8 -*-
import os
import sys
import json
import urllib.request

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "ai-team"))
from _shared.env_loader import load_env

load_env(os.path.dirname(__file__))

TOKEN = os.getenv("TELEGRAM_BOT_TOKEN")
CHAT_ID = os.getenv("TELEGRAM_CHAT_ID")

if TOKEN and CHAT_ID:
    url = f"https://api.telegram.org/bot{TOKEN}/sendMessage"
    data = json.dumps({
        "chat_id": CHAT_ID,
        "text": "리서치 보고서 작성해줘"
    }).encode("utf-8")

    req = urllib.request.Request(url, data=data, headers={"Content-Type": "application/json"})

    try:
        with urllib.request.urlopen(req, timeout=10) as response:
            result = json.loads(response.read())
            print("OK - Message sent, ID:", result.get('result', {}).get('message_id'))
    except Exception as e:
        print("ERROR:", str(e))
else:
    print("ERROR: No token or chat ID")
