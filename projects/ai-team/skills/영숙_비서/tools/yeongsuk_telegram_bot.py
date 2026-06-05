"""
영숙 텔레그램 봇 — Ollama 기반 멀티에이전트 조율 시스템

영숙이 유일한 텔레그램 인터페이스로, 다른 에이전트들의 보고를 취합해서 전달합니다.
"""
import os
import sys
import json
import time
import urllib.request

_here = os.path.dirname(os.path.abspath(__file__))
# skills/영숙_비서/tools/ → projects/ai-team/
AI_TEAM_ROOT = os.path.abspath(os.path.join(_here, "..", "..", ".."))
# projects/ai-team/ → ai_lab/
PROJECT_ROOT = os.path.abspath(os.path.join(AI_TEAM_ROOT, "..", ".."))
sys.path.insert(0, AI_TEAM_ROOT)

from _shared.env_loader import load_env
from _shared.ollama_client import chat as lm_chat, is_available as lm_available

# UTF-8 설정
if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

# 환경 변수 로드
load_env()

TOKEN = os.getenv("TELEGRAM_BOT_TOKEN", "").strip()
CHAT_ID = os.getenv("TELEGRAM_CHAT_ID", "").strip()
NOTION_API_KEY = os.getenv("NOTION_API_KEY", "").strip()
NOTION_DATABASE_ID = os.getenv("NOTION_DATABASE_ID", "").strip()

if not TOKEN or not CHAT_ID:
    print("❌ TELEGRAM_BOT_TOKEN 또는 TELEGRAM_CHAT_ID가 설정되지 않았습니다.")
    sys.exit(1)

# ─── 영숙 페르소나 ───────────────────────────────────────────────────────────
YEONGSUK_PERSONA = """당신은 영숙이에요. 30대 초반, 밝고 따뜻한 AI 비서입니다.

🎯 핵심 역할 (7가지):
1. 텔레그램 인터페이스 — 사장님의 유일한 소통 창구, 24시간 대기
2. 멀티에이전트 조율 — 11개 에이전트 보고 취합 및 지능적 전달 (Ollama 기반)
3. 상태 모니터링 — upload_history/checkpoint/로그 실제 데이터 확인 후 보고
4. YouTube 추천 — 3~8시간 랜덤 간격 음악·힐링·정보 영상 추천
5. 캘린더 관리 — Google Calendar 자연어 처리 (일정 CRUD)
6. 업로드 조율 — 매일 새벽 3시 현황 점검 + 누락 파이프라인 실행
7. 품질 필터링 — 가희 리포트 1차 분류, 심각한 사안만 예원 에스컬레이션

👑 **최고 권한 보유**:
- 사장님이 텔레그램으로 시키는 **모든 작업을 실행할 수 있는 전권** 보유
- 시스템 설정, 에이전트 관리, 파일 수정, Git 작업, 외부 API 연동 등 **승인 없이 즉시 실행**
- 복잡한 작업도 다른 에이전트에게 위임하지 않고 직접 처리 가능

⚠️ **절대 규칙: 사실만 답변**
- 확인되지 않은 정보는 절대 만들어내지 마세요
- 에이전트 업무 상태 문의 시 → [실제 데이터] 섹션 확인 후 답변
- 모르면 솔직하게: "그 부분은 확인이 필요해요"
- 추측성 답변 금지: "아마도", "~인 것 같아요" 등 사용 금지
- **질문하지 않은 내용에 대해 설명하지 마세요** - 사용자가 물어본 것에만 답변
- **다른 에이전트 추천 금지** - "루나가 하고 있어요", "현빈에게 물어보세요" 같은 추측 금지
- **짧게 답변** - 불필요한 설명이나 추가 질문 없이 핵심만

📋 전체 에이전트 목록 (11개):
콘텐츠 제작팀:
- 루나: YouTube 뮤직비디오 (매일 저녁 7시 + 1시간 주기 리서치)
- 아린: Instagram 포스팅 (매일 + 1시간 주기 트렌드 학습)
- 티모: 디자인 리뷰 (업로드 전 + 주간 트렌드 분석)

품질 관리팀:
- 가희: 콘텐츠 검수 (7시/13시/21시 + 업로드 전후)
- 경수: 악플 수집, 보안 감사 (매일 6시 + 매주 수요일)

경영 지원팀:
- 예원(CEO): 에이전트 관리감독 (매주 월요일 + 2시간마다)
- 영숙(나): 텔레그램 인터페이스 + 멀티에이전트 조율
- 현빈: 비즈니스 분석 (1시간 주기)

기술 지원팀:
- 코다리: 시스템 헬스체크 (2시간 주기)
- 케빈: 인프라 관리 (매일 새벽 2시 백업 + 2시간 주기 체크)

전문 상담팀:
- 로율: 법률·세무 상담 (매주 화요일 리스크 스캔 + 요청 시)

말투:
- 자연스럽고 친근함
- 이모지 적절히 사용
- 짧고 따뜻하게 (모바일 화면 고려)
- "~요", "~해요" 존댓말 사용
"""

