#!/usr/bin/env python3
"""울온 개발 현황 보드. 파일만 읽고 쓴다. 판단·실행은 하지 않는다.

    python3 loop/board_server.py
    http://127.0.0.1:8787  (localhost 바인딩만)
"""
from __future__ import annotations

import json
import os
import re
import subprocess
import sys
import threading
from datetime import datetime
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, unquote, urlparse
from zoneinfo import ZoneInfo

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
TZ = ZoneInfo("Asia/Seoul")
LOCK = threading.Lock()

def load_env_port() -> int:
    env = HERE / "env.sh"
    port = os.environ.get("BOARD_PORT", "")
    if port.isdigit():
        return int(port)
    if env.exists():
        for line in env.read_text(encoding="utf-8").splitlines():
            s = line.strip()
            if s.startswith("BOARD_PORT="):
                v = s.split("=", 1)[1].strip().split()[0]
                if v.isdigit():
                    return int(v)
    return 8787


PORT = load_env_port()
HTML = HERE / "board.html"
INBOX = ROOT / "docs" / "feedback" / "INBOX.md"
STATUS = ROOT / "docs" / "STATUS.md"
ASSETS = ROOT / "docs" / "ASSETS.md"
BOARD = ROOT / "docs" / "board.json"
CREDITS = ROOT / "docs" / "CREDITS.md"
STATE = ROOT / "logs" / "loop_state.json"
HISTORY = ROOT / "logs" / "loop_history.jsonl"
STOP = HERE / "STOP"
LOCKFILE = HERE / ".lock"

INBOX_ITEM = re.compile(
    r"^- \[([ xX])\](?:\s*\[(긴급)\])?\s*(?:\((\d{4}-\d{2}-\d{2} \d{2}:\d{2})\)\s*)?(.*)$"
)


def now_stamp() -> str:
    return datetime.now(TZ).strftime("%Y-%m-%d %H:%M")


def read_text(path: Path) -> str:
    if not path.exists():
        return ""
    return path.read_text(encoding="utf-8")


def write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def read_json(path: Path, default):
    if not path.exists():
        return default
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return default


def git(*args: str) -> str:
    try:
        return subprocess.check_output(
            ["git", *args],
            cwd=str(ROOT),
            text=True,
            stderr=subprocess.DEVNULL,
        )
    except Exception:
        return ""


def parse_inbox(text: str) -> list[dict]:
    items = []
    for i, raw in enumerate(text.splitlines()):
        m = INBOX_ITEM.match(raw.rstrip())
        if not m:
            continue
        done = m.group(1).lower() == "x"
        urgent = m.group(2) == "긴급"
        ts = m.group(3) or ""
        body = (m.group(4) or "").strip()
        loop_n = None
        lm = re.search(r"바퀴#(\d+)", body)
        if lm:
            loop_n = int(lm.group(1))
        items.append(
            {
                "id": i,
                "done": done,
                "urgent": urgent,
                "ts": ts,
                "text": body,
                "loop": loop_n,
                "raw": raw,
            }
        )
    return items


def append_inbox(text: str, urgent: bool) -> dict:
    with LOCK:
        cur = read_text(INBOX)
        if cur and not cur.endswith("\n"):
            cur += "\n"
        stamp = now_stamp()
        flag = "[긴급] " if urgent else ""
        line = f"- [ ] {flag}({stamp}) {text.strip()}"
        if "## 항목" in cur and not cur.rstrip().endswith("## 항목"):
            cur = cur.rstrip() + "\n" + line + "\n"
        elif "## 항목" in cur:
            cur = cur.rstrip() + "\n" + line + "\n"
        else:
            cur = cur + ("\n" if cur else "") + line + "\n"
        write_text(INBOX, cur)
        # 다시 읽어 확인
        check = read_text(INBOX)
        ok = line in check
        return {"ok": ok, "line": line}


def patch_inbox(item_id: int, new_text: str) -> dict:
    with LOCK:
        lines = read_text(INBOX).splitlines()
        if item_id < 0 or item_id >= len(lines):
            return {"ok": False, "error": "없는 항목"}
        m = INBOX_ITEM.match(lines[item_id])
        if not m:
            return {"ok": False, "error": "INBOX 항목이 아님"}
        if m.group(1).lower() == "x":
            return {"ok": False, "error": "처리된 항목은 수정하지 않음"}
        urgent = m.group(2) == "긴급"
        ts = m.group(3) or now_stamp()
        flag = "[긴급] " if urgent else ""
        lines[item_id] = f"- [ ] {flag}({ts}) {new_text.strip()}"
        write_text(INBOX, "\n".join(lines) + "\n")
        return {"ok": True, "line": lines[item_id]}


