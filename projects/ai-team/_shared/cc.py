"""Claude Code 헤드리스(구독) 실행 헬퍼 — 펫나 에이전트 공용.

`claude -p`를 subprocess로 호출해 구독 사용량으로 클로드를 쓴다(API 크레딧 불필요).
- `--bare` 금지(구독 OAuth 무시), launchd PATH 폴백 필수 (가드레일 2026-07-05/07-08)
- 기본으로 WebSearch/WebFetch 허용 — 에이전트가 모르는 것은 웹서치로 해결
"""

from __future__ import annotations

import json
import os
import re
import shutil
import subprocess
from pathlib import Path

_DEAD_PAID_KEYS = ("ANTHROPIC_API_KEY", "ANTHROPIC_AUTH_TOKEN", "ANTHROPIC_BASE_URL")


def _subscription_env() -> dict:
    """죽은 ANTHROPIC_API_KEY(크레딧0) 상속 차단 — CLI가 API키 대신 구독 OAuth로 인증하게
    강제한다(2026-07-09: 이 키가 남아있으면 claude -p가 "credit balance too low"를
    응답처럼 뱉어 그대로 사용자에게 노출되는 사고가 났다)."""
    return {k: v for k, v in os.environ.items() if k not in _DEAD_PAID_KEYS}


def find_claude() -> str | None:
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
    """헤드리스 클로드 실행. (성공여부, 응답/오류 텍스트) 반환."""
    cli = find_claude()
    if not cli:
        return False, "claude CLI 미발견 (PATH·표준 경로 모두 없음)"
    cmd = [cli, "-p", "--permission-mode", permission_mode]
    if allowed_tools:
        cmd += ["--allowedTools", allowed_tools]
    try:
        # 프롬프트는 argv가 아니라 stdin으로 전달한다 — Windows의 claude.CMD(npm 셔임)가
        # 개행이 든 argv를 첫 줄에서 잘라먹는다(2026-07-10 사고).
        r = subprocess.run(cmd, cwd=str(cwd), input=prompt, capture_output=True, text=True,
                            encoding="utf-8", errors="replace", timeout=timeout,
                            env=_subscription_env())
        out = (r.stdout or "").strip() or (r.stderr or "").strip()
        return r.returncode == 0, out
    except subprocess.TimeoutExpired:
        return False, f"claude -p 타임아웃({timeout}s)"
    except Exception as e:
        return False, f"claude 실행 실패: {e}"


def extract_json(text: str):
    """응답에서 첫 JSON 객체/배열을 관대하게 추출. 실패 시 None."""
    if not text:
        return None
    # 코드펜스 우선
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
