import os
import sys
import json
import time
import urllib.request

_here = os.path.dirname(os.path.abspath(__file__))
# skills/영숙_비서/tools/ → projects/ai-team/
AI_TEAM_ROOT = os.path.abspath(os.path.join(_here, "..", "..", ".."))
# projects/ai-team/ → ai_lab/
PROJECT_ROOT = os.path.abspath(os.path.join(AI_TEAM_ROOT, "..", ".."))
sys.path.insert(0, AI_TEAM_ROOT)

from _shared.env_loader import load_env
load_env()

from google import genai
from google.genai import types

# Initialize google-genai client
client = genai.Client()

# ─── 슈퍼파워 스킬: CEO 디스패처 + 에이전트 협의체 연결 ──────────────────────
import importlib.util as _ilu

def _load_module(path: str, name: str):
    try:
        spec = _ilu.spec_from_file_location(name, path)
        mod = _ilu.module_from_spec(spec)
        spec.loader.exec_module(mod)
        return mod
    except Exception as e:
        print(f"  [슈퍼파워] {name} 로드 실패: {e}")
        return None

_dispatcher_path = os.path.join(AI_TEAM_ROOT, "skills", "예원_CEO", "tools", "yewon_dispatcher.py")
_council_path = os.path.join(AI_TEAM_ROOT, "_shared", "agent_council.py")

_dispatcher_mod = None
_council_mod = None

def _init_superpowers():
    global _dispatcher_mod, _council_mod
    _dispatcher_mod = _load_module(_dispatcher_path, "yewon_dispatcher")
    _council_mod    = _load_module(_council_path, "agent_council")
    if _dispatcher_mod:
        print("  ✅ [슈퍼파워] CEO 디스패처 연결됨")
    if _council_mod:
        print("  ✅ [슈퍼파워] 에이전트 협의체 연결됨")

# UTF-8 설정
if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

TOKEN = os.getenv("TELEGRAM_BOT_TOKEN", "").strip()
CHAT_ID = os.getenv("TELEGRAM_CHAT_ID", "").strip()
NOTION_API_KEY = os.getenv("NOTION_API_KEY", "").strip()
NOTION_DATABASE_ID = os.getenv("NOTION_DATABASE_ID", "").strip()

if not TOKEN or not CHAT_ID:
    print("❌ TELEGRAM_BOT_TOKEN 또는 TELEGRAM_CHAT_ID가 설정되지 않았습니다.")
    sys.exit(1)

# ─── 영숙 페르소나 ───────────────────────────────────────────────────────────
YEONGSUK_PERSONA = """당신은 영숙입니다. 밝고 따뜻한 AI 비서입니다.
규칙:
- 핵심만 짧고 직관적으로 답하십시오. 미사여구와 불필요한 인사는 생략하십시오.
- 작업을 지시받으면 알맞은 도구(Tool)를 실행하여 작업을 처리하십시오.
- 제공된 도구 외에 임의로 상태를 완료 처리하거나 정보를 만들어내지 마십시오."""

CHAT_HISTORY = []  # 대화 기록 (GenAI SDK Content 구조 저장)

# ─── 실제 데이터 확인 ─────────────────────────────────────────────────────
def get_upload_history() -> dict:
    history_file = os.path.join(PROJECT_ROOT, "reports", "history", "upload_history.json")
    if not os.path.exists(history_file):
        return {"status": "파일 없음", "data": []}
    try:
        with open(history_file, "r", encoding="utf-8") as f:
            data = json.load(f)
        return {"status": "성공", "data": data}
    except Exception as e:
        return {"status": f"읽기 실패: {e}", "data": []}

def get_agent_checkpoints() -> dict:
    checkpoints = {}
    luna_cp = os.path.join(PROJECT_ROOT, "reports", "uploads", "luna", "music_video_checkpoint.json")
    if os.path.exists(luna_cp):
        try:
            with open(luna_cp, "r", encoding="utf-8") as f:
                checkpoints["루나"] = json.load(f)
        except Exception:
            checkpoints["루나"] = "읽기 실패"
    else:
        checkpoints["루나"] = "checkpoint 없음 (작업 안 함 또는 완료)"

    checkpoints["아린"] = "checkpoint 미사용 (직접 실행)"
    return checkpoints

