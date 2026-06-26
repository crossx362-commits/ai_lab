#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""영숙 텔레그램 봇 — Gemini Function Calling + python-telegram-bot 22.8"""

from __future__ import annotations

import os
import sys
import json
import urllib.parse
import urllib.request
import subprocess
import xml.etree.ElementTree as ET
from html.parser import HTMLParser
from datetime import datetime, timezone, timedelta
from pathlib import Path

from telegram import Update
from telegram.ext import Application, CommandHandler, MessageHandler, filters, ContextTypes

SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parents[4]
AI_TEAM_ROOT = PROJECT_ROOT / "projects" / "ai-team"
LOG_PATH = SCRIPT_DIR / "telegram_receiver.log"


def _ensure_stdio() -> None:
    if sys.stdout is None or sys.stderr is None or "pythonw" in Path(sys.executable).name.lower():
        log = open(LOG_PATH, "a", encoding="utf-8", buffering=1)
        sys.stdout = log
        sys.stderr = log
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")


_ensure_stdio()
sys.path.insert(0, str(AI_TEAM_ROOT))

from _shared.env import load_env  # noqa: E402
from _shared.llm import text as llm_text  # noqa: E402
from _shared.process import ProcessLock  # noqa: E402

load_env(str(PROJECT_ROOT))

TOKEN = os.getenv("TELEGRAM_BOT_TOKEN", "").strip()
CHAT_ID = os.getenv("TELEGRAM_CHAT_ID", "").strip()
API_KEY = os.getenv("GEMINI_API_KEY", "").strip()

# Gemini client 초기화
try:
    from google import genai
    from google.genai import types as gtypes
    _gemini_client = genai.Client(api_key=API_KEY) if API_KEY else None
except Exception:
    _gemini_client = None

# 대화 히스토리 (최근 3턴 유지)
_HISTORY: list = []
_GPT_HISTORY: list[dict[str, str]] = []
_OPENAI_RESPONSE_ID: str | None = None

SYSTEM_PROMPT = """너는 영숙이야. 사장님(준호)의 AI 비서.

메시지를 분석해서:
1. 에이전트 관련 질문 → get_agent_status로 현황 조회
2. 일정 조회 → list_calendar 도구 사용
3. 웹 검색 필요 → web_search 도구 사용
4. 주식 현재가 → get_stock_price 도구 사용
5. 일반 질문/대화 → 직접 답변

일반 질문 처리 원칙:
- 날씨, 뉴스, 일반 상식 → "제가 직접 확인은 못하지만..." 형태로 친절하게 답변
- 모르는 건 솔직히 "잘 모르겠어요" + 대안 제시
- 짧고 자연스럽게 (텔레그램 채팅처럼)

현재 팀원: 영숙(나), 예원(CEO), 소미(국내주식 수급·세력·매수판단 분석)
도구가 없어도 대화는 계속해. 로봇처럼 "할 수 없습니다" 금지."""

PSYCHOLOGY_SYSTEM = """너는 영숙이야. 사장님(준호)의 AI 트레이딩팀 비서이자 대화 상대다.
- 어떤 말에도 먼저 감정과 의도를 읽고 짧게 받아준다.
- 불안/분노/무기력/외로움 등은 심리학 관점으로 풀되 전문 용어 남발 금지.
- 보통 1) 감정 반영 2) 핵심 패턴 3) 지금 할 수 있는 작은 행동 1개 순서로 답한다.
- 텔레그램답게 짧고 자연스럽게."""
CONVERSATION_PROMPT = """

Behavior update:
- You are Youngsuk, a warm Korean AI secretary talking in Telegram.
- Reply in natural Korean unless the user explicitly asks for another language.
- For normal chat, answer directly like a helpful GPT-style assistant.
- If a question needs fresh facts, current events, prices, releases, schedules, or source-like evidence, call web_search first.
- Use tools only when they help. Do not force dispatch for casual questions.
- Keep Telegram answers concise: usually 3-8 short lines, with links only when useful.
- If search results are weak, say so plainly and separate what you found from your own inference.
"""


def log(message: str) -> None:
    print(f"[{datetime.now().strftime('%Y-%m-%d %H:%M:%S')}] {message}", flush=True)


