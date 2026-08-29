"""Unified LLM client.

현재 기본 체인:
Ollama(로컬) → Grok Build 구독 CLI → Codex 구독 CLI → Gemini.
Claude는 현재 구독이 없으므로 기본 체인에서 완전히 제외한다.

목표는 '모델을 많이 쓰기'가 아니라 로컬로 해결되지 않는 판단만 클라우드로 넘기는 것이다.
"""
from __future__ import annotations

import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request
from pathlib import Path

_cache: dict[str, object] = {}
_CACHE_TTL = 60
GEMINI_MODEL = "gemini-2.5-flash"
_NOWIN = {"creationflags": subprocess.CREATE_NO_WINDOW} if sys.platform == "win32" else {}


def _env_bool(name: str, default: str = "1") -> bool:
    return os.getenv(name, default).strip().lower() in {"1", "true", "yes", "on"}


def _cloud_llm_allowed() -> bool:
    return _env_bool("AI_TEAM_ALLOW_CLOUD_LLM", "1")


def _json_ok(s: str) -> bool:
    if not s:
        return False
    try:
        json.loads(s.strip())
        return True
    except Exception:
        pass
    dec = json.JSONDecoder()
    for i, ch in enumerate(s):
        if ch not in "[{":
            continue
        try:
            dec.raw_decode(s[i:])
            return True
        except Exception:
            continue
    return False


# -------------------- Ollama: local / zero cloud quota --------------------

def _ollama_base() -> str:
    raw = os.getenv("OLLAMA_URL", "http://localhost:11434").rstrip("/")
    for suffix in ("/v1/chat/completions", "/api/chat", "/v1", "/api"):
        if raw.endswith(suffix):
            raw = raw[: -len(suffix)]
            break
    return raw.rstrip("/")


def _is_ollama_cloud(name: str) -> bool:
    return name.split(":")[-1].lower().endswith("cloud")


def _list_ollama() -> list[str]:
    allow_cloud = _env_bool("AI_TEAM_ALLOW_OLLAMA_CLOUD", "0")
    try:
        with urllib.request.urlopen(_ollama_base() + "/v1/models", timeout=12) as r:
            data = json.loads(r.read())
        return [m.get("id", "") for m in data.get("data", [])
                if m.get("id") and "embed" not in m.get("id", "").lower()
                and (allow_cloud or not _is_ollama_cloud(m.get("id", "")))]
    except Exception:
        return []


def is_available() -> bool:
    return bool(_list_ollama())


def _pick_ollama(models: list[str], task: str = "") -> str | None:
    if not models:
        return None
    pinned = os.getenv("OLLAMA_MODEL", "").strip()
    if pinned and pinned in models:
        return pinned
    if task == "coding":
        for m in models:
            if any(k in m.lower() for k in ("coder", "code", "deepseek", "codestral", "qwen")):
                return m
    def size(name: str) -> float:
        mm = re.search(r"(\d+(?:\.\d+)?)b", name.lower())
        return float(mm.group(1)) if mm else 0.0
    return max(models, key=size)


def _ollama(prompt: str, system: str = "", max_tokens: int = 2000,
            temperature: float = 0.7, task: str = "", json_mode: bool = False) -> str | None:
    now = time.time()
    if "ollama_models" not in _cache or now - float(_cache.get("ollama_ts", 0) or 0) > _CACHE_TTL:
        _cache["ollama_models"] = _list_ollama()
        _cache["ollama_ts"] = now
    models = list(_cache.get("ollama_models", []) or [])
    first = _pick_ollama(models, task)
    if not first:
        return None
    candidates = [first] + [m for m in models if m != first]
    messages = []
    if system:
        messages.append({"role": "system", "content": system})
    messages.append({"role": "user", "content": prompt})
    url = _ollama_base() + "/api/chat"
    for model in candidates:
        body = {
            "model": model,
            "messages": messages,
            "stream": False,
            "think": False,
            "options": {"num_predict": max_tokens, "temperature": temperature},
        }
        if json_mode:
            body["format"] = "json"
        try:
            req = urllib.request.Request(url, data=json.dumps(body).encode("utf-8"),
                                         headers={"Content-Type": "application/json"})
            with urllib.request.urlopen(req, timeout=120) as r:
                data = json.loads(r.read())
            out = (data.get("message", {}).get("content") or "").strip()
            if out and (not json_mode or _json_ok(out)):
                print(f"  ✅ [Ollama:{model}] {len(out)} chars")
                return out
        except Exception as e:
            print(f"  ⚠️ [Ollama:{model}] {str(e)[:100]}")
    return None


