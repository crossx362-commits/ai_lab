import os, sys, urllib.request, json

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

_here = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.abspath(os.path.join(_here, '..', '..', '..')))
from _shared.env_loader import load_env
load_env()

token = os.getenv("SUPABASE_ACCESS_TOKEN", "").strip()
project_ref = "nlgjsdffgkygaylbjooc"  # .env의 URL에서 추출

print(f"🔍 Supabase Management API 디버그")
print(f"Project Ref: {project_ref}")
print(f"Token: {token[:15]}..." if token else "Token: 없음")
print()

# 현재 설정 조회
url = f"https://api.supabase.com/v1/projects/{project_ref}/config/auth"
req = urllib.request.Request(url, headers={
    "Authorization": f"Bearer {token}",
    "Content-Type": "application/json"
})

try:
    with urllib.request.urlopen(req, timeout=10) as r:
        config = json.loads(r.read())
    print("✅ 현재 Auth 설정:")
    print(json.dumps(config, indent=2))
except Exception as e:
    print(f"❌ 조회 실패: {e}")