def delete_inbox(item_id: int) -> dict:
    with LOCK:
        lines = read_text(INBOX).splitlines()
        if item_id < 0 or item_id >= len(lines):
            return {"ok": False, "error": "없는 항목"}
        m = INBOX_ITEM.match(lines[item_id])
        if not m:
            return {"ok": False, "error": "INBOX 항목이 아님"}
        if m.group(1).lower() == "x":
            return {"ok": False, "error": "처리된 항목은 삭제하지 않음"}
        del lines[item_id]
        write_text(INBOX, "\n".join(lines) + "\n")
        return {"ok": True}


def loop_alive(state: dict) -> bool:
    pid = state.get("pid")
    if not pid:
        # lock 파일의 pid도 본다
        if LOCKFILE.exists():
            try:
                pid = int(LOCKFILE.read_text(encoding="utf-8").strip())
            except Exception:
                return False
        else:
            return False
    try:
        os.kill(int(pid), 0)
        return True
    except Exception:
        return False


def badge(state: dict) -> str:
    if STOP.exists() and not loop_alive(state):
        return "STOP으로 정지"
    if state.get("status") == "stopped_fail":
        return "실패로 정지"
    if state.get("status") == "stopped_stop":
        return "STOP으로 정지"
    if loop_alive(state):
        st = state.get("status") or "running"
        if st == "waiting":
            n = state.get("wait_remaining_sec") or 0
            return f"대기 중(다음 바퀴까지 {n}초)"
        if st == "running":
            return "실행 중"
        return st
    if state:
        return "서비스 꺼짐"
    return "서비스 꺼짐"


def parse_status_highlights(md: str) -> dict:
    blocked = []
    table = []
    if not md:
        return {"blocked": blocked, "system_table_md": ""}
    lines = md.splitlines()
    in_blocked = False
    in_table = False
    table_lines = []
    for line in lines:
        if re.match(r"^#+ .*막힌", line):
            in_blocked = True
            in_table = False
            continue
        if in_blocked and re.match(r"^#+ ", line):
            in_blocked = False
        if in_blocked:
            s = line.strip()
            if s.startswith("- ") or s.startswith("* "):
                blocked.append(s[2:].strip())
            elif s and not s.startswith("#"):
                blocked.append(s)
        if "시스템 상태" in line and line.strip().startswith("|"):
            in_table = True
        if re.match(r"^#+ .*시스템 상태", line):
            in_table = True
            table_lines = []
            continue
        if in_table:
            if line.startswith("|"):
                table_lines.append(line)
            elif table_lines and not line.strip():
                continue
            elif table_lines and not line.startswith("|"):
                in_table = False
    return {"blocked": blocked, "system_table_md": "\n".join(table_lines)}


def parse_assets(md: str) -> list[dict]:
    rows = []
    if not md:
        return rows
    in_table = False
    headers = []
    for line in md.splitlines():
        if not line.startswith("|"):
            in_table = False
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if all(set(c) <= set("-: ") and c for c in cells):
            in_table = True
            continue
        if not in_table and any("경로" in c or "종류" in c or "라이선스" in c for c in cells):
            headers = cells
            in_table = True
            continue
        if in_table and headers:
            rec = {}
            for i, h in enumerate(headers):
                rec[h] = cells[i] if i < len(cells) else ""
            blob = " ".join(rec.values())
            rec["_used"] = any(x in blob for x in ("사용", "적용", "씬", "프리팹"))
            rec["_unused"] = "미사용" in blob
            rec["_unknown_license"] = any(
                x in blob for x in ("미확인", "출처 미확인", "라이선스 미확인")
            )
            rec["_generated"] = "생성" in blob or "GPT" in blob
            rows.append(rec)
    return rows


def recent_previews() -> list[dict]:
    out = []
    roots = [
        ROOT / "assets" / "generated",
        ROOT / "assets" / "3d" / "preview",
    ]
    for base in roots:
        if not base.exists():
            continue
        for p in sorted(base.rglob("*"), key=lambda x: x.stat().st_mtime if x.is_file() else 0, reverse=True):
            if p.suffix.lower() not in {".png", ".jpg", ".jpeg", ".webp", ".gif"}:
                continue
            rel = str(p.relative_to(ROOT))
            prompt = p.with_suffix(".prompt.txt")
            if not prompt.exists():
                prompt = p.with_name(p.stem + ".prompt.txt")
            out.append(
                {
                    "path": rel,
                    "name": p.name,
                    "mtime": datetime.fromtimestamp(p.stat().st_mtime, TZ).isoformat(),
                    "prompt": str(prompt.relative_to(ROOT)) if prompt.exists() else "",
                }
            )
            if len(out) >= 24:
                return out
    return out


