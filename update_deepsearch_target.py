import os

fpath = r"d:\ai_lab\.agent\skills\현빈\tools\deep_search_6h.py"
with open(fpath, "r", encoding="utf-8") as f:
    content = f.read()

# Replace petnna/펫과나 references with ai-lab
content = content.replace("기본(펫과나)", "기본(ai-lab)")
content = content.replace("펫과나 기본", "ai-lab 기본")
content = content.replace("펫과나 플랫폼 딥서치", "ai-lab 플랫폼 딥서치")
content = content.replace("펫과나 플랫폼", "ai-lab 플랫폼")

# Update default topics to be ai-lab generic
new_defaults = """DEFAULT_TOPICS = [
    {"hour": 1, "topic": "멀티 에이전트 협업 시스템 아키텍처", "prompt": "다중 AI 에이전트가 각자의 역할을 수행하며 텔레그램을 통해 상호작용하는 협업 시스템 아키텍처 설계 (상세 생략)"},
    {"hour": 2, "topic": "AI SaaS 구독 모델 LTV 극대화 전략", "prompt": "B2B/B2C 대상 AI SaaS의 월 구독 모델 단위 경제학 수립 및 이탈률 방어 전략 (상세 생략)"},
    {"hour": 3, "topic": "개인화 비서 봇의 데이터 프라이버시 유지 방안", "prompt": "클라이언트 로컬 캐싱 및 안전한 API 호출을 통한 사용자 데이터 보호 해징 아키텍처 (상세 생략)"},
    {"hour": 4, "topic": "로컬 LLM (Ollama) 서버 비용 효율성 최적화", "prompt": "로컬 언어 모델의 추론 성능(Tokens/s)과 유지보수 비용을 고려한 인프라 최적화 방안 (상세 생략)"},
    {"hour": 5, "topic": "API 에코시스템을 통한 수익 창출 방안", "prompt": "외부 파트너 및 개발자가 사용할 수 있는 유료 API 엔드포인트 설계 및 수수료 정산 프로토콜 (상세 생략)"},
    {"hour": 6, "topic": "10x 가치 혁신: AI 능동형(Proactive) 추천 엔진", "prompt": "사용자 질문을 기다리지 않고 선제적으로 비즈니스 인사이트와 콘텐츠를 추천하는 AI 엔진 기획 (상세 생략)"}
]"""

# Regex or simple replace to swap DEFAULT_TOPICS block
import re
content = re.sub(r"DEFAULT_TOPICS = \[.*?\]", new_defaults, content, flags=re.DOTALL)

with open(fpath, "w", encoding="utf-8") as f:
    f.write(content)

print("Updated deep_search_6h.py to reflect ai-lab context.")