AGENT_PROFILES = {
    "예원": "예원이는 AI 팀의 CEO예요. 사장님 명령을 해석해서 어느 에이전트가 맡을지 나누고, 하네스 체크나 작업 분배 같은 총괄 판단을 담당해요.",
    "영숙": "저는 영숙이에요. 텔레그램에서 사장님 말을 받아 일정, 상태 확인, 자연어 대화, 작업 전달을 도와주는 비서 역할이에요.",
    "코다리": "코다리는 개발자예요. Petnna 웹 개발, 미리보기 서버, 헬스체크 같은 개발 쪽 일을 맡아요.",
    "케빈": "케빈은 인프라 담당이에요. Vercel, Supabase, 배포, 환경변수 동기화 같은 운영 일을 맡아요.",
    "티모": "티모는 디자이너예요. Petnna 화면과 UI/UX를 검토하고 개선점을 잡아줘요.",
    "시그널": "시그널은 시장 데이터 수집 담당 분석가예요. Upbit 시장 신호와 가격 흐름을 모아줘요.",
    "펄스": "펄스는 시장 분위기 분석가예요. 시장 펄스와 단기 흐름을 읽어서 트레이더들이 참고하게 해요.",
    "데이브": "데이브는 보수적인 자동매매 트레이더예요. 안전한 진입, 손절, 리스크 관리를 우선해요.",
    "레오": "레오는 공격적인 단타 트레이더예요. 변동성이 큰 알트코인과 빠른 매매 쪽을 맡아요.",
    "소미": "소미는 국내주식 수급·세력상황·큰 수익 가능성·매수판단 에이전트예요. 최근 5거래일 흐름, 거래량, 거래대금, 대차잔고, 공매도, 외국인·기관 수급, 오버행 리스크를 보고 냉정하게 판단해요.",
    "경수": "경수는 수사관이에요. 악성 댓글, 보안, 포렌식 쪽 탐지를 담당해요.",
    "로율": "로율은 변호사예요. 법무, 세무, 컴플라이언스 판단을 도와줘요.",
}


def _answer_agent_profile(user_text: str) -> str | None:
    text = user_text.lower()
    profile_words = ["누구", "뭐하", "뭐 하", "역할", "소개", "담당", "정체", "뭐야", "뭐임"]
    for name, desc in AGENT_PROFILES.items():
        if name.lower() in text and (any(word in text for word in profile_words) or "에이전트" in text):
            return desc
    return None


def _is_agent_status_request(user_text: str) -> bool:
    text = user_text.lower()
    if _answer_agent_profile(user_text):
        return False
    status_words = ["상태", "현황", "작동", "실행", "켜져", "살아", "다운", "팀 현황", "다들 뭐해"]
    return any(word in text for word in status_words)


def _is_yewon_dispatch_request(user_text: str) -> bool:
    text = user_text.lower()
    yewon_words = ["예원", "ceo", "yewon"]
    task_words = ["하네스", "harness", "체크", "점검", "분석", "시켜", "돌려", "실행", "검사"]
    return any(word in text for word in yewon_words) and any(word in text for word in task_words)


def _is_somi_request(user_text: str) -> bool:
    text = user_text.lower()
    somi_words = [
        "소미",
        "국내주식",
        "종목",
        "수급",
        "세력",
        "큰 수익",
        "매수 판단",
        "우리기술",
        "숏커버링",
        "숏스퀴즈",
        "공매도 상환",
        "대차잔고",
        "cb",
        "전환가",
        "리픽싱",
        "물타기",
    ]
    task_words = ["리포트", "감시", "분석", "작성", "점수", "데이터", "전략", "확인", "사도", "매수", "팔아", "보유"]
    return any(word in text for word in somi_words) and any(word in text for word in task_words)


# =====================================================================
# 도구 함수 (Gemini Function Calling용)
# =====================================================================

def get_agent_status(agent: str = "전체") -> str:
    """에이전트 현황 조회. Args: agent - 에이전트명 또는 '전체'"""
    log(f"에이전트 현황 조회: {agent}")
    try:
        result = subprocess.run(
            [sys.executable, str(AI_TEAM_ROOT / "scripts" / "unified_control.py"), "agent", "status"],
            capture_output=True, text=True, timeout=10, encoding="utf-8", errors="replace"
        )
        output = (result.stdout or "").strip()
        if output:
            return output[:1000]
    except Exception as e:
        log(f"unified_control status 실패: {e}")

    try:
        from _shared.notify import status_report  # noqa: PLC0415
        report = status_report()
        if isinstance(report, str) and report.strip():
            return report[:1200]
    except Exception as e:
        log(f"notify status_report 실패: {e}")

    try:
        from _shared.notify import agent_status as _agent_status  # noqa: PLC0415
        status = _agent_status()
        if isinstance(status, dict) and status:
            lines = ["📊 에이전트 상태"]
            for name, state in status.items():
                lines.append(f"- {name}: {state}")
            return "\n".join(lines)[:1200]
    except Exception as e:
        log(f"notify agent_status 실패: {e}")

    return "상태 정보를 가져오지 못했어요. 상태조회 경로를 다시 점검해야 해요."

