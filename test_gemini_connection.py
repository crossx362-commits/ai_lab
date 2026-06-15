import sys
import os

# Add project root to path
PROJECT_ROOT = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, PROJECT_ROOT)
sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team"))

# Load environment
from _shared.env_loader import load_env
load_env(PROJECT_ROOT)

print("=" * 60)
print("Testing Gemini API Connection")
print("=" * 60)

# Check API key
api_key = os.getenv("GEMINI_API_KEY", "")
print(f"[OK] API Key loaded: {api_key[:20]}...{api_key[-10:] if len(api_key) > 30 else ''}")
print(f"  Length: {len(api_key)} characters")

# Initialize Gemini client
try:
    from google import genai
    from google.genai import types

    print("\n[OK] Importing genai module successful")

    # Initialize with explicit API key
    client = genai.Client(api_key=api_key)
    print("[OK] Gemini client initialized")

    # Test simple generation
    print("\nTesting simple generation...")
    response = client.models.generate_content(
        model="gemini-2.0-flash-exp",
        contents="Say 'Hello' in one word"
    )

    print(f"[OK] Response received: {response.text}")
    print("\n" + "=" * 60)
    print("[SUCCESS] Gemini API connection working!")
    print("=" * 60)

except Exception as e:
    print(f"\n[ERROR] {e}")
    import traceback
    traceback.print_exc()