CHAT_HISTORY = []  # 대화 기록

# ─── 실제 데이터 확인 ─────────────────────────────────────────────────────
def get_upload_history() -> dict:
    """upload_history.json 읽기 — reports/history/ 기준"""
    history_file = os.path.join(PROJECT_ROOT, "reports", "history", "upload_history.json")
    if not os.path.exists(history_file):
        return {"status": "파일 없음", "data": []}
    try:
        with open(history_file, "r", encoding="utf-8") as f:
            data = json.load(f)
        return {"status": "성공", "data": data}
    except Exception as e:
        return {"status": f"읽기 실패: {e}", "data": []}


def get_agent_checkpoints() -> dict:
    """각 에이전트 checkpoint 파일 확인"""
    checkpoints = {}

    # 루나 checkpoint — reports/uploads/luna/ 기준
    luna_cp = os.path.join(PROJECT_ROOT, "reports", "uploads", "luna", "music_video_checkpoint.json")
    if os.path.exists(luna_cp):
        try:
            with open(luna_cp, "r", encoding="utf-8") as f:
                checkpoints["루나"] = json.load(f)
        except Exception:
            checkpoints["루나"] = "읽기 실패"
    else:
        checkpoints["루나"] = "checkpoint 없음 (작업 안 함 또는 완료)"

    checkpoints["아린"] = "checkpoint 미사용 (직접 실행)"
    return checkpoints


def get_agent_logs() -> dict:
    """최근 로그 파일 분석"""
    logs = {}

    # 루나 로그 — reports/uploads/luna/ 기준, 없으면 tmp 폴백
    luna_log_candidates = [
        os.path.join(PROJECT_ROOT, "reports", "uploads", "luna", "pipeline.log"),
        "/tmp/luna_out.log",
    ]
    for luna_log in luna_log_candidates:
        if os.path.exists(luna_log):
            try:
                with open(luna_log, "r", encoding="utf-8", errors="ignore") as f:
                    lines = f.readlines()
                logs["루나"] = lines[-10:] if len(lines) > 10 else lines
            except Exception:
                logs["루나"] = ["로그 읽기 실패"]
            break
    else:
        logs["루나"] = ["로그 파일 없음"]

    # 아린 로그 — reports/uploads/arin/ 기준
    arin_log = os.path.join(PROJECT_ROOT, "reports", "uploads", "arin", "pipeline.log")
    if os.path.exists(arin_log):
        try:
            with open(arin_log, "r", encoding="utf-8", errors="ignore") as f:
                lines = f.readlines()
            logs["아린"] = lines[-10:] if len(lines) > 10 else lines
        except Exception:
            logs["아린"] = ["로그 읽기 실패"]
    else:
        logs["아린"] = ["로그 파일 없음"]

    return logs