def list_calendar(days: int = 7) -> str:
    """캘린더 일정 조회. Args: days - 조회 일수"""
    log(f"캘린더 조회: {days}일")
    try:
        sys.path.insert(0, str(AI_TEAM_ROOT / "_shared"))
        from calendar_client import get_service, list_events as _list_events  # noqa: PLC0415
        svc = get_service()
        if not svc:
            raise RuntimeError("Google Calendar 인증 필요")
        events = _list_events(svc, days_ahead=days)
        if not events:
            return f"📅 향후 {days}일 일정 없음"
        lines = []
        for ev in events[:15]:
            start = ev.get("start", {})
            dt = start.get("dateTime") or start.get("date", "")
            if "T" in dt:
                from datetime import datetime as _dt  # noqa: PLC0415
                d = _dt.fromisoformat(dt)
                time_str = f"{d.month}/{d.day} {d.hour:02d}:{d.minute:02d}"
            else:
                time_str = dt[5:]
            lines.append(f"• {time_str} {ev.get('summary', '(제목없음)')}")
        return f"📅 향후 {days}일 일정 ({len(events)}건):\n" + "\n".join(lines)
    except Exception:
        cache = AI_TEAM_ROOT / "_shared" / "calendar_cache.md"
        if cache.exists():
            return f"📅 일정 (캐시):\n{cache.read_text(encoding='utf-8')[:800]}"
        return "📅 캘린더 조회에 실패했어요."


