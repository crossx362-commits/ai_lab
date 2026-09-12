#!/usr/bin/env python3
"""울온 개발 현황 보드. 표준 라이브러리만. 판단·실행은 하지 않는다."""
from __future__ import annotations

import json
import os
import re
import subprocess
import sys
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, unquote, urlparse

ROOT = Path(__file__).resolve().parent.parent
LOOP = ROOT / "loop"
DOCS = ROOT / "docs"
INBOX = DOCS / "feedback" / "INBOX.md"
BOARD = DOCS / "board.json"
STATUS = DOCS / "STATUS.md"
ASSETS = DOCS / "ASSETS.md"
STOP = LOOP / "STOP"
LOG_DIR = ROOT / "logs"
STATE = LOG_DIR / "loop_state.json"
HTML = LOOP / "board.html"
ENV = LOOP / "env.sh"

INBOX_LINE = re.compile(
    r"^- \[(?P<done>[ xX])\]"
    r"(?: · 바퀴#(?P<loop>\d+))?"
    r"(?P<urgent> \[긴급\])?"
    r"(?: \((?P<ts>[^)]+)\))?"
    r" (?P<text>.*)$"
)


def load_env_port() -> int:
    port = 8787
    if ENV.exists():
        for line in ENV.read_text(encoding="utf-8").splitlines():
            s = line.strip()
            if s.startswith("BOARD_PORT="):
                raw = s.split("=", 1)[1].split("#", 1)[0].strip().strip("'\"")
                try:
                    port = int(raw)
                except ValueError:
                    pass
    return int(os.environ.get("BOARD_PORT", port))


def git_root() -> Path:
    p = ROOT
    while p != p.parent:
        if (p / ".git").exists():
            return p
        p = p.parent
    return ROOT


def git_rel() -> str:
    try:
        return str(ROOT.relative_to(git_root()))
    except ValueError:
        return "."


def read_json(path: Path, default):
    if not path.exists():
        return default
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return default


def now_kst() -> str:
    # Asia/Seoul = UTC+9, stdlib only (zoneinfo may miss tzdata)
    return datetime.now(timezone.utc).astimezone().strftime("%Y-%m-%d %H:%M")


def parse_inbox() -> list[dict]:
    if not INBOX.exists():
        return []
    items = []
    in_items = False
    for i, line in enumerate(INBOX.read_text(encoding="utf-8").splitlines()):
        if line.strip() == "## 항목":
            in_items = True
            continue
        if not in_items:
            continue
        m = INBOX_LINE.match(line)
        if not m:
            if line.strip().startswith("- ["):
                items.append(
                    {
                        "index": i,
                        "raw": line,
                        "done": False,
                        "urgent": "[긴급]" in line,
                        "loop": None,
                        "ts": "",
                        "text": line.lstrip("- ").strip(),
                    }
                )
            continue
        items.append(
            {
                "index": i,
                "raw": line,
                "done": m.group("done").lower() == "x",
                "urgent": bool(m.group("urgent")),
                "loop": int(m.group("loop")) if m.group("loop") else None,
                "ts": m.group("ts") or "",
                "text": (m.group("text") or "").strip(),
            }
        )
    return items


def rewrite_inbox_line(index: int, new_line: str | None) -> bool:
    if not INBOX.exists():
        return False
    lines = INBOX.read_text(encoding="utf-8").splitlines()
    if index < 0 or index >= len(lines):
        return False
    if new_line is None:
        del lines[index]
    else:
        lines[index] = new_line
    INBOX.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return True


