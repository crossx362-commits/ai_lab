"""Claude API 상세 테스트"""
import os
import sys
import json
import urllib.request

PROJECT_ROOT = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, PROJECT_ROOT)
sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team"))

from _shared.env_loader import load_env
load_env(PROJECT_ROOT)

API_KEY = os.getenv("ANTHROPIC_API_KEY", "")

print("=" * 60)
print("Claude API 상세 테스트")
print("=" * 60)
print(f"API Key: {API_KEY[:20]}...{API_KEY[-10:]}")
print(f"Length: {len(API_KEY)}")

url = "https://api.anthropic.com/v1/messages"

payload = {
    "model": "claude-3-5-haiku-20241022",
    "max_tokens": 20,
    "messages": [
        {"role": "user", "content": "Say OK"}
    ]
}

headers = {
    "x-api-key": API_KEY,
    "anthropic-version": "2023-06-01",
    "content-type": "application/json"
}

print(f"\nPayload: {json.dumps(payload, indent=2)}")

try:
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(url, data=data, headers=headers)

    with urllib.request.urlopen(req, timeout=30) as r:
        response = json.loads(r.read().decode())

    print(f"\n[SUCCESS] Response: {json.dumps(response, indent=2)}")

except urllib.error.HTTPError as e:
    print(f"\n[ERROR] HTTP {e.code}: {e.reason}")
    error_body = e.read().decode()
    print(f"Error body: {error_body}")

except Exception as e:
    print(f"\n[ERROR] {e}")
    import traceback
    traceback.print_exc()
