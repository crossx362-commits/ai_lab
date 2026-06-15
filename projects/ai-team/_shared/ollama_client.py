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
_recovery_triggered = False  # 복구 중복 호출 방지


def _trigger_kodari_recovery():
    """Ollama 연결 실패 시 코다리 헬스체크·자동 복구 호출 (1회만)."""
    global _recovery_triggered
    if _recovery_triggered:
        return
    _recovery_triggered = True
    try:
        import threading, importlib.util as _ilu
        _here = os.path.dirname(os.path.abspath(__file__))
        _check_path = os.path.join(_here, "..", "skills", "코다리_개발자", "tools", "ollama_health_check.py")
        if not os.path.exists(_check_path):
            return
        def _run():
            global _recovery_triggered
            spec = _ilu.spec_from_file_location("ollama_health_check", _check_path)
            mod = _ilu.module_from_spec(spec)
            spec.loader.exec_module(mod)
            mod.run_check()
            _recovery_triggered = False  # 복구 후 다음 실패에도 재시도 허용
        threading.Thread(target=_run, daemon=True).start()
        print("  [코다리] Ollama 연결 실패 감지 → 자동 복구 시작...")
    except Exception as e:
        _recovery_triggered = False
        print(f"  [코다리] 복구 훅 실행 실패: {e}")


def _request_kodari_fix():
    print("  [Ollama Client] Ollama 연결 불가 - 코다리에게 헬스체크 및 자동 복구 요청...")
    try:
        import sys
        import os
        import subprocess
        # Get projects/ai-team directory path
        here = os.path.dirname(os.path.abspath(__file__))
        script_path = os.path.abspath(os.path.join(here, "..", "skills", "코다리_개발자", "tools", "ollama_health_check.py"))
        if os.path.exists(script_path):
            # Run the script synchronously to let it try to restart Ollama
            subprocess.run([sys.executable, script_path], capture_output=True, text=True, encoding="utf-8")
        else:
            print("  ❌ [Ollama Client] 코다리 헬스체크 스크립트를 찾을 수 없습니다.")
    except Exception as e:
        print(f"  ❌ [Ollama Client] 코다리 복구 요청 실패: {e}")


def _endpoint() -> str:
    return os.getenv("OLLAMA_URL", "http://localhost:11434/v1/chat/completions")


def _list_models() -> list:
    """Ollama에 로드된 텍스트 모델 목록 반환 (임베딩 제외). 타임아웃 시 재시도."""
    for attempt in range(3):
        try:
            url = _endpoint().replace("/chat/completions", "/models")
            timeout = 45 if attempt == 0 else 15  # 첫 시도 45초, 재시도 15초 (콜드 스타트 대응)
            with urllib.request.urlopen(url, timeout=timeout) as r:
                data = json.loads(r.read())
            return [m["id"] for m in data.get("data", [])
                    if "embed" not in m.get("id", "").lower()]
        except Exception as e:
            if attempt < 2:
                print(f"  [Ollama] 모델 목록 조회 재시도 ({attempt + 1}/3): {e}")
                time.sleep(2)
            continue
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
    """Ollama에서 자동 모델 감지. 60초 캐시. 연결 실패 시 코다리에게 복구 요청 후 대기."""
    explicit = os.getenv("OLLAMA_MODEL", "")
    if explicit:
        return explicit

    cache_key = task or "__any__"
    entry = _cache.get(cache_key)
    if entry and time.time() - entry[1] < _CACHE_TTL:
        return entry[0]

    models = _list_models()
    
    if not models:
        max_retries = 30
        for i in range(max_retries):
            _request_kodari_fix()
            print(f"  [Ollama Client] Ollama 복구 대기 중... ({i+1}/{max_retries})")
            time.sleep(10)
            models = _list_models()
            if models:
                print("  [Ollama Client] ✅ Ollama 연결 복구 완료! 작업을 재개합니다.")
                break

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
    try:
        import inspect
        is_dave = False
        for frame in inspect.stack():
            f_path = frame.filename.lower()
            if "데이브" in f_path or "dave" in f_path or "stock_analyzer" in f_path or "upbit_analyzer" in f_path:
                is_dave = True
                break
        if is_dave:
            import _shared.gemini_client as _gc
            res = _gc.text(
                prompt, system=system, temperature=temperature,
                max_tokens=max_tokens, json_mode=json_mode, task=task,
                lm_first=False
            )
            if res:
                return res
    except Exception as ge:
        print(f"  [Ollama Client] Gemini 우선 호출 실패 (Ollama 폴백 진행): {ge}")

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
    if json_mode:
        payload["response_format"] = {"type": "json_object"}
        payload["format"] = "json"

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
        _trigger_kodari_recovery()
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
    """Ollama 서버가 응답하고 모델이 로드돼 있는지 확인. 연결 실패 시 코다리에게 복구를 요청하고 대기."""
    if os.getenv("GEMINI_API_KEY", "").strip():
        return True
    models = _list_models()
    if bool(models):
        return True
        
    max_retries = 30
    for i in range(max_retries):
        _request_kodari_fix()
        print(f"  [Ollama Client] Ollama 복구 대기 중... ({i+1}/{max_retries})")
        time.sleep(10)
        models = _list_models()
        if bool(models):
            print("  [Ollama Client] ✅ Ollama 연결 복구 완료! 작업을 재개합니다.")
            return True
            
    return False