def append_inbox(text: str, urgent: bool) -> str:
    INBOX.parent.mkdir(parents=True, exist_ok=True)
    if not INBOX.exists():
        INBOX.write_text(
            "# INBOX\n\n형식:\n\n- `- [ ] (YYYY-MM-DD HH:MM) 내용`\n\n## 항목\n",
            encoding="utf-8",
        )
    body = INBOX.read_text(encoding="utf-8")
    if "## 항목" not in body:
        body = body.rstrip() + "\n\n## 항목\n"
    flag = " [긴급]" if urgent else ""
    line = f"- [ ]{flag} ({now_kst()}) {text.strip()}"
    if not body.endswith("\n"):
        body += "\n"
    body += line + "\n"
    INBOX.write_text(body, encoding="utf-8")
    # 다시 읽어 확인
    return line if line in INBOX.read_text(encoding="utf-8") else ""


def git_log(n: int = 30) -> list[dict]:
    root = git_root()
    rel = git_rel()
    try:
        out = subprocess.check_output(
            [
                "git",
                "log",
                f"-{n}",
                "--pretty=format:%H%x09%ad%x09%s",
                "--date=iso-strict",
                "--",
                rel,
            ],
            cwd=str(root),
            text=True,
            stderr=subprocess.DEVNULL,
        )
    except Exception:
        return []
    rows = []
    for line in out.splitlines():
        if not line.strip():
            continue
        parts = line.split("\t", 2)
        if len(parts) < 3:
            continue
        h, ts, msg = parts
        files = 0
        try:
            stat = subprocess.check_output(
                ["git", "show", "--pretty=", "--name-only", h, "--", rel],
                cwd=str(root),
                text=True,
                stderr=subprocess.DEVNULL,
            )
            files = len([x for x in stat.splitlines() if x.strip()])
        except Exception:
            files = 0
        m = re.search(r"\[loop#(\d+)\]", msg)
        rows.append(
            {
                "hash": h,
                "short": h[:8],
                "date": ts,
                "message": msg,
                "files": files,
                "loop": int(m.group(1)) if m else None,
            }
        )
    return rows


def git_commit(h: str) -> dict:
    if not re.fullmatch(r"[0-9a-fA-F]{7,40}", h):
        return {"error": "bad hash"}
    root = git_root()
    rel = git_rel()
    try:
        stat = subprocess.check_output(
            ["git", "show", "--stat", "--format=fuller", h, "--", rel],
            cwd=str(root),
            text=True,
            stderr=subprocess.STDOUT,
        )
        diff = subprocess.check_output(
            ["git", "show", "--pretty=", "--unified=3", h, "--", rel],
            cwd=str(root),
            text=True,
            stderr=subprocess.STDOUT,
        )
    except subprocess.CalledProcessError as e:
        return {"error": e.output[-4000:] if e.output else str(e)}
    if len(diff) > 40000:
        diff = diff[:40000] + "\n… (truncated)\n"
    return {"hash": h, "stat": stat, "diff": diff}


def wheel_log(n: int) -> str:
    path = LOG_DIR / f"loop_{int(n):04d}.log"
    if not path.exists():
        return ""
    text = path.read_text(encoding="utf-8", errors="replace")
    if len(text) > 200000:
        return text[-200000:]
    return text


def asset_previews() -> list[dict]:
    out = []
    for base in (ROOT / "assets" / "generated", ROOT / "assets" / "3d" / "preview"):
        if not base.exists():
            continue
        for p in sorted(base.rglob("*")):
            if p.suffix.lower() not in {".png", ".jpg", ".jpeg", ".webp"}:
                continue
            rel = str(p.relative_to(ROOT))
            prompt = p.with_suffix(".prompt.txt")
            if not prompt.exists():
                prompt = p.with_name(p.stem + ".prompt.txt")
            out.append(
                {
                    "path": rel,
                    "name": p.name,
                    "mtime": p.stat().st_mtime,
                    "prompt": str(prompt.relative_to(ROOT)) if prompt.exists() else "",
                }
            )
    out.sort(key=lambda x: x["mtime"], reverse=True)
    return out[:40]


def license_unknown_count(assets_md: str) -> int:
    n = 0
    for line in assets_md.splitlines():
        if "출처 미확인" in line or "라이선스 미확인" in line:
            n += 1
    return n


