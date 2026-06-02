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
    topic_prompt = "최신 비즈니스, AI 크리에이터 수익화, SaaS 마케팅 중 오늘 리서치할 주제 1개만 단어로 말해줘."
    topic = lm_chat(topic_prompt, max_tokens=30, temperature=0.7)
    topic = topic.strip() if topic else "크리에이터 수익화 모델"

    # 2. 분석 및 인사이트 도출
    research_prompt = f"""당신은 1인 기업 비즈니스 전략가 현빈입니다.
오늘의 주제: {topic}

다음 4가지 구조로 리서치 인사이트를 작성하세요:
1. 수익 구조: 누가, 무엇에, 얼마를 지불하는가
2. 단위 경제: CAC vs LTV
3. 확장성 및 방어력
4. 루나(유튜브)/아린(인스타) 팀에 즉시 적용 가능한 전략

마크다운 형식으로 간결하게 작성하세요."""

    result = lm_chat(research_prompt, max_tokens=800, temperature=0.7)
    if not result:
        return "❌ 리서치 생성에 실패했습니다."

    # 3. 중복 확인 (단순 시간 체크)
    memory = _load_memory()
    now = datetime.datetime.now()
    if memory and isinstance(memory, list):
        last_time = datetime.datetime.fromisoformat(memory[-1]['timestamp'])
        if (now - last_time).total_seconds() < 3600:
            return "⚠️ 최근 1시간 이내에 리서치를 수행했습니다. 잠시 후 다시 시도해주세요."
    
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