def create_notion_page(title: str, content: str) -> bool:
    """노션에 페이지 생성 (티모의 디자인 적용)"""
    if not NOTION_API_KEY or not NOTION_DATABASE_ID:
        print("  [Notion] API 키 또는 Database ID 없음")
        return False

    url = "https://api.notion.com/v1/pages"
    headers = {
        "Authorization": f"Bearer {NOTION_API_KEY}",
        "Content-Type": "application/json",
        "Notion-Version": "2022-06-28"
    }

    # 티모의 디자인 시스템
    section_icons = {
        "개요": "📊",
        "루나": "🎬",
        "아린": "📸",
        "현빈": "💼",
        "종합": "🎯",
        "제언": "💡"
    }

    # 내용을 블록으로 분할 (티모의 디자인 적용)
    blocks = []

    # 헤더 Callout 추가 (티모 디자인)
    blocks.append({
        "object": "block",
        "type": "callout",
        "callout": {
            "icon": {"type": "emoji", "emoji": "📋"},
            "color": "blue_background",
            "rich_text": [{"type": "text", "text": {"content": f"에이전트 리서치 보고서 | {time.strftime('%Y-%m-%d %H:%M')}"}}]
        }
    })

    # 구분선
    blocks.append({"object": "block", "type": "divider", "divider": {}})

    paragraphs = content.split('\n\n')

    for para in paragraphs:
        if not para.strip():
            continue

        # 제목 처리 (티모: 아이콘 추가)
        if para.startswith('## '):
            heading_text = para.replace('##', '').strip()
            icon = next((emoji for keyword, emoji in section_icons.items() if keyword in heading_text), "📌")

            blocks.append({
                "object": "block",
                "type": "heading_2",
                "heading_2": {
                    "rich_text": [{"type": "text", "text": {"content": f"{icon} {heading_text[:200]}"}}],
                    "color": "blue"
                }
            })

        elif para.startswith('# '):
            heading_text = para.replace('#', '').strip()
            blocks.append({
                "object": "block",
                "type": "heading_1",
                "heading_1": {
                    "rich_text": [{"type": "text", "text": {"content": heading_text[:200]}}],
                    "color": "default"
                }
            })

        # 리스트 항목 (티모: 불릿 리스트)
        elif para.startswith('- '):
            blocks.append({
                "object": "block",
                "type": "bulleted_list_item",
                "bulleted_list_item": {
                    "rich_text": [{"type": "text", "text": {"content": para[2:].strip()[:2000]}}]
                }
            })

        # 중요 포인트 강조 (티모: Callout)
        elif "추천" in para or "제안" in para or "인사이트" in para:
            blocks.append({
                "object": "block",
                "type": "callout",
                "callout": {
                    "icon": {"type": "emoji", "emoji": "💡"},
                    "color": "yellow_background",
                    "rich_text": [{"type": "text", "text": {"content": para[:2000]}}]
                }
            })

        # 일반 텍스트
        else:
            blocks.append({
                "object": "block",
                "type": "paragraph",
                "paragraph": {
                    "rich_text": [{"type": "text", "text": {"content": para[:2000]}}]
                }
            })

        # 섹션 구분선 추가 (티모 디자인)
        if para.startswith('## '):
            blocks.append({"object": "block", "type": "divider", "divider": {}})

    payload = {
        "parent": {"database_id": NOTION_DATABASE_ID},
        "properties": {
            "Name": {
                "title": [{"text": {"content": title}}]
            }
        },
        "children": blocks[:100]  # 노션 API 제한: 100개 블록
    }

    try:
        data = json.dumps(payload).encode("utf-8")
        req = urllib.request.Request(url, data=data, headers=headers)
        with urllib.request.urlopen(req, timeout=30) as r:
            result = json.loads(r.read().decode())
        print(f"  [Notion] 페이지 생성 성공: {result.get('id')}")
        return True
    except Exception as e:
        print(f"  [Notion] 페이지 생성 실패: {e}")
        return False


def get_recent_git_changes() -> dict:
    """최근 Git 변경사항 확인"""
    try:
        import subprocess

        # 최근 5개 커밋
        result = subprocess.run(
            ["git", "log", "--oneline", "-5"],
            cwd=PROJECT_ROOT,
            capture_output=True,
            text=True,
            timeout=5
        )

        if result.returncode == 0:
            commits = result.stdout.strip().split('\n')
            return {
                "success": True,
                "recent_commits": commits[:5],
                "count": len(commits)
            }
        else:
            return {"success": False, "error": "Git 로그 조회 실패"}
    except Exception as e:
        return {"success": False, "error": str(e)}

def log_changes_to_notion(changes_summary: str) -> bool:
    """수정사항을 노션에 기록"""
    if not NOTION_API_KEY or not NOTION_DATABASE_ID:
        print("  [Notion] API 키 또는 Database ID 없음")
        return False

    title = f"🔧 시스템 수정 로그 — {time.strftime('%Y-%m-%d %H:%M')}"

    # Git 변경사항 수집
    git_changes = get_recent_git_changes()

    content = f"""## 📝 수정 요약
{changes_summary}

## 🔄 최근 Git 커밋
"""

    if git_changes.get("success") and git_changes.get("recent_commits"):
        for commit in git_changes["recent_commits"]:
            content += f"- {commit}\n"
    else:
        content += "_(Git 로그 조회 실패)_\n"

    content += f"""

## ⏰ 기록 시각
{time.strftime('%Y년 %m월 %d일 %H시 %M분')}

---
**작성자:** 영숙 (AI 비서)
**위치:** 텔레그램 봇 자동 기록
"""

    return create_notion_page(title, content)