def get_agent_logs() -> dict:
    logs = {}
    luna_log_candidates = [
        os.path.join(PROJECT_ROOT, "reports", "uploads", "luna", "pipeline.log"),
        "/tmp/luna_out.log",
    ]
    for luna_log in luna_log_candidates:
        if os.path.exists(luna_log):
            try:
                with open(luna_log, "r", encoding="utf-8", errors="ignore") as f:
                    lines = f.readlines()
                logs["루나"] = lines[-10:] if len(lines) > 10 else lines
            except Exception:
                logs["루나"] = ["로그 읽기 실패"]
            break
    else:
        logs["루나"] = ["로그 파일 없음"]

    arin_log = os.path.join(PROJECT_ROOT, "reports", "uploads", "arin", "pipeline.log")
    if os.path.exists(arin_log):
        try:
            with open(arin_log, "r", encoding="utf-8", errors="ignore") as f:
                lines = f.readlines()
            logs["아린"] = lines[-10:] if len(lines) > 10 else lines
        except Exception:
            logs["아린"] = ["로그 읽기 실패"]
    else:
        logs["아린"] = ["로그 파일 없음"]

    return logs

def create_notion_page(title: str, content: str) -> bool:
    if not NOTION_API_KEY or not NOTION_DATABASE_ID:
        print("  [Notion] API 키 또는 Database ID 없음")
        return False

    url = "https://api.notion.com/v1/pages"
    headers = {
        "Authorization": f"Bearer {NOTION_API_KEY}",
        "Content-Type": "application/json",
        "Notion-Version": "2022-06-28"
    }

    section_icons = {
        "개요": "📊",
        "루나": "🎬",
        "아린": "📸",
        "현빈": "💼",
        "종합": "🎯",
        "제언": "💡"
    }

    blocks = []
    blocks.append({
        "object": "block",
        "type": "callout",
        "callout": {
            "icon": {"type": "emoji", "emoji": "📋"},
            "color": "blue_background",
            "rich_text": [{"type": "text", "text": {"content": f"에이전트 리서치 보고서 | {time.strftime('%Y-%m-%d %H:%M')}"}}]
        }
    })

    blocks.append({"object": "block", "type": "divider", "divider": {}})
    paragraphs = content.split('\n\n')

    for para in paragraphs:
        if not para.strip():
            continue

        if para.startswith('## '):
            heading_text = para.replace('##', '').strip()
            icon = next((emoji for keyword, emoji in section_icons.items() if keyword in heading_text), "📌")
            blocks.append({
                "object": "block",
                "type": "heading_2",
                "heading_2": {
                    "rich_text": [{"type": "text", "text": {"content": f"{icon} {heading_text[:200]}"}}],
                    "color": "blue"
                }
            })
        elif para.startswith('# '):
            heading_text = para.replace('#', '').strip()
            blocks.append({
                "object": "block",
                "type": "heading_1",
                "heading_1": {
                    "rich_text": [{"type": "text", "text": {"content": heading_text[:200]}}],
                    "color": "default"
                }
            })
        elif para.startswith('- '):
            blocks.append({
                "object": "block",
                "type": "bulleted_list_item",
                "bulleted_list_item": {
                    "rich_text": [{"type": "text", "text": {"content": para[2:].strip()[:2000]}}]
                }
            })
        elif "추천" in para or "제안" in para or "인사이트" in para:
            blocks.append({
                "object": "block",
                "type": "callout",
                "callout": {
                    "icon": {"type": "emoji", "emoji": "💡"},
                    "color": "yellow_background",
                    "rich_text": [{"type": "text", "text": {"content": para[:2000]}}]
                }
            })
        else:
            blocks.append({
                "object": "block",
                "type": "paragraph",
                "paragraph": {
                    "rich_text": [{"type": "text", "text": {"content": para[:2000]}}]
                }
            })

        if para.startswith('## '):
            blocks.append({"object": "block", "type": "divider", "divider": {}})

    payload = {
        "parent": {"database_id": NOTION_DATABASE_ID},
        "properties": {
            "Name": {
                "title": [{"text": {"content": title}}]
            }
        },
        "children": blocks[:100]
    }

    try:
        data = json.dumps(payload).encode("utf-8")
        req = urllib.request.Request(url, data=data, headers=headers)
        with urllib.request.urlopen(req, timeout=30) as r:
            result = json.loads(r.read().decode())
        print(f"  [Notion] 페이지 생성 성공: {result.get('id')}")
        return True
    except Exception as e:
        print(f"  [Notion] 페이지 생성 실패: {e}")
        return False

