import os
import sys

# 기존 환경 변수 클리어
for key in ['GEMINI_API_KEY', 'YOUTUBE_API_KEY', 'TELEGRAM_BOT_TOKEN']:
    if key in os.environ:
        del os.environ[key]

sys.path.insert(0, 'ai-team/_shared')
from env_loader import _find_root, load_env

current = os.getcwd()
print(f'Current dir: {current}')
root = _find_root(current)
print(f'Found root: {root}')
env_path = os.path.join(root, '.env')
print(f'Env path: {env_path}')
print(f'Env exists: {os.path.exists(env_path)}')

load_env()
print('\nAfter load_env:')
print(f'GEMINI_API_KEY: {len(os.getenv("GEMINI_API_KEY", ""))} chars')
print(f'YOUTUBE_API_KEY: {len(os.getenv("YOUTUBE_API_KEY", ""))} chars')
print(f'TELEGRAM_BOT_TOKEN: {len(os.getenv("TELEGRAM_BOT_TOKEN", ""))} chars')
print(f'\nAll loaded: {all([os.getenv(k) for k in ["GEMINI_API_KEY", "YOUTUBE_API_KEY", "TELEGRAM_BOT_TOKEN"]])}')
