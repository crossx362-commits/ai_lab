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
import sys
from pathlib import Path

# 콘솔 없는 데몬에서 claude.CMD(npm 셔임) 호출 시 매번 새 콘솔 창이 플래시되는 것을 막는다
# (2026-07-09 가드레일, 이 파일은 당시 전수 수정에서 빠져 있었음 — 2026-07-13 발견).
_NOWIN = {"creationflags": subprocess.CREATE_NO_WINDOW} if sys.platform == "win32" else {}

_DEAD_PAID_KEYS = ("ANTHROPIC_API_KEY", "ANTHROPIC_AUTH_TOKEN", "ANTHROPIC_BASE_URL")

# 헤드리스 세션에 시크릿을 상속시키지 않는다. 나무(PM)가 웹서치 결과를 백로그에 적재하고
# 수리가 그걸 읽어 코드를 쓰므로, 신뢰 불가 웹 텍스트가 코드 쓰는 세션의 입력이 된다
# (프롬프트 인젝션 표면). 백로그는 자동 병합되지 않지만, 인젝션된 지시가 세션 안에서
# 시크릿을 읽어 유출하는 것은 병합 게이트로 막지 못한다 → env에서 아예 제거한다.
_SECRET_MARKERS = ("KEY", "TOKEN", "SECRET", "PASSWORD", "CREDENTIAL")
# 패턴 오탐 보존: CLAUDE_* 는 구독 CLI 인증(예: CLAUDE_CODE_OAUTH_TOKEN)에 필요할 수 있고,
# MAX_THINKING_TOKENS는 시크릿이 아니라 동작 설정이다.
_SECRET_KEEP_PREFIX = ("CLAUDE",)
_SECRET_KEEP_EXACT = frozenset({"MAX_THINKING_TOKENS"})


def scrub_secrets(env: dict) -> dict:
    """시크릿 패턴에 걸리는 환경변수를 제거한다(보존목록 우선)."""
    out = {}
    for k, v in env.items():
        upper = k.upper()
        if upper in _SECRET_KEEP_EXACT or upper.startswith(_SECRET_KEEP_PREFIX):
            out[k] = v
        elif not any(m in upper for m in _SECRET_MARKERS):
            out[k] = v
    return out


def _subscription_env() -> dict:
    """죽은 ANTHROPIC_API_KEY(크레딧0) 상속 차단 — CLI가 API키 대신 구독 OAuth로 인증하게
    강제한다(2026-07-09: 이 키가 남아있으면 claude -p가 "credit balance too low"를
    응답처럼 뱉어 그대로 사용자에게 노출되는 사고가 났다).
    더해 모든 시크릿을 제거한다(scrub_secrets)."""
    return scrub_secrets({k: v for k, v in os.environ.items() if k not in _DEAD_PAID_KEYS})


def find_claude() -> str | None:
    cli = shutil.which("claude")
    if cli:
        return cli
    for p in ("/usr/local/bin/claude", "/opt/homebrew/bin/claude",
              str(Path.home() / ".local" / "bin" / "claude")):
        if Path(p).exists():
            return p
    return None


_OUTPUT_HYGIENE = ("출력 규칙(최우선, 다른 스타일 지침보다 우선): 요청된 결과 본문만 출력한다. "
                   "서두·계획·사고과정 같은 메타 발화, '★ Insight' 등 학습/코칭 형식 블록, 마무리 제안을 절대 넣지 마라.")


def run_claude(prompt: str, cwd: str | Path, timeout: int = 900,
               allowed_tools: str = "WebSearch,WebFetch",
               permission_mode: str = "acceptEdits") -> tuple[bool, str]:
    """헤드리스 클로드 실행. (성공여부, 응답/오류 텍스트) 반환."""
    cli = find_claude()
    if not cli:
        return False, "claude CLI 미발견 (PATH·표준 경로 모두 없음)"
    # 헤드리스 세션이 사용자 로컬 출력 스타일(learning 모드의 '★ Insight' 등)을 물려받아
    # 보고서 본문에 그대로 섞여 나가는 것을 막는다(2026-07-08 llm.py._claude_code 가드레일과
    # 동형 — 이 함수엔 빠져 있었다, 2026-07-13 발견: petnna_backend_guard.llm_analysis() 실
    # 호출에서 실제로 재현됨).
    cmd = [cli, "-p", "--permission-mode", permission_mode, "--append-system-prompt", _OUTPUT_HYGIENE]
    if allowed_tools:
        cmd += ["--allowedTools", allowed_tools]
    try:
        # 프롬프트는 argv가 아니라 stdin으로 전달한다 — Windows의 claude.CMD(npm 셔임)가
        # 개행이 든 argv를 첫 줄에서 잘라먹는다(2026-07-10 사고).
        r = subprocess.run(cmd, cwd=str(cwd), input=prompt, capture_output=True, text=True,
                            encoding="utf-8", errors="replace", timeout=timeout,
                            env=_subscription_env(), **_NOWIN)
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
