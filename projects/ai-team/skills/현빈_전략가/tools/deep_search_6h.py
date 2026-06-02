#!/usr/bin/env python3
"""
deep_search_6h.py — 현빈 (비즈니스 전략가) 6시간 딥서치 및 딥러닝 리서치 시뮬레이션 모듈.
"""
import os
import sys
import json
import time
import datetime

# ── 인코딩 설정 ───────────────────────────────────────────────────────────────
if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

# ── 프로젝트 루트 탐색 ────────────────────────────────────────────────────────
_here = os.path.dirname(os.path.abspath(__file__))
_ai_team_root = os.path.abspath(os.path.join(_here, "..", "..", ".."))
if _ai_team_root not in sys.path:
    sys.path.insert(0, _ai_team_root)
from _shared.env_loader import find_project_root
_root = find_project_root(_here)
try:
    from _shared.telegram_notifier import send_telegram_message
except ImportError:
    def send_telegram_message(msg):
        print(msg)

from _shared.env_loader import load_env\nfrom _shared.knowledge_base import store_knowledge as _load_env
from _shared.ollama_client import chat as lm_chat, is_available as lm_available

# 기본 주제 (펫과나)
DEFAULT_TOPICS = [
    {"hour": 1, "topic": "멀티 에이전트 협업 시스템 아키텍처", "prompt": "다중 AI 에이전트가 각자의 역할을 수행하며 텔레그램을 통해 상호작용하는 협업 시스템 아키텍처 설계 (상세 생략)"},
    {"hour": 2, "topic": "AI SaaS 구독 모델 LTV 극대화 전략", "prompt": "B2B/B2C 대상 AI SaaS의 월 구독 모델 단위 경제학 수립 및 이탈률 방어 전략 (상세 생략)"},
    {"hour": 3, "topic": "개인화 비서 봇의 데이터 프라이버시 유지 방안", "prompt": "클라이언트 로컬 캐싱 및 안전한 API 호출을 통한 사용자 데이터 보호 해징 아키텍처 (상세 생략)"},
    {"hour": 4, "topic": "로컬 LLM (Ollama) 서버 비용 효율성 최적화", "prompt": "로컬 언어 모델의 추론 성능(Tokens/s)과 유지보수 비용을 고려한 인프라 최적화 방안 (상세 생략)"},
    {"hour": 5, "topic": "API 에코시스템을 통한 수익 창출 방안", "prompt": "외부 파트너 및 개발자가 사용할 수 있는 유료 API 엔드포인트 설계 및 수수료 정산 프로토콜 (상세 생략)"},
    {"hour": 6, "topic": "10x 가치 혁신: AI 능동형(Proactive) 추천 엔진", "prompt": "사용자 질문을 기다리지 않고 선제적으로 비즈니스 인사이트와 콘텐츠를 추천하는 AI 엔진 기획 (상세 생략)"}
]

MEMORY_FILE = os.path.join(_root, "reports", "research", "hyunbin_research.json")

def _load_memory():
    if os.path.exists(MEMORY_FILE):
        try:
            with open(MEMORY_FILE, 'r', encoding='utf-8') as f:
                memory = json.load(f)
                return memory if isinstance(memory, list) else []
        except:
            pass
    return []

def _save_memory(memory):
    os.makedirs(os.path.dirname(MEMORY_FILE), exist_ok=True)
    with open(MEMORY_FILE, 'w', encoding='utf-8') as f:
        json.dump(memory[-100:], f, ensure_ascii=False, indent=2)

def _log(msg: str) -> None:
    print(f"[{datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')}] {msg}", flush=True)

def generate_dynamic_topics(custom_topic: str) -> list:
    _log(f"🧠 커스텀 주제 '{custom_topic}'에 대한 6단계 딥서치 기획 중...")
    prompt = f"""당신은 비즈니스 전략가 현빈입니다. 
주제: "{custom_topic}"
이 주제에 대해 6시간 동안 심층 리서치할 6가지 하위 주제와 세부 리서치 프롬프트를 JSON 배열로 작성하세요.
형식: [ {{"hour": 1, "topic": "서브주제명", "prompt": "상세 리서치 요구사항"}}, ... 6개 ]
반드시 JSON 배열만 출력하세요."""
    
    try:
        raw = lm_chat(prompt, json_mode=True, max_tokens=1000)
        topics = json.loads(raw)
        if isinstance(topics, list) and len(topics) > 0:
            return topics[:6]
    except Exception as e:
        _log(f"⚠️ 동적 주제 생성 실패 ({e}), 기본 주제로 폴백합니다.")
    
    return DEFAULT_TOPICS