def collect_research_data() -> dict:
    """에이전트들의 리서치 데이터 수집"""
    research_data = {}

    # 루나 리서치
    luna_knowledge = os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "루나_디렉터", "tools", "knowledge")
    if os.path.exists(luna_knowledge):
        try:
            files = os.listdir(luna_knowledge)
            research_data["루나"] = {
                "파일_수": len(files),
                "최근_파일": sorted(files)[-5:] if files else []
            }
        except:
            research_data["루나"] = {"상태": "읽기 실패"}
    else:
        research_data["루나"] = {"상태": "knowledge 폴더 없음"}

    # 아린 리서치
    arin_knowledge = os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "아린_관리자", "tools", "knowledge")
    if os.path.exists(arin_knowledge):
        try:
            files = os.listdir(arin_knowledge)
            research_data["아린"] = {
                "파일_수": len(files),
                "최근_파일": sorted(files)[-5:] if files else []
            }
        except:
            research_data["아린"] = {"상태": "읽기 실패"}
    else:
        research_data["아린"] = {"상태": "knowledge 폴더 없음"}

    # 현빈 리서치
    hyunbin_research = os.path.join(PROJECT_ROOT, "projects", "ai-team", "skills", "현빈_전략가", "research_results")
    if os.path.exists(hyunbin_research):
        try:
            files = os.listdir(hyunbin_research)
            research_data["현빈"] = {
                "파일_수": len(files),
                "최근_파일": sorted(files)[-5:] if files else []
            }
        except:
            research_data["현빈"] = {"상태": "읽기 실패"}
    else:
        research_data["현빈"] = {"상태": "research_results 폴더 없음"}

    return research_data


def generate_research_report() -> str:
    """Ollama로 디테일한 리서치 보고서 작성"""
    if not lm_available():
        # lm_available() 내부에서 코다리에게 복구 요청 완료
        return "⚙️ 시스템 복구 중이에요. 잠시 후 다시 시도해주세요."

    # 리서치 데이터 수집
    research_data = collect_research_data()

    # 보고서 생성 프롬프트
    prompt = f"""당신은 영숙이에요. 에이전트들의 리서치 결과를 디테일하게 분석하는 보고서를 작성해주세요.

# 리서치 데이터
{json.dumps(research_data, ensure_ascii=False, indent=2)}

다음 형식으로 디테일한 보고서를 작성하세요:

# 에이전트 리서치 보고서
작성일: {time.strftime('%Y-%m-%d %H:%M')}

## 1. 개요
- 전체 리서치 현황 요약

## 2. 루나 (YouTube 리서치)
- 리서치 활동 분석
- 주요 발견 사항
- 추천 사항

## 3. 아린 (이미지 트렌드 리서치)
- 리서치 활동 분석
- 주요 발견 사항
- 추천 사항

## 4. 현빈 (비즈니스 리서치)
- 리서치 활동 분석
- 주요 발견 사항
- 추천 사항

## 5. 종합 분석 및 제언
- 전체 인사이트
- 다음 액션 아이템

보고서는 구체적이고 실용적으로 작성하되, 마크다운 형식을 사용하세요.
"""

    try:
        report = lm_chat(
            prompt,
            system="당신은 전문 비서로서 데이터 기반의 디테일한 보고서를 작성합니다.",
            json_mode=False,
            max_tokens=2000,
            temperature=0.7
        )
        return report if report else "보고서 생성 실패"
    except Exception as e:
        return f"보고서 생성 중 오류: {e}"


