#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""AutoDev v2용 Grok CLI 호환/쿼터 래퍼.

책임은 두 가지뿐이다.
1) Grok Build CLI 버전별 선택 옵션 차이를 흡수한다.
2) 402/usage balance exhausted를 감지해 짧은 쿨다운 동안 실제 Grok 재호출을 막는다.

Director 라우팅은 runner.py가 담당한다.
실제 Grok 바이너리는 AUTODEV_REAL_GROK 환경변수로 전달받는다.
"""
from __future__ import annotations

import json
import os
import subprocess
import sys
import time
from pathlib import Path

OPTIONAL_FLAGS = (
    "--no-plan",
    "--no-subagents",
    "--no-memory",
    "--disable-web-search",
)

QUOTA_MARKERS = (
    "payment required",
    "usage balance exhausted",
    "weekly usage",
    "usage exhausted",
    "quota exhausted",
    "insufficient balance",
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
    """성공한 help 출력만 인정한다. 실패 Usage를 옵션 목록으로 오인하지 않는다."""
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
            if r.returncode == 0 and text:
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


def quota_state_path() -> Path:
    explicit = os.environ.get("AUTODEV_GROK_QUOTA_STATE", "").strip()
    if explicit:
        return Path(explicit).expanduser()
    return Path.home() / ".cache" / "autodev_v2" / "grok_quota_exhausted.json"


def quota_cooldown_seconds() -> int:
    try:
        return max(60, int(os.environ.get("AUTODEV_GROK_QUOTA_COOLDOWN_SECONDS", "3600")))
    except ValueError:
        return 3600


def quota_cooldown_active() -> bool:
    p = quota_state_path()
    try:
        data = json.loads(p.read_text(encoding="utf-8"))
        detected = float(data.get("detected_at", 0))
        return time.time() - detected < quota_cooldown_seconds()
    except Exception:
        return False


def mark_quota_exhausted(output: str) -> None:
    p = quota_state_path()
    try:
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(
            json.dumps(
                {
                    "detected_at": time.time(),
                    "reason": "Grok Build usage balance exhausted",
                    "sample": output[-1200:],
                },
                ensure_ascii=False,
                indent=2,
            ),
            encoding="utf-8",
        )
    except Exception:
        pass


def looks_like_quota_error(text: str) -> bool:
    lower = text.lower()
    return "402" in lower and any(marker in lower for marker in QUOTA_MARKERS)


def run_real_grok(exe: str, args: list[str]) -> tuple[int, str]:
    """실시간 출력을 전달하면서 마지막 결과를 모아 쿼터 오류를 판별한다."""
    collected: list[str] = []
    try:
        p = subprocess.Popen(
            [exe, *args],
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            encoding="utf-8",
            errors="replace",
            bufsize=1,
            env=os.environ.copy(),
        )
        assert p.stdout is not None
        for line in p.stdout:
            sys.stdout.write(line)
            sys.stdout.flush()
            collected.append(line.rstrip("\n"))
            if len(collected) > 300:
                collected = collected[-300:]
        rc = p.wait()
        return rc, "\n".join(collected)
    except Exception as e:
        text = f"grok compat exec error: {type(e).__name__}: {e}"
        print(text, file=sys.stderr)
        return 125, text


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

    if quota_cooldown_active():
        print(
            "[GROK-QUOTA] 최근 Grok Build 402 한도 소진을 감지해 실제 Grok 재호출을 생략합니다. "
            "Usage 리셋/추가 크레딧 후 다시 실행하세요.",
            file=sys.stderr,
        )
        return 88

    rc, output = run_real_grok(exe, filtered)
    if looks_like_quota_error(output):
        mark_quota_exhausted(output)
        print(
            "[GROK-QUOTA] 402 usage balance exhausted 감지. 이후 동일 호출은 쿨다운 동안 차단합니다.",
            file=sys.stderr,
        )
        return 88
    return rc


if __name__ == "__main__":
    raise SystemExit(main())
