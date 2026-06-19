#!/usr/bin/env python3
import sys, os
sys.path.insert(0, 'projects/ai-team')
from _shared.env import load_env
load_env()

import requests
token = os.getenv('TELEGRAM_BOT_TOKEN')

try:
    r = requests.get(f'https://api.telegram.org/bot{token}/getUpdates?limit=10')
    data = r.json()

    if data.get('ok'):
        updates = data.get('result', [])
        print(f"\n✅ 최근 메시지 {len(updates)}개:")
        for u in updates[-5:]:
            msg = u.get('message', {})
            text = msg.get('text', 'N/A')
            date = msg.get('date', 0)
            print(f"  [{date}] {text}")
    else:
        print(f"❌ Telegram API 오류: {data}")
except Exception as e:
    print(f"❌ 오류: {e}")
