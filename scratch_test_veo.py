import os
import sys

# Set paths
_here = os.path.dirname(os.path.abspath(__file__))
AI_TEAM_ROOT = os.path.abspath(os.path.join(_here, "projects", "ai-team"))
sys.path.insert(0, AI_TEAM_ROOT)

from _shared.env_loader import load_env
load_env()

from google import genai
from google.genai import types

try:
    print("Testing Veo Fast Preview Model...")
    client = genai.Client(api_key=os.getenv("GEMINI_API_KEY"))
    op = client.models.generate_videos(
        model="veo-3.1-fast-generate-preview",
        prompt="A cute puppy playing with a ball, vertical 9:16",
        config=types.GenerateVideosConfig(aspect_ratio="9:16")
    )
    print("Operation started, done status:", op.done)
except Exception as e:
    print("Error listing models:", e)