def dispatch(cmd: str) -> str:
    """에이전트에게 작업 지시. Args: cmd - 명령"""
    try:
        sys.path.insert(0, str(AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools"))
        import yewon_dispatcher  # noqa: PLC0415
        result = yewon_dispatcher.dispatch_and_execute(cmd)
        if not result:
            return "예원 CEO 대기 중"
        if "[소미]" in result or "종목 간단 분석 리포트" in result:
            return result[:4000] + ("..." if len(result) > 4000 else "")
        if len(result) > 400 and _gemini_client:
            try:
                s = _gemini_client.models.generate_content(
                    model="gemini-2.5-flash",
                    contents=f"2줄 요약:\n{result[:600]}",
                    config=gtypes.GenerateContentConfig(max_output_tokens=100),
                )
                if s.text:
                    return f"요약: {s.text.strip()}"
            except Exception:
                pass
        return result[:4000] + ("..." if len(result) > 4000 else "")
    except Exception as e:
        return f"❌ {str(e)[:100]}"


def get_weather(location: str = "서울") -> str:
    """날씨 정보 조회. Args: location - 지역명 (기본값: 서울)"""
    log(f"날씨 조회: {location}")
    try:
        import urllib.request
        import json

        # OpenWeatherMap API (무료)
        api_key = os.getenv("OPENWEATHER_API_KEY", "").strip()
        if not api_key:
            # API 키 없으면 간단한 응답
            return f"날씨 API 키가 설정되지 않았습니다. 날씨 정보는 인터넷 검색을 통해 확인해주세요."

        # 도시명을 영어로 변환
        city_map = {
            "서울": "Seoul",
            "부산": "Busan",
            "인천": "Incheon",
            "대구": "Daegu",
            "대전": "Daejeon",
            "광주": "Gwangju",
            "울산": "Ulsan",
        }
        city = city_map.get(location, location)

        url = f"http://api.openweathermap.org/data/2.5/weather?q={city},KR&appid={api_key}&units=metric&lang=kr"

        with urllib.request.urlopen(url, timeout=5) as response:
            data = json.loads(response.read())

        temp = data["main"]["temp"]
        feels_like = data["main"]["feels_like"]
        desc = data["weather"][0]["description"]
        humidity = data["main"]["humidity"]

        return f"""🌤 {location} 날씨
기온: {temp}°C (체감 {feels_like}°C)
상태: {desc}
습도: {humidity}%"""
    except Exception as e:
        # 실패해도 자연스럽게 응답
        return f"죄송합니다. 날씨 정보를 가져올 수 없습니다. 구글이나 네이버에서 '{location} 날씨'로 검색해보세요."


class _DuckDuckGoHTMLParser(HTMLParser):
    def __init__(self) -> None:
        super().__init__()
        self.results: list[dict[str, str]] = []
        self._current: dict[str, str] | None = None
        self._capture: str | None = None
        self._buf: list[str] = []

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        attrs_dict = dict(attrs)
        classes = attrs_dict.get("class", "")
        if tag == "a" and "result__a" in classes:
            self._current = {"title": "", "url": attrs_dict.get("href", "") or "", "snippet": ""}
            self._capture = "title"
            self._buf = []
        elif self._current is not None and tag in {"a", "div"} and "result__snippet" in classes:
            self._capture = "snippet"
            self._buf = []

    def handle_data(self, data: str) -> None:
        if self._capture:
            self._buf.append(data)

    def handle_endtag(self, tag: str) -> None:
        if self._current is None or not self._capture:
            return
        if self._capture == "title" and tag == "a":
            self._current["title"] = " ".join("".join(self._buf).split())
            self._capture = None
            self._buf = []
            if len(self.results) < 8:
                self.results.append(self._current)
        elif self._capture == "snippet" and tag in {"a", "div"}:
            self._current["snippet"] = " ".join("".join(self._buf).split())
            self._capture = None
            self._buf = []


def _clean_duckduckgo_url(url: str) -> str:
    if url.startswith("//duckduckgo.com/l/?"):
        parsed = urllib.parse.urlparse("https:" + url)
        qs = urllib.parse.parse_qs(parsed.query)
        if qs.get("uddg"):
            return qs["uddg"][0]
    return url


def _should_force_web_search(user_text: str) -> bool:
    text = (user_text or "").lower()
    keywords = [
        "웹검색", "검색", "찾아줘", "찾아 봐", "찾아봐", "알아봐", "최신", "뉴스",
        "방금", "요즘", "실시간", "속보",
        "web search", "search the web", "latest", "news",
    ]
    return any(keyword in text for keyword in keywords)


def web_search(query: str, max_results: int = 5) -> str:
    """Search the web for current information. Args: query - search query, max_results - 1 to 8 results."""
    query = (query or "").strip()
    if not query:
        return "Search query is empty."
    max_results = max(1, min(int(max_results or 5), 8))
    log(f"web_search: {query}")

    bing_url = "https://www.bing.com/search?" + urllib.parse.urlencode({"q": query, "format": "rss"})
    bing_req = urllib.request.Request(
        bing_url,
        headers={
            "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
            "Accept-Language": "ko-KR,ko;q=0.9,en-US;q=0.7,en;q=0.6",
        },
    )
    try:
        with urllib.request.urlopen(bing_req, timeout=12) as response:
            rss_text = response.read().decode("utf-8", errors="replace")
        root = ET.fromstring(rss_text)
        results = []
        for item in root.findall("./channel/item")[:max_results]:
            title = " ".join((item.findtext("title") or "").split())
            link = (item.findtext("link") or "").strip()
            desc = " ".join((item.findtext("description") or "").split())
            pub_date = " ".join((item.findtext("pubDate") or "").split())
            if title and link:
                date_line = f"  date: {pub_date}\n" if pub_date else ""
                results.append(f"- {title}\n  {link}\n{date_line}  {desc}")
        if results:
            return "\n".join(results)
    except Exception as e:
        log(f"Bing RSS search failed: {e}")

    url = "https://duckduckgo.com/html/?" + urllib.parse.urlencode({"q": query, "kl": "kr-kr"})
    req = urllib.request.Request(
        url,
        headers={
            "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
            "Accept-Language": "ko-KR,ko;q=0.9,en-US;q=0.7,en;q=0.6",
        },
    )
    try:
        with urllib.request.urlopen(req, timeout=12) as response:
            html_text = response.read().decode("utf-8", errors="replace")
    except Exception as e:
        return f"Web search failed: {e}"

    parser = _DuckDuckGoHTMLParser()
    parser.feed(html_text)
    results = []
    for item in parser.results[:max_results]:
        title = item.get("title", "").strip()
        link = _clean_duckduckgo_url(item.get("url", "").strip())
        snippet = item.get("snippet", "").strip()
        if title and link:
            results.append(f"- {title}\n  {link}\n  {snippet}")

    if not results:
        return "No useful search results found."
    return "\n".join(results)


def get_stock_price(stock_code: str, stock_name: str = "") -> str:
    """주식 현재가 조회 (종목코드 직접 입력). Args: stock_code - 6자리 종목코드 (예: '005930' -> 삼성전자, '035720' -> 카카오, '000660' -> SK하이닉스), stock_name - 종목명 (옵션)"""
    log(f"주식 현재가 조회: {stock_name} ({stock_code})")
    try:
        sys.path.insert(0, str(AI_TEAM_ROOT / "skills" / "데이브_주식" / "tools"))
        from kis_client import KISClient  # noqa: PLC0415
        client = KISClient()
        price_data = client.get_current_price(stock_code)
        if "output" not in price_data:
            return f"❌ {stock_name or stock_code} 조회 실패: {price_data.get('msg1', '알 수 없는 오류')}"

        output = price_data["output"]
        current_price = int(output.get("stck_prpr", 0))
        change = int(output.get("prdy_vrss", 0))
        change_rate = float(output.get("prdy_ctrt", 0.0))
        sign = output.get("prdy_vrss_sign", "3")

        sign_emoji = "➕"
        if sign in ["1", "2"]:
            sign_emoji = "📈"
        elif sign in ["4", "5"]:
            sign_emoji = "📉"
            change = -change
        else:
            sign_emoji = "➖"

        name_str = f"{stock_name}" if stock_name else f"종목코드 {stock_code}"
        return (
            f"{sign_emoji} {name_str}\n"
            f"현재가: {current_price:,}원\n"
            f"전일대비: {change:+,}원 ({change_rate:+.2f}%)"
        )
    except Exception as e:
        log(f"주가 조회 오류: {e}")
        return f"❌ 주가 조회 중 오류 발생: {e}"


_TOOL_MAP = {
    "get_agent_status": get_agent_status,
    "list_calendar": list_calendar,
    "web_search": web_search,
    "get_weather": get_weather,
    "get_stock_price": get_stock_price,
}

def _process_with_gemini(user_text: str) -> str | None:
    """Gemini Function Calling으로 메시지 처리. 실패 시 None 반환."""
    global _HISTORY

    if not _gemini_client:
        return None

    now = datetime.now(timezone(timedelta(hours=9)))
    time_ctx = f"\n[지금: {now.strftime('%Y-%m-%d %H:%M %a')}]"

    _HISTORY.append(gtypes.Content(role="user", parts=[gtypes.Part.from_text(text=user_text)]))
    if len(_HISTORY) > 6:
        _HISTORY = _HISTORY[-6:]

    model = "gemini-2.5-flash"
    try:
        resp = _gemini_client.models.generate_content(
            model=model,
            contents=_HISTORY,
            config=gtypes.GenerateContentConfig(
                system_instruction=SYSTEM_PROMPT + CONVERSATION_PROMPT + time_ctx,
                tools=_TOOLS,
                max_output_tokens=500,
                temperature=0.8,
            ),
        )
    except Exception as e:
        err = str(e)
        if any(code in err for code in ["429", "503", "RESOURCE_EXHAUSTED"]) or any(k in err.lower() for k in ["quota", "demand", "unavailable"]):
            log("Gemini Flash 오류 (429/503/Quota) → Pro 전환")
            model = "gemini-2.5-pro"
            try:
                resp = _gemini_client.models.generate_content(
                    model=model,
                    contents=_HISTORY,
                    config=gtypes.GenerateContentConfig(
                        system_instruction=SYSTEM_PROMPT + CONVERSATION_PROMPT + time_ctx,
                        tools=_TOOLS,
                        max_output_tokens=500,
                        temperature=0.8,
                    ),
                )
            except Exception as e2:
                log(f"Gemini Pro 실패: {e2}")
                return None
        else:
            log(f"Gemini 오류: {e}")
            return None

    answer = ""
    tool_results = []

    if resp.candidates and resp.candidates[0].content.parts:
        for part in resp.candidates[0].content.parts:
            if part.text:
                answer += part.text
            elif part.function_call:
                fn = part.function_call.name
                args = dict(part.function_call.args) if part.function_call.args else {}
                log(f"Tool: {fn}({args})")
                if fn in _TOOL_MAP:
                    try:
                        res = _TOOL_MAP[fn](**args)
                        tool_results.append({"name": fn, "result": res})
                    except Exception as e:
                        tool_results.append({"name": fn, "result": f"❌ {e}"})

    # 도구 결과가 있으면 최종 답변 생성
    if tool_results:
        fn_parts = [
            gtypes.Part.from_function_response(name=tr["name"], response={"result": tr["result"]})
            for tr in tool_results
        ]
        _HISTORY.append(gtypes.Content(
            role="model",
            parts=[gtypes.Part.from_function_call(name=tool_results[0]["name"], args={})],
        ))
        _HISTORY.append(gtypes.Content(role="user", parts=fn_parts))

        is_status = any(tr["name"] == "get_agent_status" for tr in tool_results)
        is_search = any(tr["name"] == "web_search" for tr in tool_results)
        max_tok = 1000 if is_status else 700 if is_search else 200
        sys_inst = SYSTEM_PROMPT + CONVERSATION_PROMPT + time_ctx
        if is_status:
            sys_inst += "\n현황 보고는 모든 에이전트 내용을 줄이지 말고 상세하게 보고하세요."

        try:
            final = _gemini_client.models.generate_content(
                model=model,
                contents=_HISTORY,
                config=gtypes.GenerateContentConfig(
                    system_instruction=sys_inst,
                    max_output_tokens=max_tok,
                    temperature=0.7,
                ),
            )
            answer = final.text.strip() if final.text else "\n\n".join(tr["result"] for tr in tool_results)
        except Exception as e:
            log(f"Gemini 최종 답변 실패: {e}")
            answer = "\n\n".join(tr["result"] for tr in tool_results)

    if not answer or answer.strip() == "":
        # 빈 응답 대신 상황에 맞는 기본 답변
        if tool_results:
            answer = "완료했습니다."
        else:
            answer = "알겠습니다."

    _HISTORY.append(gtypes.Content(role="model", parts=[gtypes.Part.from_text(text=answer)]))
    if len(_HISTORY) > 6:
        _HISTORY = _HISTORY[-6:]

    return answer


def _process_with_gpt(user_text: str) -> str | None:
    """OpenAI GPT-4o-mini를 사용하여 Function Calling 처리. 실패 시 None 반환."""
    global _GPT_HISTORY

    api_key = os.getenv("OPENAI_API_KEY", "").strip()
    if not api_key:
        return None

    import urllib.request
    import json

    now = datetime.now(timezone(timedelta(hours=9)))
    time_ctx = f"\n[지금: {now.strftime('%Y-%m-%d %H:%M %a')}]"

    openai_tools = [
        {
            "type": "function",
            "function": {
                "name": "get_agent_status",
                "description": "에이전트 현황 조회. agent: 에이전트명 또는 '전체'",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "agent": {"type": "string", "default": "전체"}
                    }
                }
            }
        },
        {
            "type": "function",
            "function": {
                "name": "list_calendar",
                "description": "캘린더 일정 조회. days: 조회 일수",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "days": {"type": "integer", "default": 7}
                    }
                }
            }
        },
        {
            "type": "function",
            "function": {
                "name": "web_search",
                "description": "최신 정보, 뉴스, 가격, 출시일, 일정, 근거가 필요한 질문을 웹에서 검색합니다.",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "query": {"type": "string", "description": "검색어"},
                        "max_results": {"type": "integer", "default": 5}
                    },
                    "required": ["query"]
                }
            }
        },
        {
            "type": "function",
            "function": {
                "name": "get_stock_price",
                "description": "주식 현재가 조회. stock_code: 6자리 종목코드, stock_name: 종목명 (옵션)",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "stock_code": {"type": "string", "description": "6자리 종목코드 (예: '005930' -> 삼성전자)"},
                        "stock_name": {"type": "string", "description": "종목명"}
                    },
                    "required": ["stock_code"]
                }
            }
        }
    ]

    messages = [{"role": "system", "content": SYSTEM_PROMPT + CONVERSATION_PROMPT + time_ctx}]
    messages.extend(_GPT_HISTORY[-6:])
    messages.append({"role": "user", "content": user_text})

    payload = {
        "model": "gpt-4o-mini",
        "messages": messages,
        "tools": openai_tools,
        "temperature": 0.7,
        "max_tokens": 500
    }
    if _should_force_web_search(user_text):
        payload["tool_choice"] = {"type": "function", "function": {"name": "web_search"}}

    try:
        req = urllib.request.Request(
            "https://api.openai.com/v1/chat/completions",
            data=json.dumps(payload).encode(),
            headers={"Content-Type": "application/json", "Authorization": f"Bearer {api_key}"},
        )
        with urllib.request.urlopen(req, timeout=15) as r:
            res = json.loads(r.read())
        
        message = res["choices"][0]["message"]
        answer = message.get("content") or ""
        tool_calls = message.get("tool_calls") or []

        if tool_calls:
            tool_results = []
            for tc in tool_calls:
                fn = tc["function"]["name"]
                args = json.loads(tc["function"]["arguments"])
                log(f"GPT Tool: {fn}({args})")
                if fn in _TOOL_MAP:
                    try:
                        res_str = _TOOL_MAP[fn](**args)
                        tool_results.append({"name": fn, "result": res_str, "id": tc["id"]})
                    except Exception as e:
                        tool_results.append({"name": fn, "result": f"❌ {e}", "id": tc["id"]})

            # 도구 실행 결과를 다시 GPT에 전달하여 최종 답변 생성
            messages.append(message)
            for tr in tool_results:
                messages.append({
                    "role": "tool",
                    "tool_call_id": tr["id"],
                    "name": tr["name"],
                    "content": tr["result"]
                })

            is_status = any(tr["name"] == "get_agent_status" for tr in tool_results)
            is_search = any(tr["name"] == "web_search" for tr in tool_results)
            max_tok = 1000 if is_status else 700 if is_search else 200
            
            payload_final = {
                "model": "gpt-4o-mini",
                "messages": messages,
                "temperature": 0.7,
                "max_tokens": max_tok
            }

            req_final = urllib.request.Request(
                "https://api.openai.com/v1/chat/completions",
                data=json.dumps(payload_final).encode(),
                headers={"Content-Type": "application/json", "Authorization": f"Bearer {api_key}"},
            )
            with urllib.request.urlopen(req_final, timeout=15) as r:
                res_final = json.loads(r.read())
            
            answer = res_final["choices"][0]["message"]["content"].strip()
        
        if answer:
            _GPT_HISTORY.extend([
                {"role": "user", "content": user_text},
                {"role": "assistant", "content": answer},
            ])
            _GPT_HISTORY = _GPT_HISTORY[-6:]
        return answer
    except Exception as e:
        log(f"GPT Function Calling 오류: {e}")
        return None