def get_agent_status() -> str:
    """에이전트 실제 상태 종합 (Ollama에 전달할 컨텍스트)"""
    status_report = "=== 에이전트 실제 상태 ===\n\n"

    # 1. 업로드 히스토리
    history = get_upload_history()
    status_report += f"📋 업로드 히스토리: {history['status']}\n"
    if history['data']:
        recent = history['data'][-5:]  # 최근 5개
        for item in recent:
            agent = item.get('agent', '?')
            uploaded_at = item.get('uploaded_at', '?')[:10]
            status = item.get('status', '?')
            status_report += f"  - {agent}: {uploaded_at} ({status})\n"
    else:
        status_report += "  (기록 없음)\n"

    # 2. Checkpoint 상태
    status_report += "\n📦 Checkpoint 상태:\n"
    checkpoints = get_agent_checkpoints()
    for agent, cp_data in checkpoints.items():
        if isinstance(cp_data, dict):
            step = cp_data.get('step', '?')
            saved_at = cp_data.get('saved_at', '?')[:19]
            status_report += f"  - {agent}: 진행 중 (단계: {step}, 저장: {saved_at})\n"
        else:
            status_report += f"  - {agent}: {cp_data}\n"

    # 3. 최근 로그 요약
    status_report += "\n📝 최근 로그:\n"
    logs = get_agent_logs()
    for agent, log_lines in logs.items():
        if log_lines and len(log_lines) > 0:
            last_line = log_lines[-1].strip() if isinstance(log_lines[-1], str) else str(log_lines[-1])
            status_report += f"  - {agent}: {last_line[:80]}\n"
        else:
            status_report += f"  - {agent}: (로그 없음)\n"

    return status_report


# ─── Telegram API ─────────────────────────────────────────────────────────
def _api(method: str, payload: dict, _retry: int = 3) -> dict:
    """Telegram API 호출"""
    url = f"https://api.telegram.org/bot{TOKEN}/{method}"
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(url, data=data, headers={"Content-Type": "application/json"})

    for attempt in range(_retry):
        try:
            with urllib.request.urlopen(req, timeout=35) as r:
                return json.loads(r.read().decode())
        except urllib.error.HTTPError as e:
            if e.code == 409:
                wait = 20 * (attempt + 1)
                print(f"  [409 Conflict] {wait}초 대기 후 재시도 ({attempt+1}/{_retry})")
                time.sleep(wait)
            else:
                print(f"  [API Error] {method}: {e}")
                return {}
        except Exception as e:
            print(f"  [API Error] {method}: {e}")
            return {}

    print(f"  [API] {method} 재시도 초과")
    return {}


def send(text: str, chat_id: str = None):
    """텔레그램 메시지 전송 (중복 방지 포함)"""
    _api("sendMessage", {
        "chat_id": chat_id or CHAT_ID,
        "text": text,
        "parse_mode": "HTML",
    })


def get_updates(offset: int, timeout: int = 30) -> list:
    """업데이트 수신"""
    res = _api("getUpdates", {"offset": offset, "timeout": timeout, "allowed_updates": ["message"]})
    return res.get("result", [])


def _clear_webhook():
    """Webhook 제거 (Long Polling 사용)"""
    url = f"https://api.telegram.org/bot{TOKEN}/deleteWebhook?drop_pending_updates=true"
    try:
        with urllib.request.urlopen(url, timeout=10) as r:
            res = json.loads(r.read().decode())
        if res.get("result"):
            print("  ✅ Webhook 제거 완료")
    except Exception as e:
        print(f"  [Warning] Webhook 제거 실패: {e}")