# -------------------- subscription CLIs --------------------

def _find_cli(name: str) -> str | None:
    exe = shutil.which(name)
    if exe:
        return exe
    for p in (f"/usr/local/bin/{name}", f"/opt/homebrew/bin/{name}",
              str(Path.home() / ".local" / "bin" / name)):
        if os.path.isfile(p) and os.access(p, os.X_OK):
            return p
    return None


def _subscription_env(provider: str) -> dict[str, str]:
    env = os.environ.copy()
    if provider == "grok":
        env.pop("XAI_API_KEY", None)
    if provider == "codex":
        env.pop("OPENAI_API_KEY", None)
        env.pop("OPENAI_BASE_URL", None)
    return env


def _grok_help(exe: str) -> str:
    key = "grok_help:" + exe
    cached = _cache.get(key)
    if isinstance(cached, str):
        return cached
    for cmd in ([exe, "--no-auto-update", "--help"], [exe, "--help"]):
        try:
            r = subprocess.run(
                cmd, capture_output=True, text=True, timeout=15,
                encoding="utf-8", errors="replace", **_NOWIN
            )
            text = ((r.stdout or "") + "\n" + (r.stderr or "")).strip()
            if r.returncode == 0 and text:
                _cache[key] = text
                return text
        except Exception:
            continue
    _cache[key] = ""
    return ""


def _grok_command(exe: str, prompt: str) -> list[str] | None:
    """설치된 Grok가 지원하는 옵션만 사용해 1턴 절약형 명령을 만든다."""
    h = _grok_help(exe)
    if not h:
        return None
    single = "--single" if "--single" in h else ("-p" if "-p" in h else None)
    if single is None:
        return None
    for required in ("--cwd", "--output-format", "--max-turns"):
        if required not in h:
            return None

    cmd = [exe]
    if "--no-auto-update" in h:
        cmd.append("--no-auto-update")
    cmd += [single, prompt, "--cwd", os.getcwd(), "--output-format", "plain", "--max-turns", "1"]

    # 버전별 선택 절약 플래그. 없으면 생략하고 새 headless 세션 자체로 격리한다.
    for flag in ("--no-plan", "--no-subagents", "--no-memory", "--disable-web-search"):
        if flag in h:
            cmd.append(flag)
    return cmd


def _grok_build(prompt: str, system: str = "", max_tokens: int = 2000,
                temperature: float = 0.7, json_mode: bool = False) -> str | None:
    """Grok Build CLI 구독 세션. 일반 공용 작업은 읽기/판단 1턴만 사용한다."""
    if not _cloud_llm_allowed():
        return None
    exe = _find_cli("grok")
    if not exe:
        return None
    full = ((system + "\n\n") if system else "") + prompt
    if json_mode:
        full += "\n\n반드시 유효한 JSON만 출력하라. 설명·코드펜스 금지."
    cmd = _grok_command(exe, full)
    if not cmd:
        print("  ⚠️ [Grok] 현재 CLI에 필요한 headless 핵심 옵션이 없습니다.")
        return None
    try:
        r = subprocess.run(cmd, capture_output=True, text=True, timeout=180,
                           encoding="utf-8", errors="replace",
                           env=_subscription_env("grok"), **_NOWIN)
        out = (r.stdout or "").strip()
        if r.returncode != 0 or not out or (json_mode and not _json_ok(out)):
            if r.returncode != 0:
                print(f"  ⚠️ [Grok] exit {r.returncode}: {(r.stderr or '')[:120]}")
            return None
        print(f"  ✅ [Grok:구독] {len(out)} chars")
        return out
    except Exception as e:
        print(f"  ⚠️ [Grok] {str(e)[:100]}")
        return None