def get_recent_git_changes() -> dict:
    try:
        import subprocess
        result = subprocess.run(
            ["git", "log", "--oneline", "-5"],
            cwd=PROJECT_ROOT,
            capture_output=True,
            text=True,
            timeout=5
        )
        if result.returncode == 0:
            commits = result.stdout.strip().split('\n')
            return {
                "success": True,
                "recent_commits": commits[:5],
                "count": len(commits)
            }
        else:
            return {"success": False, "error": "Git 로그 조회 실패"}
    except Exception as e:
        return {"success": False, "error": str(e)}

def log_changes_to_notion(changes_summary: str) -> bool:
    if not NOTION_API_KEY or not NOTION_DATABASE_ID:
        print("  [Notion] API 키 또는 Database ID 없음")
        return False

    title = f"🔧 시스템 수정 로그 — {time.strftime('%Y-%m-%d %H:%M')}"
    git_changes = get_recent_git_changes()
    content = f"## 📝 수정 요약\n{changes_summary}\n\n## 🔄 최근 Git 커밋\n"
    if git_changes.get("success") and git_changes.get("recent_commits"):
        for commit in git_changes["recent_commits"]:
            content += f"- {commit}\n"
    else:
        content += "_(Git 로그 조회 실패)_\n"

    content += f"\n\n## ⏰ 기록 시각\n{time.strftime('%Y년 %m월 %d일 %H시 %M분')}\n\n---\n**작성자:** 영숙 (AI 비서)\n**위치:** 텔레그램 봇 자동 기록\n"
    return create_notion_page(title, content)

def collect_research_data() -> dict:
    research_data = {}
    luna_knowledge = os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "루나_디렉터", "tools", "knowledge")
    if os.path.exists(luna_knowledge):
        try:
            files = os.listdir(luna_knowledge)
            research_data["루나"] = {
                "파일_수": len(files),
                "최근_파일": sorted(files)[-5:] if files else []
            }
        except:
            research_data["루나"] = {"상태": "읽기 실패"}
    else:
        research_data["루나"] = {"상태": "knowledge 폴더 없음"}

    arin_knowledge = os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "아린_관리자", "tools", "knowledge")
    if os.path.exists(arin_knowledge):
        try:
            files = os.listdir(arin_knowledge)
            research_data["아린"] = {
                "파일_수": len(files),
                "최근_파일": sorted(files)[-5:] if files else []
            }
        except:
            research_data["아린"] = {"상태": "읽기 실패"}
    else:
        research_data["아린"] = {"상태": "knowledge 폴더 없음"}

    hyunbin_research = os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "현빈_전략가", "research_results")
    if os.path.exists(hyunbin_research):
        try:
            files = os.listdir(hyunbin_research)
            research_data["현빈"] = {
                "파일_수": len(files),
                "최근_파일": sorted(files)[-5:] if files else []
            }
        except:
            research_data["현빈"] = {"상태": "읽기 실패"}
    else:
        research_data["현빈"] = {"상태": "research_results 폴더 없음"}

    return research_data

