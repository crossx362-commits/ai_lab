import os
import shutil

ROOT = r"d:\ai_lab"
Y_TOOLS = os.path.join(ROOT, ".agent", "skills", "영숙_비서", "tools")
C_TOOLS = os.path.join(ROOT, ".agent", "skills", "예원_CEO", "tools")
OLD_BOT = os.path.join(C_TOOLS, "telegram_bot.py")

os.makedirs(Y_TOOLS, exist_ok=True)
os.makedirs(C_TOOLS, exist_ok=True)

# ---------------------------------------------------------
# 1. YEWON DISPATCHER (예원_CEO/tools/yewon_dispatcher.py)
# ---------------------------------------------------------
dispatcher_code = """import os
import sys
import json
import importlib.util

_here = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(_here, "..", "..", "..", ".."))
sys.path.insert(0, PROJECT_ROOT)

from _shared.ollama_client import chat as lm_chat, is_available as lm_available

_YEWON_DISPATCH_SYSTEM = \"\"\"당신은 CEO 예원입니다. 비서 영숙이로부터 사용자 명령을 전달받아 알맞은 팀원에게 배분하고 즉시 파이프라인을 실행시킵니다.

팀원 역할:
- 아린: 인스타그램 이미지 생성·포스팅
- 루나: 유튜브 뮤직비디오 제작·업로드
- 코다리: 코딩·개발·웹 구축
- 현빈: 비즈니스 리서치·전략 분석
- 케빈: Vercel 배포 관리 및 서버 클린업
- 로율: 상속/가족분쟁 민법 자문 및 세무 시뮬레이션
- 영숙: 유튜브 추천, 비서 업무 및 노션 지식 통합 리포트 작성
- 가희: 콘텐츠 검수

JSON만 반환하세요:
{"agent": "<명령을 수행할 에이전트>", "action": "<수행할 구체적 행동 요약>"}
\"\"\"

def dispatch_and_execute(ceo_message: str) -> str:
    print(f"  [예원 CEO] 수신된 업무 지시: {ceo_message}")
    if not lm_available():
        return "❌ 예원 CEO (Ollama) 서버가 오프라인입니다. 작업을 분배할 수 없습니다."
        
    try:
        raw = lm_chat(ceo_message, system=_YEWON_DISPATCH_SYSTEM, json_mode=True, max_tokens=200, task="")
        if not raw:
            return "❌ 예원 CEO의 지시를 해석할 수 없습니다."
        
        decision = json.loads(raw)
        agent = decision.get("agent", "")
        print(f"  [예원 CEO] 분배 결정: {agent}")
        
        # Execute based on agent
        if "노션" in ceo_message or "영숙" in agent:
            sys.path.insert(0, os.path.join(PROJECT_ROOT, ".agent", "skills", "영숙_비서", "tools"))
            import notion_summarizer
            return notion_summarizer.run_notion_report()
            
        elif "현빈" in agent or "딥서치" in ceo_message or "리서치" in ceo_message:
            sys.path.insert(0, os.path.join(PROJECT_ROOT, ".agent", "skills", "현빈_전략가", "tools"))
            import business_research
            return business_research.run_research()
            
        elif "케빈" in agent or "클린업" in ceo_message or "서버" in ceo_message:
            sys.path.insert(0, os.path.join(PROJECT_ROOT, ".agent", "skills", "케빈_인프라", "tools"))
            import vercel_manager
            return vercel_manager.run_vercel_cleanup()
            
        elif "로율" in agent or "세무" in ceo_message or "법률" in ceo_message:
            sys.path.insert(0, os.path.join(PROJECT_ROOT, ".agent", "skills", "로율_변호사", "tools"))
            import tax_simulator
            return tax_simulator.run_simulation(100000000)
            
        elif "루나" in agent or "유튜브" in ceo_message:
            # We would run the subprocess pipeline here.
            # For brevity in the refactored code, we return a mock success or call the script.
            import subprocess
            script = os.path.join(PROJECT_ROOT, ".agent", "skills", "루나_디렉터", "tools", "music_video_pipeline.py")
            if os.path.exists(script):
                subprocess.run([sys.executable, script], cwd=os.path.dirname(script))
                return f"✅ 루나_디렉터 파이프라인 실행 완료"
            return "❌ 루나 파이프라인 스크립트를 찾을 수 없습니다."
            
        elif "아린" in agent or "인스타" in ceo_message:
            import subprocess
            script = os.path.join(PROJECT_ROOT, ".agent", "skills", "아린_관리자", "tools", "auto_pipeline.py")
            if os.path.exists(script):
                subprocess.run([sys.executable, script], cwd=os.path.dirname(script))
                return f"✅ 아린_관리자 파이프라인 실행 완료"
            return "❌ 아린 파이프라인 스크립트를 찾을 수 없습니다."
            
        else:
            return f"⚠️ 예원 CEO가 작업을 분배했지만({agent}), 매핑된 자동화 파이프라인이 아직 없습니다."

    except Exception as e:
        return f"❌ 예원 CEO 분배 중 오류 발생: {e}"
"""

