"""Unified LLM client - Ollama → GPT → Gemini fallback chain."""
import json
import os
import re
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
    # 기본: 파라미터 수가 가장 큰 모델(2026-07-02) — models[0]이 최소 모델(e2b)이면
    # 구조화 프롬프트에 빈 응답을 뱉어 전체 폴백 체인이 무너진다(성장엔진 'LLM 종합 실패').
    def _size(name: str) -> float:
        m = re.search(r"(\d+(?:\.\d+)?)b", name.lower())
        return float(m.group(1)) if m else 0.0
    return max(models, key=_size)


def _json_ok(s: str) -> bool:
    """관대한 JSON 판정 — 코드펜스/서문 허용(호출부와 동일한 find-슬라이스 방식)."""
    try:
        json.loads(s[s.find("{"):s.rfind("}") + 1])
        return True
    except Exception:
        return False


def _ollama(prompt: str, system: str = "", max_tokens: int = 2000, temperature: float = 0.7,
            task: str = "", json_mode: bool = False) -> str | None:
    """Call Ollama local LLM. 소형 모델이 빈 응답/깨진 JSON을 뱉으면 최대 모델로 1회
    재시도(2026-07-02) — OLLAMA_MODEL 강제(e2b 등 속도 우선)가 구조화 프롬프트를 못 버텨
    전체 폴백이 무너지던 문제. 사용자의 소형 기본값은 존중하고 실패 시에만 승급."""
    try:
        now = time.time()
        if "ollama_models" not in _cache or now - _cache.get("ollama_ts", 0) > _CACHE_TTL:
            _cache["ollama_models"] = _list_ollama()
            _cache["ollama_ts"] = now
        available = _cache.get("ollama_models", [])
        model = os.getenv("OLLAMA_MODEL") or _pick_ollama(available, task)
        if not model:
            return None
        biggest = _pick_ollama(available, "")
        candidates = [model] + ([biggest] if biggest and biggest != model else [])

        messages = []
        if system:
            messages.append({"role": "system", "content": system})
        messages.append({"role": "user", "content": prompt})

        for m in candidates:
            payload = json.dumps({"model": m, "messages": messages, "max_tokens": max_tokens, "temperature": temperature}).encode()
            req = urllib.request.Request(_ollama_endpoint(), data=payload, headers={"Content-Type": "application/json"})
            with urllib.request.urlopen(req, timeout=120) as r:
                res = json.loads(r.read())
            result = res["choices"][0]["message"]["content"].strip()
            if result and (not json_mode or _json_ok(result)):
                print(f"  ✅ [Ollama:{m}] {len(result)} chars")
                return result
            why = "empty response" if not result else "invalid json"
            print(f"  ⚠️ [Ollama:{m}] {why}, falling back")
        return None
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
    lm_first: bool | None = None,
) -> str | None:
    """
    Generate text with a cost-safe fallback chain.

    Default(lm_first 미지정): Ollama → GPT → Gemini (AI_TEAM_LLM_PRIMARY 준수).
    lm_first=True: 명시적 로컬 우선. lm_first=False: 명시적 클라우드 우선(GPT→Gemini→Ollama)
    — 기존엔 False가 무시돼 '클라우드 우선' 호출(성장엔진·issue_impact)이 전부 로컬을 탔다(2026-07-02 수정).
    Set AI_TEAM_ALLOW_CLOUD_LLM=0 to block paid/cloud fallback entirely.
    """
    cloud_allowed = _cloud_llm_allowed()
    primary = os.getenv("AI_TEAM_LLM_PRIMARY", "ollama").strip().lower()
    local_first = lm_first if lm_first is not None else primary in {"local", "ollama"}

    if local_first:
        result = _ollama(prompt, system, max_tokens, temperature, task, json_mode)
        if result:
            return result
        if not cloud_allowed:
            return None
        result = _gpt(prompt, system, max_tokens, temperature, json_mode)
        if result:
            return result
        return _gemini(prompt, system, max_tokens, temperature, json_mode)

    if not cloud_allowed:
        return _ollama(prompt, system, max_tokens, temperature, task, json_mode)

    result = _gpt(prompt, system, max_tokens, temperature, json_mode)
    if result:
        return result
    result = _gemini(prompt, system, max_tokens, temperature, json_mode)
    if result:
        return result
    return _ollama(prompt, system, max_tokens, temperature, task, json_mode)


# Shorthand aliases
ollama = _ollama
gpt = _gpt
gemini = _gemini
