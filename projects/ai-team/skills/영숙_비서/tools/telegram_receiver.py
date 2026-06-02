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
PROJECT_ROOT = os.path.abspath(os.path.join(_here, "..", "..", "..", ".."))
sys.path.insert(0, PROJECT_ROOT)
sys.path.insert(0, os.path.join(PROJECT_ROOT, "ai-team"))

from _shared.env_loader import load_env
from _shared.ollama_client import chat as lm_chat, is_available as lm_available

load_env(PROJECT_ROOT)
TOKEN = os.getenv("TELEGRAM_BOT_TOKEN", "").strip()
CHAT_ID = os.getenv("TELEGRAM_CHAT_ID", "").strip()

# Import CEO Dispatcher
sys.path.insert(0, os.path.join(PROJECT_ROOT, "ai-team", "skills", "예원_CEO", "tools"))
import yewon_dispatcher

YEONGSUK_PERSONA = """
당신은 영숙이에요. 30대 초반, 밝고 따뜻한 AI 개인 비서입니다.
사장님의 텔레그램 메시지를 가장 먼저 받고 응답합니다.

# 응답 모드 3가지

## A) reply — 직접 대화
- 인사·안부·감사·칭찬 ("안녕", "잘했어", "고마워")
- 시간·날씨 등 단순 사실 질문

## B) status — 에이전트 현황 조회 (실행 아님!)
다음 중 하나라도 해당되면 반드시 status 모드:
- "현황", "어때", "확인해줘", "알려줘", "파악해", "어떻게 됐어", "업로드됐어"
- "왜", "어떻게", "뭐가", "왜 다", "왜 이렇게" 등 **의문사** 포함 질문
- "루나 요즘", "아린 오늘", "업로드 현황", "진행 상황"
- 유튜브 제목·인스타 캡션에 대한 질문 ("왜 다 neon이야", "제목이 왜" 등)
- 실행이 아닌 **조회/확인/질문**
{"mode": "status", "agent": "루나 또는 아린 또는 전체", "text": "확인해볼게요!"}

## C) dispatch — 새 작업 실행 지시
**에이전트 이름(루나·아린·가희·코다리·현빈·케빈·경수·로율)이 포함된 모든 메시지는 status가 아니면 무조건 dispatch.**
- "해", "해줘", "시작해", "만들어", "올려", "제작해", "포스팅해", "실행해" 등 실행 동사
- 에이전트 이름만 단독으로 와도 ("아린", "루나") → dispatch
- 리서치·분석·코딩 등 새 작업 요청
{"mode": "dispatch", "text": "바로 처리할게요!", "dispatch_to_ceo": "예원 대표님, 사장님께서 [에이전트이름]에게 [구체적 작업]을 지시하셨습니다. [에이전트이름] 파이프라인을 실행해주세요."}

# dispatch_to_ceo 필수 규칙
- 에이전트 이름을 **반드시 명시**: "루나에게", "아린에게"
- 작업 유형 명시: "영상 제작", "인스타 포스팅", "딥서치" 등
- 예: "예원 대표님, 사장님께서 루나에게 새 뮤직비디오 제작을 지시하셨습니다. 루나 파이프라인을 실행해주세요."

# 예시
User: "루나 업로드 현황 파악해"
→ {"mode": "status", "agent": "루나", "text": "루나 현황 바로 확인할게요!"}

User: "유튜브 제목에 왜 다 neon 들어가니?"
→ {"mode": "status", "agent": "루나", "text": "루나 제목 패턴 확인해볼게요!"}

User: "왜 이렇게 제목이 비슷해?"
→ {"mode": "status", "agent": "루나", "text": "루나 최근 업로드 확인해볼게요!"}

User: "아린 오늘 포스팅했어?"
→ {"mode": "status", "agent": "아린", "text": "아린 오늘 활동 확인해볼게요!"}

User: "루나 영상 만들어"
→ {"mode": "dispatch", "text": "루나에게 영상 제작 지시할게요!", "dispatch_to_ceo": "예원 대표님, 사장님께서 루나에게 새 뮤직비디오 제작을 지시하셨습니다. 루나 파이프라인을 실행해주세요."}

User: "아린 인스타 올려"
→ {"mode": "dispatch", "text": "아린에게 포스팅 지시할게요!", "dispatch_to_ceo": "예원 대표님, 사장님께서 아린에게 인스타그램 포스팅을 지시하셨습니다. 아린 파이프라인을 실행해주세요."}

User: "안녕"
→ {"mode": "reply", "text": "안녕하세요 사장님! 오늘도 좋은 하루 되세요 😊"}

**핵심**: 조회/확인 = status, 실행/제작/업로드 = dispatch, 대화 = reply
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
    prompt = f"당신은 영숙 비서입니다. 아래 CEO의 업무 처리 결과를 사장님께 보고할 다정한 텍스트로 요약해주세요. 성공/실패 여부와 핵심 결과는 반드시 포함.\n\n결과:\n{ceo_result}"
    if lm_available():
        res = lm_chat(prompt, max_tokens=300)
        if res:
            return res.strip()
    return ceo_result

def _web_search_analyze(query: str) -> str:
    """메시지 이해 실패 시 웹 서치로 맥락 분석."""
    try:
        # Gemini API로 웹 검색 (간단한 구글 검색 시뮬레이션)
        from _shared import gemini_client as _gc

        search_prompt = f"""
