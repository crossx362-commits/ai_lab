#!/usr/bin/env python3
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
AUTO = ROOT / "projects" / "autodev-v2"


def read(rel: str) -> str:
    return (ROOT / rel).read_text(encoding="utf-8")


def write(rel: str, text: str) -> None:
    (ROOT / rel).write_text(text, encoding="utf-8")


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"{label}: expected exactly 1 match, got {count}")
    return text.replace(old, new, 1)


def sub_once(text: str, pattern: str, repl: str, label: str, flags: int = 0) -> str:
    out, count = re.subn(pattern, repl, text, count=1, flags=flags)
    if count != 1:
        raise RuntimeError(f"{label}: expected exactly 1 regex match, got {count}")
    return out


def patch_webview() -> None:
    rel = "projects/autodev-v2/webview_app.py"
    text = read(rel)
    text = text.replace("import hashlib\n", "", 1)
    text = replace_once(
        text,
        "from codex_usage import cached_codex_usage, refresh_codex_usage\n",
        "from codex_usage import cached_codex_usage, refresh_codex_usage\nfrom runtime_contract import CONTROL_FILES, CONTROL_PROTOCOL, control_fingerprint\n",
        "webview shared contract import",
    )
    text = sub_once(
        text,
        r'\nCONTROL_PROTOCOL = 7\nCONTROL_FILES = \(\n.*?\n\)\n',
        "\n",
        "webview duplicate contract constants",
        flags=re.S,
    )
    text = sub_once(
        text,
        r'\ndef control_fingerprint\(\) -> str:\n.*?\n    return h\.hexdigest\(\)\[:16\]\n',
        "\n",
        "webview duplicate fingerprint",
        flags=re.S,
    )
    text = text.replace("control_fingerprint()", "control_fingerprint(HERE)")
    write(rel, text)


def patch_autodev() -> None:
    rel = "projects/autodev-v2/autodev.py"
    text = read(rel)
    old = '''    for flag in ("--no-plan", "--no-subagents", "--no-memory", "--disable-web-search"):\n        if not _has_flag(help_text, flag):\n            raise RuntimeError(f"현재 Grok CLI가 절약 옵션 {flag}를 지원하지 않습니다. `grok update`가 필요합니다.")\n        cmd.append(flag)\n'''
    new = '''    # Savings flags are optional across Grok CLI versions. Preflight warns when\n    # missing; runtime must follow the same policy instead of crashing.\n    for flag in ("--no-plan", "--no-subagents", "--no-memory", "--disable-web-search"):\n        if _has_flag(help_text, flag):\n            cmd.append(flag)\n'''
    text = replace_once(text, old, new, "optional Grok savings flags")
    text = text.replace("[AutoDev v3 추가 안전 규칙]", "[AutoDev v2 추가 안전 규칙]")
    text = replace_once(
        text,
        '''    if a.cmd == "run":\n        return run_loop(cfg, bool(a.continuous))\n''',
        '''    if a.cmd == "run":\n        # Direct CLI is compatibility-only. Replace this process with the canonical\n        # engine so the stale legacy execute_one/run_loop path is unreachable.\n        print("autodev.py run은 호환 진입점입니다. 단일 engine.py로 전환합니다.")\n        os.execv(sys.executable, [sys.executable, str(HERE / "engine.py")])\n        return 0\n''',
        "autodev direct run delegation",
    )
    write(rel, text)


def patch_functional_verify() -> None:
    rel = "projects/autodev-v2/functional_verify.py"
    text = read(rel)
    text = replace_once(
        text,
        'DEFAULT_AREAS = {"combat", "character", "progression", "items", "ui", "stage", "systems"}\n',
        'DEFAULT_AREAS = {"combat", "character", "progression", "items", "ui", "stage", "system", "systems", "estate", "formation", "raid", "fusion", "class_change"}\n',
        "functional default areas",
    )
    text = replace_once(
        text,
        '    configured = cfg.get("functional_verify_areas")\n',
        '    configured = cfg.get("functional_verify_areas", cfg.get("functional_verify_categories"))\n',
        "functional config alias",
    )
    write(rel, text)


def patch_runner() -> None:
    rel = "projects/autodev-v2/runner.py"
    text = read(rel).replace("[AutoDev v3 추가 안전 규칙]", "[AutoDev v2 추가 안전 규칙]")
    write(rel, text)


def main() -> None:
    patch_webview()
    patch_autodev()
    patch_functional_verify()
    patch_runner()
    print("audit fixes applied")


if __name__ == "__main__":
    main()
