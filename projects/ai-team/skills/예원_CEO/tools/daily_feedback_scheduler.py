"""
daily_feedback_scheduler.py — 예원 CEO: 피드백 평가 도구

기능:
  - Instagram·YouTube 콘텐츠 성과 평가
  - 성공/실패 기준에 따라 보상(Reward) / 패널티(Punishment) 기록
  - 텔레그램으로 일일/주간 보고서 전송
  - 주간 트렌드 분석 및 개선 제안

⚠️ 스케줄 관리: 영숙 비서의 schedule_manager.py에서 중앙 관리
  - 매일 오전 9시: 영숙 → 예원 일일 평가
  - 매주 월요일 9시: 영숙 → 예원 주간 리포트
"""
import os
import sys
import json
import datetime
from typing import Dict, List

# 프로젝트 루트 경로 설정
_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, "_shared")):
        break
    _root = os.path.dirname(_root)
if _root not in sys.path:
    sys.path.insert(0, _root)

from _shared.env import load_env
from _shared.notify import send
from _shared.llm import ollama as lm_chat, is_available as lm_available

load_env()

# evaluate_feedback.py import
sys.path.insert(0, os.path.dirname(__file__))
from evaluate_feedback import auto_evaluate_performance, REWARD_DIR, PUNISH_DIR


def _read_recent_evaluations(days: int = 1) -> Dict[str, List[dict]]:
    """최근 N일간 평가 결과 읽기."""
    cutoff = datetime.datetime.now() - datetime.timedelta(days=days)

    rewards = []
    punishments = []

    # 보상 로그
    reward_log = os.path.join(REWARD_DIR, "success_log.jsonl")
    if os.path.exists(reward_log):
        with open(reward_log, "r", encoding="utf-8") as f:
            for line in f:
                try:
                    record = json.loads(line.strip())
                    record_date = datetime.datetime.strptime(
                        record.get("feedback_date", "2000-01-01 00:00"),
                        "%Y-%m-%d %H:%M"
                    )
                    if record_date >= cutoff:
                        rewards.append(record)
                except Exception:
                    continue

    # 패널티 로그
    punish_log = os.path.join(PUNISH_DIR, "fail_log.jsonl")
    if os.path.exists(punish_log):
        with open(punish_log, "r", encoding="utf-8") as f:
            for line in f:
                try:
                    record = json.loads(line.strip())
                    record_date = datetime.datetime.strptime(
                        record.get("feedback_date", "2000-01-01 00:00"),
                        "%Y-%m-%d %H:%M"
                    )
                    if record_date >= cutoff:
                        punishments.append(record)
                except Exception:
                    continue

    return {"rewards": rewards, "punishments": punishments}


def _generate_daily_insights(rewards: List[dict], punishments: List[dict]) -> str:
    """Ollama로 일일 인사이트 생성."""
    if not lm_available():
        return "💡 인사이트: Ollama 미연결로 자동 분석 불가"

    # 데이터 요약
    total = len(rewards) + len(punishments)
    success_rate = (len(rewards) / total * 100) if total > 0 else 0

    # 성공 패턴 추출
    success_titles = [r["title"][:40] for r in rewards[:5]]
    fail_titles = [p["title"][:40] for p in punishments[:5]]

    # Ollama 분석
    prompt = f"""
다음은 오늘의 콘텐츠 성과입니다:

**전체**: {total}개 | **성공률**: {success_rate:.0f}%

**성공한 콘텐츠** (조회수 1만+ 또는 Instagram):
{chr(10).join(f"- {t}" for t in success_titles) if success_titles else "없음"}

**실패한 콘텐츠** (조회수 1만 미만):
{chr(10).join(f"- {t}" for t in fail_titles) if fail_titles else "없음"}

**분석 요청**:
1. 성공한 콘텐츠의 공통 패턴 (제목 스타일, 키워드 등)
2. 실패한 콘텐츠의 개선 방향
3. 내일 콘텐츠 제작 시 주의사항 (1~2가지)

짧고 구체적으로 3~5줄 이내로 답변해줘.
"""

    result = lm_chat(prompt, task="", max_tokens=300, temperature=0.7)
    return result.strip() if result else "💡 인사이트 생성 실패"


def generate_daily_report() -> str:
    """일일 피드백 보고서 생성."""
    data = _read_recent_evaluations(days=1)
    rewards = data["rewards"]
    punishments = data["punishments"]

    total = len(rewards) + len(punishments)
    if total == 0:
        return "📊 **금일 평가 대상 없음**\n\n아직 평가할 콘텐츠가 없습니다."

    success_rate = (len(rewards) / total * 100) if total > 0 else 0

    # 보고서 생성
    report = f"""📊 **예원 CEO — 일일 콘텐츠 성과 보고**
날짜: {datetime.date.today().isoformat()}

━━━━━━━━━━━━━━━━━━━━

📈 **전체 성과**
• 총 평가: {total}개
• ✅ 성공: {len(rewards)}개 ({success_rate:.0f}%)
• ❌ 실패: {len(punishments)}개 ({100 - success_rate:.0f}%)

━━━━━━━━━━━━━━━━━━━━
"""

    if punishments:
        report += f"\n⚠️ **개선 필요**\n• 조회수 미달: {len(punishments)}개\n"
        for item in punishments[:2]:
            report += f"  - {item['title'][:30]}... ({item['views']:,} 조회수)\n"

    # Ollama 인사이트
    insights = _generate_daily_insights(rewards, punishments)
    report += f"\n━━━━━━━━━━━━━━━━━━━━\n\n{insights}"

    return report


