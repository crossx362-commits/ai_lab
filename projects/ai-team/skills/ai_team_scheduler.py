"""
ai_team_scheduler.py — AI 팀 자동 작업 스케줄러

Notion 통합 리서치 리포트에서 작업을 읽고,
각 에이전트에게 작업을 할당하여 실행합니다.
"""
import sys
import os
import time
import datetime

_here = os.path.dirname(os.path.abspath(__file__))
_ai_team_root = os.path.abspath(os.path.join(_here, ".."))
if _ai_team_root not in sys.path:
    sys.path.insert(0, _ai_team_root)

from _shared.env_loader import load_env
load_env()

from _shared.notion_report_manager import NotionReportManager
from _shared.telegram_notifier import send_telegram_message


# 에이전트 실행 함수 매핑
AGENT_EXECUTORS = {
    "루나": "루나_디렉터.tools.music_video_pipeline",
    "아린": "아린_관리자.tools.auto_pipeline",
    "가희": "가희_검수관.tools.content_inspector",
}


def execute_agent_task(agent_name: str, task: dict) -> tuple[bool, str]:
    """에이전트 작업 실행.

    Args:
        agent_name: 에이전트 이름
        task: 작업 정보

    Returns:
        (성공 여부, 결과 메시지)
    """
    print(f"\n{'='*60}")
    print(f"  [{agent_name}] 작업 실행: {task['title']}")
    print(f"{'='*60}")

    try:
        if agent_name == "루나":
            # 루나 뮤직비디오 파이프라인 실행
            import subprocess
            pipeline_path = os.path.join(
                _ai_team_root, "skills", "루나_디렉터", "tools", "music_video_pipeline.py"
            )

            print(f"  파이프라인 실행: {pipeline_path}")
            result = subprocess.run(
                [sys.executable, pipeline_path],
                capture_output=True,
                text=True,
                timeout=3600  # 1시간 제한
            )

            if result.returncode == 0:
                # 성공 - 업로드된 영상 URL 추출
                output = result.stdout
                video_url = None
                for line in output.split('\n'):
                    if 'youtu.be' in line or 'youtube.com' in line:
                        import re
                        match = re.search(r'https://youtu\.be/([a-zA-Z0-9_-]+)', line)
                        if match:
                            video_url = f"https://youtu.be/{match.group(1)}"
                            break

                result_msg = f"뮤직비디오 생성 완료"
                if video_url:
                    result_msg += f"\nURL: {video_url}"

                return True, result_msg
            else:
                return False, f"파이프라인 실패: {result.stderr[:500]}"

        elif agent_name == "아린":
            # 아린 인스타그램 파이프라인 실행
            import subprocess
            pipeline_path = os.path.join(
                _ai_team_root, "skills", "아린_관리자", "tools", "auto_pipeline.py"
            )

            print(f"  파이프라인 실행: {pipeline_path}")
            result = subprocess.run(
                [sys.executable, pipeline_path],
                capture_output=True,
                text=True,
                timeout=1800  # 30분 제한
            )

            if result.returncode == 0:
                return True, "인스타그램 포스팅 완료"
            else:
                return False, f"파이프라인 실패: {result.stderr[:500]}"

        elif agent_name == "가희":
            # 가희 검수 실행
            result_msg = f"검수 작업: {task['description']}\n수동 실행 필요"
            return True, result_msg

        else:
            return False, f"지원하지 않는 에이전트: {agent_name}"

    except subprocess.TimeoutExpired:
        return False, "작업 시간 초과 (timeout)"
    except Exception as e:
        return False, f"실행 중 오류: {str(e)[:500]}"


def run_scheduler(interval_minutes: int = 30, max_iterations: int = None):
    """스케줄러 실행.

    Args:
        interval_minutes: 확인 주기 (분)
        max_iterations: 최대 반복 횟수 (None이면 무한)
    """
    print("="*60)
    print("  AI 팀 자동 작업 스케줄러 시작")
    print("="*60)
    print(f"  확인 주기: {interval_minutes}분")
    print(f"  Notion 연동: {'활성화' if os.getenv('NOTION_TOKEN') else '비활성화'}")
    print("="*60)

    if not os.getenv('NOTION_TOKEN'):
        print("\n[WARNING] NOTION_TOKEN이 설정되지 않았습니다.")
        print("설정 방법은 NOTION_SETUP.md를 참고하세요.\n")
        return

    manager = NotionReportManager()
    iteration = 0

    try:
        while True:
            iteration += 1
            print(f"\n[{datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')}] 반복 {iteration}")

            if max_iterations and iteration > max_iterations:
                print(f"\n최대 반복 횟수({max_iterations}) 도달, 종료합니다.")
                break

            # 각 에이전트의 대기 작업 확인
            total_tasks = 0
            for agent_name in AGENT_EXECUTORS.keys():
                tasks = manager.get_pending_tasks(agent_name)

                if not tasks:
                    print(f"  [{agent_name}] 대기 작업 없음")
                    continue

                print(f"  [{agent_name}] 대기 작업 {len(tasks)}개 발견")
                total_tasks += len(tasks)

                # 우선순위 높은 작업부터 처리
                tasks.sort(key=lambda x: {"High": 0, "Medium": 1, "Low": 2}.get(x.get("priority", "Medium"), 1))

                for task in tasks[:1]:  # 한 번에 1개씩 처리
                    task_id = task["id"]
                    task_title = task["title"]

                    # 작업 시작 알림
                    manager.update_task_status(task_id, "In progress")
                    send_telegram_message(
                        f"🤖 [{agent_name}] 작업 시작\n"
                        f"제목: {task_title}\n"
                        f"설명: {task.get('description', 'N/A')}"
                    )

                    # 작업 실행
                    success, result = execute_agent_task(agent_name, task)

                    # 결과 기록
                    if success:
                        manager.update_task_status(task_id, "Done", result)
                        send_telegram_message(
                            f"✅ [{agent_name}] 작업 완료\n"
                            f"제목: {task_title}\n"
                            f"결과: {result[:200]}"
                        )
                        print(f"  ✅ 작업 완료: {task_title}")
                    else:
                        manager.update_task_status(task_id, "Failed", result)
                        send_telegram_message(
                            f"❌ [{agent_name}] 작업 실패\n"
                            f"제목: {task_title}\n"
                            f"오류: {result[:200]}"
                        )
                        print(f"  ❌ 작업 실패: {task_title}")

            if total_tasks == 0:
                print("  모든 에이전트의 대기 작업 없음")

            # 다음 확인까지 대기
            print(f"\n  다음 확인: {interval_minutes}분 후...")
            time.sleep(interval_minutes * 60)

    except KeyboardInterrupt:
        print("\n\n사용자가 중단했습니다.")
    except Exception as e:
        print(f"\n[ERROR] 스케줄러 오류: {e}")
        import traceback
        traceback.print_exc()


if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(description="AI 팀 자동 작업 스케줄러")
    parser.add_argument(
        "--interval",
        type=int,
        default=30,
        help="작업 확인 주기 (분, 기본값: 30)"
    )
    parser.add_argument(
        "--once",
        action="store_true",
        help="한 번만 실행하고 종료"
    )

    args = parser.parse_args()

    if args.once:
        run_scheduler(args.interval, max_iterations=1)
    else:
        run_scheduler(args.interval)
