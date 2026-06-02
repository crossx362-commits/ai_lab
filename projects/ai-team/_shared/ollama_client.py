"""
ollama_client.py — Ollama 로컬 AI 클라이언트 (OpenAI 호환 API)

환경변수 (선택):
  OLLAMA_URL   — 기본값: http://localhost:11434/v1/chat/completions
  OLLAMA_MODEL — 강제 모델 지정 (비우면 Ollama 로드된 모델 자동 감지)

task별 모델 우선순위 (같은 모델 여러 개일 때만 의미 있음):
  task="coding"  → deepseek/code/coder/codestral 포함 모델 우선
  task="blog"    → qwen 포함 (deepseek 제외) 모델 우선
  task=""        → 첫 번째 모델 사용
"""
import json
import urllib.request
import urllib.error
import os
import time

_cache: dict = {}
_CACHE_TTL = 60  # 1분 — 모델 변경 시 빠르게 반영


def _endpoint() -> str:
    return os.getenv("OLLAMA_URL", "http://localhost:11434/v1/chat/completions")


def _list_models() -> list:
    """Ollama에 로드된 텍스트 모델 목록 반환 (임베딩 제외)."""
    try:
        url = _endpoint().replace("/chat/completions", "/models")
        with urllib.request.urlopen(url, timeout=3) as r:
            data = json.loads(r.read())
        return [m["id"] for m in data.get("data", [])
                if "embed" not in m.get("id", "").lower()]
    except Exception:
        return []


def _pick_model(models: list, task: str) -> str | None:
    """task에 맞는 모델 선택. 매칭 없으면 첫 번째 모델 반환."""
    if not models:
        return None

    if task == "coding":
        keywords = ("deepseek", "code", "coder", "codestral")
        preferred = [m for m in models if any(k in m.lower() for k in keywords)]
        if preferred:
            return preferred[0]
    elif task == "blog":
        preferred = [m for m in models
                     if "qwen" in m.lower() and "deepseek" not in m.lower()]
        if preferred:
            return preferred[0]

    return models[0]


def _detect_model(task: str = "") -> str | None:
    """Ollama에서 자동 모델 감지. 60초 캐시."""
    explicit = os.getenv("OLLAMA_MODEL", "")
    if explicit:
        return explicit

    cache_key = task or "__any__"
    entry = _cache.get(cache_key)
    if entry and time.time() - entry[1] < _CACHE_TTL:
        return entry[0]

    models = _list_models()
    model_id = _pick_model(models, task)

    if model_id:
        label = f"[{task}]" if task else ""
        print(f"  [Ollama{label}] 자동 감지: {model_id}")
        _cache[cache_key] = (model_id, time.time())
    else:
        print("  [Ollama] 로드된 모델 없음 — `ollama pull <모델명>` 으로 설치하세요")

    return model_id


_JSON_SYSTEM = (
    "You must respond with valid JSON only. "
    "No explanations, no markdown fences, no extra text — pure JSON."
)


def chat(prompt: str, system: str = "", temperature: float = 0.7,
         max_tokens: int = 2000, json_mode: bool = False,
         task: str = "") -> str | None:
    """Ollama에 단일 프롬프트 전송 후 응답 텍스트 반환. 실패 시 None."""
    model_id = _detect_model(task)
    if model_id is None:
        return None

    print(f"  [로컬 AI → {model_id}]")
    effective_system = (_JSON_SYSTEM + "\n" + system if json_mode and system
                        else _JSON_SYSTEM if json_mode
                        else system)
    messages = []
    if effective_system:
        messages.append({"role": "system", "content": effective_system})
    messages.append({"role": "user", "content": prompt})

    payload = {
        "model": model_id,
        "messages": messages,
        "temperature": temperature,
        "max_tokens": max_tokens,
        "stream": False,
    }

    try:
        data = json.dumps(payload).encode("utf-8")
        req = urllib.request.Request(
            _endpoint(), data=data,
            headers={"Content-Type": "application/json"},
        )
        with urllib.request.urlopen(req, timeout=300) as r:
            res = json.loads(r.read())
        msg  = res["choices"][0]["message"]
        text = msg.get("content", "").strip()

        # thinking 모델 대응: content가 비어있으면 reasoning에서 추출
        if not text:
            reasoning = msg.get("reasoning", "").strip()
            finish    = res["choices"][0].get("finish_reason", "")
            if reasoning and finish in ("len", "length"):
                # thinking 모델: 토큰 한도로 추론 중단 → 충분한 토큰으로 재시도
                payload["max_tokens"] = max(max_tokens * 4, 2000)
                data2 = json.dumps(payload).encode("utf-8")
                req2  = urllib.request.Request(
                    _endpoint(), data=data2,
                    headers={"Content-Type": "application/json"},
                )
                with urllib.request.urlopen(req2, timeout=300) as r2:
                    res2 = json.loads(r2.read())
                text = res2["choices"][0]["message"].get("content", "").strip()
                if not text:
                    # 재시도도 실패하면 reasoning 마지막 문장 사용
                    lines = [l.strip() for l in reasoning.splitlines() if l.strip()]
                    text  = lines[-1] if lines else ""
            if not text:
                print(f"  [Ollama] 빈 응답 (model={model_id})")
                return None
        if json_mode and text.startswith("```"):
            lines = text.splitlines()
            text = "\n".join(l for l in lines if not l.strip().startswith("```")).strip()
        return text
    except urllib.error.URLError:
        return None
    except Exception as e:
        print(f"  [Ollama] 오류: {e}")
        return None


def chat_vision(prompt: str, image_bytes: bytes, max_tokens: int = 500) -> str | None:
    """이미지 + 텍스트를 Ollama 비전 모델에 전송. gemma3 계열 지원."""
    import base64 as _b64
    model_id = _detect_model("")
    if model_id is None:
        return None
    img_b64 = _b64.b64encode(image_bytes).decode()
    payload = {
        "model": model_id,
        "messages": [{"role": "user", "content": prompt, "images": [img_b64]}],
        "max_tokens": max_tokens,
        "stream": False,
    }
    try:
        data = json.dumps(payload).encode("utf-8")
        req = urllib.request.Request(
            _endpoint(), data=data,
            headers={"Content-Type": "application/json"},
        )
        with urllib.request.urlopen(req, timeout=120) as r:
            res = json.loads(r.read())
        return res["choices"][0]["message"].get("content", "").strip() or None
    except Exception as e:
        print(f"  [Ollama Vision] 오류: {e}")
        return None


def is_available() -> bool:
    """Ollama 서버가 응답하고 모델이 로드돼 있는지 확인."""
    return bool(_list_models())
