#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""영숙 봇 공유 헬퍼 — 의도 판별·서브프로세스 실행.

게이트웨이(telegram_receiver)와 도메인 툴 모듈(yewon/info)이 함께 쓴다.
이 모듈은 도메인 모듈을 import하지 않는다(순환 방지 — 의존은 언제나 이쪽으로 들어온다)."""

from __future__ import annotations

import os
import re
import subprocess
import sys
from datetime import datetime
from pathlib import Path

HERE = Path(__file__).resolve()
AI_TEAM_ROOT = HERE.parents[3]
PROJECT_ROOT = AI_TEAM_ROOT.parents[1]
if str(AI_TEAM_ROOT) not in sys.path:
    sys.path.insert(0, str(AI_TEAM_ROOT))


def log(message: str) -> None:
    stamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    print(f"[{stamp}] {message}", flush=True)


def run_python(script: Path, *args: str, timeout: int = 60) -> str:
    if not script.exists():
        return f"경로가 없습니다: {script}"
    env = os.environ.copy()
    env["PYTHONUTF8"] = "1"
    kwargs = {}
    if sys.platform == "win32":
        kwargs["creationflags"] = subprocess.CREATE_NO_WINDOW
    result = subprocess.run(
        [sys.executable, str(script), *args],
        cwd=str(PROJECT_ROOT),
        env=env,
        capture_output=True,
        text=True,
        timeout=timeout,
        **kwargs,
    )
    output = (result.stdout or "").strip()
    error = (result.stderr or "").strip()
    if result.returncode != 0:
        return error or output or f"실행 실패: {result.returncode}"
    return output or "완료했습니다."


def normalize_text(text: str) -> str:
    return re.sub(r"\s+", "", text or "").lower()


def is_search_request(text: str) -> bool:
    normalized = normalize_text(text)
    return any(word in normalized for word in ("검색", "찾아봐", "찾아줘", "최신자료", "자료찾"))


def is_bots_off_request(text: str) -> bool:
    """맥 봇 전체 원격 종료 지시 — '봇 다 꺼/맥봇 전부 중지/전체 종료' 등."""
    n = normalize_text(text).replace(" ", "")
    if not any(w in n for w in ("봇", "맥봇", "전체", "다", "전부")):
        return False
    return any(w in n for w in ("꺼", "끄", "종료", "중지", "정지", "내려", "죽여")) and "켜" not in n


def is_bots_on_request(text: str) -> bool:
    """맥 봇 전체 원격 기동 지시 — '봇 다 켜/맥봇 전부 시작' 등."""
    n = normalize_text(text).replace(" ", "")
    if not any(w in n for w in ("봇", "맥봇", "전체", "다", "전부")):
        return False
    return any(w in n for w in ("켜", "켜줘", "시작", "가동", "올려", "살려"))


def is_weather_request(text: str) -> bool:
    n = normalize_text(text)
    return any(w in n for w in ("날씨", "기온", "더워", "추워", "비와", "비온", "눈와", "미세먼지", "weather"))
