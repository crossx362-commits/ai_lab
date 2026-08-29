#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""AutoDev v2용 Grok CLI 호환 래퍼.

목적:
- Grok Build CLI 버전별 선택 옵션 차이 때문에 AutoDev가 rc=2로 죽지 않게 한다.
- 핵심 headless 옵션은 그대로 요구한다.
- 절약용 선택 옵션은 실제 설치 CLI가 지원할 때만 전달한다.

이 래퍼 자체는 모델을 호출하지 않고 `--help`만 읽어 호환성을 판단한다.
실제 Grok 바이너리는 AUTODEV_REAL_GROK 환경변수로 전달받는다.
"""
from __future__ import annotations

import os
import subprocess
import sys
from pathlib import Path

OPTIONAL_FLAGS = (
    "--no-plan",
    "--no-subagents",
    "--no-memory",
    "--disable-web-search",
)


def real_grok() -> str:
    value = os.environ.get("AUTODEV_REAL_GROK", "").strip()
    if not value:
        raise RuntimeError("AUTODEV_REAL_GROK가 설정되지 않았습니다.")
    p = Path(value)
    if not p.exists():
        raise RuntimeError(f"실제 Grok CLI를 찾을 수 없습니다: {value}")
    return str(p)


def help_text(exe: str) -> str:
    for cmd in ([exe, "--no-auto-update", "--help"], [exe, "--help"]):
        try:
            r = subprocess.run(
                cmd,
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
                timeout=15,
            )
            text = ((r.stdout or "") + "\n" + (r.stderr or "")).strip()
            if text:
                return text
        except Exception:
            continue
    return ""


def filter_args(args: list[str], supported_help: str) -> tuple[list[str], list[str]]:
    """미지원 선택 플래그만 제거한다. 값이 붙는 핵심 플래그는 건드리지 않는다."""
    out: list[str] = []
    dropped: list[str] = []
    for arg in args:
        if arg in OPTIONAL_FLAGS and arg not in supported_help:
            dropped.append(arg)
            continue
        out.append(arg)
    return out, dropped


def main() -> int:
    try:
        exe = real_grok()
    except RuntimeError as e:
        print(f"grok compat error: {e}", file=sys.stderr)
        return 127

    args = sys.argv[1:]
    h = help_text(exe)
    if not h:
        print("grok compat error: 실제 Grok --help를 읽지 못했습니다.", file=sys.stderr)
        return 126

    # autodev.py가 자신의 명령을 구성할 때 보는 help에는 선택 절약 플래그를 노출한다.
    # 실제 실행 때는 아래 filter_args가 현재 바이너리 미지원 플래그를 제거한다.
    if args in (["--help"], ["--no-auto-update", "--help"]):
        print(h)
        print("\n[AutoDev compatibility flags]")
        for flag in OPTIONAL_FLAGS:
            if flag not in h:
                print(f"  {flag}  (compat: unsupported by real CLI, omitted at execution)")
        return 0

    filtered, dropped = filter_args(args, h)
    if dropped:
        print(
            "[GROK-COMPAT] 현재 CLI 미지원 선택 옵션 생략: " + ", ".join(dropped),
            file=sys.stderr,
        )

    try:
        os.execve(exe, [exe, *filtered], os.environ.copy())
    except Exception as e:
        print(f"grok compat exec error: {e}", file=sys.stderr)
        return 125


if __name__ == "__main__":
    raise SystemExit(main())