def _process_with_openai_web_chat(user_text: str) -> str | None:
    """OpenAI Responses API hosted web_search로 ChatGPT 웹대화처럼 답변."""
    global _OPENAI_RESPONSE_ID

    api_key = os.getenv("OPENAI_API_KEY", "").strip()
    if not api_key:
        return None

    now = datetime.now(timezone(timedelta(hours=9)))
    instructions = (
        SYSTEM_PROMPT
        + CONVERSATION_PROMPT
        + f"\n[지금: {now.strftime('%Y-%m-%d %H:%M %a')} KST]\n"
        + "Use hosted web_search when fresh or source-backed information would help. "
        + "Answer naturally in Korean for Telegram."
    )

    payload = {
        "model": os.getenv("YOUNGSUK_OPENAI_MODEL", "gpt-4.1-mini"),
        "instructions": instructions,
        "input": user_text,
        "tools": [{"type": "web_search", "search_context_size": "low"}],
        "temperature": 0.7,
        "max_output_tokens": 900,
    }
    if _OPENAI_RESPONSE_ID:
        payload["previous_response_id"] = _OPENAI_RESPONSE_ID

    def _request(body: dict) -> dict:
        req = urllib.request.Request(
            "https://api.openai.com/v1/responses",
            data=json.dumps(body).encode("utf-8"),
            headers={"Content-Type": "application/json", "Authorization": f"Bearer {api_key}"},
        )
        with urllib.request.urlopen(req, timeout=35) as r:
            return json.loads(r.read())

    try:
        res = _request(payload)
    except Exception as e:
        msg = str(e)
        if "web_search" in msg or "400" in msg:
            try:
                payload["tools"] = [{"type": "web_search_preview", "search_context_size": "low"}]
                res = _request(payload)
            except Exception as e2:
                log(f"OpenAI web chat 오류: {e2}")
                return None
        else:
            log(f"OpenAI web chat 오류: {e}")
            return None

    _OPENAI_RESPONSE_ID = res.get("id") or _OPENAI_RESPONSE_ID
    answer = (res.get("output_text") or "").strip()
    if answer:
        return answer

    chunks: list[str] = []
    for item in res.get("output", []) or []:
        for content in item.get("content", []) or []:
            text = content.get("text") if isinstance(content, dict) else None
            if text:
                chunks.append(text)
    return "\n".join(chunks).strip() or None