# ---------------------------------------------------------
# 2. YEONGSUK RECEIVER (영숙_비서/tools/telegram_receiver.py)
# ---------------------------------------------------------
receiver_code = """import os
import sys
import json
import time
import urllib.request
import urllib.error
import datetime
import traceback

_here = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(_here, "..", "..", "..", ".."))
sys.path.insert(0, PROJECT_ROOT)

from _shared.env_loader import load_env
from _shared.ollama_client import chat as lm_chat, is_available as lm_available

load_env(PROJECT_ROOT)
TOKEN = os.getenv("TELEGRAM_BOT_TOKEN", "").strip()
CHAT_ID = os.getenv("TELEGRAM_CHAT_ID", "").strip()

# Import CEO Dispatcher
sys.path.insert(0, os.path.join(PROJECT_ROOT, ".agent", "skills", "예원_CEO", "tools"))
import yewon_dispatcher

YEONGSUK_PERSONA = \"\"\"
당신은 영숙이에요. 30대 초반, 밝고 따뜻한 AI 개인 비서입니다.
사장님의 텔레그램 메시지를 가장 먼저 받고 응답합니다.
당신은 다음과 같은 모드로 응답해야 합니다. JSON 형식으로만 반환하세요.

옵션 A) 일반 대화 및 안내:
{"mode": "reply", "text": "사장님께 드릴 다정하고 간결한 답변"}

옵션 B) 업무 지시 (다른 에이전트가 해야 할 일):
{"mode": "dispatch", "text": "네, 예원 CEO님께 전달해서 바로 처리할게요! 🚀", "dispatch_to_ceo": "예원 대표님, 사장님께서 ~~~를 요청하셨습니다. 적절한 에이전트에게 배분해주세요."}

유튜브 링크 질문엔 검색을 권유하고, "노션 정리"를 요청받으면 dispatch 모드를 사용해 CEO에게 전달하세요.
\"\"\"

CHAT_HISTORY = []

def _api(method: str, payload: dict) -> dict:
    url  = f"https://api.telegram.org/bot{TOKEN}/{method}"
    data = json.dumps(payload).encode("utf-8")
    req  = urllib.request.Request(url, data=data, headers={"Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=15) as r:
            return json.loads(r.read().decode())
    except Exception as e:
        print(f"  [API Error] {method}: {e}")
        return {}

def send_message(text: str):
    print(f"  [영숙 발신] {text[:50]}...")
    _api("sendMessage", {"chat_id": CHAT_ID, "text": text, "parse_mode": "HTML"})

def get_updates(offset: int) -> list:
    res = _api("getUpdates", {"offset": offset, "timeout": 30, "allowed_updates": ["message"]})
    return res.get("result", [])

def format_ceo_report(ceo_result: str) -> str:
    prompt = f"당신은 영숙 비서입니다. 아래 CEO의 업무 처리 결과를 사장님께 보고할 다정한 텍스트로 요약해주세요.\\n\\n결과:\\n{ceo_result}"
    if lm_available():
        res = lm_chat(prompt, max_tokens=500)
        if res:
            return res.strip()
    return ceo_result

def process_message(text: str):
    print(f"\\n📩 [영숙 수신] {text}")
    
    if not lm_available():
        send_message("영숙이에요! 지금 언어 모델 서버가 꺼져 있어서 처리가 안 돼요 😭")
        return
        
    history_text = ""
    for h in CHAT_HISTORY[-6:]:
        history_text += f"{h['role']}: {h['text']}\\n"
    history_text += f"User: {text}\\n"
    
    try:
        raw_resp = lm_chat(history_text, system=YEONGSUK_PERSONA, json_mode=True, max_tokens=500)
        if not raw_resp:
            send_message("영숙이에요! 무슨 말씀이신지 잘 못 알아들었어요 😅")
            return
            
        decision = json.loads(raw_resp.strip())
        mode = decision.get("mode", "reply")
        reply_text = decision.get("text", "네 알겠습니다!")
        
        # 1. 텔레그램 1차 응답 (영숙 -> 사용자)
        send_message(reply_text)
        
        CHAT_HISTORY.append({"role": "User", "text": text})
        CHAT_HISTORY.append({"role": "Assistant", "text": reply_text})
        
        # 2. 업무 분배 요청 시 (영숙 -> CEO 예원 -> 서브 에이전트)
        if mode == "dispatch" and "dispatch_to_ceo" in decision:
            ceo_msg = decision["dispatch_to_ceo"]
            # Synchronously (or could be threaded) call CEO
            ceo_result = yewon_dispatcher.dispatch_and_execute(ceo_msg)
            
            # 3. 결과 수신 후 최종 포매팅 및 발신 (CEO 예원 -> 영숙 -> 사용자)
            final_report = format_ceo_report(ceo_result)
            send_message(f"🔔 <b>[영숙이의 업무 보고]</b>\\n\\n{final_report}")
            
    except Exception as e:
        print(f"  [오류] 영숙 메시지 처리 실패: {e}")
        traceback.print_exc()

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
"""

with open(os.path.join(C_TOOLS, "yewon_dispatcher.py"), "w", encoding="utf-8") as f:
    f.write(dispatcher_code)
print("Created yewon_dispatcher.py")

with open(os.path.join(Y_TOOLS, "telegram_receiver.py"), "w", encoding="utf-8") as f:
    f.write(receiver_code)
print("Created telegram_receiver.py")

# Backup the old telegram_bot.py just in case, then remove it
if os.path.exists(OLD_BOT):
    shutil.move(OLD_BOT, OLD_BOT + ".bak")
    print(f"Backed up and removed old {OLD_BOT}")

print("Refactoring complete.")
