#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Codex ChatGPT 구독 사용량을 app-server에서 읽어 ELI5용 스냅샷으로 변환한다.

공식 Codex app-server의 account/rateLimits/read 응답을 사용한다.
토큰/계정 정보는 직접 읽거나 출력하지 않는다.
"""
from __future__ import annotations

import json
import os
import queue
import shutil
import signal
import subprocess
import threading
import time
from pathlib import Path
from typing import Any

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
CACHE = REPO / "output" / "autodev_v2" / "codex_usage.json"


def _find_codex() -> str | None:
    exe = shutil.which("codex")
    if exe:
        return exe
    for p in (
        "/opt/homebrew/bin/codex",
        "/usr/local/bin/codex",
        str(Path.home() / ".local" / "bin" / "codex"),
    ):
        if os.path.isfile(p) and os.access(p, os.X_OK):
            return p
    return None


def _kill(p: subprocess.Popen[str]) -> None:
    try:
        if os.name != "nt":
            os.killpg(p.pid, signal.SIGTERM)
        else:
            p.terminate()
        p.wait(timeout=1)
    except Exception:
        try:
            p.kill()
        except Exception:
            pass


def _window(raw: Any, fallback_name: str) -> dict[str, Any] | None:
    if not isinstance(raw, dict):
        return None
    try:
        used = max(0, min(100, int(raw.get("usedPercent", 0) or 0)))
    except Exception:
        used = 0
    try:
        mins = int(raw.get("windowDurationMins", 0) or 0)
    except Exception:
        mins = 0
    try:
        reset = int(raw.get("resetsAt", 0) or 0)
    except Exception:
        reset = 0
    if 240 <= mins <= 420:
        name = "5시간"
    elif mins >= 24 * 60 * 5:
        name = "주간"
    else:
        name = fallback_name
    return {
        "name": name,
        "used_percent": used,
        "remaining_percent": max(0, 100 - used),
        "window_minutes": mins,
        "resets_at": reset or None,
    }


def _save(value: dict[str, Any]) -> dict[str, Any]:
    CACHE.parent.mkdir(parents=True, exist_ok=True)
    tmp = CACHE.with_suffix(".json.tmp")
    tmp.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")
    tmp.replace(CACHE)
    return value


def cached_codex_usage() -> dict[str, Any]:
    try:
        v = json.loads(CACHE.read_text(encoding="utf-8"))
        return v if isinstance(v, dict) else {}
    except Exception:
        return {}


def refresh_codex_usage(timeout: float = 10.0) -> dict[str, Any]:
    exe = _find_codex()
    base: dict[str, Any] = {
        "ok": False,
        "checked_at": time.time(),
        "source": "codex app-server account/rateLimits/read",
        "windows": [],
        "error": "",
    }
    if not exe:
        base["error"] = "Codex CLI를 찾을 수 없습니다."
        return _save(base)

    q: queue.Queue[str | None] = queue.Queue()
    try:
        p = subprocess.Popen(
            [exe, "app-server"],
            cwd=REPO,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
            text=True,
            encoding="utf-8",
            errors="replace",
            bufsize=1,
            start_new_session=(os.name != "nt"),
        )
    except Exception as e:
        base["error"] = f"Codex 사용량 조회 시작 실패: {type(e).__name__}: {e}"
        return _save(base)

    assert p.stdin is not None and p.stdout is not None

    def reader() -> None:
        try:
            for line in p.stdout:
                q.put(line.rstrip("\n"))
        finally:
            q.put(None)

    threading.Thread(target=reader, daemon=True).start()

    def send(obj: dict[str, Any]) -> None:
        p.stdin.write(json.dumps(obj, ensure_ascii=False) + "\n")
        p.stdin.flush()

    def wait_id(request_id: int, deadline: float) -> dict[str, Any] | None:
        while time.monotonic() < deadline:
            try:
                line = q.get(timeout=0.15)
            except queue.Empty:
                continue
            if line is None:
                return None
            try:
                msg = json.loads(line)
            except Exception:
                continue
            if isinstance(msg, dict) and msg.get("id") == request_id:
                return msg
        return None

    deadline = time.monotonic() + timeout
    try:
        send({
            "method": "initialize",
            "id": 1,
            "params": {
                "clientInfo": {"name": "autodev_v2", "title": "AutoDev v2", "version": "2.0"},
                "capabilities": {"experimentalApi": True},
            },
        })
        init = wait_id(1, deadline)
        if not init or init.get("error"):
            base["error"] = "Codex app-server 초기화 실패"
            if isinstance(init, dict) and init.get("error"):
                base["error"] += ": " + str(init.get("error"))[:500]
            return _save(base)

        send({"method": "initialized", "params": {}})
        send({"method": "account/rateLimits/read", "id": 2, "params": {}})
        reply = wait_id(2, deadline)
        if not reply:
            base["error"] = "Codex 사용량 응답 시간이 초과되었습니다."
            return _save(base)
        if reply.get("error"):
            err = reply.get("error")
            msg = err.get("message") if isinstance(err, dict) else err
            base["error"] = "Codex 사용량 조회 실패: " + str(msg)[:700]
            return _save(base)

        result = reply.get("result") if isinstance(reply.get("result"), dict) else {}
        limits = result.get("rateLimits") if isinstance(result.get("rateLimits"), dict) else {}
        windows = [
            x for x in (
                _window(limits.get("primary"), "짧은 한도"),
                _window(limits.get("secondary"), "긴 한도"),
            ) if x
        ]
        base.update({
            "ok": bool(windows),
            "windows": windows,
            "plan_type": limits.get("planType"),
            "limit_name": limits.get("limitName"),
            "rate_limit_reached_type": limits.get("rateLimitReachedType"),
            "error": "" if windows else "사용량 창 정보가 응답에 없습니다.",
        })
        return _save(base)
    except Exception as e:
        base["error"] = f"Codex 사용량 조회 오류: {type(e).__name__}: {e}"
        return _save(base)
    finally:
        try:
            p.stdin.close()
        except Exception:
            pass
        _kill(p)


if __name__ == "__main__":
    print(json.dumps(refresh_codex_usage(), ensure_ascii=False, indent=2))