def generate_weekly_report() -> str:
    """주간 피드백 보고서 생성 (매주 월요일)."""
    data = _read_recent_evaluations(days=7)
    rewards = data["rewards"]
    punishments = data["punishments"]

    total = len(rewards) + len(punishments)
    if total == 0:
        return "📊 **주간 리포트**: 평가 대상 없음"

    # 통계 계산
    youtube_rewards = [r for r in rewards if r.get("platform") == "youtube"]
    instagram_rewards = [r for r in rewards if r.get("platform") == "instagram"]

    total_views = sum(r.get("views", 0) for r in youtube_rewards)
    avg_views = total_views / len(youtube_rewards) if youtube_rewards else 0

    # 요일별 성과
    weekday_performance = {i: {"success": 0, "fail": 0} for i in range(7)}
    for r in rewards:
        try:
            dt = datetime.datetime.strptime(r["feedback_date"], "%Y-%m-%d %H:%M")
            weekday_performance[dt.weekday()]["success"] += 1
        except Exception:
            pass

    for p in punishments:
        try:
            dt = datetime.datetime.strptime(p["feedback_date"], "%Y-%m-%d %H:%M")
            weekday_performance[dt.weekday()]["fail"] += 1
        except Exception:
            pass

    # 최적 요일 찾기
    best_day = max(
        weekday_performance.items(),
        key=lambda x: x[1]["success"] / (x[1]["success"] + x[1]["fail"] + 0.01)
    )[0]
    weekday_names = ["월", "화", "수", "목", "금", "토", "일"]

    report = f"""📊 **예원 CEO — 주간 콘텐츠 리포트**
기간: {(datetime.date.today() - datetime.timedelta(days=7)).isoformat()} ~ {datetime.date.today().isoformat()}

━━━━━━━━━━━━━━━━━━━━

📈 **주간 성과**
• 총 콘텐츠: {total}개
• YouTube: {len(youtube_rewards)}개
• Instagram: {len(instagram_rewards)}개
• 성공률: {len(rewards) / total * 100:.0f}%

📺 **YouTube 분석**
• 총 조회수: {total_views:,}
• 평균 조회수: {avg_views:,.0f}
• 상위 콘텐츠: {max((r.get("views", 0) for r in youtube_rewards), default=0):,} 조회

📅 **최적 업로드 요일**
• {weekday_names[best_day]}요일 (성공률: {weekday_performance[best_day]["success"] / (weekday_performance[best_day]["success"] + weekday_performance[best_day]["fail"] + 0.01) * 100:.0f}%)

━━━━━━━━━━━━━━━━━━━━

💡 **다음 주 권장 사항**
1. {weekday_names[best_day]}요일 집중 업로드
2. 평균 조회수 {avg_views * 1.2:,.0f} 목표 설정
3. 성공 패턴 분석 후 재활용
"""

    return report


def run_daily_evaluation():
    """매일 자동 평가 실행."""
    print("=" * 60)
    print("  [예원 CEO] 일일 콘텐츠 성과 평가 시작")
    print("=" * 60)

    # 1. 성과 평가 실행
    auto_evaluate_performance()

    # 2. 일일 보고서 생성
    report = generate_daily_report()

    # 3. 텔레그램 전송
    sent = send(report)

    print("\n✅ 일일 평가 완료 — 텔레그램 보고서 전송됨" if sent
          else "\n⚠️ 일일 평가 완료 — 텔레그램 전송 실패(토큰/네트워크 확인)")


def run_weekly_evaluation():
    """주간 평가 실행 (매주 월요일)."""
    print("=" * 60)
    print("  [예원 CEO] 주간 콘텐츠 리포트 생성")
    print("=" * 60)

    # 주간 보고서 생성
    report = generate_weekly_report()

    # 텔레그램 전송
    sent = send(report)

    print("\n✅ 주간 리포트 완료 — 텔레그램 전송됨" if sent
          else "\n⚠️ 주간 리포트 완료 — 텔레그램 전송 실패(토큰/네트워크 확인)")


# ⚠️ DEPRECATED: 독립 스케줄러 제거됨
# 스케줄은 영숙 비서의 schedule_manager.py에서 중앙 관리
# 이 함수는 더 이상 사용되지 않음
def schedule_loop():
    """
    [DEPRECATED] 독립 스케줄 루프 - 더 이상 사용하지 않음

    스케줄은 영숙 비서가 중앙 관리:
    - skills/영숙_비서/tools/schedule_manager.py
    - skills/영숙_비서/tools/schedules.json

    영숙이 스케줄 시간에 CEO에게 보고 후 이 스크립트 호출
    """
    print("⚠️  이 스케줄러는 더 이상 사용되지 않습니다.")
    print("    영숙 비서의 schedule_manager.py를 사용하세요.")
    print()
    print("실행 방법:")
    print("  --daily   일일 평가 즉시 실행")
    print("  --weekly  주간 리포트 즉시 실행")
    return


if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(description="예원 CEO 피드백 평가 도구 (영숙 스케줄러에서 호출)")
    parser.add_argument("--daily", action="store_true", help="일일 평가 즉시 실행")
    parser.add_argument("--weekly", action="store_true", help="주간 리포트 즉시 실행")

    args = parser.parse_args()

    if args.daily:
        run_daily_evaluation()
    elif args.weekly:
        run_weekly_evaluation()
    else:
        # 기본: 일일 평가
        run_daily_evaluation()