# =====================================================================
# 명령어 핸들러
# =====================================================================

async def start_command(update: Update, _context: ContextTypes.DEFAULT_TYPE) -> None:
    await update.message.reply_text(
        "안녕하세요! 영숙이에요.\n\n"
        "/status - 에이전트 상태\n"
        "/balance - 잔고\n"
        "/holdings - 보유 현황\n\n"
        "자연어로 뭐든 말해주세요."
    )
    log(f"start: {update.effective_user.username}")


async def status_command(update: Update, _context: ContextTypes.DEFAULT_TYPE) -> None:
    text = get_agent_status()
    await update.message.reply_text(f"📊 에이전트 상태:\n\n{text}")
    log("status 명령")


async def balance_command(update: Update, _context: ContextTypes.DEFAULT_TYPE) -> None:
    try:
        result = subprocess.run(
            [sys.executable, str(AI_TEAM_ROOT / "scripts" / "daily_balance_check.py")],
            capture_output=True, text=True, timeout=30, encoding="utf-8", errors="replace"
        )
        text = result.stdout if result.returncode == 0 else f"실패:\n{result.stderr}"
        await update.message.reply_text(f"💰 잔고:\n\n{text}")
    except Exception as e:
        await update.message.reply_text(f"⚠️ 잔고 확인 오류:\n{e}")
    log("balance 명령")