def no_commit_streak(history: list, commits: list[dict]) -> int:
    hashed = {c.get("loop") for c in commits if c.get("loop") is not None}
    streak = 0
    for row in reversed(history):
        if row.get("result") != "success":
            continue
        loop = row.get("loop")
        if loop in hashed:
            break
        streak += 1
        if streak >= 3:
            break
    return streak


def history_rows(limit: int = 50) -> list[dict]:
    path = LOG_DIR / "loop_history.jsonl"
    if not path.exists():
        return []
    rows = []
    for line in path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line:
            continue
        try:
            rows.append(json.loads(line))
        except Exception:
            continue
    return rows[-limit:]


def build_state() -> dict:
    state = read_json(STATE, {})
    board = read_json(BOARD, {"updated_at": "", "cards": []})
    cards = board.get("cards") or []
    inbox = parse_inbox()
    status = STATUS.read_text(encoding="utf-8") if STATUS.exists() else ""
    assets = ASSETS.read_text(encoding="utf-8") if ASSETS.exists() else ""
    commits = git_log(30)
    history = history_rows(50)
    blocked = sum(1 for c in cards if c.get("status") == "막힘")
    return {
        "loop_state": state,
        "board": board,
        "inbox": inbox,
        "status_md": status,
        "assets_md": assets,
        "commits": commits,
        "history": history,
        "previews": asset_previews(),
        "stop": STOP.exists(),
        "warnings": {
            "blocked": blocked,
            "license_unknown": license_unknown_count(assets),
            "no_commit_streak": no_commit_streak(history, commits),
        },
        "max_consec_fail": 3,
    }


