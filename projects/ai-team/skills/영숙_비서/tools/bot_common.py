#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""영숙 봇 공유 헬퍼 — 종목 해석·의도 판별·서브프로세스 실행.

게이트웨이(telegram_receiver)와 도메인 툴 모듈(somi/yewon/info)이 함께 쓴다.
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


_STOCK_ALIASES: dict[str, tuple[str, str]] = {
    "삼전": ("005930", "삼성전자"),
    "삼성": ("005930", "삼성전자"),
    "삼성전자": ("005930", "삼성전자"),
    "하이닉스": ("000660", "SK하이닉스"),
    "sk하이닉스": ("000660", "SK하이닉스"),
    "sk하닉": ("000660", "SK하이닉스"),
    "하닉": ("000660", "SK하이닉스"),
    "카카오": ("035720", "카카오"),
    "네이버": ("035420", "NAVER"),
    "현대차": ("005380", "현대차"),
    "기아": ("000270", "기아"),
    "우리기술": ("032820", "우리기술"),
}

_STOCK_CMD_WORDS = [
    "분석해줘", "분석해", "분석", "리포트", "보고서", "보고", "전망", "주가", "현재가",
    "가격", "종목", "알려줘", "보여줘", "해줘", "봐줘", "어때", "체크", "확인", "해봐",
    "말해줘", "좀", "오늘", "지금",
]


def normalize_text(text: str) -> str:
    return re.sub(r"\s+", "", text or "").lower()


def _extract_stock_query(text: str) -> str:
    """문장에서 명령어/조사를 제거해 종목명 후보만 추출."""
    t = text or ""
    for word in _STOCK_CMD_WORDS:
        t = t.replace(word, " ")
    t = re.sub(r"\d{6}", " ", t)
    t = re.sub(r"[^0-9A-Za-z가-힣 ]", " ", t)
    return re.sub(r"\s+", " ", t).strip()


def _resolve_from_search(text: str) -> tuple[str, str] | None:
    """본문에서 종목 해석. 로컬 주요 종목 맵 → 네이버 자동완성(임의 종목) 순."""
    try:
        sys.path.insert(0, str(AI_TEAM_ROOT / "skills" / "소미_분석가" / "tools"))
        from stock_search import MAJOR_STOCKS, STOCK_NAME_ALIASES, naver_search
    except Exception:
        return None
    normalized = normalize_text(text)
    for alias, name in sorted(STOCK_NAME_ALIASES.items(), key=lambda i: len(i[0]), reverse=True):
        if normalize_text(alias) in normalized and name in MAJOR_STOCKS:
            return MAJOR_STOCKS[name], name
    for name, code in sorted(MAJOR_STOCKS.items(), key=lambda i: len(i[0]), reverse=True):
        if normalize_text(name) in normalized:
            return code, name
    candidate = _extract_stock_query(text)
    if candidate:
        hit = naver_search(candidate)
        if hit:
            return hit
        tokens = sorted(candidate.split(), key=len, reverse=True)
        if len(tokens) > 1:
            hit = naver_search(tokens[0])
            if hit:
                return hit
    return None


def stock_from_text(text: str) -> tuple[str, str] | None:
    """텍스트에서 종목코드+종목명 해석 (별칭 → 6자리코드 → 검색 순).

    (버그수정 2026-07-04) 기존 telegram_receiver.py는 이 함수를 두 번 정의했고
    나중 정의가 6자리 코드 인식을 빠뜨려 '005930 주가' 같은 코드 직접입력이 안 먹혔다.
    코드 인식을 포함한 완전판으로 통일한다."""
    normalized = normalize_text(text)
    for alias, stock in sorted(_STOCK_ALIASES.items(), key=lambda item: len(item[0]), reverse=True):
        if normalize_text(alias) in normalized:
            return stock
    code_match = re.search(r"\b(\d{6})\b", text or "")
    if code_match:
        return code_match.group(1), code_match.group(1)
    return _resolve_from_search(text)


def is_search_request(text: str) -> bool:
    normalized = normalize_text(text)
    return any(word in normalized for word in ("검색", "찾아봐", "찾아줘", "최신자료", "자료찾"))


def is_screener_request(text: str) -> bool:
    normalized = normalize_text(text)
    return any(
        word in normalized
        for word in ("유망종목", "유망주", "종목발굴", "발굴", "종목추천", "추천종목", "급등주", "살만한", "뭐사")
    )


def is_stock_analysis_request(text: str) -> bool:
    normalized = normalize_text(text)
    if not any(word in normalized for word in ("분석", "리포트", "보고서", "전망")):
        return False
    return stock_from_text(text) is not None or any(word in normalized for word in ("주식", "종목"))


def is_stock_price_request(text: str) -> bool:
    normalized = normalize_text(text)
    return any(word in normalized for word in ("주가", "현재가", "가격")) and stock_from_text(text) is not None


def is_trading_status_request(text: str) -> bool:
    normalized = normalize_text(text)
    return any(word in normalized for word in (
        "거래현황", "봇현황", "자동매매현황", "매매현황", "투자현황", "모의투자현황",
        "포지션현황", "보유현황", "보유종목현황", "내종목", "수익률현황",
    ))


def is_weather_request(text: str) -> bool:
    n = normalize_text(text)
    return any(w in n for w in ("날씨", "기온", "더워", "추워", "비와", "비온", "눈와", "미세먼지", "weather"))


def is_balance_request(text: str) -> bool:
    n = normalize_text(text)
    return any(w in n for w in ("잔고", "보유종목", "내주식", "내종목", "예수금", "내계좌"))


def is_pass(text: str) -> bool:
    return normalize_text(text) in ("패스", "pass", "넘겨", "스킵", "skip", "보류")
