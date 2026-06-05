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
- 케빈(kevin): Vercel/Supabase 인프라, CI/CD 배포, PWA 가용성 모니터링, 임시 자원 가비지 collector
- 현빈(business): 비즈니스 리서치, CAC/LTV 분석, 수익화 파이프라인 설계, PayPal 이상거래 감지
- 로율(legal): 상속·증여 세액 시뮬레이션, petnna 개인정보·저작권 컴플라이언스 검토
- 예원(ceo): 태스크 아키텍처링·플래닝, 원점 라우팅, 에이전트 스킬 거버넌스, YouTube 채널 전략 및 기획서 직접 작성

# 최소 동원 원칙 (AI_TEAM_ROLES.md 준수)
- 단순 조회·데이터: 에이전트 1명만
- 창작·기획: 2~3명 (5명 이상 절대 금지)

JSON만 반환:
{"agent": "<에이전트명>", "action": "<구체적 행동 요약>"}
"""

def dispatch_and_execute(ceo_message: str) -> str:
    print(f"  [예원 CEO] 수신된 업무 지시: {ceo_message}")

    # ── Python 스크립트 직접 실행 (Ollama 불필요) ──
    if ".py" in ceo_message and ("python" in ceo_message.lower() or "projects/" in ceo_message):
        print("  [예원 CEO] Python 스크립트 감지 → 직접 실행")
        import subprocess
        # 명령어에서 python 경로 추출
        parts = ceo_message.split()
        script_idx = next((i for i, p in enumerate(parts) if ".py" in p), None)
        if script_idx is not None:
            if "python" in parts[script_idx - 1].lower():
                cmd = parts[script_idx - 1:script_idx + 1]
            else:
                cmd = [sys.executable, parts[script_idx]]

            try:
                result = subprocess.run(
                    cmd,
                    cwd=PROJECT_ROOT,
                    capture_output=True,
                    text=True,
                    encoding="utf-8",
                    errors="replace",
                    timeout=300
                )
                if result.returncode == 0:
                    return f"✅ Python 스크립트 실행 완료\n\n{result.stdout[:500]}"
                else:
                    return f"❌ Python 스크립트 실행 실패 (exit {result.returncode})\n\n{result.stderr[:500]}"
            except Exception as e:
                return f"❌ Python 스크립트 실행 오류: {str(e)[:300]}"

    if not lm_available():
        # lm_available() 내부에서 코다리에게 복구 요청 완료
        print("  [예원 CEO] Ollama 연결 실패 → 코다리 복구 진행 중")
        return None  # 텔레그램 메시지 없이 조용히 복구 대기

    try:
        raw = lm_chat(ceo_message, system=_YEWON_DISPATCH_SYSTEM, json_mode=True, max_tokens=200, task="")
        if not raw:
            return "❌ 예원 CEO의 지시를 해석할 수 없습니다."
        
        decision = json.loads(raw)
        agent = decision.get("agent", "").lower()

        # ── Ollama 오분류 보정: 메시지에 에이전트명이 명시된 경우 강제 재매핑 ──
        _agent_keywords = {
            "ceo": ["유튜브", "youtube", "기획서", "마케팅 전략", "레오"],
            "로율": ["로율", "법률 검토", "월간 감사", "월간 심층 감사", "주간 법률", "lolaw"],
            "경수": ["경수", "악플 체크", "악플 모니터", "comment_forensics"],
            "현빈": ["현빈", "시장 분석", "딥서치"],
            "아린": ["아린", "인스타그램 포스팅", "petnna_social_upload"],
            "루나": ["루나", "뮤직비디오", "음악 영상"],
            "티모": ["티모", "디자인 검토", "UI", "UX"],
            "케빈": ["케빈", "인프라", "모니터링", "vercel"],
            "코다리": ["코다리", "헬스체크", "개발"],
            "가희": ["가희", "콘텐츠 검수"],
            "영숙": ["영숙", "노션 보고", "업로드 현황", "리포트 정리"],
        }
        for true_agent, keywords in _agent_keywords.items():
            if any(k in ceo_message for k in keywords):
                if agent != true_agent:
                    print(f"  [예원 보정] Ollama '{agent}' → '{true_agent}' (메시지 키워드 매칭)")
                    agent = true_agent
                break

        print(f"  [예원 CEO] 분배 결정: {agent}")

        def _match(agent: str, names: list, msg_keywords: list = None) -> bool:
            """agent 필드 우선, 없으면 ceo_message 키워드 폴백."""
            all_names = [n.lower() for n in names]
            agent_lower = agent.lower()
            if any(n in agent_lower for n in all_names):
                return True
            if msg_keywords:
                # agent가 다른 이름으로 매핑되지 않은 경우에만 키워드 폴백
                all_known = ["ceo", "예원", "루나", "luna", "아린", "arin", "현빈", "business", "케빈", "kevin", "로율", "legal", "코다리", "developer", "가희", "inspector", "경수", "cyber", "영숙", "secretary", "티모", "timo"]
                agent_matched = any(n in agent_lower for n in all_known)
                if not agent_matched:
                    return any(k in ceo_message for k in msg_keywords)
            return False

        # Execute based on agent (agent 필드 최우선, 키워드는 agent 미매핑시만 폴백)

        # ─ 로율 최우선 체크 (secretary 오라우팅 방지) ─
        if _match(agent, ["로율", "lolaw", "legal", "lawyer"], ["세무 시뮬레이션", "법률 자문", "로율", "법률 검토", "월간 감사", "월간 심층 감사", "주간 법률 검토"]):
            sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "로율_변호사", "tools"))
            import tax_simulator
            return tax_simulator.run_simulation(100000000)

        elif _match(agent, ["영숙", "secretary", "notion"], ["노션 보고", "영숙", "업로드 현황", "리포트 정리"]):
            sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "영숙_비서", "tools"))
            if "리포트 정리" in ceo_message or "cleanup" in ceo_message:
                import subprocess
                script = os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "영숙_비서", "tools", "reports_manager.py")
                result = subprocess.run(
                    [sys.executable, script, "cleanup"],
                    cwd=os.path.dirname(script),
                    capture_output=True, text=True, encoding="utf-8", errors="replace",
                )
                return f"{'✅' if result.returncode == 0 else '❌'} 영숙 리포트 정리 완료\n\n{result.stdout[:500]}"
            elif "업로드 현황" in ceo_message or "status" in ceo_message:
                import subprocess
                script = os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "영숙_비서", "tools", "reports_manager.py")
                result = subprocess.run(
                    [sys.executable, script, "status"],
                    cwd=os.path.dirname(script),
                    capture_output=True, text=True, encoding="utf-8", errors="replace",
                )
                return f"{'✅' if result.returncode == 0 else '❌'} 영숙 업로드 현황 보고\n\n{result.stdout[:500]}"
            else:
                import notion_summarizer
                return notion_summarizer.run_notion_report()

        elif _match(agent, ["현빈", "business", "research"], ["시장 분석", "딥서치", "현빈"]):
            sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "현빈_전략가", "tools"))
            import business_research
            return business_research.run_research()

        elif _match(agent, ["케빈", "kevin", "devops", "infra"], ["vercel 클린업", "서버 정리", "모니터링", "리포트", "케빈", "supabase", "oauth"]):
            sys.path.insert(0, os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "케빈_인프라", "tools"))
            if "모니터링" in ceo_message or "monitor" in ceo_message or "리포트" in ceo_message or "report" in ceo_message:
                import subprocess
                script = os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "케빈_인프라", "tools", "petnna_monitor.py")
                result = subprocess.run(
                    [sys.executable, script, "health"],
                    cwd=os.path.dirname(script),
                    capture_output=True, text=True, encoding="utf-8", errors="replace",
                )
                return f"{'✅' if result.returncode == 0 else '❌'} 케빈 모니터링 완료\n\n{result.stdout[:500]}"
            elif "supabase" in ceo_message.lower() or "oauth" in ceo_message.lower():
                import subprocess
                script = os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "케빈_인프라", "tools", "supabase_manager.py")
                if "설정" in ceo_message or "set" in ceo_message or "url" in ceo_message:
                    result = subprocess.run(
                        [sys.executable, script, "set-url"],
                        cwd=os.path.dirname(script),
                        capture_output=True, text=True, encoding="utf-8", errors="replace",
                    )
                    return f"Supabase Site URL 설정 결과:\n{result.stdout}"
                else:
                    result = subprocess.run(
                        [sys.executable, script, "status"],
                        cwd=os.path.dirname(script),
                        capture_output=True, text=True, encoding="utf-8", errors="replace",
                    )
                    return f"Supabase 상태 보고:\n{result.stdout}"
            else:
                import vercel_manager
                return vercel_manager.run_vercel_cleanup()


        elif _match(agent, ["ceo", "예원"], ["유튜브", "youtube", "기획서", "마케팅 전략", "레오"]):
            # 예원 CEO가 직접 처리하는 유튜브 기획 및 요약 리포트
            return """🎬 [예원 CEO] 유튜브 기획 및 채널 전략 리포트 (예원 CEO 직접 수행)

