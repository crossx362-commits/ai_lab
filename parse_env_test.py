import os

env_path = '.env'
with open(env_path, 'r', encoding='utf-8') as f:
    lines = f.readlines()

count = 0
for i, line in enumerate(lines[:60], 1):
    line = line.strip()
    if not line or line.startswith('#') or '=' not in line:
        continue
    k, v = line.split('=', 1)
    k = k.strip()
    v = v.strip().strip('"').strip("'")
    if k in ['GEMINI_API_KEY', 'YOUTUBE_API_KEY', 'TELEGRAM_BOT_TOKEN', 'VERCEL_TOKEN']:
        print(f'Line {i}: {k}: {len(v)} chars - {v[:20]}...')
        count += 1

print(f'\nTotal key env vars found: {count}')