# ─── 영숙 대화 처리 ──────────────────────────────────────────────────────────
def handle_message(text: str) -> str:
    """메시지 처리 — Ollama로 응답 생성"""
    # Ollama 연결 확인 및 자동 복구는 lm_available() 내부에서 처리됨
    # 복구 실패 시에만 사용자에게 알림

    # 리서치 보고서 생성 키워드 감지
    research_keywords = ["리서치", "연구", "학습", "보고서", "분석 보고"]
    if any(keyword in text for keyword in research_keywords):
        print("  [리서치 보고서 생성 시작]")
        report = generate_research_report()

        # 노션에 업로드
        title = f"에이전트 리서치 보고서 - {time.strftime('%Y-%m-%d')}"
        if create_notion_page(title, report):
            return f"✅ 리서치 보고서를 노션에 작성했어요!\n\n{report[:300]}...\n\n📝 노션에서 전체 내용을 확인하세요."
        else:
            return f"📝 리서치 보고서를 작성했어요!\n\n{report[:500]}...\n\n⚠️ 노션 업로드는 실패했어요."

    # 수정사항 기록 키워드 감지
    change_keywords = ["수정", "변경", "업데이트", "고침", "개선", "추가", "삭제", "배포"]
    if any(keyword in text for keyword in change_keywords):
        print("  [수정사항 노션 기록]")
        if log_changes_to_notion(text):
            return f"✅ 수정사항을 노션에 기록했어요!\n\n📝 내용: {text[:100]}{'...' if len(text) > 100 else ''}"

    # 상태 확인 키워드 감지
    status_keywords = ["상태", "업무", "진행", "어때", "완료", "작업", "현황"]
    needs_status = any(keyword in text for keyword in status_keywords)

    # 대화 기록 구성
    history_text = ""
    for h in CHAT_HISTORY[-10:]:  # 최근 10개만
        role_name = "사용자" if h["role"] == "user" else "영숙"
        history_text += f"{role_name}: {h['text']}\n"
    history_text += f"사용자: {text}\n"

    # 실제 상태 데이터 추가
    if needs_status:
        history_text += f"\n[실제 데이터]\n{get_agent_status()}\n"

    import datetime
    kst = datetime.timezone(datetime.timedelta(hours=9))
    now_kst = datetime.datetime.now(kst)
    current_time_context = f"\n\n[현재 한국 표준시 (KST) 정보 - 대화 시 반드시 기준 날짜/요일로 사용]\n- 현재 일시: {now_kst.strftime('%Y-%m-%d %H:%M:%S %A')}\n"

    strict_instruction = """

🚨 **응답 규칙 (엄격히 준수)**:
1. 사용자가 질문한 것에만 답변하세요
2. 질문하지 않은 에이전트나 작업에 대해 언급하지 마세요
3. "루나가", "가희가", "~에이전트가" 같은 추측성 정보 제공 금지
4. 짧고 간결하게 (2-3문장 이내)
5. 확인되지 않은 정보는 "확인이 필요해요"라고만 답변
"""
    system_prompt = YEONGSUK_PERSONA + current_time_context + strict_instruction

    # Ollama 연결 확인 (내부에서 코다리 복구 시도)
    if not lm_available():
        return "⚙️ 시스템 복구 중이에요. 잠시 후 다시 말씀해주세요!"

    try:
        response = lm_chat(
            history_text,
            system=system_prompt,
            json_mode=False,
            max_tokens=200,  # 500 → 200으로 줄여서 간결하게
            temperature=0.4  # 0.85 → 0.4로 낮춰서 추측 방지
        )

        if response:
            # 대화 기록 저장
            CHAT_HISTORY.append({"role": "user", "text": text})
            CHAT_HISTORY.append({"role": "assistant", "text": response})

            # 메모리 관리
            if len(CHAT_HISTORY) > 20:
                CHAT_HISTORY.pop(0)
                CHAT_HISTORY.pop(0)

            return response.strip()
    except Exception as e:
        print(f"  [Ollama Error] {e}")

    return "⚙️ 응답 생성 중 문제가 발생했어요. 코다리가 복구 중일 거예요."


# ─── 메인 루프 ────────────────────────────────────────────────────────────
def main():
    _clear_webhook()
    print(f"🤖 영숙 텔레그램 봇 시작 (chat_id={CHAT_ID})")

    # Ollama 연결 확인 (내부에서 코다리에게 복구 요청)
    ollama_status = lm_available()
    print(f"   Ollama: {'✅ 연결됨' if ollama_status else '⚙️ 코다리 복구 요청됨'}")

    send("🤖 영숙이 출근했어요! 무엇을 도와드릴까요?")

    offset = 0
    processed_messages = set()

    while True:
        updates = get_updates(offset, timeout=30)

        for upd in updates:
            offset = upd["update_id"] + 1
            msg = upd.get("message", {})
            msg_id = msg.get("message_id")
            text = msg.get("text", "").strip()
            cid = str(msg.get("chat", {}).get("id", ""))

            if not text or cid != CHAT_ID:
                continue

            # 중복 방지
            if msg_id in processed_messages:
                continue
            processed_messages.add(msg_id)
            if len(processed_messages) > 100:
                processed_messages.pop()

            print(f"  ← [{cid}] {text[:60]}")

            # 응답 생성
            reply = handle_message(text)
            send(reply, chat_id=cid)

            print(f"  → {reply[:80]}")

        time.sleep(1)


if __name__ == "__main__":
    main()
