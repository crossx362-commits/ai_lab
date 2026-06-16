#!/usr/bin/env python
# -*- coding: utf-8 -*-
import os
import sys

# Add project root to path
PROJECT_ROOT = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team"))

from _shared.env_loader import load_env
load_env(PROJECT_ROOT)

API_KEY = os.getenv("GEMINI_API_KEY", "").strip()
print(f"API Key loaded: {bool(API_KEY)}")
print(f"API Key (first 30 chars): {API_KEY[:30] if API_KEY else 'EMPTY'}")

try:
    from google import genai
    print("genai module: OK")

    if API_KEY:
        client = genai.Client(api_key=API_KEY)
        print(f"Gemini client created: OK")
    else:
        print("Gemini client: No API key")
except ImportError as e:
    print(f"genai module: Not installed ({e})")
except Exception as e:
    print(f"Gemini client: Error - {e}")
