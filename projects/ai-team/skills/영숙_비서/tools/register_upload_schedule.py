"""
register_upload_schedule.py — 업로드 자동화 일정을 Google Calendar에 등록.
루나의 client_secret.json 으로 OAuth 인증 후 반복 일정 생성.
"""
import os
import sys
import pickle
import datetime

try:
    from google_auth_oauthlib.flow import InstalledAppFlow
    from google.auth.transport.requests import Request
    from googleapiclient.discovery import build
except ImportError:
    print("❌ pip install google-api-python-client google-auth-oauthlib")
    sys.exit(1)

HERE           = os.path.dirname(os.path.abspath(__file__))
CLIENT_SECRET  = os.path.abspath(os.path.join(HERE, "..", "루나_사운드디렉터", "client_secret.json"))
TOKEN_FILE     = os.path.join(HERE, "token_calendar.pickle")
SCOPES         = ["https://www.googleapis.com/auth/calendar.events"]

UPLOAD_SCHEDULE = [
    {"agent": "경수",  "hour": 1,  "summary": "🔍 경수 — 악플 자동 수사",          "color": "11"},  # tomato
    {"agent": "루나",  "hour": 3,  "summary": "🎵 루나 — 음악 채널 자동 업로드",    "color": "2"},   # sage
    {"agent": "아린",  "hour": 3,  "summary": "📸 아린 — 인스타그램 자동 업로드",   "color": "6"},   # tangerine
]


def authenticate():
    creds = None
    if os.path.exists(TOKEN_FILE):
        with open(TOKEN_FILE, "rb") as f:
            creds = pickle.load(f)
    if not creds or not creds.valid:
        if creds and creds.expired and creds.refresh_token:
            creds.refresh(Request())
        else:
            flow = InstalledAppFlow.from_client_secrets_file(CLIENT_SECRET, SCOPES)
            creds = flow.run_local_server(port=0)
        with open(TOKEN_FILE, "wb") as f:
            pickle.dump(creds, f)
    return build("calendar", "v3", credentials=creds)


def create_recurring_event(service, summary: str, hour: int, color_id: str):
    """매일 반복 일정 생성 (오늘부터 시작)."""
    kst  = datetime.timezone(datetime.timedelta(hours=9))
    now  = datetime.datetime.now(kst)
    # 오늘 해당 시각 (이미 지났으면 내일부터)
    start = now.replace(hour=hour, minute=0, second=0, microsecond=0)
    if start <= now:
        start += datetime.timedelta(days=1)
    end   = start + datetime.timedelta(minutes=30)

    event = {
        "summary": summary,
        "colorId": color_id,
        "start": {"dateTime": start.isoformat(), "timeZone": "Asia/Seoul"},
        "end":   {"dateTime": end.isoformat(),   "timeZone": "Asia/Seoul"},
        "recurrence": ["RRULE:FREQ=DAILY"],
        "reminders": {"useDefault": False, "overrides": []},
    }
    result = service.events().insert(calendarId="primary", body=event).execute()
    print(f"  ✅ 등록: {summary} → {start.strftime('%Y-%m-%d %H:%M')} KST (매일 반복)")
    return result.get("id")


def main():
    print("📅 영숙 — 업로드 일정 Google Calendar 등록 시작\n")

    if not os.path.exists(CLIENT_SECRET):
        print(f"❌ client_secret.json 없음: {CLIENT_SECRET}")
        sys.exit(1)

    service = authenticate()
    print("✅ Google Calendar OAuth 인증 완료\n")

    for item in UPLOAD_SCHEDULE:
        create_recurring_event(service, item["summary"], item["hour"], item["color"])

    print(f"\n🎉 총 {len(UPLOAD_SCHEDULE)}개 반복 일정 등록 완료!")
    print("   Google Calendar에서 확인하세요: https://calendar.google.com")


if __name__ == "__main__":
    main()
