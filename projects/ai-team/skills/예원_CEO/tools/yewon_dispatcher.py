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

_YEWON_DISPATCH_SYSTEM = """당신은 CEO 예원입니다. 사장님 명령을 분석해 최적의 에이전트에게 배분합니다.

# 에이전트 역할 (SKILL.md Section 2 기준)
- 루나(luna): 유튜브 뮤직 채널 총괄, Lyria 3 Pro 완곡 생성, Veo 3.1 비디오 렌더링, SEO 최적화
- 아린(arin): 인스타그램 채널 총괄, 구글 트렌드 분석, Gemini 비주얼 생성, 인앱 SEO Alt 텍스트 빌드
- 영숙(secretary): 텔레그램 최우선 보좌, 구글 캘린더 연동, 데일리 브리핑, 일일 업로드 자동 통제
- 가희(inspector): 모든 에이전트 산출물 품질·정책 검수, 캡션 클리셰·유튜브 콘텐츠 심사
- 코다리(developer): 소스 코드 자율 제어, 시스템 자동화, API 통합, 에이전트 헬스 감시
- 현빈(business): 수익화 파이프라인, 광고 단가·KPI 분석, 10x 비즈니스 전략
- 케빈: Vercel 배포 관리 및 서버 클린업
- 로율: 민법 자문 및 세무 시뮬레이션
- 디자이너(designer): 유튜브 썸네일·브랜드 비주얼, 컬러·타이포그래피 설계
- 라이터(writer): 카피라이팅, 영상 후킹 스크립트, 블로그·SNS 카피
- 리서처(researcher): 글로벌 트렌드·경쟁사 분석, 데이터 수집·팩트 검증

# 최소 동원 원칙
- 단순 조회: 에이전트 1명만
- 창작·기획: 2~3명 (5명 이상 절대 금지)

JSON만 반환:
{"agent": "<에이전트명>", "action": "<구체적 행동 요약>"}
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

        elif _match(agent, ["티모", "timo", "designer", "디자이너"], ["UI", "UX", "petnna 검토", "디자인 검토"]):
            import subprocess
            script = os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "티모_디자이너", "tools", "petnna_reviewer.py")
            if os.path.exists(script):
                result = subprocess.run(
                    [sys.executable, script],
                    cwd=os.path.dirname(script),
                    capture_output=True, text=True, encoding="utf-8", errors="replace",
                )
                return f"{'✅' if result.returncode == 0 else '❌'} 티모 UI/UX 검토 완료\n\n{result.stdout[:500]}"
            return "❌ 티모 스크립트를 찾을 수 없습니다."

        else:
            return f"⚠️ 예원 CEO: '{decision.get('agent', '?')}' 에이전트에 대한 파이프라인이 아직 없습니다. action={decision.get('action', '')}"

    except Exception as e:
        return f"❌ 예원 CEO 분배 중 오류 발생: {e}"