async def holdings_command(update: Update, _context: ContextTypes.DEFAULT_TYPE) -> None:
    try:
        result = subprocess.run(
            [sys.executable, str(AI_TEAM_ROOT / "scripts" / "check_holdings.py")],
            capture_output=True, text=True, timeout=30, encoding="utf-8", errors="replace"
        )
        text = result.stdout if result.returncode == 0 else f"실패:\n{result.stderr}"
        await update.message.reply_text(f"📈 보유 현황:\n\n{text}")
    except Exception as e:
        await update.message.reply_text(f"⚠️ 보유 현황 오류:\n{e}")
    log("holdings 명령")


# =====================================================================
# 자연어 메시지 핸들러
# =====================================================================

async def handle_message(update: Update, _context: ContextTypes.DEFAULT_TYPE) -> None:
    user_text = update.message.text or ""
    log(f"메시지: {user_text[:50]}")

    await update.message.reply_text("생각 중...")

    try:
        text_lower = user_text.lower()
        response = None

        profile_answer = _answer_agent_profile(user_text)
        if _is_yewon_dispatch_request(user_text):
            log("직접처리: yewon dispatch")
            response = dispatch(user_text)
        elif _is_somi_request(user_text):
            log("직접처리: somi dispatch")
            response = dispatch(user_text)
        elif profile_answer:
            log("직접처리: agent profile")
            response = profile_answer
        elif any(k in text_lower for k in ["잔고", "보유", "holdings", "balance", "포트폴리오"]):
            log("직접처리: holdings/balance")
            try:
                result = subprocess.run(
                    [sys.executable, str(AI_TEAM_ROOT / "scripts" / "check_holdings.py")],
                    capture_output=True, text=True, timeout=30, encoding="utf-8", errors="replace"
                )
                response = result.stdout if result.returncode == 0 else f"실패:\n{result.stderr}"
            except Exception as e:
                response = f"⚠️ 보유/잔고 확인 오류: {e}"
        elif _is_agent_status_request(user_text):
            log("직접처리: agent status")
            response = get_agent_status()
        elif any(k in text_lower for k in ["일정", "캘린더", "스케줄", "calendar"]):
            log("직접처리: calendar")
            response = list_calendar()

        # 1차: ChatGPT 웹대화 스타일 (OpenAI Responses API + hosted web_search)
        if not response:
            response = _process_with_openai_web_chat(user_text)

        # 2차: GPT Function Calling (Responses API 실패 시)
        if not response:
            log("OpenAI web chat 실패 → GPT Function Calling 진행")
            response = _process_with_gpt(user_text)

        # 3차: Gemini Function Calling (GPT 실패 시)
        if not response:
            log("GPT 실패 → Gemini Function Calling 진행")
            response = _process_with_gemini(user_text)

        # 4차 폴백: Ollama (최후)
        if not response:
            log("GPT/Gemini 실패 → Ollama llm_text 폴백")
            response = llm_text(
                user_text,
                system=PSYCHOLOGY_SYSTEM,
                max_tokens=500,
                temperature=0.8,
                lm_first=True,  # Ollama 사용
            )

        await update.message.reply_text(response or "지금은 답변하기 어려워요. 잠시 후 다시 말해주세요.")

    except Exception as e:
        log(f"메시지 처리 오류: {e}")
        await update.message.reply_text(f"⚠️ 오류:\n{e}")


