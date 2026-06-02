"""
calendar_client.py — Google Calendar 공통 클라이언트

인증: .agent/credentials/calendar_token.pickle (없으면 브라우저 OAuth)
필요 scope: https://www.googleapis.com/auth/calendar
"""
import os
import pickle
import datetime

_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, ".agent")):
        break
    _root = os.path.dirname(_root)

TOKEN_FILE  = os.path.join(_root, ".agent", "credentials", "calendar_token.pickle")
SECRET_FILE = os.path.join(_root, ".agent", "credentials", "client_secret.json")
SCOPES      = ["https://www.googleapis.com/auth/calendar"]
KST         = datetime.timezone(datetime.timedelta(hours=9))


def get_service():
    """Google Calendar API 서비스 객체 반환. 토큰 없으면 브라우저 OAuth."""
    from google.auth.transport.requests import Request
    from google_auth_oauthlib.flow import InstalledAppFlow
    from googleapiclient.discovery import build

    creds = None
    if os.path.exists(TOKEN_FILE):
        with open(TOKEN_FILE, "rb") as f:
            creds = pickle.load(f)

    if creds and creds.expired and creds.refresh_token:
        try:
            creds.refresh(Request())
        except Exception:
            creds = None

    if not creds or not creds.valid:
        if not os.path.exists(SECRET_FILE):
            print("❌ client_secret.json 없음 — .agent/credentials/ 에 추가하세요")
            return None
        flow = InstalledAppFlow.from_client_secrets_file(SECRET_FILE, SCOPES)
        creds = flow.run_local_server(port=0)
        with open(TOKEN_FILE, "wb") as f:
            pickle.dump(creds, f)
        print("✅ Google Calendar 인증 완료")

    return build("calendar", "v3", credentials=creds)


def create_event(service, title: str, start_dt: datetime.datetime,
                 duration_min: int = 30, description: str = "",
                 color_id: str = None) -> str | None:
    """이벤트 생성 후 event ID 반환."""
    end_dt = start_dt + datetime.timedelta(minutes=duration_min)
    body = {
        "summary": title,
        "description": description,
        "start": {"dateTime": start_dt.isoformat(), "timeZone": "Asia/Seoul"},
        "end":   {"dateTime": end_dt.isoformat(),   "timeZone": "Asia/Seoul"},
    }
    if color_id:
        body["colorId"] = color_id
    try:
        ev = service.events().insert(calendarId="primary", body=body).execute()
        return ev.get("id")
    except Exception as e:
        print(f"  [캘린더] 이벤트 생성 실패: {e}")
        return None


def list_events(service, days_ahead: int = 7) -> list[dict]:
    """향후 N일 이벤트 목록 반환."""
    now     = datetime.datetime.now(KST)
    time_max = (now + datetime.timedelta(days=days_ahead)).isoformat()
    try:
        result = service.events().list(
            calendarId="primary",
            timeMin=now.isoformat(),
            timeMax=time_max,
            singleEvents=True,
            orderBy="startTime",
        ).execute()
        return result.get("items", [])
    except Exception as e:
        print(f"  [캘린더] 이벤트 조회 실패: {e}")
        return []


def event_exists(service, title_keyword: str, date: datetime.date) -> bool:
    """당일 이벤트 중 title_keyword 포함 이벤트가 이미 있는지 확인 (중복 방지)."""
    start = datetime.datetime.combine(date, datetime.time.min, tzinfo=KST)
    end   = datetime.datetime.combine(date, datetime.time.max, tzinfo=KST)
    try:
        result = service.events().list(
            calendarId="primary",
            timeMin=start.isoformat(),
            timeMax=end.isoformat(),
            singleEvents=True,
        ).execute()
        for ev in result.get("items", []):
            if title_keyword.lower() in ev.get("summary", "").lower():
                return True
    except Exception:
        pass
    return False