def history_rows(limit: int = 50) -> list[dict]:
    if not HISTORY.exists():
        return []
    rows = []
    for line in HISTORY.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line:
            continue
        try:
            rows.append(json.loads(line))
        except Exception:
            continue
    return rows[-limit:]


def recent_commits(n: int = 30) -> list[dict]:
    fmt = "%H%x09%ad%x09%s"
    raw = git("log", f"-{n}", f"--pretty=format:{fmt}", "--date=iso-strict", "--", ".")
    out = []
    for line in raw.splitlines():
        parts = line.split("\t", 2)
        if len(parts) < 3:
            continue
        h, ts, msg = parts
        stat = git("show", "--numstat", "--format=", h)
        files = 0
        for sl in stat.splitlines():
            if sl.strip():
                files += 1
        lm = re.search(r"\[loop#(\d+)\]", msg)
        out.append(
            {
                "hash": h,
                "short": h[:8],
                "ts": ts,
                "message": msg,
                "files": files,
                "loop": int(lm.group(1)) if lm else None,
            }
        )
    return out


def warnings(state: dict, inbox: list[dict], assets: list[dict], cards: list[dict]) -> list[str]:
    w = []
    blocked = sum(1 for c in cards if c.get("status") == "막힘")
    if blocked:
        w.append(f"막힘 {blocked}건")
    lic = sum(1 for a in assets if a.get("_unknown_license"))
    if lic:
        w.append(f"라이선스 미확인 {lic}건")
    hist = history_rows(8)
    streak = 0
    for row in reversed(hist):
        if row.get("result") == "success":
            # 커밋 없는 성공도 노랑. 여기서는 커밋 여부 별도.
            msg = git("log", "-1", "--pretty=%s", "--", ".")
            # 최근 바퀴 3연속 커밋 없음: history만으로는 약함. loop 커밋 메시지로 본다.
        if row.get("result") in ("success",) and not git(
            "log", "-1", "--pretty=%s", "--grep", f"\\[loop#{row.get('loop')}\\]", "--", "."
        ):
            streak += 1
        else:
            break
    # 더 단순한 판정: 최근 3바퀴 모두 success인데 해당 바퀴 커밋이 없으면 경고
    last3 = hist[-3:] if len(hist) >= 3 else []
    if len(last3) == 3:
        no_commit = 0
        for row in last3:
            n = row.get("loop")
            found = git("log", "--pretty=%s", "--grep", f"\\[loop#{n}\\]", "--", ".")
            if not found.strip():
                no_commit += 1
        if no_commit == 3:
            w.append("커밋 없는 바퀴 3연속")
    return w


def build_state() -> dict:
    state = read_json(STATE, {})
    board = read_json(BOARD, {"cards": []})
    cards = board.get("cards") if isinstance(board, dict) else []
    inbox_text = read_text(INBOX)
    inbox = parse_inbox(inbox_text)
    status_md = read_text(STATUS)
    assets_md = read_text(ASSETS)
    assets = parse_assets(assets_md)
    hl = parse_status_highlights(status_md)
    hist = history_rows(50)
    commits = recent_commits(30)
    today = state.get("today") or {"loops": 0, "success": 0, "fail": 0, "commits": 0}
    return {
        "badge": badge(state),
        "alive": loop_alive(state),
        "stop": STOP.exists(),
        "loop": state,
        "today": today,
        "warnings": warnings(state, inbox, assets, cards),
        "inbox": inbox,
        "board": board,
        "cards": cards,
        "status_md": status_md,
        "status_highlights": hl,
        "assets_md": assets_md,
        "assets": assets,
        "previews": recent_previews(),
        "history": hist,
        "commits": commits,
        "credits_md": read_text(CREDITS),
    }


def log_path_for(loop_no: int) -> Path:
    return ROOT / "logs" / f"loop_{int(loop_no):04d}.log"


def safe_under(root: Path, rel: str) -> Path | None:
    rel = rel.lstrip("/")
    if ".." in Path(rel).parts:
        return None
    p = (root / rel).resolve()
    try:
        p.relative_to(root.resolve())
    except ValueError:
        return None
    return p if p.exists() else None


