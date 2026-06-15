"""Gemini 사용 가능한 모델 확인"""
import os
import sys

PROJECT_ROOT = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, PROJECT_ROOT)
sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team"))

from _shared.env_loader import load_env
load_env(PROJECT_ROOT)

from google import genai

API_KEY = os.getenv("GEMINI_API_KEY", "")
client = genai.Client(api_key=API_KEY)

print("=" * 60)
print("사용 가능한 Gemini 모델 목록")
print("=" * 60)

try:
    models = client.models.list()
    for model in models:
        print(f"  - {model.name}")
        if hasattr(model, 'supported_generation_methods'):
            print(f"    Methods: {model.supported_generation_methods}")
except Exception as e:
    print(f"오류: {e}")
    import traceback
    traceback.print_exc()
