"""원본 로그 보존 + 비밀정보 마스킹.

모든 AI 원본 출력과 Unity 로그는 지우지 않고 파일로 남긴다(§2-11).
단 파일에 쓰기 전에 마스킹한다 — 로그가 유출 경로가 되면 안 된다(§17).
"""

from __future__ import annotations

import os
import re
from pathlib import Path

# 값 형태로 잡는 패턴 (키 이름을 몰라도 잡힌다)
_PATTERNS = [
    re.compile(r"sk-[A-Za-z0-9_\-]{16,}"),
    re.compile(r"sk-ant-[A-Za-z0-9_\-]{16,}"),
    re.compile(r"xai-[A-Za-z0-9_\-]{16,}"),
    re.compile(r"gh[pousr]_[A-Za-z0-9]{16,}"),
    re.compile(r"AIza[0-9A-Za-z_\-]{20,}"),
    re.compile(r"ey[A-Za-z0-9_\-]{10,}\.[A-Za-z0-9_\-]{10,}\.[A-Za-z0-9_\-]{10,}"),
    re.compile(r"-----BEGIN [A-Z ]*PRIVATE KEY-----[\s\S]*?-----END [A-Z ]*PRIVATE KEY-----"),
]

_SENSITIVE_ENV_HINT = re.compile(r"(KEY|TOKEN|SECRET|PASSWORD|PASSWD|CREDENTIAL)", re.I)

_MASK = "***REDACTED***"


def _env_secret_values() -> list[str]:
    out = []
    for k, v in os.environ.items():
        if not v or len(v) < 12:
            continue
        if _SENSITIVE_ENV_HINT.search(k):
            out.append(v)
    # 긴 값부터 지워야 부분 치환으로 새는 일이 없다
    return sorted(set(out), key=len, reverse=True)


def mask(text: str) -> str:
    if not text:
        return text
    for v in _env_secret_values():
        text = text.replace(v, _MASK)
    for pat in _PATTERNS:
        text = pat.sub(_MASK, text)
    return text


def write(path: Path, text: str) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(mask(text or ""), encoding="utf-8", errors="replace")
    return path


def tail(path: Path, limit: int = 8000) -> str:
    """로그 꼬리만 읽는다 — 긴 로그를 통째로 문맥에 싣지 않기 위해."""
    if not path.is_file():
        return ""
    data = path.read_text(encoding="utf-8", errors="replace")
    return data if len(data) <= limit else data[-limit:]
