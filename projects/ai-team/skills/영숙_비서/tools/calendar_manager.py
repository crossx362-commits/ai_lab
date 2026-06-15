#!/usr/bin/env python3
"""Google Calendar Manager — calendar_manager.py

영숙 비서가 Google Calendar를 통합 관리합니다.
1) 캘린더 읽기 (iCal): 다가오는 일정을 가져와 calendar_cache.md에 저장
2) 설정 상태 확인: OAuth 쓰기 설정 검사
"""
import os
import json
import sys
import re
import datetime
import urllib.request
import urllib.error

HERE = os.path.dirname(os.path.abspath(__file__))
CONFIG_READ = os.path.join(HERE, "google_calendar.json")
CONFIG_WRITE = os.path.join(HERE, "google_calendar_write.json")
BRAIN_ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
CACHE = os.path.join(BRAIN_ROOT, "_shared", "calendar_cache.md")

def sync_read_calendar():
    """Google Calendar에서 다가오는 일정을 읽어 캐시 파일에 저장"""
    if not os.path.exists(CONFIG_READ):
        print("❌ google_calendar.json이 없어요. 먼저 ICAL_URL을 입력해주세요.")
        return False
    try:
        with open(CONFIG_READ, "r", encoding="utf-8") as f:
            cfg = json.load(f)
    except Exception as e:
        print(f"❌ 설정 파일 파싱 실패: {e}")
        return False
        
    url = (cfg.get("ICAL_URL") or "").strip()
    days_ahead = int(cfg.get("DAYS_AHEAD", 14))
    if not url:
        print("❌ ICAL_URL이 비어있어요.")
        return False
        
    print(f"📅 Google Calendar 가져오는 중… (다가오는 {days_ahead}일)")
    try:
        req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
        with urllib.request.urlopen(req, timeout=15) as r:
            raw = r.read().decode("utf-8", errors="replace")
    except urllib.error.HTTPError as e:
        print(f"❌ HTTP {e.code} — URL이 잘못됐거나 만료됐을 수 있어요.")
        return False
    except Exception as e:
        print(f"❌ 다운로드 실패: {e}")
        return False

    raw = re.sub(r"\r?\n[ \t]", "", raw)
    events = []
    cur = None
    for line in raw.split("\n"):
        line = line.rstrip("\r")
        if line == "BEGIN:VEVENT":
            cur = {}
        elif line == "END:VEVENT":
            if cur is not None:
                events.append(cur)
            cur = None
        elif cur is not None and ":" in line:
            key, val = line.split(":", 1)
            base = key.split(";", 1)[0]
            if base in ("SUMMARY", "DESCRIPTION", "LOCATION", "DTSTART", "DTEND"):
                cur[base] = val.strip()
                if base in ("DTSTART", "DTEND") and ";VALUE=DATE" in key:
                    cur[base + "_DATE_ONLY"] = True

    def parse_dt(s):
        if not s: return None
        s = s.strip().rstrip("Z")
        try:
            if "T" in s:
                return datetime.datetime.strptime(s, "%Y%m%dT%H%M%S")
            return datetime.datetime.strptime(s, "%Y%m%d")
        except Exception:
            return None

    now = datetime.datetime.now()
    cutoff = now + datetime.timedelta(days=days_ahead)
    upcoming = []
    for ev in events:
        dt = parse_dt(ev.get("DTSTART", ""))
        if not dt:
            continue
        if dt < now - datetime.timedelta(hours=1):
            continue
        if dt > cutoff:
            continue
        upcoming.append({
            "start": dt,
            "summary": (ev.get("SUMMARY") or "(제목 없음)").replace("\\,", ",").replace("\\n", " "),
            "location": (ev.get("LOCATION") or "").replace("\\,", ",").replace("\\n", " "),
            "all_day": ev.get("DTSTART_DATE_ONLY", False),
        })
    upcoming.sort(key=lambda e: e["start"])

    if not upcoming:
        print(f"📭 다음 {days_ahead}일 안에 일정 없음.")
    else:
        print(f"✅ {len(upcoming)}개 일정 가져옴:")
        for ev in upcoming[:10]:
            ts = ev["start"].strftime("%m/%d %a") if ev["all_day"] else ev["start"].strftime("%m/%d %a %H:%M")
            loc = f" @ {ev['location']}" if ev["location"] else ""
            print(f"  • {ts} — {ev['summary']}{loc}")

    os.makedirs(os.path.dirname(CACHE), exist_ok=True)
    lines = [
        "# 📅 다가오는 일정 (Google Calendar)",
        f"_업데이트: {now.strftime('%Y-%m-%d %H:%M')} · 향후 {days_ahead}일_",
        "",
    ]
    if not upcoming:
        lines.append("_없음_")
    else:
        for ev in upcoming:
            ts = ev["start"].strftime("%Y-%m-%d (%a)") if ev["all_day"] else ev["start"].strftime("%Y-%m-%d (%a) %H:%M")
            loc = f" — 📍 {ev['location']}" if ev["location"] else ""
            lines.append(f"- **{ts}** · {ev['summary']}{loc}")
    with open(CACHE, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    print(f"\n💾 캐시 저장 완료: {CACHE}")
    return True

def show_write_status():
    """자동 일정 등록 OAuth 상태 출력"""
    if not os.path.exists(CONFIG_WRITE):
        print("⚠️ 아직 일정 등록 OAuth 설정이 없어요.")
        print("   명령 팔레트(Cmd+Shift+P) -> 'AI Team: Google Calendar 자동 일정 연결' 실행")
        return False
    try:
        with open(CONFIG_WRITE, "r", encoding="utf-8") as f:
            cfg = json.load(f)
    except Exception as e:
        print(f"❌ 설정 파일 파싱 실패: {e}")
        return False
        
    cid = (cfg.get("CLIENT_ID") or "").strip()
    cs  = (cfg.get("CLIENT_SECRET") or "").strip()
    rt  = (cfg.get("REFRESH_TOKEN") or "").strip()
    cal = (cfg.get("CALENDAR_ID") or "primary").strip()
    dur = int(cfg.get("DEFAULT_DURATION_MINUTES") or 60)
    who = (cfg.get("_CONNECTED_AS") or "").strip()
    when = (cfg.get("_CONNECTED_AT") or "").strip()
    
    print("─── Google Calendar 자동 일정 등록 상태 ───")
    print(f"  Client ID         : {'설정됨 (' + cid[:8] + '…)' if cid else '(없음)'}")
    print(f"  Client Secret     : {'설정됨' if cs else '(없음)'}")
    print(f"  Refresh Token     : {'유효 ✓' if rt else '(없음)'}")
    print(f"  Calendar ID       : {cal}")
    print(f"  기본 일정 길이     : {dur}분")
    if who:
        print(f"  연결 계정          : {who}")
    if when:
        print(f"  연결 시각          : {when[:19]}")
    if not (cid and cs and rt):
        print("\n⚠️ 셋업이 완료되지 않았습니다.")
        return False
    print("\n✅ 연결 정상. 마감일(due) 있는 작업이 등록되면 자동으로 캘린더에 일정이 생성됩니다.")
    return True

def main():
    if len(sys.argv) < 2:
        print("사용법:")
        print("  python calendar_manager.py sync     - 다가오는 일정 읽기 및 캐시")
        print("  python calendar_manager.py status   - 일정 등록(OAuth) 상태 확인")
        return

    cmd = sys.argv[1].lower()
    if cmd == "sync":
        sync_read_calendar()
    elif cmd == "status":
        show_write_status()
    else:
        print(f"❌ 알 수 없는 명령어: {cmd}")

if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        try:
            sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        except Exception:
            pass
    main()
