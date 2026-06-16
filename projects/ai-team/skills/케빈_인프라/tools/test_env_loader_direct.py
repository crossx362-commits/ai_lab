#!/usr/bin/env python3
import os

# env_loader.py의 로직을 직접 재현
root = os.getcwd()
env_path = os.path.join(root, ".env")

print(f"Env path: {env_path}")
print(f"Exists: {os.path.exists(env_path)}")

if os.path.exists(env_path):
    with open(env_path, "r", encoding="utf-8") as f:
        count = 0
        for line in f:
            line = line.strip()
            if not line or line.startswith("#") or "=" not in line:
                continue
            k, v = line.split("=", 1)
            key = k.strip()
            value = v.strip().strip('"').strip("'")

            # setdefault 대신 직접 설정
            os.environ[key] = value

            if key in ['GEMINI_API_KEY', 'YOUTUBE_API_KEY', 'TELEGRAM_BOT_TOKEN']:
                print(f"Set {key}: {len(value)} chars")
                count += 1

print(f"\nTotal set: {count}")
print(f"\nVerify:")
for key in ['GEMINI_API_KEY', 'YOUTUBE_API_KEY', 'TELEGRAM_BOT_TOKEN']:
    val = os.getenv(key, "")
    print(f"  {key}: {len(val)} chars - {'OK' if val else 'MISSING'}")