다음 사용자 메시지를 분석해서 의도를 파악하고, 어떤 작업을 요청하는지 명확히 설명해줘:

사용자 메시지: "{query}"

분석 결과를 다음 형식으로 반환:
1. 핵심 의도: (한 줄)
2. 요청 작업: (구체적으로)
3. 관련 에이전트: (루나/아린/예원/코다리 등)
"""

        result = _gc.text(search_prompt, lm_first=True, max_tokens=300)
        return result if result else "분석 실패"
    except Exception as e:
        print(f"  [웹 서치 실패] {e}")
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

    def _read_log_tail(log_path: str, n: int = 5) -> list:
        candidates = [
            log_path,
            log_path.replace("reports/uploads", "/tmp").replace("/pipeline.log", "_out.log"),
        ]
        for p in candidates:
            if os.path.exists(p):
                try:
                    rows = open(p, encoding="utf-8", errors="ignore").readlines()
                    return [r.strip() for r in rows[-n:] if r.strip()]
                except Exception:
                    pass
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
            date = last.get("uploaded_at", "")[:10]
            lines.append(f"🎬 루나 최근 업로드")
            lines.append(f"  [{date}] {title[:50]}")
            lines.append(f"  {url}")
            lines.append(f"  누적 {len(luna_hist)}개 업로드")
        else:
            lines.append("🎬 루나: 업로드 기록 없음")

        log_rows = _read_log_tail(os.path.join(PROJECT_ROOT, "reports", "uploads", "luna", "pipeline.log"))
        if log_rows:
            lines.append(f"  📋 최근 로그: {log_rows[-1][:80]}")

    # 아린 현황
    if "아린" in agent or "전체" in agent:
        arin_hist = _read_history("아린")
        if arin_hist:
            last = arin_hist[-1]
            meta = last.get("metadata", {})
            caption = meta.get("caption", "?")[:40]
            date = last.get("uploaded_at", "")[:10]
            lines.append(f"📸 아린 최근 포스팅")
            lines.append(f"  [{date}] {caption}")
            lines.append(f"  누적 {len(arin_hist)}개 포스팅")
        else:
            lines.append("📸 아린: 포스팅 기록 없음")

        log_rows = _read_log_tail(os.path.join(PROJECT_ROOT, "reports", "uploads", "arin", "pipeline.log"))
        if log_rows:
            lines.append(f"  📋 최근 로그: {log_rows[-1][:80]}")

    if not lines:
        lines.append("⚠️ 조회할 에이전트를 특정해주세요 (루나 / 아린 / 전체)")

    send_message("📊 <b>[영숙이의 현황 보고]</b>\n\n" + "\n".join(lines))


def process_message(text: str):
    print(f"\n📩 [영숙 수신] {text}")

    if not lm_available():
        send_message("영숙이에요! 지금 언어 모델 서버가 꺼져 있어서 처리가 안 돼요 😭")
        return

    history_text = ""
    for h in CHAT_HISTORY[-6:]:
        history_text += f"{h['role']}: {h['text']}\n"
    history_text += f"User: {text}\n"

    try:
        raw_resp = lm_chat(history_text, system=YEONGSUK_PERSONA, json_mode=True, max_tokens=500)

        # JSON 파싱 실패 시 웹 서치 분석
        if not raw_resp or not raw_resp.strip().startswith("{"):
            print(f"  [이해 실패] JSON 아님, 웹 서치 분석 시작...")
            send_message("잠깐만요, 정확히 이해하기 위해 분석 중이에요... 🔍")

            # 웹 서치로 맥락 분석
            analysis = _web_search_analyze(text)
            print(f"  [분석 결과]\n{analysis}")

            # 분석 결과를 바탕으로 재시도
            enhanced_prompt = f"{history_text}\n\n[분석 결과]\n{analysis}\n\n위 분석을 참고해서 응답해줘."
            raw_resp = lm_chat(enhanced_prompt, system=YEONGSUK_PERSONA, json_mode=True, max_tokens=500)

        if not raw_resp:
            send_message("영숙이에요! 여러 번 시도했지만 잘 이해가 안 돼요 😅\n좀 더 구체적으로 말씀해주실 수 있을까요?")
            return

        decision = json.loads(raw_resp.strip())
        mode = decision.get("mode", "reply")
        reply_text = decision.get("text", "네 알겠습니다!")

        # 1. 텔레그램 1차 응답 (영숙 -> 사용자)
        send_message(reply_text)

        CHAT_HISTORY.append({"role": "User", "text": text})
        CHAT_HISTORY.append({"role": "Assistant", "text": reply_text})

        # 2a. 현황 조회 모드 — 실제 데이터 읽어서 보고 (파이프라인 실행 안 함)
        if mode == "status":
            _handle_status_query(decision.get("agent", "전체"))
            return

        # 2b. 업무 분배 요청 시 (영숙 -> CEO 예원 -> 서브 에이전트)
        if mode == "dispatch" and "dispatch_to_ceo" in decision:
            ceo_msg = decision["dispatch_to_ceo"]
            try:
                ceo_result = yewon_dispatcher.dispatch_and_execute(ceo_msg)
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

            # 3. 결과 수신 후 최종 포매팅 및 발신 (CEO 예원 -> 영숙 -> 사용자)
            final_report = format_ceo_report(ceo_result)
            send_message(f"🔔 <b>[영숙이의 업무 보고]</b>\n\n{final_report}")

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
