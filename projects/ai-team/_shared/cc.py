"""Legacy Claude helper.

현재 운영에는 Claude 구독이 없으므로 직접 Claude 호출은 기본 비활성이다.
오래된 도구의 import 호환을 위해 함수는 유지하되, 명시적으로
AI_TEAM_ENABLE_CLAUDE=1 을 준 경우에만 CLI를 실행한다.
"""
from __future__ import annotations

import json
import os
import re
import shutil
import subprocess
import sys
from pathlib import Path

_NOWIN = {"creationflags": subprocess.CREATE_NO_WINDOW} if sys.platform == "win32" else {}
_SECRET_MARKERS = ("KEY", "TOKEN", "SECRET", "PASSWORD", "CREDENTIAL")


def scrub_secrets(env: dict) -> dict:
    out = {}
    for k, v in env.items():
        upper = k.upper()
        if not any(m in upper for m in _SECRET_MARKERS):
            out[k] = v
    return out


def _enabled() -> bool:
    return os.getenv("AI_TEAM_ENABLE_CLAUDE", "0").strip().lower() in {"1", "true", "yes", "on"}


def find_claude() -> str | None:
    if not _enabled():
        return None
    cli = shutil.which("claude")
    if cli:
        return cli
    for p in ("/usr/local/bin/claude", "/opt/homebrew/bin/claude",
              str(Path.home() / ".local" / "bin" / "claude")):
        if Path(p).exists():
            return p
    return None


def run_claude(prompt: str, cwd: str | Path, timeout: int = 900,
               allowed_tools: str = "WebSearch,WebFetch",
               permission_mode: str = "acceptEdits") -> tuple[bool, str]:
    """호환용 Claude 호출. 기본은 즉시 실패해 불필요한 로그인/재시도를 막는다."""
    if not _enabled():
        return False, "Claude 비활성: 현재 운영은 Claude 구독을 사용하지 않음"
    cli = find_claude()
    if not cli:
        return False, "claude CLI 미발견"
    cmd = [cli, "-p", "--permission-mode", permission_mode]
    if allowed_tools:
        cmd += ["--allowedTools", allowed_tools]
    env = scrub_secrets(os.environ.copy())
    try:
        r = subprocess.run(cmd, cwd=str(cwd), input=prompt, capture_output=True, text=True,
                           encoding="utf-8", errors="replace", timeout=timeout,
                           env=env, **_NOWIN)
        out = (r.stdout or "").strip() or (r.stderr or "").strip()
        return r.returncode == 0, out
    except subprocess.TimeoutExpired:
        return False, f"claude -p 타임아웃({timeout}s)"
    except Exception as e:
        return False, f"claude 실행 실패: {e}"


def extract_json(text: str):
    """응답에서 첫 JSON 객체/배열을 추출. 실패 시 None."""
    if not text:
        return None
    m = re.search(r"```(?:json)?\s*([\[{].*?[\]}])\s*```", text, re.DOTALL)
    candidates = [m.group(1)] if m else []
    for opener, closer in (("[", "]"), ("{", "}")):
        start = text.find(opener)
        if start != -1:
            end = text.rfind(closer)
            if end > start:
                candidates.append(text[start:end + 1])
    for cand in candidates:
        try:
            return json.loads(cand)
        except Exception:
            continue
    return None
