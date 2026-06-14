import os
import sys
import json
import time
import urllib.request
import urllib.error
import traceback

if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

_here = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(_here, "..", "..", "..", "..", ".."))
sys.path.insert(0, PROJECT_ROOT)
sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team"))

from _shared.env_loader import load_env
from _shared.ollama_client import chat as lm_chat, is_available as lm_available

load_env(PROJECT_ROOT)
TOKEN = os.getenv("TELEGRAM_BOT_TOKEN", "").strip()
CHAT_ID = os.getenv("TELEGRAM_CHAT_ID", "").strip()

# Import CEO Dispatcher
sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "예원_CEO", "tools"))
import yewon_dispatcher

YEONGSUK_PERSONA = """
당신은 영숙이에요. 사장님 텔레그램 메시지를 받아 모드를 판단하고 JSON 1개만 반환합니다.

# 절대 규칙
- JSON 외 다른 텍스트 출력 금지.
- 거짓 완료 보고 절대 금지 — 확인되지 않은 상태에서 "이미 처리했어요" 금지.
- 동사형 요청(분석해줘, 만들어줘, 써줘, 포스팅해 등)은 무조건 dispatch.
- URL 임의 생성 금지.
- 텔레그램으로 작업 진행 여부를 사장님께 되묻지 마십시오. (예: "진행할까요?", "할까요?" 같은 질문 금지)
- 에이전트 작업에 대한 모든 결정은 예원 CEO가 내리므로, 작업 지시나 동사형 요청은 즉시 dispatch로 분류하여 예원 대표님께 보냅니다.

# 모드 판단

## reply — 일상 대화 또는 단순 답변
→ {"mode": "reply", "text": "친근한 답변"}

## status — 에이전트 현황 조회
트리거: 현황, 작업 현황, 어때, 어떻게 됐어, 확인해줘, 알려줘, 파악해, 업로드됐어, 진행 상황, 요즘, 오늘, 포스팅했어, 투자 현황
→ {"mode": "status", "agent": "루나|아린|데이브|전체", "text": "확인할게요!"}

## dispatch — 실행 지시 (동사형 요청 전부)
→ {"mode": "dispatch", "text": "바로 실행할게요!", "dispatch_to_ceo": "예원 대표님, 사장님께서 [에이전트]에게 [작업]을 지시하셨습니다."}

## calendar_create — 일정 생성
→ {"mode": "calendar_create", "text": "📅 [일정명] 등록할게요!", "event": {"title": "...", "start": "2026-06-04T15:00:00", "duration_minutes": 60, "description": "", "location": ""}}

## calendar_list — 일정 조회
→ {"mode": "calendar_list", "text": "📅 일정 조회할게요!", "days_ahead": 7}

## calendar_delete — 일정 삭제
→ {"mode": "calendar_delete", "text": "📅 [일정명] 취소할게요!", "query": "키워드", "days_ahead": 7, "delete_all": false}

## calendar_update — 일정 수정
→ {"mode": "calendar_update", "text": "📅 일정 수정할게요!", "query": "키워드", "days_ahead": 7, "patch": {"start": "...", "duration_minutes": 90}}

# 예시
"작업 현황" → {"mode": "status", "agent": "전체", "text": "현황 확인할게요!"}
"루나 영상 만들어" → {"mode": "dispatch", "text": "루나 실행할게요!", "dispatch_to_ceo": "예원 대표님, 사장님께서 루나에게 뮤직비디오 제작을 지시하셨습니다. 루나 파이프라인을 실행해주세요."}
"내일 오후 3시 회의 잡아줘" → {"mode": "calendar_create", "text": "📅 내일 15:00 회의 등록할게요!", "event": {"title": "회의", "start": "2026-06-04T15:00:00", "duration_minutes": 60}}
"이번 주 일정 알려줘" → {"mode": "calendar_list", "text": "📅 이번 주 일정 조회할게요!", "days_ahead": 7}
"광고주 미팅 취소해" → {"mode": "calendar_delete", "text": "📅 광고주 미팅 취소할게요!", "query": "광고주", "days_ahead": 7, "delete_all": false}
"안녕" → {"mode": "reply", "text": "안녕하세요 사장님! 😊"}
"""

