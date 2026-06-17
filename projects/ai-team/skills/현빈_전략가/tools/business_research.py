import os
import sys
import json
import datetime

_here = os.path.dirname(os.path.abspath(__file__))
_ai_team_root = os.path.abspath(os.path.join(_here, "..", "..", ".."))
if _ai_team_root not in sys.path:
    sys.path.insert(0, _ai_team_root)
from _shared.ollama_client import chat as lm_chat, is_available as lm_available
from _shared.env_loader import find_project_root
_root = find_project_root(_here)

MEMORY_FILE = os.path.join(_root, "reports", "research", "hyunbin_research.json")

def _load_memory():
    if os.path.exists(MEMORY_FILE):
        try:
            with open(MEMORY_FILE, 'r', encoding='utf-8') as f:
                return json.load(f)
        except:
            pass
    return []

def _save_memory(memory):
    os.makedirs(os.path.dirname(MEMORY_FILE), exist_ok=True)
    with open(MEMORY_FILE, 'w', encoding='utf-8') as f:
        json.dump(memory[-100:], f, ensure_ascii=False, indent=2)

def run_research():
    if not lm_available():
        return "❌ Ollama(로컬 AI)가 켜져 있지 않습니다."
    
    # 1. 주제 선정
    topic_prompt = "AI 크리에이터 수익화/SaaS 마케팅 중 오늘 리서치 주제 1개. 8단어 이하."
    topic = lm_chat(topic_prompt, max_tokens=20, temperature=0.5, task="strategy")
    topic = topic.strip() if topic else "크리에이터 수익화 모델"

    # 2. 분석 및 인사이트 도출
    research_prompt = (
        f"현빈 비즈니스 리서치|주제:{topic}\n"
        "markdown 4섹션만: 수익구조, CAC/LTV, 방어력, 즉시실행. "
        "섹션당 bullet 2개 이하. 숫자는 추정이면 '추정' 표시."
    )

    result = lm_chat(research_prompt, max_tokens=500, temperature=0.5, task="strategy")
    if not result:
        return "❌ 리서치 생성에 실패했습니다."

    # 3. 중복 확인 (단순 시간 체크)
    memory = _load_memory()
    # 파일이 dict로 저장된 경우 list로 초기화
    if not isinstance(memory, list):
        memory = []
    now = datetime.datetime.now()
    if memory:
        try:
            last_time = datetime.datetime.fromisoformat(memory[-1]['timestamp'])
            if (now - last_time).total_seconds() < 3600:
                return "⚠️ 최근 1시간 이내에 리서치를 수행했습니다. 잠시 후 다시 시도해주세요."
        except (KeyError, TypeError, ValueError):
            pass

    # 4. 저장
    memory.append({
        "timestamp": now.isoformat(),
        "topic": topic,
        "content": result[:100]
    })
    _save_memory(memory)


    return f"👔 **[현빈의 비즈니스 리서치]**\n주제: {topic}\n\n{result}"

if __name__ == "__main__":
    print(run_research())
