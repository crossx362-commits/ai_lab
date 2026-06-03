"""
gemini_client.py — 공통 Gemini API 클라이언트

Vision(이미지→텍스트) / 웹서치(Google Search Grounding) 전용.
텍스트 생성은 Ollama만 사용 — Gemini 텍스트 폴백 제거됨.
"""
import os
import json
import base64
import urllib.request

_BASE = "https://generativelanguage.googleapis.com/v1beta/models"


def _api_key() -> str:
    return os.getenv("GEMINI_API_KEY", "")


# ── 텍스트 생성 (Ollama 전용 래퍼) ───────────────────────────────────────────

def text(
    prompt: str,
    system: str = "",
    max_tokens: int = 2000,
    temperature: float = 0.7,
    json_mode: bool = False,
    task: str = "",
    lm_first: bool = True,  # 하위 호환성 유지 (무시됨)
) -> str | None:
    """Ollama 텍스트 생성, 실패 시 Gemini API 폴백."""
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

    # Gemini API 폴백
    api_key = _api_key()
    if not api_key:
        return None
    try:
        parts = []
        if system:
            parts.append({"text": f"{system}\n\n{prompt}"})
        else:
            parts.append({"text": prompt})
        payload = json.dumps({
            "contents": [{"parts": parts}],
            "generationConfig": {
                "maxOutputTokens": max_tokens,
                "temperature": temperature,
                **({"responseMimeType": "application/json"} if json_mode else {}),
            },
        }).encode("utf-8")
        url = f"{_BASE}/gemini-2.5-flash:generateContent?key={api_key}"
        req = urllib.request.Request(url, data=payload, headers={"Content-Type": "application/json"})
        with urllib.request.urlopen(req, timeout=60) as r:
            res = json.loads(r.read())
        result = res["candidates"][0]["content"]["parts"][0]["text"].strip()
        print(f"  [Gemini 폴백] 텍스트 생성 완료")
        return result
    except Exception as e:
        print(f"  [Gemini 폴백] 실패: {e}")
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
        url = f"{_BASE}/gemini-2.5-flash:generateContent?key={api_key}"
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
        url = f"{_BASE}/gemini-2.5-flash:generateContent?key={api_key}"
        req = urllib.request.Request(url, data=payload, headers={"Content-Type": "application/json"})
        with urllib.request.urlopen(req, timeout=30) as r:
            res = json.loads(r.read())
        parts = res.get("candidates", [{}])[0].get("content", {}).get("parts", [])
        return " ".join(p.get("text", "") for p in parts).strip() or None
    except Exception as e:
        print(f"  [Gemini 웹서치] 실패: {e}")
    return None
