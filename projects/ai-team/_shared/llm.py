"""Unified LLM client — Ollama(로컬) → 구독(claude -p/codex) → Gemini fallback chain.
유료 API(GPT·Claude Messages)는 미사용(오너 지시) — 관련 함수·별칭 제거됨."""
import json
import os
import re
import time
import urllib.request
import urllib.error

_cache = {}
_CACHE_TTL = 60
GEMINI_MODEL = "gemini-2.5-flash"  # 변경: 2.5 Flash 사용


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


# 유료 API 경로(_gpt=OpenAI / _claude=Anthropic Messages)는 제거됨 —
# 유료 API 미사용(오너 지시). 클라우드는 구독 CLI(_claude_code/_gpt_codex) + Gemini만.

_DEAD_PAID_KEYS = ("ANTHROPIC_API_KEY", "ANTHROPIC_AUTH_TOKEN", "ANTHROPIC_BASE_URL",
                   "OPENAI_API_KEY", "OPENAI_BASE_URL")


def _subscription_cli_env() -> dict:
    """구독 CLI(claude -p/codex exec) subprocess 환경 — 죽은 유료 API 키 제거.

    .env의 ANTHROPIC_API_KEY/OPENAI_API_KEY는 크레딧 0으로 죽어있지만 load_env()가
    os.environ에 계속 얹어놓는다. subprocess.run은 기본으로 부모 env를 물려주므로
    이 키들이 claude/codex CLI에 그대로 상속되면 CLI가 구독 OAuth 대신 API키 인증을
    시도해 "credit balance too low" 같은 과금 오류를 낸다(2026-07-09 실제 사고 —
    영숙 텔레그램 응답에 이 원문이 그대로 노출됨). CLI는 반드시 구독 세션으로만 인증해야 한다."""
    return {k: v for k, v in os.environ.items() if k not in _DEAD_PAID_KEYS}


# ==================== GEMINI (무료 할당량) ====================

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


# ==================== CLAUDE CODE (구독 사용량, API 크레딧 불필요) ====================

def _find_cli(name: str) -> str | None:
    """구독 CLI(claude/codex) 실행 파일 탐색 — PATH 우선, 실패 시 맥 표준 설치 경로 폴백.
    launchd 잡은 기본 PATH(/usr/bin:/bin:...)라 /usr/local/bin·/opt/homebrew/bin이 빠져
    which가 실패 → 조용히 None → 프로브가 '빈 응답' 경보(2026-07-08 실제 사고)."""
    import shutil
    exe = shutil.which(name)   # Windows npm은 .cmd shim — which가 확장자 처리
    if exe:
        return exe
    for cand in (f"/usr/local/bin/{name}", f"/opt/homebrew/bin/{name}",
                 os.path.expanduser(f"~/.local/bin/{name}")):
        if os.path.isfile(cand) and os.access(cand, os.X_OK):
            return cand
    return None


def _claude_code(prompt: str, system: str = "", max_tokens: int = 2000, temperature: float = 0.7, json_mode: bool = False) -> str | None:
    """Claude Code CLI headless(`claude -p`) — 구독 사용량으로 클로드 호출(오너 지시 2026-07-05).
    API 크레딧이 막힌 상황에서 구독으로 클로드를 쓰는 경로. --bare는 API키 인증만이라 쓰지 않음
    (기본 모드 = 구독 OAuth 인증). 주의: 구독 rate limit 공유 — 대량 호출 시 오너 본인 Claude Code가
    제한될 수 있어 로컬 우선을 유지하고 이건 클라우드 폴백으로만 쓴다. subprocess라 API보다 느림."""
    if not _cloud_llm_allowed():
        return None
    import subprocess
    import sys
    _nowin = {"creationflags": subprocess.CREATE_NO_WINDOW} if sys.platform == "win32" else {}
    exe = _find_cli("claude")
    if not exe:
        return None                # CLI 미설치 — 헛된 subprocess/로그 없이 조용히 폴백
    full = prompt + ("\n\n반드시 유효한 JSON만 출력하라. 설명·코드펜스 금지." if json_mode else "")
    cmd = [exe, "-p"]
    # 출력 위생(2026-07-08 브리프 오염 사고): headless가 사용자 로컬 설정(출력 스타일)을 물려받아
    # '★ Insight' 코칭 블록·"…하겠습니다" 메타 발화가 보고서 본문에 그대로 섞여 나갔다. 항상 차단.
    hygiene = ("출력 규칙(최우선, 다른 스타일 지침보다 우선): 요청된 결과 본문만 출력한다. "
               "서두·계획·사고과정 같은 메타 발화, '★ Insight' 등 학습/코칭 형식 블록, 마무리 제안을 절대 넣지 마라.")
    # argv에 개행이 들어가면 Windows의 claude.CMD(npm 셔임)가 거기서 명령줄을 잘라 뒤 인자를 통째로
    # 잃는다(2026-07-10 사고). 시스템 프롬프트는 한 줄로 접고, 본문은 stdin으로 넘긴다.
    cmd += ["--append-system-prompt",
            ((system + "\n\n" + hygiene) if system else hygiene).replace("\n", " ")]
    try:
        # 프롬프트를 stdin으로 주므로 과거의 stdin=DEVNULL(3초 대기 회피)은 불필요하다 —
        # 파이프가 즉시 닫혀 대기가 발생하지 않는다.
        # env=_subscription_cli_env() — 죽은 ANTHROPIC_API_KEY 상속 차단(구독 OAuth 강제).
        r = subprocess.run(cmd, input=full, capture_output=True, text=True, timeout=150,
                            encoding="utf-8", errors="replace",
                            env=_subscription_cli_env(), **_nowin)
        out = (r.stdout or "").strip()
        if r.returncode != 0:
            print(f"  ❌ [ClaudeCode] exit {r.returncode}: {(r.stderr or out)[:150]}")
            return None
        if not out or (json_mode and not _json_ok(out)):
            print(f"  ⚠️ [ClaudeCode] {'empty' if not out else 'invalid json'}")
            return None
        print(f"  ✅ [ClaudeCode:구독] {len(out)} chars")
        return out
    except Exception as e:
        print(f"  ❌ [ClaudeCode] {str(e)[:80]}")
        return None