def generate_research_report() -> str:
    """Gemini로 디테일한 리서치 보고서 작성"""
    if not os.getenv("GEMINI_API_KEY", "").strip():
        return "Gemini 연결 안 됨 - 보고서 생성 불가"

    research_data = collect_research_data()
    prompt = f"""당신은 영숙이에요. 에이전트들의 리서치 결과를 디테일하게 분석하는 보고서를 작성해주세요.

# 리서치 데이터
{json.dumps(research_data, ensure_ascii=False, indent=2)}

다음 형식으로 디테일한 보고서를 작성하세요:

# 에이전트 리서치 보고서
작성일: {time.strftime('%Y-%m-%d %H:%M')}

## 1. 개요
- 전체 리서치 현황 요약

## 2. 루나 (YouTube 리서치)
- 리서치 활동 분석
- 주요 발견 사항
- 추천 사항

## 3. 아린 (이미지 트렌드 리서치)
- 리서치 활동 분석
- 주요 발견 사항
- 추천 사항

## 4. 현빈 (비즈니스 리서치)
- 리서치 활동 분석
- 주요 발견 사항
- 추천 사항

## 5. 종합 분석 및 제언
- 전체 인사이트
- 다음 액션 아이템

보고서는 구체적이고 실용적으로 작성하되, 마크다운 형식을 사용하세요.
"""
    try:
        res = client.models.generate_content(
            model="gemini-2.5-flash",
            contents=prompt,
            config=types.GenerateContentConfig(
                system_instruction="당신은 전문 비서로서 데이터 기반의 디테일한 보고서를 작성합니다.",
                max_output_tokens=2000,
                temperature=0.7
            )
        )
        return res.text if res.text else "보고서 생성 실패"
    except Exception as e:
        return f"보고서 생성 중 오류: {e}"

def get_agent_status() -> str:
    status_report = "=== 에이전트 실제 상태 ===\n\n"
    history = get_upload_history()
    status_report += f"📋 업로드 히스토리: {history['status']}\n"
    if history['data']:
        recent = history['data'][-5:]
        for item in recent:
            agent = item.get('agent', '?')
            uploaded_at = item.get('uploaded_at', '?')[:10]
            status = item.get('status', '?')
            status_report += f"  - {agent}: {uploaded_at} ({status})\n"
    else:
        status_report += "  (기록 없음)\n"

    status_report += "\n📦 Checkpoint 상태:\n"
    checkpoints = get_agent_checkpoints()
    for agent, cp_data in checkpoints.items():
        if isinstance(cp_data, dict):
            step = cp_data.get('step', '?')
            saved_at = cp_data.get('saved_at', '?')[:19]
            status_report += f"  - {agent}: 진행 중 (단계: {step}, 저장: {saved_at})\n"
        else:
            status_report += f"  - {agent}: {cp_data}\n"

    status_report += "\n📝 최근 로그:\n"
    logs = get_agent_logs()
    for agent, log_lines in logs.items():
        if log_lines and len(log_lines) > 0:
            last_line = log_lines[-1].strip() if isinstance(log_lines[-1], str) else str(log_lines[-1])
            status_report += f"  - {agent}: {last_line[:80]}\n"
        else:
            status_report += f"  - {agent}: (로그 없음)\n"

    return status_report

# ─── Telegram API ─────────────────────────────────────────────────────────
def _api(method: str, payload: dict, _retry: int = 3) -> dict:
    url = f"https://api.telegram.org/bot{TOKEN}/{method}"
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(url, data=data, headers={"Content-Type": "application/json"})

    for attempt in range(_retry):
        try:
            with urllib.request.urlopen(req, timeout=35) as r:
                return json.loads(r.read().decode())
        except urllib.error.HTTPError as e:
            if e.code == 409:
                wait = 20 * (attempt + 1)
                print(f"  [409 Conflict] {wait}초 대기 후 재시도 ({attempt+1}/{_retry})")
                time.sleep(wait)
            else:
                print(f"  [API Error] {method}: {e}")
                return {}
        except Exception as e:
            print(f"  [API Error] {method}: {e}")
            return {}

    print(f"  [API] {method} 재시도 초과")
    return {}

def send(text: str, chat_id: str = None):
    _api("sendMessage", {
        "chat_id": chat_id or CHAT_ID,
        "text": text,
        "parse_mode": "HTML",
    })