def run_deep_research(custom_topic=None):
    _load_env(_root)
    _log(f"🚀 현빈(비즈니스 전략가) - 6시간 딥서치 프로세스를 시작합니다. (주제: {custom_topic or '기본(ai-lab)'})")
    
    if not lm_available():
        msg = "❌ Ollama 로컬 서버를 사용할 수 없습니다."
        _log(msg)
        send_telegram_message(f"⚠️ [현빈] 6시간 딥서치 실패 — Ollama 미가동")
        return msg
        
    topics = generate_dynamic_topics(custom_topic) if custom_topic else DEFAULT_TOPICS
    results = []
    
    for i, topic_info in enumerate(topics):
        h = i + 1
        topic = topic_info.get("topic", f"주제 {h}")
        prompt = topic_info.get("prompt", "리서치 진행")
        
        _log(f"⏰ [Hour {h}/6] 연구 시작: {topic}")
        send_telegram_message(f"📊 [현빈 딥서치 {h}/6] {topic} 연구 분석 중...")
        
        research_prompt = (
            f"당신은 글로벌 비즈니스 및 UX 전문가 '현빈'입니다. 다음 주제에 대해 정교한 리포트를 작성하세요.\n\n"
            f"주제: {topic}\n요구 지침: {prompt}\n\n"
            "출력 형식: 한국어 마크다운. 핵심 데이터 분석 및 구체적 아키텍처 모델 포함."
        )
        
        try:
            analysis = lm_chat(research_prompt, max_tokens=4000, temperature=0.7)
            if not analysis:
                analysis = "분석 결과 생성 실패."
            
            results.append({
                "hour": h,
                "topic": topic,
                "analysis": analysis,
            })
            _log(f"✅ [Hour {h}/6] 연구 완료.")
        except Exception as e:
            _log(f"❌ [Hour {h}/6] 연구 오류: {e}")
            results.append({"hour": h, "topic": topic, "analysis": f"오류: {e}"})
            
        time.sleep(2)
        
    _log("📝 최종 보고서 작성 및 메모리 저장 중...")
    
    report_md = f"""# 📑 [현빈] 6시간 딥서치 비즈니스 분석 보고서
**작성일**: {datetime.datetime.now().strftime('%Y-%m-%d')}
**주제**: {custom_topic or 'ai-lab 플랫폼 딥서치'}

---
"""
    combined_insight = ""
    for r in results:
        report_md += f"## ⏰ Hour {r['hour']}. {r['topic']}\n\n{r['analysis']}\n\n---\n\n"
        combined_insight += f"{r['topic']} 요약: {r['analysis'][:100]}...\n"
        
    report_path = os.path.join(_root, "reports", "research", f"deep_research_{int(time.time())}.md")
    os.makedirs(os.path.dirname(report_path), exist_ok=True)
    with open(report_path, "w", encoding="utf-8") as f:
        f.write(report_md)
        
    # 메모리 저장
    memory = _load_memory()
    memory.append({
        "timestamp": datetime.datetime.now().isoformat(),
        "topic": f"[딥서치] {custom_topic or 'ai-lab 기본'}",
        "content": combined_insight,
        "report_file": report_path
    })
    _save_memory(memory)
    
    msg = f"🏆 [현빈] 6시간 딥서치가 완료되었습니다!\n주제: {custom_topic or 'ai-lab 플랫폼'}\n보고서 저장 위치: `{report_path}`"
    _log(msg)
    send_telegram_message(msg)
    return msg

if __name__ == "__main__":
    arg = sys.argv[1] if len(sys.argv) > 1 else None
    run_deep_research(arg)
