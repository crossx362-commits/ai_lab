"""Unified LLM client - Ollama → GPT → Gemini fallback chain."""
import json
import os
import time
import urllib.request
import urllib.error

_cache = {}
_CACHE_TTL = 60
OPENAI_GPT_MODEL = "gpt-4o-mini"
GEMINI_MODEL = "gemini-2.5-flash"  # 변경: 2.5 Flash 사용


def _env_bool(name: str, default: str = "1") -> bool:
    return os.getenv(name, default).strip().lower() in {"1", "true", "yes", "on"}


def _cloud_llm_allowed() -> bool:
    return _env_bool("AI_TEAM_ALLOW_CLOUD_LLM", "1")


# ==================== OLLAMA (LOCAL, FREE) ====================

def _ollama_endpoint() -> str:
    return os.getenv("OLLAMA_URL", "http://localhost:11434/v1/chat/completions")


def _list_ollama() -> list[str]:
    """List Ollama models (exclude embeddings)."""
    for attempt in range(3):
        try:
            url = _ollama_endpoint().replace("/chat/completions", "/models")
            timeout = 45 if attempt == 0 else 15
            with urllib.request.urlopen(url, timeout=timeout) as r:
                data = json.loads(r.read())
            return [m["id"] for m in data.get("data", []) if "embed" not in m.get("id", "").lower()]
        except Exception as e:
            if attempt < 2:
                time.sleep(2)
            continue
    return []


def is_available() -> bool:
    """Return whether at least one local Ollama chat model is available."""
    return bool(_list_ollama())


def _pick_ollama(models: list[str], task: str) -> str | None:
    """Pick best Ollama model for task."""
    if not models:
        return None
    if task == "coding":
        for m in models:
            if any(k in m.lower() for k in ("deepseek", "code", "coder", "codestral")):
                return m
    elif task == "blog":
        for m in models:
            if "qwen" in m.lower() and "deepseek" not in m.lower():
                return m
    return models[0]


def _ollama(prompt: str, system: str = "", max_tokens: int = 2000, temperature: float = 0.7, task: str = "") -> str | None:
    """Call Ollama local LLM."""
    try:
        model = os.getenv("OLLAMA_MODEL")
        if not model:
            now = time.time()
            if "ollama_models" not in _cache or now - _cache.get("ollama_ts", 0) > _CACHE_TTL:
                _cache["ollama_models"] = _list_ollama()
                _cache["ollama_ts"] = now
            model = _pick_ollama(_cache.get("ollama_models", []), task)
        if not model:
            return None

        messages = []
        if system:
            messages.append({"role": "system", "content": system})
        messages.append({"role": "user", "content": prompt})

        payload = json.dumps({"model": model, "messages": messages, "max_tokens": max_tokens, "temperature": temperature}).encode()
        req = urllib.request.Request(_ollama_endpoint(), data=payload, headers={"Content-Type": "application/json"})

        with urllib.request.urlopen(req, timeout=60) as r:
            res = json.loads(r.read())
        result = res["choices"][0]["message"]["content"].strip()
        if not result:
            print(f"  ⚠️ [Ollama:{model}] empty response, falling back")
            return None
        print(f"  ✅ [Ollama:{model}] {len(result)} chars")
        return result
    except Exception as e:
        print(f"  ❌ [Ollama] {e}")
        return None


# ==================== GPT-4o-mini (CLOUD, PAID) ====================

def _gpt(prompt: str, system: str = "", max_tokens: int = 2000, temperature: float = 0.7, json_mode: bool = False) -> str | None:
    """Call OpenAI GPT-4o-mini."""
    if not _cloud_llm_allowed():
        print("  ⏭️ [GPT] blocked by AI_TEAM_ALLOW_CLOUD_LLM=0")
        return None
    api_key = os.getenv("OPENAI_API_KEY", "")
    if not api_key:
        return None
    try:
        messages = []
        if system:
            messages.append({"role": "system", "content": system})
        messages.append({"role": "user", "content": prompt})

        payload = {"model": OPENAI_GPT_MODEL, "messages": messages, "max_tokens": max_tokens, "temperature": temperature}
        if json_mode:
            payload["response_format"] = {"type": "json_object"}

        req = urllib.request.Request(
            "https://api.openai.com/v1/chat/completions",
            data=json.dumps(payload).encode(),
            headers={"Content-Type": "application/json", "Authorization": f"Bearer {api_key}"},
        )
        with urllib.request.urlopen(req, timeout=60) as r:
            res = json.loads(r.read())
        result = res["choices"][0]["message"]["content"].strip()
        print(f"  ✅ [{OPENAI_GPT_MODEL}] {len(result)} chars")
        return result
    except Exception as e:
        print(f"  ❌ [GPT] {e}")
        return None


# ==================== GEMINI (CLOUD, PAID) ====================

def _gemini(prompt: str, system: str = "", max_tokens: int = 2000, temperature: float = 0.7, json_mode: bool = False) -> str | None:
    """Call Google Gemini 2.5 Flash."""
    if not _cloud_llm_allowed():
        print("  ⏭️ [Gemini] blocked by AI_TEAM_ALLOW_CLOUD_LLM=0")
        return None
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
                "thinkingConfig": {"thinkingBudget": 0},  # thinking 토큰 차단 (결의안 ③)
            },
        }
        if json_mode:
            payload["generationConfig"]["responseMimeType"] = "application/json"

        url = f"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={api_key}"
        req = urllib.request.Request(url, data=json.dumps(payload).encode(), headers={"Content-Type": "application/json"})

        with urllib.request.urlopen(req, timeout=60) as r:
            res = json.loads(r.read())
        parts = res["candidates"][0]["content"]["parts"]
        # 2.5 Flash: parts[0]=thinking, parts[-1]=answer — 마지막 text part 사용
        text_parts = [p["text"] for p in parts if "text" in p]
        result = text_parts[-1].strip() if text_parts else ""
        print(f"  ✅ [Gemini] {len(result)} chars")
        return result
    except Exception as e:
        print(f"  ❌ [Gemini] {e}")
        return None


# ==================== UNIFIED INTERFACE ====================

def text(
    prompt: str,
    system: str = "",
    max_tokens: int = 2000,
    temperature: float = 0.7,
    json_mode: bool = False,
    task: str = "",
    lm_first: bool = False,
) -> str | None:
    """
    Generate text with a cost-safe fallback chain.

    Default: Ollama → GPT → Gemini.
    Set AI_TEAM_LLM_PRIMARY=cloud only for explicit cloud-first runs.
    Set AI_TEAM_ALLOW_CLOUD_LLM=0 to block paid/cloud fallback entirely.
    """
    cloud_allowed = _cloud_llm_allowed()
    primary = os.getenv("AI_TEAM_LLM_PRIMARY", "ollama").strip().lower()

    if lm_first or primary in {"local", "ollama"}:
        result = _ollama(prompt, system, max_tokens, temperature, task)
        if result:
            return result
        if not cloud_allowed:
            return None
        result = _gpt(prompt, system, max_tokens, temperature, json_mode)
        if result:
            return result
        return _gemini(prompt, system, max_tokens, temperature, json_mode)

    if not cloud_allowed:
        return _ollama(prompt, system, max_tokens, temperature, task)

    result = _gpt(prompt, system, max_tokens, temperature, json_mode)
    if result:
        return result
    result = _gemini(prompt, system, max_tokens, temperature, json_mode)
    if result:
        return result
    return _ollama(prompt, system, max_tokens, temperature, task)


# Shorthand aliases
ollama = _ollama
gpt = _gpt
gemini = _gemini