async def error_handler(update: object, context: ContextTypes.DEFAULT_TYPE) -> None:
    log(f"업데이트 오류: {context.error}")
    if isinstance(update, Update) and update.effective_message:
        try:
            await update.effective_message.reply_text("⚠️ 요청 처리 중 문제가 발생했어요.")
        except Exception:
            pass


# =====================================================================
# 메인
# =====================================================================

def main() -> None:
    if not TOKEN:
        log("❌ TELEGRAM_BOT_TOKEN 없음")
        return

    log("🚀 영숙 텔레그램 봇 시작...")
    log(f"Token: {TOKEN[:20]}...")
    log(f"Gemini: {'✅' if _gemini_client else '❌ (폴백 모드)'}")

    application = Application.builder().token(TOKEN).build()
    application.add_handler(CommandHandler("start", start_command))
    application.add_handler(CommandHandler("status", status_command))
    application.add_handler(CommandHandler("balance", balance_command))
    application.add_handler(CommandHandler("holdings", holdings_command))
    application.add_handler(MessageHandler(filters.TEXT & ~filters.COMMAND, handle_message))
    application.add_error_handler(error_handler)

    log("✅ 봇 준비 완료 - 메시지 대기 중...")
    application.run_polling(allowed_updates=Update.ALL_TYPES)


def daemon() -> None:
    with ProcessLock("youngsuk"):
        try:
            main()
        except KeyboardInterrupt:
            log("봇 중지됨")
        except Exception as e:
            log(f"봇 실행 오류: {e}")
            import traceback
            traceback.print_exc()


if __name__ == "__main__":
    if "--daemon" in sys.argv:
        daemon()
    else:
        try:
            main()
        except KeyboardInterrupt:
            log("봇 중지됨")
