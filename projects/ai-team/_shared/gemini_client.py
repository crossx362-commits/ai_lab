import os
import json
import urllib.request

def text(
    prompt: str,
    system: str = "",
    max_tokens: int = 2000,
    temperature: float = 0.7,
    json_mode: bool = False,
    task: str = "",
    lm_first: bool = False,
) -> str | None:
    """텍스트 생성 (Ollama/Gemini)."""
    import inspect
    allowed = False
    for frame in inspect.stack():
        f_path = frame.filename.lower()
        if any(k in f_path for k in ["dave", "데이브", "영숙", "yeongsuk", "yewon", "예원", "telegram_receiver"]):
            allowed = True
            break
    if not allowed:
        # 허용되지 않은 에이전트는 Gemini API 사용을 차단하고 Ollama만 사용하도록 강제
        lm_first = True

    if lm_first:
        # 1. Ollama 시도
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
        # lm_first가 True인 경우 (Ollama 우선), Ollama 실패 시 Gemini API 폴백을 완전히 차단
        return None

    # 2. Gemini API 사용
    api_key = os.getenv("GEMINI_API_KEY", "")
    if not api_key:
        # 만약 lm_first가 False여서 여기로 왔는데 API 키가 없으면 Ollama로 폴백
        if not lm_first:
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
        return None

    try:
        parts = [{"text": prompt}]
        payload = {
            "contents": [{"parts": parts}],
            "generationConfig": {
                "maxOutputTokens": max_tokens,
                "temperature": temperature,
            }
        }
        if system:
            payload["systemInstruction"] = {
                "parts": [{"text": system}]
            }
        if json_mode:
            payload["generationConfig"]["responseMimeType"] = "application/json"

        url = f"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={api_key}"
        req = urllib.request.Request(url, data=json.dumps(payload).encode("utf-8"), headers={"Content-Type": "application/json"})
        
        with urllib.request.urlopen(req, timeout=60) as r:
            res = json.loads(r.read())
        
        result = res["candidates"][0]["content"]["parts"][0]["text"].strip()
        print(f"  [Gemini API] 텍스트 생성 완료")
        return result
    except Exception as e:
        print(f"  [Gemini API] 실패: {e}")
        # 실패 시 Ollama로 폴백
        if not lm_first:
            try:
                from _shared.ollama_client import chat as lm_chat, is_available as lm_available
                if lm_available():
                    return lm_chat(
                        prompt, system=system,
                        max_tokens=max_tokens, temperature=temperature,
                        json_mode=json_mode, task=task,
                    )
            except Exception:
                pass
    return None



# ── Vision (이미지 → 텍스트) ─────────────────────────────────────────────────

def vision(img_bytes: bytes, prompt: str, max_tokens: int = 800) -> str | None:
    """Gemini Vision 비활성화 — None 반환."""
    return None


# ── 웹 서치 (Google Search Grounding) ────────────────────────────────────────

def web_search(query: str, max_tokens: int = 1500) -> str | None:
    """Gemini 웹서치 비활성화 — None 반환."""
    return None


# ── 이미지 생성 (Imagen 4) ───────────────────────────────────────────────────

def generate_image(
    prompt: str,
    output_path: str,
    aspect_ratio: str = "16:9",
    model: str = "imagen-4.0-generate-001",
    negative_prompt: str = "",
    num_images: int = 1,
) -> str | None:
    """Gemini Imagen 비활성화 — None 반환."""
    return None

