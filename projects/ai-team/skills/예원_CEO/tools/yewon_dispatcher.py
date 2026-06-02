import os
import sys
import json
import importlib.util

_here = os.path.dirname(os.path.abspath(__file__))
PROJECT_ROOT = os.path.abspath(os.path.join(_here, "..", "..", "..", ".."))
sys.path.insert(0, PROJECT_ROOT)
sys.path.insert(0, os.path.join(PROJECT_ROOT, "ai-team"))

from _shared.ollama_client import chat as lm_chat, is_available as lm_available

_YEWON_DISPATCH_SYSTEM = """당신은 CEO 예원입니다. 비서 영숙이로부터 사용자 명령을 전달받아 알맞은 팀원에게 배분하고 즉시 파이프라인을 실행시킵니다.

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
"""

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
