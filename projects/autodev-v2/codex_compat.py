#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""AutoDev v2용 Codex CLI 쿼터 보호 래퍼.

Codex 구독 한도 소진 문구를 감지하면 짧은 쿨다운을 두어 반복 호출을 막는다.
쿨다운 뒤 실제 호출이 성공하면 과거 한도 기록을 즉시 지운다.
"""
from __future__ import annotations

import json
import os
import subprocess
import sys
import time
from pathlib import Path

QUOTA_MARKERS = (
    "usage limit", "usage limits", "weekly limit", "quota exceeded",
    "quota exhausted", "limit reached", "you've hit your", "you have hit your",
    "insufficient quota",
)


def real_codex() -> str:
    value = os.environ.get("AUTODEV_REAL_CODEX", "").strip()
    if not value:
        raise RuntimeError("AUTODEV_REAL_CODEX가 설정되지 않았습니다.")
    p = Path(value)
    if not p.exists():
        raise RuntimeError(f"실제 Codex CLI를 찾을 수 없습니다: {value}")
    return str(p)


def quota_state_path() -> Path:
    explicit = os.environ.get("AUTODEV_CODEX_QUOTA_STATE", "").strip()
    if explicit:
        return Path(explicit).expanduser()
    return Path.home() / ".cache" / "autodev_v2" / "codex_quota_exhausted.json"


def cooldown_seconds() -> int:
    try:
        return max(60, int(os.environ.get("AUTODEV_CODEX_QUOTA_COOLDOWN_SECONDS", "300")))
    except ValueError:
        return 300


def cooldown_active() -> bool:
    try:
        data = json.loads(quota_state_path().read_text(encoding="utf-8"))
        return time.time() - float(data.get("detected_at", 0)) < cooldown_seconds()
    except Exception:
        return False


def looks_like_quota_error(text: str) -> bool:
    lower = text.lower()
    return any(marker in lower for marker in QUOTA_MARKERS)


def mark_exhausted(output: str) -> None:
    p = quota_state_path()
    try:
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(json.dumps({
            "detected_at": time.time(),
            "reason": "Codex subscription usage exhausted",
            "sample": output[-1200:],
        }, ensure_ascii=False, indent=2), encoding="utf-8")
    except Exception:
        pass


def clear_exhausted() -> None:
    try:
        p = quota_state_path()
        if p.exists():
            p.unlink()
    except OSError:
        pass


def run_real(exe: str, args: list[str]) -> tuple[int, str]:
    try:
        r = subprocess.run([exe, *args], capture_output=True, text=True,
                           encoding="utf-8", errors="replace", env=os.environ.copy())
        if r.stdout:
            sys.stdout.write(r.stdout)
        if r.stderr:
            sys.stderr.write(r.stderr)
        return r.returncode, ((r.stdout or "") + "\n" + (r.stderr or "")).strip()
    except Exception as e:
        text = f"codex compat exec error: {type(e).__name__}: {e}"
        print(text, file=sys.stderr)
        return 125, text


def main() -> int:
    try:
        exe = real_codex()
    except RuntimeError as e:
        print(f"codex compat error: {e}", file=sys.stderr)
        return 127

    args = sys.argv[1:]
    local_only = any(x in args for x in ("--help", "-h", "--version", "version"))
    if not local_only and cooldown_active():
        print("[CODEX-QUOTA] 최근 한도 소진 감지. 5분 보호시간 동안 실제 호출을 생략합니다.", file=sys.stderr)
        return 88

    rc, output = run_real(exe, args)
    if not local_only and rc != 0 and looks_like_quota_error(output):
        mark_exhausted(output)
        print("[CODEX-QUOTA] 사용 한도 소진 감지. 5분 후 자동 재확인합니다.", file=sys.stderr)
        return 88
    if not local_only and rc == 0:
        clear_exhausted()
    return rc


if __name__ == "__main__":
    raise SystemExit(main())
