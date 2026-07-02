#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Somi watchlist manager — 감시 종목 추가/제거/조회"""

from __future__ import annotations

import json
import sys
from pathlib import Path


if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")


SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parents[4]
WATCHLIST_FILE = PROJECT_ROOT / "output" / "cache" / "somi_watchlist.json"


def load_watchlist() -> dict[str, str]:
    """감시 목록 로드 {종목코드: 종목명}"""
    if not WATCHLIST_FILE.exists():
        return {}  # 기본값 없음 — 사용자가 등록한 종목만 감시
    try:
        with WATCHLIST_FILE.open("r", encoding="utf-8") as f:
            return json.load(f)
    except Exception:
        return {}


def save_watchlist(watchlist: dict[str, str]) -> None:
    """감시 목록 저장"""
    WATCHLIST_FILE.parent.mkdir(parents=True, exist_ok=True)
    with WATCHLIST_FILE.open("w", encoding="utf-8") as f:
        json.dump(watchlist, f, ensure_ascii=False, indent=2)


def add_watch(symbol: str, name: str) -> str:
    """종목 추가"""
    symbol = symbol.strip()
    name = name.strip()

    if not symbol or not name:
        return "❌ 종목코드와 종목명을 모두 입력하세요."

    watchlist = load_watchlist()

    if symbol in watchlist:
        return f"⚠️ {name}({symbol})은 이미 감시 중입니다."

    watchlist[symbol] = name
    save_watchlist(watchlist)
    return f"✅ {name}({symbol})을 감시 목록에 추가했습니다.\n현재 {len(watchlist)}개 종목 감시 중"


def auto_register(items: list[dict], min_score: int = 60, cap: int = 15) -> list[str]:
    """발굴 종목 자동 등록 — min_score 이상만, 총 cap 개 상한, 중복 스킵.

    items: [{"symbol"/"code": str, "name": str, "score": int}, ...]
    반환: 새로 등록된 "이름(코드)" 목록.
    """
    watchlist = load_watchlist()
    added = []
    for it in sorted(items, key=lambda x: x.get("score", 0), reverse=True):
        code = (it.get("symbol") or it.get("code") or "").strip()
        name = (it.get("name") or "").strip()
        if not code or not name or it.get("score", 0) < min_score:
            continue
        if code in watchlist or len(watchlist) >= cap:
            continue
        watchlist[code] = name
        added.append(f"{name}({code})")
    if added:
        save_watchlist(watchlist)
    return added


def remove_watch(symbol: str) -> str:
    """종목 제거"""
    symbol = symbol.strip()

    if not symbol:
        return "❌ 종목코드를 입력하세요."

    watchlist = load_watchlist()

    if symbol not in watchlist:
        return f"⚠️ {symbol}은 감시 목록에 없습니다."

    name = watchlist.pop(symbol)
    save_watchlist(watchlist)
    return f"✅ {name}({symbol})을 감시 목록에서 제거했습니다.\n남은 종목: {len(watchlist)}개"


def list_watch() -> str:
    """감시 목록 조회"""
    watchlist = load_watchlist()

    if not watchlist:
        return "📋 감시 중인 종목이 없습니다."

    lines = [f"📋 감시 중인 종목 ({len(watchlist)}개)\n"]
    for symbol, name in watchlist.items():
        lines.append(f"• {name} ({symbol})")

    return "\n".join(lines)


def main() -> int:
    import argparse

    parser = argparse.ArgumentParser(description="소미 감시 종목 관리")
    parser.add_argument("action", choices=["add", "remove", "list"], help="추가/제거/조회")
    parser.add_argument("--symbol", help="종목코드")
    parser.add_argument("--name", help="종목명")
    args = parser.parse_args()

    if args.action == "add":
        if not args.symbol or not args.name:
            print("--symbol과 --name을 모두 입력하세요")
            return 1
        print(add_watch(args.symbol, args.name))
    elif args.action == "remove":
        if not args.symbol:
            print("--symbol을 입력하세요")
            return 1
        print(remove_watch(args.symbol))
    elif args.action == "list":
        print(list_watch())

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
