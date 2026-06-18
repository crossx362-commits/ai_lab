"""
Claude API 클라이언트 (Anthropic)
Gemini 할당량 초과 시 폴백용
"""
import os
import json
import urllib.request


def chat(
    prompt: str,
    system: str = "",
    max_tokens: int = 1000,
    temperature: float = 0.7,
    model: str = "claude-3-5-haiku-20241022",
) -> str | None:
    """Claude API로 텍스트 생성 (가장 저렴하고 빠른 Haiku 사용)"""
    api_key = os.getenv("ANTHROPIC_API_KEY", "")
    if not api_key:
        return None

    url = "https://api.anthropic.com/v1/messages"

    payload = {
        "model": model,
        "max_tokens": max_tokens,
        "temperature": temperature,
        "messages": [
            {"role": "user", "content": prompt}
        ]
    }

    if system:
        payload["system"] = system

    headers = {
        "x-api-key": api_key,
        "anthropic-version": "2023-06-01",
        "content-type": "application/json"
    }

    try:
        data = json.dumps(payload).encode("utf-8")
        req = urllib.request.Request(url, data=data, headers=headers)

        with urllib.request.urlopen(req, timeout=60) as r:
            response = json.loads(r.read().decode())

        if response.get("content") and len(response["content"]) > 0:
            return response["content"][0]["text"]

        return None
    except Exception as e:
        print(f"  [Claude API] 실패: {e}")
        return None
