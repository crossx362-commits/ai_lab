"""Unified LLM client - Ollama → GPT → Gemini → Claude fallback chain."""
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
ANTHROPIC_MODEL = os.getenv("ANTHROPIC_MODEL", "claude-haiku-4-5-20251001")  # 최저가 haiku 고정 — 비용 최소화(오너 지시 2026-07-05). 고품질 필요 시 .env ANTHROPIC_MODEL 오버라이드


def _env_bool(name: str, default: str = "1") -> bool:
    return os.getenv(name, default).strip().lower() in {"1", "true", "yes", "on"}


def _cloud_llm_allowed() -> bool:
    return _env_bool("AI_TEAM_ALLOW_CLOUD_LLM", "1")


# ==================== OLLAMA (LOCAL, FREE) ====================

def _ollama_base() -> str:
    """Ollama 호스트 루트. OLLAMA_URL에 구 경로(/v1/chat/completions 등)가 붙어와도 루트만 취한다
    — 7/3 네이티브 /api/chat 전환 후 엔드포인트를 여기서 조립(더는 죽은 주소를 replace 안 함)."""
    raw = os.getenv("OLLAMA_URL", "http://localhost:11434")
    for suffix in ("/v1/chat/completions", "/api/chat", "/v1", "/api"):
        if raw.endswith(suffix):
            raw = raw[: -len(suffix)]
            break
    return raw.rstrip("/")


def _list_ollama() -> list[str]:
    """List Ollama models (exclude embeddings)."""
    for attempt in range(3):
        try:
            url = _ollama_base() + "/v1/models"
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
    """Call Ollama local LLM (네이티브 /api/chat + think=false — 2026-07-03).
    e2b '빈 응답'의 진범은 thinking 모델: OpenAI 호환(/v1) 경로에선 추론이 reasoning 필드로
    새고 max_tokens를 추론이 소진해 content가 빈다. 네이티브 API에서 추론을 끄면 정상+고속.
    소형 모델이 빈 응답/깨진 JSON이면 다음 후보로 승급(2026-07-02) — 소형 기본값 존중."""
    try:
        now = time.time()
        if "ollama_models" not in _cache or now - _cache.get("ollama_ts", 0) > _CACHE_TTL:
            _cache["ollama_models"] = _list_ollama()
            _cache["ollama_ts"] = now
        available = _cache.get("ollama_models", [])
        # 동적 연결: 핀(OLLAMA_MODEL)이 실제 설치돼 있으면 존중(고속 e2b 기본 유지),
        # 사라졌으면 죽은 모델에 헛방 안 쏘고 설치 목록에서 자동 선택.
        pinned = os.getenv("OLLAMA_MODEL", "").strip()
        if pinned and (pinned in available or not available):
            model = pinned
        else:
            model = _pick_ollama(available, task)
        if not model:
            return None
        biggest = _pick_ollama(available, "")
        candidates = [model] + ([biggest] if biggest and biggest != model else [])
        # 선순위 후보가 비챗 모델(400 "does not support chat" — gemma4:e2b 사고 2026-07-03)이거나
        # 죽어도 로컬 최후선이 유지되도록 나머지 설치 모델을 후순위로 전부 편성.
        candidates += [m for m in available if m not in candidates]

        messages = []
        if system:
            messages.append({"role": "system", "content": system})
        messages.append({"role": "user", "content": prompt})

        url = _ollama_base() + "/api/chat"

        def _post(payload):
            req = urllib.request.Request(url, data=json.dumps(payload).encode(),
                                         headers={"Content-Type": "application/json"})
            with urllib.request.urlopen(req, timeout=120) as r:
                return json.loads(r.read())

        for m in candidates:
            try:
                body = {"model": m, "messages": messages, "stream": False, "think": False,
                        "options": {"num_predict": max_tokens, "temperature": temperature}}
                if json_mode:
                    # Ollama 네이티브 JSON 강제 — 로컬도 파싱 가능한 JSON만 출력(2026-07-05).
                    # 이게 없으면 gemma가 'JSON 드릴게요' 잡문을 뱉어 issue_impact가 클라우드에만 의존했다.
                    body["format"] = "json"
                try:
                    res = _post(body)
                except urllib.error.HTTPError as he:
                    detail = ""
                    try:
                        detail = he.read().decode()[:200]
                    except Exception:
                        pass
                    if "think" not in detail.lower():
                        raise
                    # 비사고 모델이 think 파라미터를 거부하면 빼고 1회 재시도
                    body.pop("think", None)
                    res = _post(body)
                result = (res.get("message", {}).get("content") or "").strip()
            except Exception as e:
                # 후보 하나의 실패(비챗 모델 400 등)로 로컬 전체를 포기하지 않는다 — 다음 모델 시도
                print(f"  ⚠️ [Ollama:{m}] {e}, falling back")
                continue
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


