#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""제목(argv[1]) + 본문(stdin)을 노션 보고서로 작성하고 페이지 URL을 stdout에 출력.

익스텐션 데일리 브리핑이 텔레그램 본문 대신 노션에 쓰기 위해 호출한다.
성공 시 URL 한 줄 출력, 실패(키 없음/오류) 시 아무것도 출력하지 않음 → 호출측이 본문 폴백.
"""
from __future__ import annotations

import sys
from pathlib import Path

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = AI_TEAM_ROOT.parents[1]
sys.path.insert(0, str(AI_TEAM_ROOT))

from _shared.env import load_env  # noqa: E402
from _shared import research  # noqa: E402


def main() -> None:
    title = sys.argv[1] if len(sys.argv) > 1 else "보고서"
    body = sys.stdin.read()
    if not body.strip():
        return
    load_env(str(REPO_ROOT))
    try:
        url = research.notion_report(title, body)
    except Exception:
        url = ""
    if url:
        print(url)


if __name__ == "__main__":
    main()
