"""
daily_ai_team_runner.py — AI 팀 일일 자동 실행 시스템

Notion 리포트를 읽고 Ollama를 통해 에이전트들이 자율적으로 작업하며,
결과를 다시 Notion에 기록합니다.
"""
import sys
import os
import json
import datetime
import urllib.request

_here = os.path.dirname(os.path.abspath(__file__))
_ai_team_root = os.path.abspath(os.path.join(_here, ".."))
if _ai_team_root not in sys.path:
    sys.path.insert(0, _ai_team_root)

from _shared.env_loader import load_env
load_env()

from _shared.notion_report_manager import NotionReportManager
from _shared.ollama_client import chat as ollama_chat
from _shared.telegram_notifier import send_telegram_message


def read_notion_report() -> str:
    """Notion 리포트 전체 읽기."""
    token = os.getenv('NOTION_API_KEY', '').strip('"')
    db_id = os.getenv('NOTION_DATABASE_ID', '').strip('"')

    if not token or not db_id:
        return ""

    try:
        # 최근 10개 페이지 읽기
        url = f'https://api.notion.com/v1/databases/{db_id}/query'
        headers = {
            'Authorization': f'Bearer {token}',
            'Content-Type': 'application/json',
            'Notion-Version': '2022-06-28'
        }

        payload = json.dumps({'page_size': 10, 'sorts': [{'timestamp': 'created_time', 'direction': 'descending'}]}).encode('utf-8')

        req = urllib.request.Request(url, data=payload, headers=headers, method='POST')
        with urllib.request.urlopen(req, timeout=30) as response:
            data = json.loads(response.read())

        # 페이지 내용 추출
        report_content = []

        for page in data.get('results', []):
            page_id = page.get('id', '')

            # 블록 내용 읽기
            blocks_url = f'https://api.notion.com/v1/blocks/{page_id}/children'
            req2 = urllib.request.Request(blocks_url, headers=headers)

            try:
                with urllib.request.urlopen(req2, timeout=30) as resp:
                    blocks_data = json.loads(resp.read())

                # 블록 텍스트 추출
                for block in blocks_data.get('results', []):
                    block_type = block.get('type')

                    text_content = None
                    if block_type in ['paragraph', 'heading_1', 'heading_2', 'heading_3', 'bulleted_list_item', 'numbered_list_item']:
                        texts = block.get(block_type, {}).get('rich_text', [])
                        if texts:
                            text_content = ' '.join([t.get('plain_text', '') for t in texts])

                    if text_content:
                        report_content.append(text_content)

            except Exception as e:
                print(f"  [Warning] 페이지 {page_id} 블록 읽기 실패: {e}")

        return '\n'.join(report_content)

    except Exception as e:
        print(f"  [Error] Notion 리포트 읽기 실패: {e}")
        return ""


def analyze_with_ollama(report_content: str) -> dict:
    """예원 CEO 디스패처가 리포트를 읽고 자율 판단으로 작업 계획 수립."""

    # 예원 CEO 디스패처 동적 로드
    _dispatcher_path = os.path.join(_ai_team_root, "skills", "예원_CEO", "tools", "yewon_dispatcher.py")
    try:
        import importlib.util as _ilu
        _spec = _ilu.spec_from_file_location("yewon_dispatcher", _dispatcher_path)
        _mod = _ilu.module_from_spec(_spec)
        _spec.loader.exec_module(_mod)

        # 예원 CEO에게 리포트 전달 → 자율 판단
        ceo_prompt = (
            f"다음은 AI팀 Notion 활동 리포트입니다. 오늘 우선순위 높은 작업을 판단하고 "
            f"적합한 에이전트에게 배분해주세요.\n\n리포트:\n{report_content[:2000]}"
        )
        result = _mod.dispatch_and_execute(ceo_prompt)
        print(f"  [예원 CEO] 판단 완료: {result[:100]}")

        # 디스패처 결과를 태스크 형태로 변환
        tasks = []
        for agent in []:
            if agent in result:
                tasks.append({
                    "agent": agent,
                    "action": f"{agent} 일일 파이프라인 실행",
                    "priority": "high",
                    "description": result[:200],
                    "reason": "예원 CEO 자율 판단"
                })
        if tasks:
            return {"tasks": tasks, "summary": f"예원 CEO 지시: {result[:100]}"}

    except Exception as e:
        print(f"  [예원 CEO 디스패처 실패, Ollama 폴백] {e}")

    # 폴백: Ollama 직접 분석
    prompt = f"""AI팀 리포트를 분석하여 오늘 할 작업을 JSON으로 반환하세요.
리포트: {report_content[:2000]}
JSON: {{"tasks":[],"summary":"오늘 작업 없음"}}"""

    try:
        response = ollama_chat(prompt, json_mode=True, temperature=0.5, max_tokens=1000)
        if response:
            return json.loads(response)
    except Exception as e:
        print(f"  [Warning] Ollama 분석 실패: {e}")

    return {
        "tasks": [],
        "summary": "일일 정기 콘텐츠 제작 (비활성화됨)"
    }