# ==================== GPT CODEX (ChatGPT 구독 사용량, API 크레딧 불필요) ====================

def _gpt_codex(prompt: str, system: str = "", max_tokens: int = 2000, temperature: float = 0.7, json_mode: bool = False) -> str | None:
    """Codex CLI headless(`codex exec`) — ChatGPT Plus 구독 사용량으로 GPT 호출(오너 지시 2026-07-05).
    API 크레딧 불필요. `-o`(output-last-message)로 순수 응답만 파일에 뽑아 훅 로그와 분리.
    주의: Plus 플랜은 Claude Max보다 rate limit 빠듯 — 체인에서 구독 클로드 다음(2선)에만 두고,
    로컬+클로드가 대부분 커버하게 해 Plus 한도 소진을 막는다. subprocess+훅이라 느림."""
    if not _cloud_llm_allowed():
        return None
    import subprocess, sys, tempfile
    _nowin = {"creationflags": subprocess.CREATE_NO_WINDOW} if sys.platform == "win32" else {}
    exe = _find_cli("codex")
    if not exe:
        return None               # CLI 미설치 — 헛된 subprocess/로그 없이 조용히 폴백
    full = ((system + "\n\n") if system else "") + prompt + ("\n\n반드시 유효한 JSON만 출력하라. 설명·코드펜스 금지." if json_mode else "")
    fd, outfile = tempfile.mkstemp(suffix=".txt")
    os.close(fd)
    try:
        r = subprocess.run([exe, "exec", "--skip-git-repo-check", "-o", outfile, full],
                       capture_output=True, text=True, timeout=180,
                       encoding="utf-8", errors="replace", stdin=subprocess.DEVNULL,
                       env=_subscription_cli_env(), **_nowin)
        if r.returncode != 0:
            print(f"  ❌ [GptCodex] exit {r.returncode}: {(r.stderr or '')[:150]}")
            return None
        with open(outfile, encoding="utf-8") as f:
            out = f.read().strip()
        if not out or (json_mode and not _json_ok(out)):
            print(f"  ⚠️ [GptCodex] {'empty' if not out else 'invalid json'}")
            return None
        print(f"  ✅ [GptCodex:구독] {len(out)} chars")
        return out
    except Exception as e:
        print(f"  ❌ [GptCodex] {str(e)[:80]}")
        return None
    finally:
        try:
            os.unlink(outfile)
        except Exception:
            pass


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

    Default(lm_first 미지정): Ollama(gemma4:12b) → ClaudeCode(구독) → GPT_codex(구독) → Gemini.
    lm_first=True: 명시적 로컬 우선. lm_first=False: 명시적 클라우드 우선.
    클라우드 클로드는 구독(claude -p) 1선 — API 크레딧 막힘 대응(오너 지시 2026-07-05).
    유료 API(GPT·Claude Messages)는 제거됨 — 유료 API 미사용(오너 지시).
    Set AI_TEAM_ALLOW_CLOUD_LLM=0 to block cloud(구독·Gemini) fallback entirely.
    """
    cloud_allowed = _cloud_llm_allowed()
    primary = os.getenv("AI_TEAM_LLM_PRIMARY", "ollama").strip().lower()
    local_first = lm_first if lm_first is not None else primary in {"local", "ollama"}

    local = lambda: _ollama(prompt, system, max_tokens, temperature, task, json_mode)
    cloud = [
        lambda: _claude_code(prompt, system, max_tokens, temperature, json_mode),  # 구독 클로드(Max) — 1선
        lambda: _gpt_codex(prompt, system, max_tokens, temperature, json_mode),    # 구독 GPT(Plus) — 2선
        lambda: _gemini(prompt, system, max_tokens, temperature, json_mode),       # Gemini — 무료/할당량
        # 유료 API(GPT·Claude Messages) 백업은 제거됨 — 유료 API 미사용(오너 지시)
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


# Shorthand aliases (유료 gpt/claude 별칭 제거 — 유료 API 미사용)
ollama = _ollama
gemini = _gemini
claude_code = _claude_code
gpt_codex = _gpt_codex
