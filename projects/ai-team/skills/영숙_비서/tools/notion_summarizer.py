import os
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
from _shared.ollama_client import chat as ollama_chat
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
            combined_text += f.read()[:1000] + "\n\n---\n\n"
            
    prompt = (
        "당신은 스마트 비서 '영숙'입니다. 다음은 여러 에이전트들이 방금까지 수집한 최신 지식 리서치 자료입니다.\n"
        "이 자료들을 CEO가 한눈에 파악하기 쉽게 '핵심 인사이트 요약 보고서'로 작성해 주세요.\n"
        "말투는 전문적이면서도 깔끔하게, 마크다운(글머리기호 등)을 적극 사용하세요.\n\n"
        f"{combined_text}"
    )
    
    print("  [영숙] 지식 통합 분석 중...")
    summary = ollama_chat(prompt, task="", max_tokens=1000)

    if not summary:
        return "❌ 리서치 자료를 분석하는 데 실패했습니다 (AI 응답 오류)."
        
    now_str = datetime.datetime.now().strftime("%Y-%m-%d %H:%M")
    title = f"🧠 AI 팀 통합 리서치 리포트 ({now_str})"
    
    print("  [영숙] 노션(Notion) 슈퍼파워 툴 가동 중...")
    result = create_notion_page(title, summary)
    
    return result
    
if __name__ == "__main__":
    print(run_notion_report())