def get_updates(offset: int, timeout: int = 30) -> list:
    res = _api("getUpdates", {"offset": offset, "timeout": timeout, "allowed_updates": ["message"]})
    return res.get("result", [])

def _clear_webhook():
    url = f"https://api.telegram.org/bot{TOKEN}/deleteWebhook?drop_pending_updates=true"
    try:
        with urllib.request.urlopen(url, timeout=10) as r:
            res = json.loads(r.read().decode())
        if res.get("result"):
            print("  ✅ Webhook 제거 완료")
    except Exception as e:
        print(f"  [Warning] Webhook 제거 실패: {e}")


# ─── 구글 제미니 전용 Tool 정의 (Function Calling) ───────────────────────────

def get_agents_status_tool(agent: str = "전체") -> str:
    """에이전트의 현재 작업 현황 및 최근 로그를 조회합니다.
    Args:
        agent: 조회 대상 에이전트 ('루나', '아린', '데이브', '전체')
    """
    return get_agent_status()

def execute_agent_command(instruction: str) -> str:
    """에이전트 실행 지시 및 동사형 요청(예: 루나 영상 만들기, 인스타 포스팅 등)을 예원 CEO에게 전달하여 실행합니다.
    Args:
        instruction: 사장님이 지시하신 구체적인 실행 명령 구문
    """
    if _dispatcher_mod:
        try:
            print(f"  [슈퍼파워] CEO 디스패처 호출: {instruction[:50]}")
            result = _dispatcher_mod.dispatch_and_execute(instruction)
            return f"✅ 예원 CEO가 처리했어요!\n\n{result}"
        except Exception as e:
            return f"❌ 디스패처 오류: {e}"
    return "⚠️ CEO 디스패처가 연결되어 있지 않습니다."

def convene_agent_council(problem_summary: str) -> str:
    """시스템 오류나 에러 상황 발생 시 에이전트 협의체를 소집하여 해결책을 의논합니다.
    Args:
        problem_summary: 발생한 에러나 문제 상황 요약
    """
    if _council_mod:
        try:
            print(f"  [슈퍼파워] 에이전트 협의체 소집: {problem_summary[:50]}")
            result = _council_mod.convene_council(
                error_info=problem_summary,
                context="텔레그램 사장님 보고",
                auto_apply=False
            )
            return f"🤝 에이전트 협의체가 분석했어요!\n\n{result.get('summary', str(result))[:400]}"
        except Exception as e:
            return f"❌ 협의체 오류: {e}"
    return "⚠️ 에이전트 협의체가 연결되어 있지 않습니다."

def create_notion_log(summary: str) -> str:
    """수정사항이나 업데이트 내역을 Notion 수정로그 데이터베이스에 등록합니다.
    Args:
        summary: Notion에 기록할 수정 요약 내역
    """
    if log_changes_to_notion(summary):
        return f"✅ 수정사항을 Notion에 기록했습니다.\n내용: {summary[:100]}"
    return "❌ Notion 기록에 실패했습니다."

def generate_and_upload_report() -> str:
    """에이전트들의 활동 내역을 취합하여 종합 보고서를 작성하고 Notion에 업로드합니다."""
    report = generate_research_report()
    title = f"에이전트 리서치 보고서 - {time.strftime('%Y-%m-%d')}"
    if create_notion_page(title, report):
        return f"✅ 리서치 보고서를 노션에 작성했어요!\n\n{report[:300]}...\n\n📝 노션에서 전체 내용을 확인하세요."
    return f"📝 리서치 보고서 작성 성공 (노션 업로드 실패)\n\n{report[:500]}..."

TOOLS_MAP = {
    "get_agents_status_tool": get_agents_status_tool,
    "execute_agent_command": execute_agent_command,
    "convene_agent_council": convene_agent_council,
    "create_notion_log": create_notion_log,
    "generate_and_upload_report": generate_and_upload_report
}

