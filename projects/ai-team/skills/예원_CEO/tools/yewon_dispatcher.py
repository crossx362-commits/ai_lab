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
        agent = decision.get("agent", "").lower()
        print(f"  [예원 CEO] 분배 결정: {agent}")

        def _match(agent: str, names: list, msg_keywords: list = None) -> bool:
            """agent 필드 우선, 없으면 ceo_message 키워드 폴백."""
            if any(n in agent for n in names):
                return True
            if msg_keywords:
                # agent가 다른 이름으로 매핑되지 않은 경우에만 키워드 폴백
                all_known = ["루나", "아린", "현빈", "케빈", "로율", "코다리", "가희", "경수", "영숙", "티모"]
                agent_matched = any(n in agent for n in all_known)
                if not agent_matched:
                    return any(k in ceo_message for k in msg_keywords)
            return False

        # Execute based on agent (agent 필드 최우선, 키워드는 agent 미매핑시만 폴백)
        if _match(agent, ["영숙", "노션"], ["노션 보고", "영숙"]):
            sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "영숙_비서", "tools"))
            import notion_summarizer
            return notion_summarizer.run_notion_report()

        elif _match(agent, ["현빈"], ["딥서치"]):
            sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "현빈_전략가", "tools"))
            import business_research
            return business_research.run_research()

        elif _match(agent, ["케빈"], ["vercel 클린업", "서버 정리"]):
            sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "케빈_인프라", "tools"))
            import vercel_manager
            return vercel_manager.run_vercel_cleanup()

        elif _match(agent, ["로율"], ["세무 시뮬레이션", "법률 자문"]):
            sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "로율_변호사", "tools"))
            import tax_simulator
            return tax_simulator.run_simulation(100000000)

        elif _match(agent, ["루나"], ["유튜브", "뮤직비디오", "음악 영상"]):
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

        elif _match(agent, ["아린"], ["인스타그램 포스팅", "인스타 업로드"]):
            import subprocess
            script = os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "아린_관리자", "tools", "auto_pipeline.py")
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
                    return f"✅ 아린_관리자 파이프라인 실행 완료\n\n{result.stdout[:500]}"
                else:
                    error_msg = result.stderr[:500] if result.stderr else "알 수 없는 오류"
                    return f"❌ 아린 파이프라인 실행 실패\n\n에러: {error_msg}"
            return f"❌ 아린 파이프라인 스크립트를 찾을 수 없습니다."

        elif _match(agent, ["가희"], ["콘텐츠 검수", "업로드 검수"]):
            import subprocess
            script = os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "가희_검수관", "tools", "content_inspector.py")
            if os.path.exists(script):
                result = subprocess.run(
                    [sys.executable, script, "--schedule", "manual"],
                    cwd=os.path.dirname(script),
                    capture_output=True, text=True, encoding="utf-8", errors="replace",
                )
                out = result.stdout[:500] or result.stderr[:200]
                return f"{'✅' if result.returncode == 0 else '❌'} 가희 검수 완료\n\n{out}"
            return "❌ 가희 스크립트를 찾을 수 없습니다."

        elif _match(agent, ["코다리"], ["코딩", "개발", "웹 구축"]):
            return "🛠️ 코다리: 코딩 작업은 텔레그램으로 구체적인 요청사항을 보내주세요."

        elif _match(agent, ["경수"], ["악플", "댓글 수사"]):
            import subprocess
            script = os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "경수_수사관", "tools", "comment_forensics.py")
            if os.path.exists(script):
                result = subprocess.run(
                    [sys.executable, script],
                    cwd=os.path.dirname(script),
                    capture_output=True, text=True, encoding="utf-8", errors="replace",
                )
                return f"{'✅' if result.returncode == 0 else '❌'} 경수 수사 완료\n\n{result.stdout[:500]}"
            return "❌ 경수 스크립트를 찾을 수 없습니다."

        else:
            return f"⚠️ 예원 CEO: '{decision.get('agent', '?')}' 에이전트에 대한 파이프라인이 아직 없습니다. action={decision.get('action', '')}"

    except Exception as e:
        return f"❌ 예원 CEO 분배 중 오류 발생: {e}"