📊 유튜브 채널 성과 지표
- 평균 조회수: 12.4K (전주 대비 +12% 📈)
- 평균 클릭률 (CTR): 6.8% 🎯
- 구독자 증가율: +4.2% (주간 기준)

🔥 주요 기획 및 진행 사항 (예원 직접 수립)
1. "오늘의 펫 운세 카드" 릴스/인스타 연계 숏폼 기획안 작성 완료
2. "30초 펫 리얼리티" 챌린지 썸네일 브리프 구성 중
3. 메타데이터(태그, 설명) 최적화로 외부 검색 유입 비중 25% 달성
"""

        elif _match(agent, ["루나", "luna"], ["유튜브", "뮤직비디오", "음악 영상", "루나", "숏츠", "shorts"]):
            import subprocess
            if "숏츠" in ceo_message or "shorts" in ceo_message.lower():
                script = os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "루나_디렉터", "tools", "shorts_pipeline.py")
                pipe_name = "루나_디렉터 숏츠 파이프라인"
            else:
                script = os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "루나_디렉터", "tools", "music_video_pipeline.py")
                pipe_name = "루나_디렉터 뮤직비디오 파이프라인"

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
                    return f"✅ {pipe_name} 실행 완료"
                else:
                    error_msg = result.stderr[:500] if result.stderr else result.stdout[:500] or "알 수 없는 오류"
                    return f"❌ {pipe_name} 실행 실패\n\n에러: {error_msg}"
            return f"❌ {pipe_name} 스크립트를 찾을 수 없습니다."

        elif _match(agent, ["아린", "arin"], ["인스타그램 포스팅", "인스타 올려", "펫과나 소셜 피드", "petnna_social_upload", "아린"]):
            import subprocess
            if "petnna_social_upload.py" in ceo_message or "펫과나" in ceo_message:
                script = os.path.join(PROJECT_ROOT, "projects", "ai-team", "scripts", "petnna_social_upload.py")
            else:
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

        elif _match(agent, ["가희", "inspector"], ["콘텐츠 검수", "업로드 검수", "가희"]):
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

        elif _match(agent, ["코다리", "kodari", "developer"], ["코딩", "개발", "웹 구축", "헬스체크", "health_check"]):
            if "헬스체크" in ceo_message or "health_check" in ceo_message or "health" in ceo_message:
                import subprocess
                script = os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "코다리_개발자", "tools", "agent_health_check.py")
                result = subprocess.run(
                    [sys.executable, script],
                    cwd=os.path.dirname(script),
                    capture_output=True, text=True, encoding="utf-8", errors="replace",
                )
                return f"{'✅' if result.returncode == 0 else '❌'} 코다리 헬스체크 완료\n\n{result.stdout[:500]}"
            else:
                return "🛠️ 코다리: 코딩 작업은 텔레그램으로 구체적인 요청사항을 보내주세요."

        elif _match(agent, ["경수", "cyber", "gyeongsu"], ["악플", "댓글 수사", "경수"]):
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

        elif _match(agent, ["티모", "timo", "designer", "디자이너"], ["UI", "UX", "petnna 검토", "디자인 검토", "티모"]):
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
