#!/usr/bin/env python3
"""FleetView — 펫나 함대를 게더타운처럼 보여주는 실시간 사무실 뷰어.

`python fleet_view.py` → http://127.0.0.1:8765 (브라우저 자동 오픈).
데몬/등록 없음. 보고 싶을 때만 켜는 독립 뷰어라 함대에 영향 0.

상태 판정: 로컬 프로세스 생존(있으면) + 로그/산출물 파일 신선도.
  🟢 working  최근 10분 내 갱신 (또는 프로세스 살아있음)
  🟡 idle     최근 6시간 내 갱신
  ⚪ away      그보다 오래됨 / 산출물 없음
맥에선 함대가 Windows에서 돌아 프로세스가 안 잡히므로 파일 신선도가 주력.
"""
from __future__ import annotations

import json
import os
import re
import subprocess
import sys
import threading
import webbrowser
from datetime import datetime
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

HERE = os.path.dirname(os.path.abspath(__file__))
PORT = int(os.getenv("FLEETVIEW_PORT", "8765"))


def find_root() -> str:
    d = HERE
    for _ in range(8):
        if os.path.isdir(os.path.join(d, "output")) and os.path.isfile(os.path.join(d, "CLAUDE.md")):
            return d
        d = os.path.dirname(d)
    return os.path.abspath(os.path.join(HERE, "..", "..", "..", "..", ".."))


ROOT = find_root()
LOGS = os.path.join(ROOT, "output", "bot_logs")
QA = os.path.join(ROOT, "output", "qa", "petnna")

# 에이전트 정의: key, 표시명, 역할, 그룹, 아바타 색(hair/shirt), 아이콘, 로그 후보, 프로세스 스크립트
AGENTS = [
    {
        "key": "yewon", "name": "예원", "role": "CEO · 오케스트레이션/워치독", "group": "lead",
        "hair": "#5b3a1a", "shirt": "#7c4dff", "icon": "leader",
        "proc": "harness_monitor.py",
        "logs": ["com.ailab.yewon_monitor.out.log", "yewon_harness_monitor.out.log",
                 "yewon_selfheal.out.log", "sched_petnna_pr_review.out.log",
                 "sched_yewon_daily_feedback.out.log"],
        "extra": [os.path.join(QA, "council")],
    },
    {
        "key": "youngsuk", "name": "영숙", "role": "비서 · 텔레그램/일정", "group": "lead",
        "hair": "#2b2b2b", "shirt": "#ff5fa2", "icon": "chat",
        "proc": "telegram_receiver.py",
        "logs": ["youngsuk_scheduler.out.log"],
        "extra": [os.path.join(ROOT, "projects", "ai-team", "skills", "영숙_비서", "tools", "telegram_receiver.log")],
    },
    {
        "key": "bomi", "name": "봄이", "role": "QA · 펫나 상시 순찰", "group": "qa",
        "hair": "#c9721f", "shirt": "#26c281", "icon": "search",
        "proc": "petnna_qa_patrol.py",
        "logs": ["bomi_qa_patrol.out.log"],
        "extra": [os.path.join(QA, "qa_state.json")],
    },
    {
        "key": "teo", "name": "테오", "role": "테스트 · E2E 자동화", "group": "qa",
        "hair": "#3a2f2f", "shirt": "#3aa0ff", "icon": "test",
        "proc": "petnna_test_engineer.py",
        "logs": ["teo_test_engineer.out.log"],
        "extra": [os.path.join(QA, "tests", "results.json")],
    },
    {
        "key": "baekho", "name": "백호", "role": "백엔드 · Supabase 계약 감사", "group": "qa",
        "hair": "#1c1c1c", "shirt": "#00b3a4", "icon": "db",
        "proc": "petnna_backend_guard.py",
        "logs": ["baekho_backend_guard.out.log"],
        "extra": [os.path.join(QA, "backend", "state.json")],
    },
    {
        "key": "suri", "name": "수리", "role": "개발 · 펫나 자동 개선 엔진", "group": "dev",
        "hair": "#4a3020", "shirt": "#ff8c42", "icon": "code",
        "proc": "petnna_dev_engine.py",
        "logs": ["suri_dev_engine.out.log"],
        "extra": [os.path.join(QA, "dev", "dev_state.json")],
    },
    {
        "key": "mio", "name": "미오", "role": "디자인 · UX 리뷰(주간)", "group": "studio",
        "hair": "#7a3b8f", "shirt": "#e05fd8", "icon": "design",
        "proc": "petnna_design_review.py",
        "logs": ["mio_design_review.out.log"],
        "extra": [os.path.join(QA, "design")],
    },
    {
        "key": "namu", "name": "나무", "role": "기획 PM · 트렌드/경쟁 조사(주간)", "group": "studio",
        "hair": "#2f5d32", "shirt": "#6bbf59", "icon": "plan",
        "proc": "petnna_product_manager.py",
        "logs": ["namu_product_manager.out.log"],
        "extra": [os.path.join(QA, "product")],
    },
]

# 로그 노이즈(작업으로 안 칠 줄)
_NOISE = ("[telegram] sent", "lock acquired", "데몬 시작", "폐기되었습니다",
          "스케줄 반영", "목록 확인", "==", "폴백")
_TS = re.compile(r"^\[(\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2})[^\]]*\]\s*")


def _clean_line(raw: str) -> tuple[str | None, str]:
    """로그 한 줄 → (파싱된 타임스탬프 or '', 작업 텍스트). 노이즈면 텍스트 ''."""
    line = raw.rstrip("\n")
    ts = ""
    m = _TS.match(line)
    if m:
        ts = m.group(1).replace("T", " ")
        line = line[m.end():]
    line = line.strip()
    low = line.lower()
    if not line or line.startswith("---") or any(n in low for n in _NOISE):
        return (ts, "")
    return (ts, line)