# ==================== CLAUDE (CLOUD, PAID) ====================

def _claude(prompt: str, system: str = "", max_tokens: int = 2000, temperature: float = 0.7, json_mode: bool = False) -> str | None:
    """Call Anthropic Claude (Messages API 직접 호출 — 다른 클라우드와 동일하게 무SDK).
    주의: Opus 4.8만 temperature 미지원(400) — opus 모델일 때만 전송 생략, haiku/sonnet은 전송해
    분류(temperature=0) 등이 제대로 반영되게 한다(2026-07-05 haiku 전환).
    json_mode는 응답 강제 파라미터·프리필이 없어(프리필 400) 지시문 + _json_ok 검증으로 대체."""
    if not _cloud_llm_allowed():
        print("  ⏭️ [Claude] blocked by AI_TEAM_ALLOW_CLOUD_LLM=0")
        return None
    api_key = os.getenv("ANTHROPIC_API_KEY", "")
    if not api_key:
        return None
    # 호출 시점에 재조회 — 데몬이 load_env() 전에 이 모듈을 임포트해도 최신 설정 반영
    model = os.getenv("ANTHROPIC_MODEL", ANTHROPIC_MODEL) or ANTHROPIC_MODEL
    try:
        user_prompt = prompt + ("\n\n반드시 유효한 JSON만 출력하라. 설명·코드펜스 금지." if json_mode else "")
        payload = {"model": model, "max_tokens": max_tokens,
                   "messages": [{"role": "user", "content": user_prompt}]}
        if "opus" not in model:  # Opus만 temperature 미지원 — haiku/sonnet은 전송(분류 temperature=0 반영)
            payload["temperature"] = temperature
        if system:
            payload["system"] = system
        req = urllib.request.Request(
            "https://api.anthropic.com/v1/messages",
            data=json.dumps(payload).encode(),
            headers={"Content-Type": "application/json", "x-api-key": api_key,
                     "anthropic-version": "2023-06-01"},
        )
        with urllib.request.urlopen(req, timeout=120) as r:
            d = json.loads(r.read())
        if d.get("stop_reason") == "refusal":
            print("  ❌ [Claude] refusal")
            return None
        result = "".join(b.get("text", "") for b in d.get("content", []) if b.get("type") == "text").strip()
        if not result or (json_mode and not _json_ok(result)):
            print(f"  ⚠️ [Claude:{model}] {'empty' if not result else 'invalid json'}")
            return None
        print(f"  ✅ [Claude:{model}] {len(result)} chars")
        return result
    except urllib.error.HTTPError as e:
        # 에러 본문의 메시지를 노출 — "400 Bad Request"만으론 크레딧 소진/모델명 오류 구분 불가(2026-07-03)
        try:
            detail = json.loads(e.read()).get("error", {}).get("message", "")[:120]
        except Exception:
            detail = ""
        print(f"  ❌ [Claude] HTTP {e.code}: {detail}")
        return None
    except Exception as e:
        print(f"  ❌ [Claude] {e}")
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

    Default(lm_first 미지정): Ollama → Claude → Gemini (AI_TEAM_LLM_PRIMARY 준수).
    lm_first=True: 명시적 로컬 우선. lm_first=False: 명시적 클라우드 우선(Claude→Gemini→Ollama)
    — 기존엔 False가 무시돼 '클라우드 우선' 호출(성장엔진·issue_impact)이 전부 로컬을 탔다(2026-07-02 수정).
    GPT는 기본 체인에서 제거(오너 지시 2026-07-05: 클로드 사용) — 명시 호출(llm.gpt)만 가능.
    Set AI_TEAM_ALLOW_CLOUD_LLM=0 to block paid/cloud fallback entirely.
    """
    cloud_allowed = _cloud_llm_allowed()
    primary = os.getenv("AI_TEAM_LLM_PRIMARY", "ollama").strip().lower()
    local_first = lm_first if lm_first is not None else primary in {"local", "ollama"}

    local = lambda: _ollama(prompt, system, max_tokens, temperature, task, json_mode)
    cloud = [
        lambda: _claude(prompt, system, max_tokens, temperature, json_mode),
        lambda: _gemini(prompt, system, max_tokens, temperature, json_mode),
    ]

    # 클라우드 차단 시 로컬만. 아니면 우선순위대로 로컬±클라우드 순서 조립.
    if not cloud_allowed:
        chain = [local]
    else:
        chain = [local] + cloud if local_first else cloud + [local]

    for call in chain:
        result = call()
        if result:
            return result
    return None


# Shorthand aliases
ollama = _ollama
gpt = _gpt
gemini = _gemini
claude = _claude