class Handler(BaseHTTPRequestHandler):
    server_version = "UlonBoard/1"

    def log_message(self, fmt: str, *args) -> None:
        sys.stderr.write("%s - %s\n" % (self.address_string(), fmt % args))

    def _json(self, code: int, obj) -> None:
        body = json.dumps(obj, ensure_ascii=False, indent=2).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Cache-Control", "no-store")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def _bytes(self, code: int, data: bytes, ctype: str) -> None:
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Cache-Control", "no-store")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def _read_body(self) -> dict:
        n = int(self.headers.get("Content-Length") or 0)
        raw = self.rfile.read(n) if n else b""
        if not raw:
            return {}
        try:
            return json.loads(raw.decode("utf-8"))
        except Exception:
            return {}

    def do_GET(self) -> None:  # noqa: N802
        u = urlparse(self.path)
        path = unquote(u.path)
        if path in ("/", "/index.html", "/board.html"):
            if not HTML.exists():
                self._bytes(404, b"board.html missing", "text/plain")
                return
            self._bytes(200, HTML.read_bytes(), "text/html; charset=utf-8")
            return
        if path == "/api/state":
            self._json(200, build_state())
            return
        if path.startswith("/api/log/"):
            rest = path[len("/api/log/") :]
            try:
                n = int(rest)
            except ValueError:
                self._json(400, {"ok": False, "error": "바퀴 번호"})
                return
            p = log_path_for(n)
            self._json(
                200,
                {
                    "ok": p.exists(),
                    "loop": n,
                    "path": str(p.relative_to(ROOT)) if p.exists() else "",
                    "text": read_text(p)[-200000:],
                },
            )
            return
        if path.startswith("/api/commit/"):
            h = path[len("/api/commit/") :]
            if not re.fullmatch(r"[0-9a-fA-F]{4,40}", h):
                self._json(400, {"ok": False, "error": "해시"})
                return
            stat = git("show", "--stat", "--format=fuller", h)
            patch = git("show", "--format=", "--", h)
            self._json(200, {"ok": bool(stat), "hash": h, "stat": stat, "diff": patch[:120000]})
            return
        if path == "/api/file":
            q = parse_qs(u.query)
            rel = (q.get("path") or [""])[0]
            p = safe_under(ROOT, rel)
            if p is None or not p.is_file():
                self._json(404, {"ok": False})
                return
            allowed = p.suffix.lower() in {".png", ".jpg", ".jpeg", ".webp", ".gif", ".txt", ".md", ".json"}
            if not allowed:
                self._json(403, {"ok": False})
                return
            ctype = {
                ".png": "image/png",
                ".jpg": "image/jpeg",
                ".jpeg": "image/jpeg",
                ".webp": "image/webp",
                ".gif": "image/gif",
                ".txt": "text/plain; charset=utf-8",
                ".md": "text/plain; charset=utf-8",
                ".json": "application/json; charset=utf-8",
            }[p.suffix.lower()]
            self._bytes(200, p.read_bytes(), ctype)
            return
        self._json(404, {"ok": False, "error": "not found"})

    def do_POST(self) -> None:  # noqa: N802
        u = urlparse(self.path)
        path = unquote(u.path)
        body = self._read_body()
        if path == "/api/inbox":
            text = str(body.get("text") or "").strip()
            if not text:
                self._json(400, {"ok": False, "error": "내용이 비어 있음"})
                return
            urgent = str(body.get("priority") or "") in ("긴급", "urgent", "high")
            result = append_inbox(text, urgent)
            self._json(200 if result["ok"] else 500, result)
            return
        if path == "/api/stop":
            STOP.write_text("stop\n", encoding="utf-8")
            self._json(200, {"ok": True, "message": "현재 바퀴를 마친 뒤 멈춥니다."})
            return
        self._json(404, {"ok": False})

    def do_PATCH(self) -> None:  # noqa: N802
        u = urlparse(self.path)
        path = unquote(u.path)
        body = self._read_body()
        if path == "/api/inbox":
            try:
                item_id = int(body.get("id"))
            except Exception:
                self._json(400, {"ok": False, "error": "id"})
                return
            text = str(body.get("text") or "").strip()
            if not text:
                self._json(400, {"ok": False, "error": "내용이 비어 있음"})
                return
            result = patch_inbox(item_id, text)
            self._json(200 if result.get("ok") else 400, result)
            return
        self._json(404, {"ok": False})

    def do_DELETE(self) -> None:  # noqa: N802
        u = urlparse(self.path)
        path = unquote(u.path)
        if path == "/api/stop":
            if STOP.exists():
                STOP.unlink()
            self._json(200, {"ok": True, "message": "정지 해제"})
            return
        if path == "/api/inbox":
            body = self._read_body()
            q = parse_qs(u.query)
            raw_id = body.get("id", (q.get("id") or [None])[0])
            try:
                item_id = int(raw_id)
            except Exception:
                self._json(400, {"ok": False, "error": "id"})
                return
            result = delete_inbox(item_id)
            self._json(200 if result.get("ok") else 400, result)
            return
        self._json(404, {"ok": False})


def main() -> int:
    host = "127.0.0.1"
    httpd = ThreadingHTTPServer((host, PORT), Handler)
    print(f"ulon board http://{host}:{PORT}", flush=True)
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        print("stopped", flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
