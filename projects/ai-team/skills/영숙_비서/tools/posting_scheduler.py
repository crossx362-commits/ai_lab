"""
posting_scheduler.py — 영숙: 에이전트 포스팅 일정 Google Calendar 자동 등록

기능:
1) 일일 포스팅 일정 Google Calendar 자동 등록 (기본 실행)
2) 에이전트별 매일 반복 업로드 일정 신규 생성 (--register-recurring)
"""
import os
import sys
import datetime
import json
import pickle

_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, ".agent")):
        break
    _root = os.path.dirname(_root)
sys.path.insert(0, _root)

from _shared.calendar_client import get_service, create_event, event_exists
from _shared.telegram_notifier import send_telegram_message
from _shared.env_loader import load_env as _load_env

KST = datetime.timezone(datetime.timedelta(hours=9))

# 에이전트별 포스팅 일정 설정
# 루나·아린: 자동 실행 비활성화 (사장님 명령 시에만 수동 실행)
POSTING_SCHEDULE = []

# 매일 반복 일정 리스트 (register-recurring 용)
UPLOAD_SCHEDULE_RECURRING = [
    {"agent": "경수",  "hour": 1,  "summary": "🔍 경수 — 악플 자동 수사",          "color": "11"},  # tomato
]

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
    except Exception:
        pass
    return results

def run_schedule(target_date: datetime.date = None) -> int:
    """오늘(또는 target_date)의 포스팅 일정을 Google Calendar에 등록."""
    _load_env()
    service = get_service()
    if not service:
        print("❌ Google Calendar 인증 실패")
        return 0

    date = target_date or datetime.date.today()
    today_results = _get_recent_results()
    created = []

    for sched in POSTING_SCHEDULE:
        if event_exists(service, sched["keyword"], date):
            print(f"  ✅ [{sched['agent']}] 이미 등록됨 — 건너뜀")
            continue

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

def register_recurring_schedule():
    """매일 반복 일정 일괄 생성 (OAuth 인증 필요)"""
    _load_env()
    service = get_service()
    if not service:
        print("❌ Google Calendar OAuth 인증 실패")
        return

    print("📅 영숙 — 매일 반복 업로드 일정 Google Calendar 등록 시작\n")
    for item in UPLOAD_SCHEDULE_RECURRING:
        now  = datetime.datetime.now(KST)
        start = now.replace(hour=item["hour"], minute=0, second=0, microsecond=0)
        if start <= now:
            start += datetime.timedelta(days=1)
        end   = start + datetime.timedelta(minutes=30)

        event = {
            "summary": item["summary"],
            "colorId": item["color"],
            "start": {"dateTime": start.isoformat(), "timeZone": "Asia/Seoul"},
            "end":   {"dateTime": end.isoformat(),   "timeZone": "Asia/Seoul"},
            "recurrence": ["RRULE:FREQ=DAILY"],
            "reminders": {"useDefault": False, "overrides": []},
        }
        result = service.events().insert(calendarId="primary", body=event).execute()
        print(f"  ✅ 등록: {item['summary']} → {start.strftime('%Y-%m-%d %H:%M')} KST (매일 반복)")
        
    print(f"\n🎉 총 {len(UPLOAD_SCHEDULE_RECURRING)}개 반복 일정 등록 완료!")

if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        try:
            sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        except Exception:
            pass

    if "--register-recurring" in sys.argv:
        register_recurring_schedule()
    else:
        run_schedule()