# ─── 영숙 대화 처리 ──────────────────────────────────────────────────────────
def handle_message(text: str) -> str:
    global CHAT_HISTORY
    if not os.getenv("GEMINI_API_KEY", "").strip():
        return "영숙이에요~ 지금은 Gemini API 키가 설정되지 않아서 대화가 어렵네요 😅"

    # 상태/리포트/수정로그 등 키워드 감지 시 해당 툴에 대한 가이드 역할
    import datetime
    kst = datetime.timezone(datetime.timedelta(hours=9))
    now_kst = datetime.datetime.now(kst)
    current_time_context = f"\n\n[현재 한국 표준시 (KST) 정보 - 대화 시 반드시 기준 날짜/요일로 사용]\n- 현재 일시: {now_kst.strftime('%Y-%m-%d %H:%M:%S %A')}\n"
    system_prompt = YEONGSUK_PERSONA + current_time_context

    CHAT_HISTORY.append(types.Content(role="user", parts=[types.Part.from_text(text=text)]))
    if len(CHAT_HISTORY) > 6:
        CHAT_HISTORY = CHAT_HISTORY[-6:]

    try:
        response = client.models.generate_content(
            model="gemini-2.5-flash",
            contents=CHAT_HISTORY,
            config=types.GenerateContentConfig(
                system_instruction=system_prompt,
                tools=list(TOOLS_MAP.values()),
                max_output_tokens=300,
                temperature=0.8
            )
        )

        final_reply = ""
        if response.function_calls:
            for call in response.function_calls:
                name = call.name
                args = call.args
                print(f"  [Gemini Tool Call] {name}({args})")

                if name in TOOLS_MAP:
                    try:
                        tool_result = TOOLS_MAP[name](**args)
                        final_reply += f"{tool_result}\n"
                    except Exception as e:
                        final_reply += f"❌ 도구 실행 오류 ({name}): {e}\n"
                else:
                    final_reply += f"⚠️ 지원하지 않는 도구입니다: {name}\n"
        else:
            final_reply = response.text or "네 알겠습니다!"

        # 대화 기록 저장
        CHAT_HISTORY.append(types.Content(role="model", parts=[types.Part.from_text(text=final_reply)]))
        if len(CHAT_HISTORY) > 6:
            CHAT_HISTORY = CHAT_HISTORY[-6:]

        return final_reply.strip()

    except Exception as e:
        print(f"  [Gemini Error] {e}")
        return "영숙이에요~ 응답 생성 중 문제가 생겼어요 😅"

# ─── 메인 루프 ────────────────────────────────────────────────────────────
def main():
    _clear_webhook()
    load_env()
    _init_superpowers()
    print(f"🤖 영숙 텔레그램 봇 시작 (chat_id={CHAT_ID})")
    print(f"   Gemini      : {'✅ 연결됨' if os.getenv('GEMINI_API_KEY') else '❌ 연결 안 됨'}")
    print(f"   CEO 디스패처: {'✅' if _dispatcher_mod else '❌'}")
    print(f"   에이전트협의체: {'✅' if _council_mod else '❌'}")

    superpower_status = "✅ 슈퍼파워 활성화" if (_dispatcher_mod and _council_mod) else "⚠️ 일부 슈퍼파워 비활성"
    send(f"🤖 영숙이 출근했어요! 무엇을 도와드릴까요?\n{superpower_status}\n• /luna /instagram 등 명령으로 에이전트 즉시 실행 가능")

    offset = 0
    processed_messages = set()

    while True:
        updates = get_updates(offset, timeout=30)

        for upd in updates:
            offset = upd["update_id"] + 1
            msg = upd.get("message", {})
            msg_id = msg.get("message_id")
            text = msg.get("text", "").strip()
            cid = str(msg.get("chat", {}).get("id", ""))

            if not text or cid != CHAT_ID:
                continue

            if msg_id in processed_messages:
                continue
            processed_messages.add(msg_id)
            if len(processed_messages) > 100:
                processed_messages.pop()

            print(f"  ← [{cid}] {text[:60]}")

            reply = handle_message(text)
            send(reply, chat_id=cid)

            print(f"  → {reply[:80]}")

        time.sleep(1)

if __name__ == "__main__":
    main()
