"""
posting_scheduler.py — 영숙: 에이전트 포스팅 일정 Google Calendar 자동 등록

매일 오전 8시(KST) 실행:
  - 루나: YouTube 영상 예약 업로드 (오후 7시)
  - 아린: 인스타그램 포스팅 (오전 11시 30분)
  - 숙자: 블로그 포스팅 (오후 2시)
  이미 등록된 일정은 중복 생성하지 않음.

직접 실행:
  python posting_scheduler.py
"""
import os
import sys
import datetime
import json

_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, ".agent")):
        break
    _root = os.path.dirname(_root)
sys.path.insert(0, _root)

from _shared.calendar_client import get_service, create_event, event_exists
from _shared.telegram_notifier import send_telegram_message

KST = datetime.timezone(datetime.timedelta(hours=9))

# 에이전트별 포스팅 일정 설정
POSTING_SCHEDULE = [
    {
        "agent":       "루나",
        "title":       "📺 루나 — YouTube 업로드",
        "hour":        19,
        "minute":      0,
        "duration":    30,
        "color_id":    "11",  # 토마토
        "description": "루나 AI 뮤직비디오 YouTube 예약 업로드 (시티팝/레트로 장르)",
        "keyword":     "루나",
    },
    {
        "agent":       "아린",
        "title":       "🌸 아린 — 인스타그램 포스팅",
        "hour":        11,
        "minute":      30,
        "duration":    20,
        "color_id":    "6",   # 바나나
        "description": "아린 AI 인스타그램 이미지 자동 포스팅 (트렌드 기반)",
        "keyword":     "아린",
    },
    {
        "agent":       "숙자",
        "title":       "✍️ 숙자 — 블로그 포스팅",
        "hour":        14,
        "minute":      0,
        "duration":    30,
        "color_id":    "2",   # 세이지
        "description": "숙자 AI 블로그 포스팅 (Blogger 영어 콘텐츠)",
        "keyword":     "숙자",
    },
]


from _shared.env_loader import load_env as _load_env


def _get_recent_results() -> dict:
    """업로드 히스토리에서 오늘 실제 포스팅 내용 조회."""
    history_path = os.path.join(_root, ".agent", "memory", "upload_history.json")
    today = datetime.date.today().isoformat()
    results = {}
    if not os.path.exists(history_path):
        return results
    try:
        with open(history_path, encoding="utf-8") as _f:
            history = json.load(_f)
        for r in history:
            if not r.get("uploaded_at", "").startswith(today):
                continue
            agent = r.get("agent", "")
            meta  = r.get("metadata", {})
            if "루나" in agent:
                results["루나"] = meta.get("youtube_title", "")
            elif "아린" in agent:
                results["아린"] = meta.get("caption", "")[:30]
            elif "숙자" in agent:
                results["숙자"] = meta.get("title", "")
    except Exception:
        pass
    return results


def run_schedule(target_date: datetime.date = None) -> int:
    """
    오늘(또는 target_date)의 포스팅 일정을 Google Calendar에 등록.
    생성된 이벤트 수 반환.
    """
    _load_env()
    service = get_service()
    if not service:
        print("❌ Google Calendar 인증 실패")
        return 0

    date = target_date or datetime.date.today()
    today_results = _get_recent_results()
    created = []

    for sched in POSTING_SCHEDULE:
        # 중복 체크
        if event_exists(service, sched["keyword"], date):
            print(f"  ✅ [{sched['agent']}] 이미 등록됨 — 건너뜀")
            continue

        # 실제 포스팅 내용이 있으면 설명에 추가
        extra = today_results.get(sched["agent"], "")
        desc  = sched["description"]
        if extra:
            desc += f"\n\n📌 오늘 콘텐츠: {extra}"

        start_dt = datetime.datetime(
            date.year, date.month, date.day,
            sched["hour"], sched["minute"],
            tzinfo=KST,
        )

        ev_id = create_event(
            service,
            title=sched["title"],
            start_dt=start_dt,
            duration_min=sched["duration"],
            description=desc,
            color_id=sched["color_id"],
        )
        if ev_id:
            print(f"  📅 [{sched['agent']}] {sched['title']} — {sched['hour']:02d}:{sched['minute']:02d} 등록")
            created.append(sched["agent"])

    if created:
        agents_str = ", ".join(created)
        date_str   = date.strftime("%m월 %d일")
        msg = (
            f"📅 영숙이에요! {date_str} 포스팅 일정을 캘린더에 등록했어요 😊\n\n"
            + "\n".join(
                f"• {s['title']} — {s['hour']:02d}:{s['minute']:02d}"
                for s in POSTING_SCHEDULE
                if s["agent"] in created
            )
            + "\n\n구글 캘린더에서 확인해보세요! 📆"
        )
        send_telegram_message(msg)
        print(f"  [포스팅 스케줄러] {len(created)}개 등록 완료: {agents_str}")
    else:
        print("  [포스팅 스케줄러] 새로 등록할 일정 없음")

    return len(created)


if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        try:
            sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        except Exception:
            pass
    run_schedule()
