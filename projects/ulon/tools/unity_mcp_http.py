#!/usr/bin/env python3
import json, sys, urllib.request, http.client, time, uuid

HOST = "127.0.0.1"
PORT = 8080

class Mcp:
    def __init__(self):
        self.conn = http.client.HTTPConnection(HOST, PORT, timeout=600)
        self.session = None
        self._id = 0
        self.initialize()

    def initialize(self):
        self._id += 1
        payload = {
            "jsonrpc": "2.0",
            "id": self._id,
            "method": "initialize",
            "params": {
                "protocolVersion": "2024-11-05",
                "capabilities": {},
                "clientInfo": {"name": "ulon-qa", "version": "1.0"},
            },
        }
        data, headers = self._post(payload, first=True)
        self.session = headers.get("Mcp-Session-Id") or headers.get("mcp-session-id")
        # notifications/initialized
        note = {"jsonrpc": "2.0", "method": "notifications/initialized", "params": {}}
        self._post(note, notify=True)
        return data

    def _post(self, payload, first=False, notify=False):
        body = json.dumps(payload).encode()
        headers = {
            "Content-Type": "application/json",
            "Accept": "application/json, text/event-stream",
        }
        if self.session:
            headers["Mcp-Session-Id"] = self.session
        self.conn.request("POST", "/mcp", body, headers)
        resp = self.conn.getresponse()
        raw = resp.read()
        hdrs = {k: v for k, v in resp.getheaders()}
        text = raw.decode("utf-8", "replace")
        if resp.status >= 400 and not notify:
            raise RuntimeError(f"HTTP {resp.status}: {text[:500]}")
        parsed = self._parse(text)
        return parsed, hdrs

    def _parse(self, text):
        text = text.strip()
        if not text:
            return None
        # SSE
        if "data:" in text:
            chunks = []
            for line in text.splitlines():
                if line.startswith("data:"):
                    chunks.append(line[5:].strip())
            if chunks:
                return json.loads("\n".join(chunks))
        try:
            return json.loads(text)
        except Exception:
            return {"raw": text[:2000]}

    def call(self, name, arguments, timeout_s=180):
        self._id += 1
        payload = {
            "jsonrpc": "2.0",
            "id": self._id,
            "method": "tools/call",
            "params": {"name": name, "arguments": arguments},
        }
        data, _ = self._post(payload)
        return data

def unwrap(data):
    if not isinstance(data, dict):
        return data
    if "result" in data:
        r = data["result"]
        if isinstance(r, dict) and "content" in r:
            parts = []
            for c in r.get("content") or []:
                if isinstance(c, dict) and c.get("type") == "text":
                    parts.append(c.get("text", ""))
                else:
                    parts.append(str(c))
            s = "\n".join(parts)
            try:
                return json.loads(s)
            except Exception:
                return s
        return r
    if "error" in data:
        return data
    return data

if __name__ == "__main__":
    tool = sys.argv[1]
    args = json.loads(sys.argv[2]) if len(sys.argv) > 2 else {}
    m = Mcp()
    out = unwrap(m.call(tool, args))
    print(json.dumps(out, indent=2, default=str)[:20000])