def execute_agent_pipeline(agent_name: str, task_info: dict) -> tuple[bool, str]:
    """에이전트 파이프라인 실행."""
    import subprocess

    print(f"\n{'='*60}")
    print(f"  [{agent_name}] {task_info['action']} 실행")
    print(f"{'='*60}")

    # 루나·아린: 자동 실행 비활성화 (사장님 명령 시에만 수동 실행)
    pipeline_map = {}

    pipeline_path = pipeline_map.get(agent_name)
    if not pipeline_path:
        return False, f"지원하지 않는 에이전트: {agent_name}"

    full_path = os.path.join(_ai_team_root, pipeline_path)

    if not os.path.exists(full_path):
        return False, f"파이프라인 파일 없음: {full_path}"

    try:
        env = os.environ.copy()
        env['PYTHONIOENCODING'] = 'utf-8'
        result = subprocess.run(
            [sys.executable, full_path],
            capture_output=True,
            text=True,
            encoding='utf-8',
            errors='replace',
            timeout=3600,  # 1시간
            cwd=os.path.dirname(full_path),
            env=env
        )

        if result.returncode == 0:
            # 성공 - URL 추출
            output = result.stdout

            # YouTube URL 추출
            import re
            video_url = None
            for line in output.split('\n'):
                if 'youtu.be' in line or 'youtube.com' in line:
                    match = re.search(r'https://youtu\.be/([a-zA-Z0-9_-]+)', line)
                    if match:
                        video_url = match.group(0)
                        break

            result_msg = f"{task_info['action']} 완료"
            if video_url:
                result_msg += f"\nURL: {video_url}"

            return True, result_msg
        else:
            return False, f"파이프라인 실패 (exit {result.returncode})"

    except subprocess.TimeoutExpired:
        return False, "타임아웃 (1시간 초과)"
    except Exception as e:
        return False, f"실행 오류: {str(e)}"


def run_daily_automation():
    """일일 자동화 실행."""

    print("="*60)
    print(f"  AI 팀 일일 자동 실행")
    print(f"  {datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print("="*60)

    # 1. Notion 리포트 읽기
    print("\n[1/4] Notion 리포트 읽기...")
    report_content = read_notion_report()

    if report_content:
        print(f"  리포트 읽기 완료 ({len(report_content)} 문자)")
    else:
        print("  리포트 없음 - 기본 작업으로 진행")

    # 2. Ollama로 작업 분석
    print("\n[2/4] Ollama로 작업 분석...")
    plan = analyze_with_ollama(report_content)

    print(f"  요약: {plan.get('summary', 'N/A')}")
    print(f"  작업 {len(plan.get('tasks', []))}개 식별")

    # 텔레그램 알림
    send_telegram_message(
        f"🤖 AI 팀 일일 작업 시작\n\n"
        f"📋 {plan.get('summary', 'N/A')}\n"
        f"📌 작업: {len(plan.get('tasks', []))}개"
    )

    # 3. 작업 실행
    print("\n[3/4] 작업 실행...")

    results = []
    for task in plan.get('tasks', []):
        agent = task.get('agent', '')

        if agent in []:
            success, result_msg = execute_agent_pipeline(agent, task)

            results.append({
                "agent": agent,
                "task": task.get('action', ''),
                "success": success,
                "result": result_msg
            })

            # 개별 결과 알림
            icon = "✅" if success else "❌"
            send_telegram_message(
                f"{icon} [{agent}] {task.get('action', '')}\n"
                f"결과: {result_msg[:200]}"
            )

    # 4. Notion에 결과 기록 (전체 에이전트)
    print("\n[4/4] Notion에 결과 기록...")

    manager = NotionReportManager()
    today = datetime.datetime.now().strftime('%Y-%m-%d')

    # 루나/아린 파이프라인 결과 기록
    for result in results:
        try:
            manager.create_report_entry(
                agent_name=result['agent'],
                task_title=f"{result['task']} ({today})",
                result=result['result'],
                metadata={"priority": "Medium", "status": "완료" if result['success'] else "실패"}
            )
        except Exception as e:
            print(f"  [Notion] {result['agent']} 기록 실패: {e}")

    # 다른 에이전트 리포트 파일 → Notion 반영
    agent_report_map = {
        "현빈": "reports/research/hyunbin_research.json",
        "케빈": "reports/history/kevin_monitor_log.md",
        "경수": "reports/inspection/kyungsoo_audit_log.md",
        "코다리": "projects/ai-team/docs/progress.md",
        "영숙": "reports/history/yeongsuk_daily_brief.md",
        "티모": "reports/learning/timo_review.md",
        "데이브": "reports/research/dave_stock_analysis.md",
        "데이브(가상자산)": "reports/research/dave_upbit_analysis.md",
    }

    _root_dir = os.path.abspath(os.path.join(_ai_team_root, ".."))
    for agent_name, rel_path in agent_report_map.items():
        full = os.path.join(_root_dir, rel_path)
        if not os.path.exists(full):
            continue
        try:
            with open(full, 'r', encoding='utf-8', errors='replace') as f:
                content = f.read()
            # 최근 500자만 발췌
            snippet = content.strip()[-500:] if len(content) > 500 else content.strip()
            if not snippet:
                continue
            manager.create_report_entry(
                agent_name=agent_name,
                task_title=f"{agent_name} 일일 활동 보고 ({today})",
                result=snippet,
                metadata={"priority": "Low", "status": "자동기록"}
            )
            print(f"  [Notion] {agent_name} 보고 기록 완료")
        except Exception as e:
            print(f"  [Notion] {agent_name} 기록 실패: {e}")

    # 최종 요약
    success_count = sum(1 for r in results if r['success'])

    print("\n" + "="*60)
    print(f"  완료: {success_count}/{len(results)} 성공")
    print("="*60)

    send_telegram_message(
        f"📊 AI 팀 일일 작업 완료\n\n"
        f"✅ 성공: {success_count}\n"
        f"❌ 실패: {len(results) - success_count}\n"
        f"⏰ {datetime.datetime.now().strftime('%H:%M')}\n"
        f"📝 Notion 활동 보고 업데이트 완료"
    )


if __name__ == "__main__":
    run_daily_automation()
