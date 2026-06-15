#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Simplest possible working bot"""

import json
import time
import urllib.request
import sys

# Load environment
import os
import sys
_here = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(_here, "projects", "ai-team"))
from _shared.env_loader import load_env
load_env(_here)

TOKEN = os.getenv("TELEGRAM_BOT_TOKEN", "")
CHAT_ID = os.getenv("TELEGRAM_CHAT_ID", "")
GEMINI_KEY = os.getenv("GEMINI_API_KEY", "")

print("Importing Gemini...")
from google import genai
from google.genai import types

print("Creating client...")
client = genai.Client(api_key=GEMINI_KEY)

def telegram_api(method, data):
    url = f"https://api.telegram.org/bot{TOKEN}/{method}"
    req = urllib.request.Request(
        url,
        json.dumps(data).encode(),
        {"Content-Type": "application/json"}
    )
    try:
        with urllib.request.urlopen(req, timeout=20) as r:
            return json.loads(r.read().decode())
    except Exception as e:
        print(f"Telegram error: {e}")
        return {}

def send_message(text):
    result = telegram_api("sendMessage", {"chat_id": CHAT_ID, "text": text})
    print(f"Sent: {text[:50]}")
    return result

def get_answer(question):
    print(f"Question: {question}")
    try:
        response = client.models.generate_content(
            model="gemini-2.5-flash",
            contents=question,
            config=types.GenerateContentConfig(max_output_tokens=150)
        )
        answer = response.text if response.text else "OK"
        print(f"Answer: {answer[:50]}")
        return answer
    except Exception as e:
        print(f"Gemini error: {e}")
        return "Error occurred"

print("="*60)
print("SIMPLE BOT STARTING")
print("="*60)

# Delete webhook
print("Deleting webhook...")
telegram_api("deleteWebhook", {"drop_pending_updates": True})
time.sleep(2)

# Send startup message
send_message("Bot started - single instance!")

offset = 0
print("\nListening for messages...\n")

try:
    while True:
        # Get updates
        updates = telegram_api("getUpdates", {
            "offset": offset,
            "timeout": 30,
            "allowed_updates": ["message"]
        })

        # Process messages
        for update in updates.get("result", []):
            offset = update["update_id"] + 1
            message = update.get("message", {})
            text = message.get("text", "").strip()

            if text:
                print(f"\n>>> Received: {text}")
                answer = get_answer(text)
                send_message(answer)

        # Small delay
        time.sleep(0.5)

except KeyboardInterrupt:
    print("\n\nStopping bot...")
    send_message("Bot stopped")
    sys.exit(0)
