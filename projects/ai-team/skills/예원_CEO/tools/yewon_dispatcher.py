import os
import sys
import json
import importlib.util

_here = os.path.dirname(os.path.abspath(__file__))
# skills/예원_CEO/tools → skills/예원_CEO → skills → ai-team → projects → ai_lab
PROJECT_ROOT = os.path.abspath(os.path.join(_here, "..", "..", "..", "..", ".."))
sys.path.insert(0, PROJECT_ROOT)
sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team"))

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
            sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "영숙_비서", "tools"))
            import notion_summarizer
            return notion_summarizer.run_notion_report()

        elif "현빈" in agent or "딥서치" in ceo_message or "리서치" in ceo_message:
            sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "현빈_전략가", "tools"))
            import business_research
            return business_research.run_research()

        elif "케빈" in agent or "클린업" in ceo_message or "서버" in ceo_message:
            sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "케빈_인프라", "tools"))
            import vercel_manager
            return vercel_manager.run_vercel_cleanup()

        elif "로율" in agent or "세무" in ceo_message or "법률" in ceo_message:
            sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "로율_변호사", "tools"))
            import tax_simulator
            return tax_simulator.run_simulation(100000000)

        elif "루나" in agent or "유튜브" in ceo_message:
            import subprocess
            script = os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "루나_디렉터", "tools", "music_video_pipeline.py")
            if os.path.exists(script):
                result = subprocess.run(
                    [sys.executable, script],
                    cwd=os.path.dirname(script),
                    capture_output=True,
                    text=True,
                    encoding="utf-8",
                    errors="replace",
                )
                if result.returncode == 0:
                    return f"✅ 루나_디렉터 파이프라인 실행 완료"
                else:
                    error_msg = result.stderr[:500] if result.stderr else result.stdout[:500] or "알 수 없는 오류"
                    return f"❌ 루나 파이프라인 실행 실패\n\n에러: {error_msg}"
            return "❌ 루나 파이프라인 스크립트를 찾을 수 없습니다."

        elif "아린" in agent or "인스타" in ceo_message:
            import subprocess
            script = os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "아린_관리자", "tools", "auto_pipeline.py")
            print(f"  [디버그] PROJECT_ROOT: {PROJECT_ROOT}")
            print(f"  [디버그] 아린 스크립트 경로: {script}")
            print(f"  [디버그] 파일 존재 여부: {os.path.exists(script)}")

            if os.path.exists(script):
                print(f"  [예원 CEO] 아린 파이프라인 실행 중...")
                result = subprocess.run(
                    [sys.executable, script],
                    cwd=os.path.dirname(script),
                    capture_output=True,
                    text=True,
                    encoding='utf-8',
                    errors='replace'
                )

                if result.returncode == 0:
                    print(f"  [예원 CEO] 아린 파이프라인 성공")
                    return f"✅ 아린_관리자 파이프라인 실행 완료\n\n{result.stdout[:500]}"
                else:
                    print(f"  [예원 CEO] 아린 파이프라인 실패 (코드: {result.returncode})")
                    error_msg = result.stderr[:500] if result.stderr else "알 수 없는 오류"
                    return f"❌ 아린 파이프라인 실행 실패\n\n에러: {error_msg}"

            return f"❌ 아린 파이프라인 스크립트를 찾을 수 없습니다.\n경로: {script}"
            
        else:
            return f"⚠️ 예원 CEO가 작업을 분배했지만({agent}), 매핑된 자동화 파이프라인이 아직 없습니다."

    except Exception as e:
        return f"❌ 예원 CEO 분배 중 오류 발생: {e}"
