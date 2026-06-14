"""
gemini_client.py — 공통 Gemini API 클라이언트 (유료 API 비활성화)

모든 Gemini/Imagen API 호출 제거됨. 텍스트 생성은 Ollama 전용.
vision/web_search/generate_image는 None 반환 (호출부 기존 폴백 로직 유지).
"""

# ── 텍스트 생성 (Ollama 전용 래퍼) ───────────────────────────────────────────

def text(
    prompt: str,
    system: str = "",
    max_tokens: int = 2000,
    temperature: float = 0.7,
    json_mode: bool = False,
    task: str = "",
    lm_first: bool = True,
) -> str | None:
    """Ollama 텍스트 생성 전용. Gemini 폴백 제거됨."""
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

