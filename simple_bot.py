#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
최적화된 텔레그램 봇 - google-genai SDK 및 Function Calling 적용 버전
"""
import os
import sys
import json
import time
from datetime import datetime, timezone, timedelta

# UTF-8 인코딩 설정
if hasattr(sys.stdout, 'reconfigure'):
    try:
        sys.stdout.reconfigure(encoding='utf-8', errors='replace')
        sys.stderr.reconfigure(encoding='utf-8', errors='replace')
    except:
        pass

# 프로젝트 루트 및 패스 추가
_here = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _here)
sys.path.insert(0, os.path.join(_here, "projects", "ai-team"))

from _shared.env_loader import load_env
load_env(_here)

from google import genai
from google.genai import types

TOKEN = os.getenv("TELEGRAM_BOT_TOKEN", "").strip()
CHAT_ID = os.getenv("TELEGRAM_CHAT_ID", "").strip()
GEMINI_KEY = os.getenv("GEMINI_API_KEY", "").strip()

client = genai.Client(api_key=GEMINI_KEY) if GEMINI_KEY else None

sys.path.insert(0, os.path.join(_here, "projects", "ai-team", "skills", "예원_CEO", "tools"))
try:
    import yewon_dispatcher
except:
    yewon_dispatcher = None

SYSTEM = "영숙(비서). 규칙: 짧게 핵심만 2줄 이내 답변. 필요시 도구(get_agent_status, list_calendar, dispatch) 즉시 호출."
HISTORY = []

def tg_api(method, data, timeout=20):
    import urllib.request
    url = f"https://api.telegram.org/bot{TOKEN}/{method}"
    req = urllib.request.Request(url, json.dumps(data).encode(), headers={"Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return json.loads(r.read().decode())
    except:
        return {}

def send_msg(text):
    print(f"📤 {text[:60]}...")
    tg_api("sendMessage", {"chat_id": CHAT_ID, "text": text, "parse_mode": "HTML"})

def get_updates(offset):
    res = tg_api("getUpdates", {"offset": offset, "timeout": 30, "allowed_updates": ["message"]}, timeout=40)
    return res.get("result", [])

def get_agent_status(agent="전체"):
    """에이전트 현황을 조회합니다. Args: agent ('루나'/'아린'/'데이브'/'전체')"""
    lines = []
    hist_path = os.path.join(_here, "reports", "history", "upload_history.json")

    def read_hist(name):
        if not os.path.exists(hist_path): return []
        try:
            with open(hist_path, "r", encoding="utf-8") as f:
                data = json.load(f)
            return [d for d in data if d.get("agent") == name] if isinstance(data, list) else []
        except: return []

    if "루나" in agent or "전체" in agent:
        luna = read_hist("루나")
        if luna:
            last = luna[-1]
            meta = last.get("metadata", {})
            title = meta.get("youtube_title", "?")[:40]
            vid = meta.get("video_id", "")
            url = f"https://youtu.be/{vid}" if vid else ""
            date = last.get("uploaded_at", "")[:16]
            lines.append(f"🎬 루나: {title}\n   {date} | {url}\n   누적 {len(luna)}개")
        else:
            lines.append("🎬 루나: 기록 없음")

    if "아린" in agent or "전체" in agent:
        arin = read_hist("아린")
        if arin:
            last = arin[-1]
            caption = last.get("metadata", {}).get("caption", "?")[:30]
            date = last.get("uploaded_at", "")[:16]
            lines.append(f"📸 아린: {caption}\n   {date} | 누적 {len(arin)}개")
        else:
            lines.append("📸 아린: 기록 없음")

    if "데이브" in agent or "전체" in agent:
        dave_path = os.path.join(_here, "reports", "research", "dave_upbit_analysis.md")
        if os.path.exists(dave_path):
            try:
                with open(dave_path, "r", encoding="utf-8") as f:
                    content = f.read()
                decision = "?"
                for line in content.split("\n"):
                    if "최종 결정" in line:
                        decision = line.split(":")[-1].strip()[:50]
                        break
                mtime = datetime.fromtimestamp(os.path.getmtime(dave_path)).strftime("%m/%d %H:%M")
                lines.append(f"📈 데이브: {decision}\n   {mtime}")
            except: lines.append("📈 데이브: 오류")
        else: lines.append("📈 데이브: 분석 없음")

    return "\n\n".join(lines) if lines else "조회 대상 지정 필요"

def list_calendar(days=7):
    """캘린더 일정을 가져옵니다. Args: days (조회 일수)"""
    cache = os.path.join(_here, "projects", "ai-team", "_shared", "calendar_cache.md")
    if os.path.exists(cache):
        with open(cache, "r", encoding="utf-8") as f:
            return f"📅 일정:\n{f.read()[:800]}"
    return "📅 캘린더 미설정"

def dispatch(cmd):
    """지정된 에이전트 작업을 즉시 실행합니다. Args: cmd (명령)"""
    if not yewon_dispatcher: return "❌ 디스패처 없음"
    try:
        print(f"🎯 {cmd}")
        result = yewon_dispatcher.dispatch_and_execute(cmd)
        if not result: return "⚠️ CEO 대기"
        if len(result) > 400:
            try:
                s = client.models.generate_content(model="gemini-2.5-flash", contents=f"2줄 요약:\n{result[:600]}", config=types.GenerateContentConfig(max_output_tokens=100))
                if s.text: return f"✅ {s.text.strip()}"
            except: pass
            return result[:400] + "..."
        return result
    except Exception as e:
        return f"❌ {str(e)[:100]}"

TOOLS = [get_agent_status, list_calendar, dispatch]
TOOL_MAP = {"get_agent_status": get_agent_status, "list_calendar": list_calendar, "dispatch": dispatch}

def process(msg):
    global HISTORY
    print(f"\n📩 {msg}")

    if not client:
        send_msg("Gemini API 키 없음")
        return

    now = datetime.now(timezone(timedelta(hours=9)))
    time_ctx = f"\n[지금: {now.strftime('%Y-%m-%d %H:%M %a')}]"

    HISTORY.append(types.Content(role="user", parts=[types.Part.from_text(text=msg)]))
    if len(HISTORY) > 6:
        HISTORY = HISTORY[-6:]

    model_name = "gemini-2.5-flash"
    try:
        resp = client.models.generate_content(
            model=model_name,
            contents=HISTORY,
            config=types.GenerateContentConfig(
                system_instruction=SYSTEM + time_ctx,
                tools=TOOLS,
                max_output_tokens=120,
                temperature=0.7
            )
        )

        answer = ""
        tool_results = []

        if resp.candidates and resp.candidates[0].content.parts:
            for part in resp.candidates[0].content.parts:
                if part.text:
                    answer += part.text
                elif part.function_call:
                    fn = part.function_call.name
                    args = dict(part.function_call.args) if part.function_call.args else {}
                    print(f"🔧 {fn}({args})")
                    if fn in TOOL_MAP:
                        try:
                            res = TOOL_MAP[fn](**args)
                            tool_results.append({"name": fn, "result": res})
                            print(f"✅ {res[:80]}...")
                        except Exception as e:
                            err = f"❌ {str(e)[:80]}"
                            tool_results.append({"name": fn, "result": err})
                            print(err)

        if tool_results:
            fn_parts = [types.Part.from_function_response(name=tr["name"], response={"result": tr["result"]}) for tr in tool_results]
            HISTORY.append(types.Content(role="model", parts=[types.Part.from_function_call(name=tool_results[0]["name"], args={})]))
            HISTORY.append(types.Content(role="user", parts=fn_parts))

            final = client.models.generate_content(
                model=model_name,
                contents=HISTORY,
                config=types.GenerateContentConfig(
                    system_instruction=SYSTEM + time_ctx + "\n도구 결과로 간결히 답변",
                    max_output_tokens=100,
                    temperature=0.7
                )
            )
            answer = final.text.strip() if final.text else "\n\n".join([tr["result"] for tr in tool_results])

        if not answer:
            answer = "네"

        send_msg(answer)

        HISTORY.append(types.Content(role="model", parts=[types.Part.from_text(text=answer)]))
        if len(HISTORY) > 6:
            HISTORY = HISTORY[-6:]

    except Exception as e:
        print(f"❌ {model_name} 오류: {e}")
        send_msg("일시적인 오류가 발생했습니다. 잠시 후 다시 시도해 주세요.")

def main():
    print("="*60)
    print("🤖 영숙 봇 (Gemini 2.5 Flash 전용 최적화 버전)")
    print("="*60)
    print(f"Gemini: {'✅' if client else '❌'}")
    print(f"Telegram: {'✅' if TOKEN and CHAT_ID else '❌'}")
    print("="*60)

    if not TOKEN or not CHAT_ID:
        print("❌ 텔레그램 설정 필요")
        return

    tg_api("deleteWebhook", {"drop_pending_updates": True})
    send_msg("🤖 영숙 출근 (최적화 모드)\n예: 현황/루나 영상/일정")

    offset = 0
    while True:
        try:
            for upd in get_updates(offset):
                offset = upd["update_id"] + 1
                text = upd.get("message", {}).get("text", "").strip()
                if text:
                    process(text)
            time.sleep(1)
        except KeyboardInterrupt:
            print("\n👋 종료")
            break
        except Exception as e:
            print(f"❌ 루프: {e}")
            time.sleep(5)

if __name__ == "__main__":
    main()
