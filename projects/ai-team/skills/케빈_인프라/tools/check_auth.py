import os, sys, urllib.request, json

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    sys.stderr.reconfigure(encoding='utf-8', errors='replace')

_here = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.abspath(os.path.join(_here, '..', '..', '..')))
from _shared.env_loader import load_env
load_env()

sb_url = os.getenv('SUPABASE_URL')
sb_key = os.getenv('SUPABASE_ANON_KEY')

if sb_url and sb_key:
    req = urllib.request.Request(f'{sb_url}/auth/v1/settings', headers={'apikey': sb_key})
    with urllib.request.urlopen(req, timeout=8) as r:
        cfg = json.loads(r.read())
    print('✅ Supabase Auth 설정:')
    print(f'  Site URL: {cfg.get("site_url")}')
    print(f'  External OAuth: {", ".join(cfg.get("external", {}).keys()) or "없음"}')
    print(f'  Autoconfirm: {cfg.get("autoconfirm", False)}')
else:
    print('❌ 환경변수 없음')
