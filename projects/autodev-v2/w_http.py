from w_ctrl import *
import infra_update

def _update(self):
    was_running = engine_info()["running"]
    if was_running or legacy_pids():
        r = self.stop()
        if not r.get("ok"):
            return r
    try:
        ok, msg = infra_update.apply(REPO)
        if not ok:
            if was_running:
                self.start()
            return {"ok": False, "message": msg}
        log = SERVER_LOG.open("a", encoding="utf-8")
        popen_retry(
            [sys.executable, str(HERE / "restart_server.py"), str(os.getpid()), str(PORT), "1" if was_running else "0"],
            cwd=REPO, stdout=log, stderr=log, start_new_session=True, env=os.environ.copy(),
        )
    except Exception as e:
        if was_running:
            self.start()
        return {"ok": False, "message": f"{type(e).__name__}: {e}"}
    return {"ok": True, "message": "AutoDev 시스템 업데이트 완료.", "restarting": True}

Controller.update = _update
CTRL = Controller()
TOKEN = secrets.token_urlsafe(24)


class Handler(BaseHTTPRequestHandler):
    server_version = "AutoDevELI5/8"

    def log_message(self, fmt, *args):
        return

    def _json(self, obj, code=200):
        data = json.dumps(obj, ensure_ascii=False).encode()
        self.send_response(code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Cache-Control", "no-store")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def _auth(self):
        q = urllib.parse.parse_qs(urllib.parse.urlparse(self.path).query)
        supplied = self.headers.get("X-AutoDev-Token", "") or q.get("token", [""])[0]
        return secrets.compare_digest(supplied, TOKEN)

    def do_GET(self):
        parsed = urllib.parse.urlparse(self.path)
        if parsed.path == "/":
            supplied = urllib.parse.parse_qs(parsed.query).get("token", [""])[0]
            if not secrets.compare_digest(supplied, TOKEN):
                self.send_response(302)
                self.send_header("Location", f"/?token={urllib.parse.quote(TOKEN)}&r={int(time.time())}")
                self.end_headers()
                return
            data = HTML_FILE.read_bytes()
            self.send_response(200)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.send_header("Cache-Control", "no-store")
            self.send_header("Content-Length", str(len(data)))
            self.end_headers()
            self.wfile.write(data)
            return
        if not self._auth():
            self._json({"ok": False, "message": "인증 오류"}, 403)
            return
        if parsed.path == "/api/status":
            self._json(CTRL.status())
            return
        if parsed.path == "/api/logs":
            try:
                after = int(urllib.parse.parse_qs(parsed.query).get("after", ["0"])[0])
            except Exception:
                after = 0
            self._json(CTRL.log_rows(after))
            return
        self._json({"ok": False, "message": "없는 주소"}, 404)

    def do_POST(self):
        if not self._auth():
            self._json({"ok": False, "message": "인증 오류"}, 403)
            return
        path = urllib.parse.urlparse(self.path).path
        if path == "/api/start":
            self._json(CTRL.start())
            return
        if path == "/api/stop":
            self._json(CTRL.stop())
            return
        if path == "/api/recover":
            self._json(CTRL.recover())
            return
        if path == "/api/update":
            r = CTRL.update()
            self._json(r)
            if r.get("restarting"):
                threading.Timer(0.4, lambda: os._exit(0)).start()
            return
        if path == "/api/reap":
            try:
                result = do_reap()
                self._json({"ok": True, "message": "잔여 프로세스 %s개 정리" % len(result.get("killed") or []), "killed": result.get("killed")})
            except Exception as e:
                self._json({"ok": False, "message": str(e)}, 500)
            return
        if path == "/api/quota/codex/clear":
            p = QUOTA_FILES["codex"]
            try:
                if p.exists():
                    p.unlink()
                self._json({"ok": True, "message": "ok"})
            except Exception as e:
                self._json({"ok": False, "message": str(e)}, 500)
            return
        if path == "/api/open-repo":
            try:
                popen_retry(["open", str(REPO)])
                self._json({"ok": True, "message": "폴더를 열었습니다."})
            except Exception as e:
                self._json({"ok": False, "message": str(e)}, 500)
            return
        self._json({"ok": False, "message": "없는 기능"}, 404)


def write_server_state():
    write_json(SERVER_STATE, {"pid": os.getpid(), "port": PORT, "token": TOKEN, "protocol": CONTROL_PROTOCOL})


def main() -> int:
    old = read_json(SERVER_STATE)
    try:
        port = int(old.get("port", PORT))
        token = str(old.get("token", ""))
        pid = int(old.get("pid", 0) or 0)
    except Exception:
        port, token, pid = PORT, "", 0
    if token and pid_alive(pid) and server_alive(port):
        webbrowser.open(f"http://{HOST}:{port}/?token={urllib.parse.quote(token)}&r={int(time.time())}")
        return 0
    try:
        server = ThreadingHTTPServer((HOST, PORT), Handler)
    except OSError:
        webbrowser.open(f"http://{HOST}:{PORT}/")
        return 0
    write_server_state()
    atexit.register(lambda: None)
    url = f"http://{HOST}:{PORT}/?token={urllib.parse.quote(TOKEN)}&r={int(time.time())}"
    if os.environ.pop("AUTODEV_RESUME_ENGINE", "0") == "1":
        threading.Timer(0.8, CTRL.start).start()
    threading.Timer(0.3, lambda: webbrowser.open(url)).start()
    try:
        server.serve_forever(poll_interval=0.25)
    finally:
        server.server_close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
