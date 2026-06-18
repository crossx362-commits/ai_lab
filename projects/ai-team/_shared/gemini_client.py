import os
import json
import urllib.request


def _call_gpt(prompt: str, system: str = "", max_tokens: int = 2000, temperature: float = 0.7, json_mode: bool = False) -> str | None:
    """OpenAI GPT-4o mini 호출."""
    api_key = os.getenv("OPENAI_API_KEY", "")
    if not api_key:
        return None
    try:
        messages = []
        if system:
            messages.append({"role": "system", "content": system})
        messages.append({"role": "user", "content": prompt})
        payload = {
            "model": "gpt-4o-mini",
            "messages": messages,
            "max_tokens": max_tokens,
            "temperature": temperature,
        }
        if json_mode:
            payload["response_format"] = {"type": "json_object"}
        req = urllib.request.Request(
            "https://api.openai.com/v1/chat/completions",
            data=json.dumps(payload).encode("utf-8"),
            headers={"Content-Type": "application/json", "Authorization": f"Bearer {api_key}"}
        )
        with urllib.request.urlopen(req, timeout=60) as r:
            res = json.loads(r.read())
        result = res["choices"][0]["message"]["content"].strip()
        print(f"  [GPT-4o mini] 텍스트 생성 완료")
        return result
    except Exception as e:
        print(f"  [GPT-4o mini] 실패: {e}")
        return None


def gpt_mini(prompt: str, system: str = "", max_tokens: int = 500, temperature: float = 0.1, json_mode: bool = False) -> str | None:
    """GPT-4o mini만 호출."""
    return _call_gpt(prompt, system=system, max_tokens=max_tokens, temperature=temperature, json_mode=json_mode)


def gemini_flash(prompt: str, system: str = "", max_tokens: int = 500, temperature: float = 0.1, json_mode: bool = False) -> str | None:
    """Gemini 2.5 Flash 호출 (최후 폴백)."""
    api_key = os.getenv("GEMINI_API_KEY", "")
    if not api_key:
        return None
    try:
        messages = []
        if system:
            messages.append({"role": "user", "parts": [{"text": f"[System]\n{system}"}]})
            messages.append({"role": "model", "parts": [{"text": "understood"}]})
        messages.append({"role": "user", "parts": [{"text": prompt}]})
        payload = {
            "contents": messages,
            "generationConfig": {
                "maxOutputTokens": max_tokens,
                "temperature": temperature,
            },
        }
        if json_mode:
            payload["generationConfig"]["responseMimeType"] = "application/json"
        url = f"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={api_key}"
        req = urllib.request.Request(
            url,
            data=json.dumps(payload).encode("utf-8"),
            headers={"Content-Type": "application/json"},
        )
        with urllib.request.urlopen(req, timeout=60) as r:
            res = json.loads(r.read())
        result = res["candidates"][0]["content"]["parts"][0]["text"].strip()
        print(f"  [Gemini 2.5 Flash] 텍스트 생성 완료")
        return result
    except Exception as e:
        print(f"  [Gemini 2.5 Flash] 실패: {e}")
        return None


def text(
    prompt: str,
    system: str = "",
    max_tokens: int = 2000,
    temperature: float = 0.7,
    json_mode: bool = False,
    task: str = "",
    lm_first: bool = False,
) -> str | None:
    """텍스트 생성. 폴백 체인: GPT-4o mini → Ollama (Gemini는 할당량 초과로 비활성화)"""
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
        # Ollama 전용 (API 비용 없는 로컬 에이전트)
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

    # 1. GPT-4o mini 최우선
    result = _call_gpt(prompt, system=system, max_tokens=max_tokens, temperature=temperature, json_mode=json_mode)
    if result:
        return result
    print(f"  [GPT] 실패 → Gemini 폴백")

    # 2. Gemini 2.5 Flash 폴백
    result = gemini_flash(prompt, system=system, max_tokens=max_tokens, temperature=temperature, json_mode=json_mode)
    if result:
        return result
    print(f"  [Gemini] 실패 → Ollama 폴백")

    # 3. Ollama 최후 폴백
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
    except Exception as e:
        print(f"  [Ollama] 실패: {e}")

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