def _gpt_codex(prompt: str, system: str = "", max_tokens: int = 2000,
               temperature: float = 0.7, json_mode: bool = False) -> str | None:
    if not _cloud_llm_allowed():
        return None
    exe = _find_cli("codex")
    if not exe:
        return None
    full = ((system + "\n\n") if system else "") + prompt
    if json_mode:
        full += "\n\n반드시 유효한 JSON만 출력하라. 설명·코드펜스 금지."
    fd, outfile = tempfile.mkstemp(prefix="ailab_codex_", suffix=".txt")
    os.close(fd)
    try:
        r = subprocess.run([exe, "exec", "--skip-git-repo-check", "-o", outfile, full],
                           capture_output=True, text=True, timeout=180,
                           encoding="utf-8", errors="replace", stdin=subprocess.DEVNULL,
                           env=_subscription_env("codex"), **_NOWIN)
        if r.returncode != 0:
            print(f"  ⚠️ [Codex] exit {r.returncode}: {(r.stderr or '')[:120]}")
            return None
        out = Path(outfile).read_text(encoding="utf-8", errors="replace").strip()
        if not out or (json_mode and not _json_ok(out)):
            return None
        print(f"  ✅ [Codex:구독] {len(out)} chars")
        return out
    except Exception as e:
        print(f"  ⚠️ [Codex] {str(e)[:100]}")
        return None
    finally:
        try:
            os.unlink(outfile)
        except OSError:
            pass


def _claude_code(prompt: str, system: str = "", max_tokens: int = 2000,
                 temperature: float = 0.7, json_mode: bool = False) -> str | None:
    """호환용. 현재 Claude 구독 미사용이므로 호출하지 않는다."""
    return None


# -------------------- Gemini: last cloud fallback --------------------

def _gemini(prompt: str, system: str = "", max_tokens: int = 2000,
            temperature: float = 0.7, json_mode: bool = False) -> str | None:
    if not _cloud_llm_allowed():
        return None
    key = os.getenv("GEMINI_API_KEY", "").strip()
    if not key:
        return None
    contents = []
    if system:
        contents.append({"role": "user", "parts": [{"text": "[System]\n" + system}]})
        contents.append({"role": "model", "parts": [{"text": "understood"}]})
    contents.append({"role": "user", "parts": [{"text": prompt}]})
    generation = {
        "maxOutputTokens": max_tokens,
        "temperature": temperature,
        "thinkingConfig": {"thinkingBudget": 0},
    }
    if json_mode:
        generation["responseMimeType"] = "application/json"
    payload = {"contents": contents, "generationConfig": generation}
    url = f"https://generativelanguage.googleapis.com/v1beta/models/{GEMINI_MODEL}:generateContent?key={key}"
    try:
        req = urllib.request.Request(url, data=json.dumps(payload).encode("utf-8"),
                                     headers={"Content-Type": "application/json"})
        with urllib.request.urlopen(req, timeout=60) as r:
            data = json.loads(r.read())
        parts = data["candidates"][0]["content"]["parts"]
        texts = [p.get("text", "") for p in parts if p.get("text")]
        out = (texts[-1] if texts else "").strip()
        if out and (not json_mode or _json_ok(out)):
            print(f"  ✅ [Gemini] {len(out)} chars")
            return out
    except Exception as e:
        print(f"  ⚠️ [Gemini] {str(e)[:100]}")
    return None


# -------------------- public interface --------------------

def text(prompt: str, system: str = "", max_tokens: int = 2000,
         temperature: float = 0.7, json_mode: bool = False,
         task: str = "", lm_first: bool | None = None) -> str | None:
    """Generate text with a quota-conscious chain.

    Default: Ollama → Grok → Codex → Gemini.
    AI_TEAM_ALLOW_CLOUD_LLM=0 이면 Ollama만 사용한다.
    AI_TEAM_LLM_PRIMARY=cloud 이면 Grok/Codex/Gemini 뒤에 Ollama를 둔다.
    """
    local = lambda: _ollama(prompt, system, max_tokens, temperature, task, json_mode)
    clouds = [
        lambda: _grok_build(prompt, system, max_tokens, temperature, json_mode),
        lambda: _gpt_codex(prompt, system, max_tokens, temperature, json_mode),
        lambda: _gemini(prompt, system, max_tokens, temperature, json_mode),
    ]
    if not _cloud_llm_allowed():
        chain = [local]
    else:
        primary = os.getenv("AI_TEAM_LLM_PRIMARY", "ollama").strip().lower()
        local_first = lm_first if lm_first is not None else primary not in {"cloud", "grok", "codex"}
        chain = [local, *clouds] if local_first else [*clouds, local]
    for call in chain:
        out = call()
        if out:
            return out
    return None


ollama = _ollama
gemini = _gemini
grok_build = _grok_build
gpt_codex = _gpt_codex
claude_code = _claude_code
