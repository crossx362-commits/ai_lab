import os
import re

ROOT_DIR = r"d:\ai_lab"
SHARED_DIR = os.path.join(ROOT_DIR, "ai-team", "_shared")
AGENT_DIR = os.path.join(ROOT_DIR, ".agent")
YEONGSUK_TOOLS = os.path.join(AGENT_DIR, "skills", "영숙_비서", "tools")
BOT_FILE = os.path.join(AGENT_DIR, "skills", "예원_CEO", "tools", "telegram_bot.py")

# 1. Create notion_client.py
notion_client_code = """import os
import json
import urllib.request
import urllib.error

def create_notion_page(title: str, markdown_content: str) -> str:
    from _shared.env_loader import load_env
    load_env()
    
    api_key = os.getenv("NOTION_API_KEY", "")
    db_id = os.getenv("NOTION_DATABASE_ID", "")
    
    if not api_key or not db_id:
        return "❌ NOTION_API_KEY 또는 NOTION_DATABASE_ID가 환경변수에 없습니다."
        
    url = "https://api.notion.com/v1/pages"
    
    # Simple block conversion: Split by newlines and create text blocks
    # For a real implementation, a full markdown parser would be used,
    # but here we just convert simple paragraphs to text blocks.
    blocks = []
    lines = markdown_content.split("\\n")
    for line in lines:
        text = line.strip()
        if not text:
            continue
        blocks.append({
            "object": "block",
            "type": "paragraph",
            "paragraph": {
                "rich_text": [{"type": "text", "text": {"content": text}}]
            }
        })
        
    # Limit blocks to 100 per request (Notion API limit)
    blocks = blocks[:100]

    payload = {
        "parent": {"database_id": db_id},
        "properties": {
            "title": {
                "title": [{"text": {"content": title}}]
            }
        },
        "children": blocks
    }
    
    headers = {
        "Authorization": f"Bearer {api_key}",
        "Content-Type": "application/json",
        "Notion-Version": "2022-06-28"
    }
    
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(url, data=data, headers=headers)
    
    try:
        with urllib.request.urlopen(req, timeout=15) as r:
            res = json.loads(r.read())
            page_url = res.get("url", "")
            return f"✅ 노션 리포트 작성 완료! 링크: {page_url}"
    except urllib.error.HTTPError as e:
        err = e.read().decode()
        return f"❌ 노션 API 에러: {err}"
    except Exception as e:
        return f"❌ 노션 작성 실패: {e}"
"""

with open(os.path.join(SHARED_DIR, "notion_client.py"), "w", encoding="utf-8") as f:
    f.write(notion_client_code)
print("Created notion_client.py")

# 2. Create notion_summarizer.py
notion_summarizer_code = """import os
import sys

_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, ".agent")):
        break
    _root = os.path.dirname(_root)
sys.path.insert(0, _root)

from _shared.knowledge_base import get_kb_dir
from _shared.notion_client import create_notion_page
from _shared.gemini_client import text as llm_text
import glob
import datetime

def run_notion_report():
    kb_path = get_kb_dir()
    md_files = glob.glob(os.path.join(kb_path, "*.md"))
    
    if not md_files:
        return "영숙이에요! 아직 지식 베이스(Knowledge Base)에 수집된 리서치 결과가 없네요."
        
    # Sort by modification time and pick the latest 5
    md_files.sort(key=os.path.getmtime, reverse=True)
    recent_files = md_files[:5]
    
    combined_text = ""
    for mf in recent_files:
        with open(mf, "r", encoding="utf-8") as f:
            combined_text += f.read()[:1000] + "\\n\\n---\\n\\n"
            
    prompt = (
        "당신은 스마트 비서 '영숙'입니다. 다음은 여러 에이전트들이 방금까지 수집한 최신 지식 리서치 자료입니다.\\n"
        "이 자료들을 CEO가 한눈에 파악하기 쉽게 '핵심 인사이트 요약 보고서'로 작성해 주세요.\\n"
        "말투는 전문적이면서도 깔끔하게, 마크다운(글머리기호 등)을 적극 사용하세요.\\n\\n"
        f"{combined_text}"
    )
    
    print("  [영숙] 지식 통합 분석 중...")
    summary = llm_text(prompt, task="")
    
    if not summary:
        return "❌ 리서치 자료를 분석하는 데 실패했습니다 (AI 응답 오류)."
        
    now_str = datetime.datetime.now().strftime("%Y-%m-%d %H:%M")
    title = f"🧠 AI 팀 통합 리서치 리포트 ({now_str})"
    
    print("  [영숙] 노션(Notion) 슈퍼파워 툴 가동 중...")
    result = create_notion_page(title, summary)
    
    return result
    
if __name__ == "__main__":
    print(run_notion_report())
"""

os.makedirs(YEONGSUK_TOOLS, exist_ok=True)
with open(os.path.join(YEONGSUK_TOOLS, "notion_summarizer.py"), "w", encoding="utf-8") as f:
    f.write(notion_summarizer_code)
print("Created notion_summarizer.py")

# 3. Update telegram_bot.py
with open(BOT_FILE, "r", encoding="utf-8") as f:
    bot_content = f.read()

if "def cmd_notion_report" not in bot_content:
    # Add help text
    bot_content = bot_content.replace(
        "/evaluate  — 전체 RL 성과 평가", 
        "/evaluate  — 전체 RL 성과 평가\\n/notion    — 영숙 노션 지식 리포팅"
    )
    
    # Add handler function
    handler_code = """
def cmd_notion_report() -> str:
    try:
        import sys
        sys.path.insert(0, os.path.join(PROJECT_ROOT, ".agent", "skills", "영숙_비서", "tools"))
        import notion_summarizer
        return notion_summarizer.run_notion_report()
    except Exception as e:
        return f"❌ 노션 툴 실행 실패: {e}"
"""
    # Insert handler before `get_updates`
    bot_content = bot_content.replace("def get_updates(", handler_code + "\\ndef get_updates(")
    
    # Add routing logic
    route_logic = """
        elif text.startswith("/notion"):
            send("⏳ 영숙이가 지식 베이스를 읽고 노션 리포트를 작성하고 있어요... (슈퍼파워 툴 가동!)")
            import threading
            def _n_task():
                res = cmd_notion_report()
                send(res)
            threading.Thread(target=_n_task, daemon=True).start()
            continue
"""
    # Insert routing logic in the main loop, right after `/evaluate`
    bot_content = bot_content.replace(
        "elif text.startswith(\"/evaluate\"):", 
        route_logic.strip() + "\\n        elif text.startswith(\"/evaluate\"):"
    )
    
    with open(BOT_FILE, "w", encoding="utf-8") as f:
        f.write(bot_content)
    print("Updated telegram_bot.py with /notion command")

print("All Notion integration scripts created.")
