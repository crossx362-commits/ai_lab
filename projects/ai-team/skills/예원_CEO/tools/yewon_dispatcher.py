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

# 팀 구성 — 11명 전문 에이전트 (AI_TEAM_ROLES.md 기준)
- 루나(luna): YouTube 음악·BGM 생성(Lyria 3 Pro), Veo 3.1 비디오 렌더링, SEO 최적화, KST 19:00 예약 업로드
- 아린(arin): Instagram 트렌드 분석·비주얼 생성·캡션·Alt Text 빌드, 2단계 Graph API 포스팅
- 영숙(secretary): 텔레그램 최우선 독점 응답, 구글 캘린더 CRUD, 일일 업로드 총괄, 유튜브 추천
- 가희(inspector): 전 채널 자산 품질·정책 다중 검수, 금지어 필터링, 사전/사후 검수, 자동 보정
- 경수(cyber): 유튜브·SNS 악플 포렌식, 채널 보안 취약점 스캔, 스프레드시트 법적 아카이빙
- 코다리(developer): Vite+React+TS+Tailwind v4 기반 petnna 웹 개발, 2시간 주기 에이전트 헬스 체크
- 티모(timo): petnna UI/UX 크리틱, 7대 사용성 기준 검수, AI Slop 클리셰 제거, CSS/JS 스니펫 제공
- 케빈(kevin): Vercel/Supabase 인프라, CI/CD 배포, PWA 가용성 모니터링, 임시 자원 가비지 컬렉션
- 현빈(business): 비즈니스 리서치, CAC/LTV 분석, 수익화 파이프라인 설계, PayPal 이상거래 감지
- 로율(legal): 상속·증여 세액 시뮬레이션, petnna 개인정보·저작권 컴플라이언스 검토
- 예원(ceo): 태스크 아키텍처링·플래닝, 원점 라우팅, 에이전트 스킬 거버넌스

# 최소 동원 원칙 (AI_TEAM_ROLES.md 준수)
- 단순 조회·데이터: 에이전트 1명만
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
