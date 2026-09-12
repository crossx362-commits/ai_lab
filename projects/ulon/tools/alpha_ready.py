#!/usr/bin/env python3
"""Closed Alpha 준비 판정.

data/alpha_status.json 의 ok:true 는 마지막 스모크 기록이지 현재 성공이 아니다.
현재 준비는 persist /ready 실측만으로 판정한다.
"""
from __future__ import annotations

import argparse
import json
import sys
import urllib.error
import urllib.request
from pathlib import Path

DEFAULT_READY = "http://127.0.0.1:8777/ready"


def probe_ready(url: str = DEFAULT_READY, timeout: float = 1.5) -> dict:
    try:
        with urllib.request.urlopen(url, timeout=timeout) as resp:
            raw = resp.read().decode("utf-8", "replace")
            status = int(resp.status)
    except urllib.error.HTTPError as e:
        raw = e.read().decode("utf-8", "replace") if e.fp else ""
        return _parse_live(raw, http=int(e.code), error=str(e))
    except Exception as e:
        return {"ok": False, "http": 0, "body": {}, "error": str(e)}
    return _parse_live(raw, http=status, error="")


def _parse_live(raw: str, *, http: int, error: str) -> dict:
    try:
        body = json.loads(raw) if raw else {}
    except json.JSONDecodeError:
        return {
            "ok": False,
            "http": http,
            "body": {},
            "error": error or "not-json",
        }
    if not isinstance(body, dict):
        return {"ok": False, "http": http, "body": {}, "error": "not-object"}
    ok = http == 200 and body.get("ok") is True
    return {"ok": ok, "http": http, "body": body, "error": error}


def file_claim(path: Path) -> dict:
    if not path.is_file():
        return {"present": False, "ok_claim": False, "body": {}}
    try:
        body = json.loads(path.read_text(encoding="utf-8"))
    except Exception as e:
        return {"present": True, "ok_claim": False, "body": {}, "error": str(e)}
    if not isinstance(body, dict):
        return {"present": True, "ok_claim": False, "body": {}, "error": "not-object"}
    return {"present": True, "ok_claim": body.get("ok") is True, "body": body}


def judge(path: Path, probe=None, url: str = DEFAULT_READY, timeout: float = 1.5) -> dict:
    live = probe() if probe is not None else probe_ready(url, timeout=timeout)
    claim = file_claim(path)
    live_ok = bool(live.get("ok"))
    stale = bool(claim.get("ok_claim") and not live_ok)
    if live_ok:
        reason = "live /ready ok"
        if not claim.get("present"):
            reason += " (스모크 파일 없음)"
    elif stale:
        reason = "파일은 ok:true 인데 live /ready 실패 — 옛 성공"
    elif live.get("error"):
        reason = "live /ready 실패: " + str(live.get("error"))
    else:
        reason = "live /ready 아님"
    return {
        "ok": live_ok,
        "stale_file": stale,
        "file": claim,
        "live": live,
        "reason": reason,
    }


def invalidate(path: Path) -> bool:
    """성공 주장을 지운다. 파일이 없으면 True."""
    if not path.exists():
        return True
    try:
        path.unlink()
        return True
    except OSError:
        return False


def main(argv=None) -> int:
    p = argparse.ArgumentParser()
    p.add_argument("cmd", nargs="?", default="judge", choices=("judge", "invalidate"))
    p.add_argument("--path", default="")
    p.add_argument("--url", default=DEFAULT_READY)
    args = p.parse_args(argv)
    root = Path(__file__).resolve().parents[1]
    path = Path(args.path) if args.path else root / "data" / "alpha_status.json"
    if args.cmd == "invalidate":
        ok = invalidate(path)
        print(json.dumps({"ok": ok, "removed": not path.exists()}, ensure_ascii=False))
        return 0 if ok else 1
    result = judge(path, url=args.url)
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if result["ok"] else 2


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    raise SystemExit(main())