def _recent_lines(path: str, limit: int = 12) -> list[dict]:
    try:
        with open(path, "r", encoding="utf-8", errors="replace") as f:
            tail = f.readlines()[-200:]
    except Exception:
        return []
    out: list[dict] = []
    for raw in tail:
        ts, text = _clean_line(raw)
        if text:
            out.append({"time": ts, "text": text[:180]})
    return out[-limit:]


def _files_for(agent: dict) -> list[str]:
    paths = [os.path.join(LOGS, b) for b in agent["logs"]]
    paths += agent.get("extra", [])
    found: list[str] = []
    for p in paths:
        if os.path.isfile(p):
            found.append(p)
        elif os.path.isdir(p):
            try:
                for name in os.listdir(p):
                    fp = os.path.join(p, name)
                    if os.path.isfile(fp):
                        found.append(fp)
            except Exception:
                pass
    return found


def _proc_alive(script: str) -> bool:
    """로컬에 해당 데몬 프로세스가 살아있는지(맥/윈 공통, 실패 시 False)."""
    try:
        if sys.platform == "win32":
            r = subprocess.run(["tasklist", "/v", "/fo", "csv"], capture_output=True, text=True, timeout=4)
        else:
            r = subprocess.run(["ps", "-axo", "command="], capture_output=True, text=True, timeout=4)
        return script.lower() in r.stdout.lower()
    except Exception:
        return False


def build_state() -> dict:
    now = datetime.now()
    agents_out = []
    feed: list[dict] = []
    for a in AGENTS:
        files = _files_for(a)
        mtime = max((os.path.getmtime(f) for f in files), default=0.0)
        age = (now.timestamp() - mtime) if mtime else 1e12
        alive = _proc_alive(a["proc"])

        # "프로세스 생존 ≠ 일하는 중"(CLAUDE.md 교훈): 데몬이 떠 있어도 최근 산출물이
        # 없으면 '대기'. '일하는중'은 실제로 최근 갱신됐을 때만.
        if age < 600:
            status = "working"
        elif alive or age < 6 * 3600:
            status = "idle"
        else:
            status = "away"

        # 현재 작업: 가장 최근 갱신된 로그의 마지막 의미있는 줄
        recent: list[dict] = []
        newest_log, newest_m = None, 0.0
        for p in files:
            if p.endswith(".log"):
                m = os.path.getmtime(p)
                if m > newest_m:
                    newest_m, newest_log = m, p
        if newest_log:
            recent = _recent_lines(newest_log)
        task = recent[-1]["text"] if recent else "대기 중"

        last_active = ""
        if mtime:
            dt = now.timestamp() - mtime
            if dt < 60:
                last_active = "방금"
            elif dt < 3600:
                last_active = f"{int(dt // 60)}분 전"
            elif dt < 86400:
                last_active = f"{int(dt // 3600)}시간 전"
            else:
                last_active = f"{int(dt // 86400)}일 전"

        agents_out.append({
            "key": a["key"], "name": a["name"], "role": a["role"], "group": a["group"],
            "hair": a["hair"], "shirt": a["shirt"], "icon": a["icon"],
            "status": status, "alive": alive, "task": task,
            "lastActive": last_active, "recent": recent[-10:],
        })
        for r in recent[-3:]:
            if r["time"]:
                feed.append({"agent": a["name"], "time": r["time"], "text": r["text"]})

    feed.sort(key=lambda x: x["time"], reverse=True)
    return {
        "now": now.strftime("%Y-%m-%d %H:%M:%S"),
        "agents": agents_out,
        "feed": feed[:14],
    }


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *args):  # 조용히
        pass

    def do_GET(self):
        if self.path.startswith("/api/state"):
            body = json.dumps(build_state(), ensure_ascii=False).encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Cache-Control", "no-store")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)
            return
        if self.path in ("/", "/index.html"):
            try:
                with open(os.path.join(HERE, "fleet_view.html"), "rb") as f:
                    body = f.read()
            except Exception as e:
                body = f"fleet_view.html 없음: {e}".encode("utf-8")
                self.send_response(500)
            else:
                self.send_response(200)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)
            return
        self.send_response(204)
        self.end_headers()


def _lan_ip() -> str:
    """이 기기의 LAN IP(같은 네트워크의 다른 기기가 접속할 주소)."""
    import socket
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    try:
        s.connect(("8.8.8.8", 80))  # UDP: 실제 패킷은 안 나감, 라우팅 소스 IP만 확인
        return s.getsockname()[0]
    except Exception:
        return "127.0.0.1"
    finally:
        s.close()


def main():
    # 공유: 기본 0.0.0.0 바인딩 → 같은 네트워크의 다른 기기(윈도우 운영 기계·폰)에서 접속 가능.
    # 이 기기에서만 보려면 FLEETVIEW_HOST=127.0.0.1 로 실행.
    host = os.getenv("FLEETVIEW_HOST", "0.0.0.0")
    srv = ThreadingHTTPServer((host, PORT), Handler)
    lan = _lan_ip()
    print(f"🏢 FleetView 실행 중  (ROOT={ROOT}, bind={host}:{PORT})")
    print(f"   • 이 기기:      http://127.0.0.1:{PORT}/")
    if host != "127.0.0.1":
        print(f"   • 다른 기기에서: http://{lan}:{PORT}/   ← 폰·윈도우에서 이 주소로 접속")
    print("   Ctrl+C 로 종료")
    threading.Timer(0.6, lambda: webbrowser.open(f"http://127.0.0.1:{PORT}/")).start()
    try:
        srv.serve_forever()
    except KeyboardInterrupt:
        print("\n종료")
        srv.shutdown()


if __name__ == "__main__":
    main()