class Handler(BaseHTTPRequestHandler):
    server_version = "UlonBoard/1"

    def log_message(self, fmt: str, *args) -> None:
        sys.stderr.write("%s - %s\n" % (self.address_string(), fmt % args))

    def _json(self, code: int, obj) -> None:
        data = json.dumps(obj, ensure_ascii=False).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(data)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(data)

    def _text(self, code: int, text: str, ctype: str) -> None:
        data = text.encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(data)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(data)

    def _bytes(self, code: int, data: bytes, ctype: str) -> None:
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def _body(self) -> bytes:
        n = int(self.headers.get("Content-Length") or 0)
        return self.rfile.read(n) if n else b""

    def do_GET(self) -> None:
        u = urlparse(self.path)
        path = unquote(u.path)
        if path in ("/", "/index.html"):
            if not HTML.exists():
                self._text(500, "board.html missing", "text/plain; charset=utf-8")
                return
            self._text(200, HTML.read_text(encoding="utf-8"), "text/html; charset=utf-8")
            return
        if path == "/api/state":
            self._json(200, build_state())
            return
        if path.startswith("/api/log/"):
            try:
                n = int(path.rsplit("/", 1)[-1])
            except ValueError:
                self._json(400, {"error": "bad loop id"})
                return
            self._json(200, {"loop": n, "text": wheel_log(n)})
            return
        if path.startswith("/api/commit/"):
            h = path.rsplit("/", 1)[-1]
            self._json(200, git_commit(h))
            return
        if path == "/api/file":
            qs = parse_qs(u.query)
            rel = (qs.get("path") or [""])[0]
            self._serve_preview(rel)
            return
        self._text(404, "not found", "text/plain; charset=utf-8")

    def _serve_preview(self, rel: str) -> None:
        if not rel or ".." in rel.split("/"):
            self._text(400, "bad path", "text/plain")
            return
        p = (ROOT / rel).resolve()
        try:
            p.relative_to(ROOT)
        except ValueError:
            self._text(403, "forbidden", "text/plain")
            return
        allowed = (ROOT / "assets").resolve()
        try:
            p.relative_to(allowed)
        except ValueError:
            self._text(403, "forbidden", "text/plain")
            return
        if not p.is_file():
            self._text(404, "missing", "text/plain")
            return
        ctype = {
            ".png": "image/png",
            ".jpg": "image/jpeg",
            ".jpeg": "image/jpeg",
            ".webp": "image/webp",
            ".txt": "text/plain; charset=utf-8",
        }.get(p.suffix.lower())
        if not ctype:
            self._text(403, "type", "text/plain")
            return
        self._bytes(200, p.read_bytes(), ctype)

    def do_POST(self) -> None:
        u = urlparse(self.path)
        path = u.path
        try:
            payload = json.loads(self._body() or b"{}")
        except json.JSONDecodeError:
            self._json(400, {"error": "invalid json"})
            return
        if path == "/api/inbox":
            text = (payload.get("text") or "").strip()
            if not text:
                self._json(400, {"error": "empty"})
                return
            line = append_inbox(text, bool(payload.get("urgent")))
            if not line:
                self._json(500, {"error": "write failed"})
                return
            items = parse_inbox()
            ok = any(it["raw"] == line for it in items)
            self._json(200 if ok else 500, {"ok": ok, "line": line, "inbox": items})
            return
        if path == "/api/stop":
            STOP.write_text("stop\n", encoding="utf-8")
            self._json(200, {"ok": True, "stop": True, "note": "현재 바퀴를 마친 뒤 멈춥니다."})
            return
        self._json(404, {"error": "not found"})

    def do_PATCH(self) -> None:
        u = urlparse(self.path)
        try:
            payload = json.loads(self._body() or b"{}")
        except json.JSONDecodeError:
            self._json(400, {"error": "invalid json"})
            return
        if u.path != "/api/inbox":
            self._json(404, {"error": "not found"})
            return
        try:
            index = int(payload["index"])
        except (KeyError, TypeError, ValueError):
            self._json(400, {"error": "index required"})
            return
        items = {it["index"]: it for it in parse_inbox()}
        it = items.get(index)
        if not it or it["done"]:
            self._json(400, {"error": "cannot edit"})
            return
        text = (payload.get("text") or "").strip()
        if not text:
            self._json(400, {"error": "empty"})
            return
        urgent = it["urgent"] if payload.get("urgent") is None else bool(payload.get("urgent"))
        ts = it["ts"] or now_kst()
        flag = " [긴급]" if urgent else ""
        line = f"- [ ]{flag} ({ts}) {text}"
        if not rewrite_inbox_line(index, line):
            self._json(500, {"error": "write failed"})
            return
        self._json(200, {"ok": True, "inbox": parse_inbox()})

    def do_DELETE(self) -> None:
        u = urlparse(self.path)
        if u.path == "/api/stop":
            if STOP.exists():
                STOP.unlink()
            self._json(200, {"ok": True, "stop": False})
            return
        if u.path != "/api/inbox":
            self._json(404, {"error": "not found"})
            return
        qs = parse_qs(u.query)
        try:
            payload = json.loads(self._body() or b"{}")
        except json.JSONDecodeError:
            payload = {}
        raw = (qs.get("index") or [payload.get("index")])[0]
        try:
            index = int(raw)
        except (TypeError, ValueError):
            self._json(400, {"error": "index required"})
            return
        items = {it["index"]: it for it in parse_inbox()}
        it = items.get(index)
        if not it or it["done"]:
            self._json(400, {"error": "cannot delete processed"})
            return
        if not rewrite_inbox_line(index, None):
            self._json(500, {"error": "write failed"})
            return
        self._json(200, {"ok": True, "inbox": parse_inbox()})


def main() -> int:
    port = load_env_port()
    LOG_DIR.mkdir(parents=True, exist_ok=True)
    httpd = ThreadingHTTPServer(("127.0.0.1", port), Handler)
    print(f"ulon board http://127.0.0.1:{port}", flush=True)
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        httpd.server_close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
