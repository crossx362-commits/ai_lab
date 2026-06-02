"""
gemini_client.py — 공통 Gemini API 클라이언트

Ollama(1순위) → Gemini API(2순위) 패턴을 모든 에이전트에서 각자 구현하던 것을 병합.
텍스트 생성 / 이미지 생성 / Vision(이미지→텍스트) 지원.
"""
import os
import json
import base64
import urllib.request
import urllib.error

_BASE = "https://generativelanguage.googleapis.com/v1beta/models"
_TEXT_MODEL  = "gemini-3.5-flash"


def _api_key() -> str:
    return os.getenv("GEMINI_API_KEY", "")


# ── 텍스트 생성 ───────────────────────────────────────────────────────────────

def text(
    prompt: str,
    system: str = "",
    max_tokens: int = 2000,
    temperature: float = 0.7,
    json_mode: bool = False,
    task: str = "",
    lm_first: bool = True,
) -> str | None:
    """Ollama(lm_first=True일 때 1순위) → Gemini 텍스트 생성.

    task="blog"  → Ollama Qwen 전용
    task="coding"→ Ollama DeepSeek 전용
    task=""      → Ollama 자동 선택
    """
    # 1순위: Ollama
    if lm_first:
        try:
            from _shared.ollama_client import chat as lm_chat, is_available as lm_available
            if lm_available():
                res = lm_chat(
                    prompt, system=system,
                    max_tokens=max_tokens, temperature=temperature,
                    json_mode=json_mode, task=task,
                )
                if res:
                    return res.strip()
        except Exception:
            pass

    # 2순위: Gemini API
    api_key = _api_key()
    if not api_key:
        return None
    print(f"  [Gemini API → {_TEXT_MODEL}]")
    try:
        payload: dict = {
            "contents": [{"parts": [{"text": prompt}]}],
            "generationConfig": {"maxOutputTokens": max_tokens, "temperature": temperature},
        }
        if system:
            payload["systemInstruction"] = {"parts": [{"text": system}]}
        if json_mode:
            payload["generationConfig"]["responseMimeType"] = "application/json"

        url = f"{_BASE}/{_TEXT_MODEL}:generateContent?key={api_key}"
        data = json.dumps(payload).encode("utf-8")
        req = urllib.request.Request(url, data=data, headers={"Content-Type": "application/json"})
        with urllib.request.urlopen(req, timeout=60) as r:
            res = json.loads(r.read())
        return res["candidates"][0]["content"]["parts"][0]["text"].strip()
    except Exception as e:
        print(f"  [Gemini 텍스트] 실패: {e}")
    return None


# ── Vision (이미지 → 텍스트) ─────────────────────────────────────────────────

def vision(img_bytes: bytes, prompt: str, max_tokens: int = 800) -> str | None:
    """Gemini Vision으로 이미지 분석 → 텍스트 반환."""
    api_key = _api_key()
    if not api_key:
        return None
    try:
        img_b64 = base64.b64encode(img_bytes).decode()
        payload = json.dumps({
            "contents": [{
                "parts": [
                    {"text": prompt},
                    {"inline_data": {"mime_type": "image/jpeg", "data": img_b64}},
                ]
            }],
            "generationConfig": {"maxOutputTokens": max_tokens},
        }).encode("utf-8")
        url = f"{_BASE}/{_TEXT_MODEL}:generateContent?key={api_key}"
        req = urllib.request.Request(url, data=payload, headers={"Content-Type": "application/json"})
        with urllib.request.urlopen(req, timeout=30) as r:
            res = json.loads(r.read())
        return res["candidates"][0]["content"]["parts"][0]["text"].strip()
    except Exception as e:
        print(f"  [Gemini Vision] 실패: {e}")
    return None


# ── 웹 서치 (Google Search Grounding) ────────────────────────────────────────

def web_search(query: str, max_tokens: int = 1500) -> str | None:
    """Gemini Google Search Grounding으로 실시간 웹 검색 후 요약 반환."""
    api_key = _api_key()
    if not api_key:
        return None
    print(f"  [Gemini 웹서치] {query[:60]}")
    try:
        payload = json.dumps({
            "contents": [{"parts": [{"text": query}]}],
            "tools": [{"google_search": {}}],
            "generationConfig": {"maxOutputTokens": max_tokens},
        }).encode("utf-8")
        url = f"{_BASE}/gemini-3.5-flash:generateContent?key={api_key}"
        req = urllib.request.Request(url, data=payload, headers={"Content-Type": "application/json"})
        with urllib.request.urlopen(req, timeout=30) as r:
            res = json.loads(r.read())
        parts = res.get("candidates", [{}])[0].get("content", {}).get("parts", [])
        return " ".join(p.get("text", "") for p in parts).strip() or None
    except Exception as e:
        print(f"  [Gemini 웹서치] 실패: {e}")
    return None
