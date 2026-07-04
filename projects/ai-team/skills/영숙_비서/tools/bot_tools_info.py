#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""영숙 본연 툴 — 날씨·일정. 영숙 게이트웨이가 BOT_TOOLS를 수집해 등록한다."""

from __future__ import annotations

import json
import re
import urllib.parse
import urllib.request
from pathlib import Path

_HERE = Path(__file__).resolve()

_WEATHER_DESC_KO = {
    "Sunny": "맑음", "Clear": "맑음", "Partly cloudy": "구름 조금", "Cloudy": "흐림",
    "Overcast": "흐림", "Mist": "안개", "Fog": "안개", "Patchy rain possible": "비 가능",
    "Patchy rain nearby": "비 약간", "Light rain": "가랑비", "Light drizzle": "이슬비",
    "Moderate rain": "비", "Heavy rain": "폭우", "Light snow": "눈 약간", "Snow": "눈",
    "Thundery outbreaks possible": "천둥 가능",
}


def parse_weather_day(text: str) -> int:
    """0=오늘(현재), 1=내일, 2=모레."""
    n = re.sub(r"\s+", "", text or "").lower()
    if "모레" in n:
        return 2
    if "내일" in n:
        return 1
    return 0


def parse_weather_city(text: str) -> str:
    t = text
    for w in ("모레", "내일", "오늘", "지금", "현재", "이번주", "주말",
              "날씨", "예보", "어때", "어떄", "기온", "좀", "알려줘", "?", "？", "미세먼지"):
        t = t.replace(w, " ")
    t = re.sub(r"\s+", " ", t).strip()
    return t or "서울"


def _weather_desc(node: dict) -> str:
    desc = (node.get("lang_ko") or [{}])[0].get("value") or (node.get("weatherDesc") or [{}])[0].get("value", "")
    return _WEATHER_DESC_KO.get(desc.strip(), desc.strip())


def get_weather(city: str = "서울", day: int = 0) -> str:
    """도시의 현재 날씨 또는 내일/모레 예보를 조회합니다 (wttr.in, 키 불필요)."""
    city = (city or "서울").strip()
    try:
        day = max(0, min(2, int(day or 0)))
    except (TypeError, ValueError):
        day = 0
    try:
        url = f"https://wttr.in/{urllib.parse.quote(city)}?format=j1&lang=ko"
        req = urllib.request.Request(url, headers={"User-Agent": "curl/8"})
        with urllib.request.urlopen(req, timeout=10) as r:
            d = json.loads(r.read().decode("utf-8", "replace"))

        if day == 0:
            c = d["current_condition"][0]
            return (
                f"🌤️ {city} 현재 날씨\n"
                f"기온 {c['temp_C']}°C (체감 {c['FeelsLikeC']}°C) / {_weather_desc(c)}\n"
                f"습도 {c['humidity']}% / 바람 {c['windspeedKmph']}km/h / 강수 {c['precipMM']}mm"
            )

        w = d["weather"][day]
        label = {1: "내일", 2: "모레"}.get(day, f"{day}일 후")
        hourly = w.get("hourly") or []
        mid = hourly[4] if len(hourly) > 4 else (hourly[len(hourly) // 2] if hourly else {})
        rain = mid.get("chanceofrain", "?")
        return (
            f"🌤️ {city} {label}({w.get('date', '')}) 날씨\n"
            f"최고 {w['maxtempC']}°C / 최저 {w['mintempC']}°C / {_weather_desc(mid)}\n"
            f"강수확률 {rain}%"
        )
    except Exception as exc:
        return f"날씨 조회 실패: {exc}"


def list_calendar() -> str:
    """일정 및 스케줄을 조회합니다."""
    import bot_common as bc
    script = _HERE.with_name("schedule_manager.py")
    return bc.run_python(script, "--list", timeout=30)


BOT_TOOLS = [
    {
        "handler": list_calendar,
        "schema": {
            "type": "function",
            "function": {
                "name": "list_calendar",
                "description": "일정 및 스케줄 조회",
                "parameters": {"type": "object", "properties": {}, "required": []},
            },
        },
    },
    {
        "handler": get_weather,
        "schema": {
            "type": "function",
            "function": {
                "name": "get_weather",
                "description": "특정 도시의 현재 날씨 또는 내일/모레 예보 조회. 사용자가 날씨/기온을 물으면 호출",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "city": {"type": "string", "description": "도시명 (예: 서울, 부산)"},
                        "day": {"type": "integer", "description": "0=오늘(현재), 1=내일, 2=모레"},
                    },
                    "required": [],
                },
            },
        },
    },
]