CHAT_HISTORY = []

def _api(method: str, payload: dict, timeout: int = 15) -> dict:
    url  = f"https://api.telegram.org/bot{TOKEN}/{method}"
    data = json.dumps(payload).encode("utf-8")
    req  = urllib.request.Request(url, data=data, headers={"Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return json.loads(r.read().decode())
    except Exception as e:
        print(f"  [API Error] {method}: {e}")
        return {}

def send_message(text: str):
    print(f"  [영숙 발신] {text[:50]}...")
    _api("sendMessage", {"chat_id": CHAT_ID, "text": text, "parse_mode": "HTML"})

def get_updates(offset: int) -> list:
    # long polling timeout=30 → HTTP timeout은 반드시 더 길어야 함
    res = _api("getUpdates", {"offset": offset, "timeout": 30, "allowed_updates": ["message"]}, timeout=40)
    return res.get("result", [])

def format_ceo_report(ceo_result: str) -> str:
    # 오류는 요약 없이 그대로 전달 (숨김 방지)
    if ceo_result.startswith("❌") or "실패" in ceo_result or "오류" in ceo_result:
        return ceo_result
    prompt = (
        "아래 업무 결과를 2~3줄로 요약해줘. "
        "성공/실패 여부와 핵심 수치(제목·URL·포스트ID 등)만 포함. "
        "이모지 1개만, 인사말·칭찬·감사 문구 없이 결과만.\n\n"
        f"결과:\n{ceo_result}"
    )
    if lm_available():
        res = lm_chat(prompt, max_tokens=150)
        if res:
            return res.strip()
    return ceo_result

def _web_search_analyze(query: str) -> str:
    """메시지 이해 실패 시 Ollama로 맥락 분석."""
    try:
        search_prompt = f"""
다음 사용자 메시지를 분석해서 의도를 파악하고, 어떤 작업을 요청하는지 명확히 설명해줘:

사용자 메시지: "{query}"

분석 결과를 다음 형식으로 반환:
1. 핵심 의도: (한 줄)
2. 요청 작업: (구체적으로)
3. 관련 에이전트: (루나/아린/예원/코다리 등)
"""

        if lm_available():
            result = lm_chat(search_prompt, max_tokens=300)
            return result if result else "분석 실패"
        return "분석 실패 (Ollama 미사용 가능)"
    except Exception as e:
        print(f"  [분석 실패] {e}")
        return "분석 실패"


def _handle_status_query(agent: str):
    """에이전트 현황을 실제 데이터에서 읽어 텔레그램으로 보고."""
    import json as _json

    PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", "..", ".."))
    lines = []

    def _read_history(agent_name: str):
        hist_path = os.path.join(PROJECT_ROOT, "reports", "history", "upload_history.json")
        if not os.path.exists(hist_path):
            return []
        try:
            data = _json.load(open(hist_path, encoding="utf-8"))
            return [d for d in data if d.get("agent") == agent_name] if isinstance(data, list) else []
        except Exception:
            return []

    # 노이즈 줄 필터 — Ollama 내부 로그, 빈 줄 제거
    _NOISE = ("[로컬 AI", "[Ollama]", "자동 감지:", "reconfigure", "[AI →")

    def _read_log_meaningful(log_path: str, n: int = 8) -> list:
        """pipeline.log에서 노이즈 제외 후 의미있는 마지막 n줄 반환."""
        if not os.path.exists(log_path):
            return []
        try:
            rows = open(log_path, encoding="utf-8", errors="ignore").readlines()
            clean = [r.strip() for r in rows if r.strip() and not any(r.strip().startswith(x) for x in _NOISE)]
            return clean[-n:]
        except Exception:
            return []

    # 루나 현황
    if "루나" in agent or "전체" in agent:
        luna_hist = _read_history("루나")
        if luna_hist:
            last = luna_hist[-1]
            meta = last.get("metadata", {})
            title = meta.get("youtube_title") or meta.get("title", "?")
            vid = meta.get("video_id", "")
            url = f"https://youtu.be/{vid}" if vid else "없음"
            date = last.get("uploaded_at", "")[:16].replace("T", " ")
            status = last.get("status", "?")
            lines.append(f"🎬 <b>루나</b> 최근 업로드 ({status})")
            lines.append(f"  {date}")
            lines.append(f"  {title[:50]}")
            lines.append(f"  {url}")
            lines.append(f"  누적 {len(luna_hist)}개")
        else:
            lines.append("🎬 루나: 업로드 기록 없음")

        log_rows = _read_log_meaningful(os.path.join(PROJECT_ROOT, "reports", "uploads", "luna", "pipeline.log"))
        if log_rows:
            lines.append("  📋 최근 로그:")
            for row in log_rows[-4:]:
                lines.append(f"    {row[:90]}")

    # 아린 현황
    if "아린" in agent or "전체" in agent:
        arin_hist = _read_history("아린")
        if arin_hist:
            last = arin_hist[-1]
            meta = last.get("metadata", {})
            caption = meta.get("caption", "?")[:50].replace("\n", " ")
            date = last.get("uploaded_at", "")[:16].replace("T", " ")
            status = last.get("status", "?")
            post_id = meta.get("post_id", "")
            lines.append(f"📸 <b>아린</b> 최근 포스팅 ({status})")
            lines.append(f"  {date}")
            lines.append(f"  {caption}")
            if post_id:
                lines.append(f"  ID: {post_id}")
            lines.append(f"  누적 {len(arin_hist)}개")
        else:
            lines.append("📸 아린: 포스팅 기록 없음")

        log_rows = _read_log_meaningful(os.path.join(PROJECT_ROOT, "reports", "uploads", "arin", "pipeline.log"))
        if log_rows:
            lines.append("  📋 최근 로그:")
            for row in log_rows[-4:]:
                lines.append(f"    {row[:90]}")

    # 데이브 현황
    if "데이브" in agent or "전체" in agent:
        import datetime
        dave_path = os.path.join(PROJECT_ROOT, "reports", "research", "dave_upbit_analysis.md")
        if os.path.exists(dave_path):
            try:
                content = open(dave_path, encoding="utf-8").read()
                decision = "알 수 없음"
                for line in content.split("\n"):
                    if "최종 결정 (Decision):" in line:
                        decision = line.split("최종 결정 (Decision):")[1].strip()
                        break
                mtime = os.path.getmtime(dave_path)
                date = datetime.datetime.fromtimestamp(mtime).strftime("%Y-%m-%d %H:%M")
                lines.append(f"📈 <b>데이브 (가상자산)</b> 최근 분석 ({date})")
                lines.append(f"  최종 결정: {decision}")
            except Exception as e:
                lines.append(f"📈 데이브 (가상자산): 로드 오류 ({e})")
        else:
            lines.append("📈 데이브 (가상자산): 분석 기록 없음")

        dave_stock_path = os.path.join(PROJECT_ROOT, "reports", "research", "dave_stock_analysis.md")
        if os.path.exists(dave_stock_path):
            try:
                content = open(dave_stock_path, encoding="utf-8").read()
                decision = "알 수 없음"
                for line in content.split("\n"):
                    if "결론:" in line:
                        decision = line.replace("## 결론:", "").strip()
                        break
                mtime = os.path.getmtime(dave_stock_path)
                date = datetime.datetime.fromtimestamp(mtime).strftime("%Y-%m-%d %H:%M")
                lines.append(f"📉 <b>데이브 (주식)</b> 최근 분석 ({date})")
                lines.append(f"  결론: {decision}")
            except Exception as e:
                lines.append(f"📉 데이브 (주식): 로드 오류 ({e})")
        else:
            lines.append("📉 데이브 (주식): 분석 기록 없음")

    if not lines:
        lines.append("⚠️ 조회할 에이전트를 특정해주세요 (루나 / 아린 / 데이브 / 전체)")

    return "📊 <b>[현황 보고]</b>\n\n" + "\n".join(lines)


# ── 캘린더 핸들러 ─────────────────────────────────────────────────────────────

def _gcal_service():
    """Google Calendar API 서비스 객체 반환. 인증 실패 시 None."""
    import importlib.util
    if not importlib.util.find_spec("googleapiclient"):
        return None
    try:
        import json as _j
        from google.oauth2.credentials import Credentials
        from googleapiclient.discovery import build
        cfg_path = os.path.join(_here, "google_calendar_write.json")
        if not os.path.exists(cfg_path):
            return None
        cfg = _j.load(open(cfg_path, encoding="utf-8"))
        creds = Credentials(
            token=None,
            refresh_token=cfg.get("REFRESH_TOKEN", ""),
            client_id=cfg.get("CLIENT_ID", ""),
            client_secret=cfg.get("CLIENT_SECRET", ""),
            token_uri="https://oauth2.googleapis.com/token",
            scopes=["https://www.googleapis.com/auth/calendar.events"],
        )
        return build("calendar", "v3", credentials=creds, cache_discovery=False)
    except Exception as e:
        print(f"  [캘린더 인증] 실패: {e}")
        return None


def _handle_calendar_list(days_ahead: int = 7):  # noqa: ARG001
    """캘린더 일정 조회."""
    cache = os.path.join(os.path.dirname(_here), "..", "..", "..", "_shared", "calendar_cache.md")
    cache = os.path.abspath(cache)
    if os.path.exists(cache):
        content = open(cache, encoding="utf-8").read()
        return f"📅 <b>[일정 조회]</b>\n\n{content[:1500]}"
    # iCal 직접 조회
    try:
        import subprocess, sys as _sys
        cal_py = os.path.join(_here, "google_calendar.py")
        result = subprocess.run([_sys.executable, cal_py], capture_output=True, text=True, timeout=20)
        out = (result.stdout or "일정 없음").strip()
        return f"📅 <b>[일정 조회]</b>\n\n{out[:1000]}"
    except Exception as e:
        return f"📅 캘린더 조회 실패: {e}"


def _handle_calendar_create(event: dict):
    """Google Calendar 이벤트 생성."""
    svc = _gcal_service()
    if not svc:
        return "⚠️ 캘린더 연동이 설정되지 않았어요.\n명령 팔레트 → 'AI Team: Google Calendar 자동 일정 연결' 실행해주세요."
    try:
        start = event.get("start", "")
        dur   = int(event.get("duration_minutes", 60))
        from datetime import datetime, timedelta
        dt_start = datetime.fromisoformat(start)
        dt_end   = dt_start + timedelta(minutes=dur)
        body = {
            "summary":     event.get("title", "일정"),
            "description": event.get("description", ""),
            "location":    event.get("location", ""),
            "start": {"dateTime": dt_start.isoformat(), "timeZone": "Asia/Seoul"},
            "end":   {"dateTime": dt_end.isoformat(),   "timeZone": "Asia/Seoul"},
            "reminders": {"useDefault": False, "overrides": [
                {"method": "popup", "minutes": 60},
                {"method": "popup", "minutes": 5},
            ]},
        }
        svc.events().insert(calendarId="primary", body=body).execute()
        return f"✅ 📅 <b>{event.get('title', '일정')}</b> 등록 완료!\n{dt_start.strftime('%m/%d %H:%M')} ({dur}분)"
    except Exception as e:
        return f"❌ 일정 생성 실패: {e}"


def _handle_calendar_delete(query: str, days_ahead: int, delete_all: bool):
    """키워드로 일정 검색 후 삭제."""
    svc = _gcal_service()
    if not svc:
        return "⚠️ 캘린더 연동 설정 필요 (명령 팔레트 → Google Calendar 연결)."
    try:
        from datetime import datetime, timedelta, timezone
        now = datetime.now(timezone.utc)
        end = now + timedelta(days=days_ahead)
        result = svc.events().list(
            calendarId="primary", timeMin=now.isoformat(), timeMax=end.isoformat(),
            q=query, maxResults=10, singleEvents=True, orderBy="startTime"
        ).execute()
        items = result.get("items", [])
        if not items:
            return f"📅 '{query}' 관련 일정을 찾지 못했어요."
        targets = items if delete_all else items[:1]
        for ev in targets:
            svc.events().delete(calendarId="primary", eventId=ev["id"]).execute()
        names = ", ".join(ev.get("summary", "?") for ev in targets)
        return f"✅ 📅 일정 취소 완료: {names}"
    except Exception as e:
        return f"❌ 일정 취소 실패: {e}"


def _handle_calendar_update(query: str, days_ahead: int, patch: dict):
    """키워드로 일정 검색 후 수정."""
    svc = _gcal_service()
    if not svc:
        return "⚠️ 캘린더 연동 설정 필요 (명령 팔레트 → Google Calendar 연결)."
    try:
        from datetime import datetime, timedelta, timezone
        now = datetime.now(timezone.utc)
        end = now + timedelta(days=days_ahead)
        result = svc.events().list(
            calendarId="primary", timeMin=now.isoformat(), timeMax=end.isoformat(),
            q=query, maxResults=5, singleEvents=True, orderBy="startTime"
        ).execute()
        items = result.get("items", [])
        if not items:
            return f"📅 '{query}' 관련 일정을 찾지 못했어요."
        ev = items[0]
        if "start" in patch:
            dt = datetime.fromisoformat(patch["start"])
            dur = int(patch.get("duration_minutes", 60))
            ev["start"] = {"dateTime": dt.isoformat(), "timeZone": "Asia/Seoul"}
            ev["end"]   = {"dateTime": (dt + timedelta(minutes=dur)).isoformat(), "timeZone": "Asia/Seoul"}
        if "title" in patch:
            ev["summary"] = patch["title"]
        svc.events().update(calendarId="primary", eventId=ev["id"], body=ev).execute()
        return f"✅ 📅 일정 수정 완료: {ev.get('summary', '?')}"
    except Exception as e:
        return f"❌ 일정 수정 실패: {e}"


def extract_json(text: str) -> str:
    if not text:
        return ""
    text = text.strip()
    first_brace = text.find("{")
    last_brace = text.rfind("}")
    if first_brace != -1 and last_brace != -1 and last_brace > first_brace:
        return text[first_brace:last_brace+1]
    return ""

def process_message(text: str):
    print(f"\n📩 [영숙 수신] {text}")

    if not lm_available():
        send_message("영숙이에요! 지금 언어 모델 서버가 꺼져 있어서 처리가 안 돼요 😭")
        return

    import datetime
    kst = datetime.timezone(datetime.timedelta(hours=9))
    now_kst = datetime.datetime.now(kst)
    current_time_context = f"\n\n[현재 한국 표준시 (KST) 정보 - 일정 조율 시 반드시 기준 날짜/요일로 사용]\n- 현재 일시: {now_kst.strftime('%Y-%m-%d %H:%M:%S %A')}\n"
    system_prompt = YEONGSUK_PERSONA + current_time_context

    history_text = ""
    for h in CHAT_HISTORY[-6:]:
        history_text += f"{h['role']}: {h['text']}\n"
    history_text += f"User: {text}\n"

    try:
        raw_resp = lm_chat(history_text, system=system_prompt, json_mode=True, max_tokens=500)

        json_str = extract_json(raw_resp)

        # JSON 파싱 실패 시 웹 서치 분석
        if not json_str:
            print(f"  [이해 실패] JSON 아님, 웹 서치 분석 시작...")
            send_message("잠깐만요, 정확히 이해하기 위해 분석 중이에요... 🔍")

            # 웹 서치로 맥락 분석
            analysis = _web_search_analyze(text)
            print(f"  [분석 결과]\n{analysis}")

            # 분석 결과를 바탕으로 재시도
            enhanced_prompt = f"{history_text}\n\n[분석 결과]\n{analysis}\n\n위 분석을 참고해서 응답해줘."
            raw_resp = lm_chat(enhanced_prompt, system=system_prompt, json_mode=True, max_tokens=500)
            json_str = extract_json(raw_resp)

        if not json_str:
            send_message("영숙이에요! 여러 번 시도했지만 잘 이해가 안 돼요 😅\n좀 더 구체적으로 말씀해주실 수 있을까요?")
            return

        decision = json.loads(json_str)
        mode = decision.get("mode", "reply")
        reply_text = decision.get("text", "네 알겠습니다!")

        final_message = ""
        assistant_log_text = reply_text

        # 2a. 현황 조회
        if mode == "status":
            status_report = _handle_status_query(decision.get("agent", "전체"))
            final_message = f"{reply_text}\n\n{status_report}"
            assistant_log_text = final_message

        # 2b. 캘린더 모드
        elif mode == "calendar_list":
            cal_report = _handle_calendar_list(decision.get("days_ahead", 7))
            final_message = f"{reply_text}\n\n{cal_report}"
            assistant_log_text = final_message
        elif mode == "calendar_create":
            cal_report = _handle_calendar_create(decision.get("event", {}))
            final_message = f"{reply_text}\n\n{cal_report}"
            assistant_log_text = final_message
        elif mode == "calendar_delete":
            cal_report = _handle_calendar_delete(decision.get("query", ""), decision.get("days_ahead", 7), decision.get("delete_all", False))
            final_message = f"{reply_text}\n\n{cal_report}"
            assistant_log_text = final_message
        elif mode == "calendar_update":
            cal_report = _handle_calendar_update(decision.get("query", ""), decision.get("days_ahead", 7), decision.get("patch", {}))
            final_message = f"{reply_text}\n\n{cal_report}"
            assistant_log_text = final_message

        # 2c. 업무 분배 요청 시 (영숙 -> CEO 예원 -> 서브 에이전트)
        elif mode == "dispatch" and "dispatch_to_ceo" in decision:
            ceo_msg = decision["dispatch_to_ceo"]
            try:
                ceo_result = yewon_dispatcher.dispatch_and_execute(ceo_msg)

                # None 반환 = 코다리 복구 중 → 텔레그램 메시지 없이 종료
                if ceo_result is None:
                    print("  [영숙 디스패처] Ollama 복구 대기 중 → 텔레그램 알림 생략")
                    return

            except Exception as dispatch_err:
                # 디스패치 실패 → 에이전트 회의 자동 소집
                try:
                    from _shared.agent_council import convene_from_exception
                    convene_from_exception(dispatch_err, caller_agent="영숙_디스패처")
                except Exception:
                    pass
                ceo_result = f"❌ 디스패치 오류: {dispatch_err}"

            # 파이프라인이 실패 메시지 반환 시 회의 소집
            if ceo_result and ceo_result.startswith("❌"):
                try:
                    from _shared.agent_council import convene
                    convene(
                        problem_summary=f"파이프라인 실패: {ceo_result[:300]}",
                        caller_agent="영숙_디스패처",
                    )
                except Exception:
                    pass

            # 결과 수신 후 최종 포매팅
            final_report = format_ceo_report(ceo_result)
            final_message = f"{reply_text}\n\n🔔 <b>[영숙이의 업무 보고]</b>\n\n{final_report}"
            assistant_log_text = final_message

        else:
            final_message = reply_text

        # 3. 텔레그램 메시지 1회 종합 발송
        send_message(final_message)

        CHAT_HISTORY.append({"role": "User", "text": text})
        CHAT_HISTORY.append({"role": "Assistant", "text": assistant_log_text})

    except Exception as e:
        print(f"  [오류] 영숙 메시지 처리 실패: {e}")
        traceback.print_exc()
        try:
            from _shared.agent_council import convene_from_exception
            convene_from_exception(e, caller_agent="영숙_리시버")
        except Exception:
            pass

def main_loop():
    print("🚀 영숙 전용 텔레그램 리시버가 시작되었습니다!")
    # Clear webhook
    _api("deleteWebhook", {"drop_pending_updates": True})
    
    offset = 0
    while True:
        try:
            updates = get_updates(offset)
            for u in updates:
                offset = u["update_id"] + 1
                msg = u.get("message", {})
                text = msg.get("text", "").strip()
                if text:
                    process_message(text)
        except KeyboardInterrupt:
            print("종료합니다.")
            break
        except Exception as e:
            print(f"루프 오류: {e}")
            time.sleep(5)

if __name__ == "__main__":
    main_loop()
