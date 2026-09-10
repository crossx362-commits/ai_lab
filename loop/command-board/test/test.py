"""지휘 보드 API 전 기능 점검 — 임시 저장소(BOARD_ROOT)에서 실제 호출. 결과는 PASS/FAIL 표."""
import json, os, subprocess, sys, time, urllib.request, urllib.error
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
BASE = "http://localhost:5178"
H = os.path.dirname(os.path.abspath(__file__))
D = os.environ.get("HARNESS_DIR") or os.path.join(H, ".tmp")
T = os.path.join(D, "tmprepo"); R = os.path.join(D, "remote.git")
results = []

def call(method, path, body=None, headers=None, raw=None):
    data = raw if raw is not None else (json.dumps(body).encode("utf-8") if body is not None else None)
    h = {"Content-Type": "application/json", "Origin": BASE, "Sec-Fetch-Site": "same-origin"}
    h.update(headers or {})
    req = urllib.request.Request(BASE + path, data=data, method=method, headers=h)
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            return r.status, json.loads(r.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        try:
            return e.code, json.loads(e.read().decode("utf-8"))
        except Exception:
            return e.code, {}

def check(name, cond, detail=""):
    results.append((name, bool(cond), detail))
    print(("PASS " if cond else "FAIL ") + name + ("  — " + str(detail)[:160] if detail else ""))

def git(*a, cwd=T):
    return subprocess.run(["git", *a], cwd=cwd, capture_output=True, text=True, encoding="utf-8").stdout.strip()

def board():
    return call("GET", "/api/board")[1]

# 0. 읽기
s, b = call("GET", "/api/board")
check("GET /api/board 200", s == 200 and b.get("ok"), b.get("root"))
check("board.root = 임시 저장소", os.path.normcase(b.get("root", "")) == os.path.normcase(T), b.get("root"))
check("roster 4", b.get("roster") == ["Grok", "GPT", "제미니", "Claude"])
check("tools 키 존재", all(k in b.get("tools", {}) for k in ("claude", "codex", "gemini", "grok")), b.get("tools"))
n0 = len(git("rev-list", "HEAD").splitlines())

# 1. CSRF 가드(네거티브 컨트롤)
s, r = call("POST", "/api/command", raw=b'{"text":"x"}', headers={"Content-Type": "text/plain"})
check("text/plain → 403", s == 403, r.get("error"))
s, r = call("POST", "/api/command", {"text": "x"}, headers={"Origin": "http://evil.example"})
check("다른 Origin → 403", s == 403, r.get("error"))
s, r = call("POST", "/api/command", {"text": "x"}, headers={"Sec-Fetch-Site": "cross-site"})
check("cross-site → 403", s == 403, r.get("error"))
s, r = call("POST", "/api/command", {"text": "   "})
check("빈 명령 → 400", s == 400, r.get("error"))

# 2. 명령 → 커밋·푸시·디스패치
s, r = call("POST", "/api/command", {"text": "하네스 명령: 로그 회전 방안을 내라", "projectId": "loop", "git": "loop"})
check("POST /api/command 200", s == 200 and r.get("ok"), r)
check("명령 커밋됨", bool(r.get("sha")), r.get("sha"))
check("명령 푸시됨", r.get("pushed") is True, r.get("note"))
check("디스패치 시작", (r.get("dispatch") or {}).get("started") is True, r.get("dispatch"))
s, r = call("POST", "/api/command", {"text": "수집 중 명령"})
check("수집 중 명령 → 409", s == 409, (s, r.get("error")))
b = board()
check("명령 카드 맨 위", b["cards"] and b["cards"][0]["col"] == "명령" and "하네스 명령" in b["cards"][0]["title"], b["cards"][0]["title"] if b["cards"] else None)
check("dispatch.running=true", b["dispatch"]["running"] is True, b["dispatch"])
s, r = call("POST", "/api/dispatch")
check("수집 중 /api/dispatch → started=false", s == 200 and r.get("started") is False, r)

# 3. 의견 도착 대기
for _ in range(20):
    time.sleep(1)
    b = board()
    if not b["dispatch"]["running"]:
        break
check("디스패치 종료(idle)", b["dispatch"]["running"] is False, b["dispatch"])
ops = [c for c in b["cards"] if c["col"] == "의견"]
check("의견 카드 4장", len(ops) == 4, [(c["who"], c["status"]) for c in ops])
gpt = next((c for c in ops if c["who"] == "GPT"), None)
check("GPT 실패 카드에 사유", gpt and gpt["status"] == "fail" and "usage limit" in gpt["body"], gpt and gpt["body"][:80])
grok = next((c for c in ops if c["who"] == "Grok"), None)
check("Grok 제목·본문 분리", grok and grok["title"].startswith("그록 제목") and "60줄" in grok["body"], grok and grok["title"])
s, r = call("GET", "/api/dispatch/log")
check("디스패치 로그 읽힘", s == 200 and "[stub] done" in r.get("text", ""), r.get("text", "")[-60:])
check("의견 파일 커밋됨(stub commit_opinions)", "opinions: stub" in git("log", "--oneline", "-3"))

# 4. 판정 — 채택(의견→결정+실행), 반려, 재판정 409, 없는 id 404
s, r = call("POST", "/api/decide", {"id": grok["id"], "verdict": "채택"})
check("채택 200", s == 200 and r.get("ok") and r.get("pushed"), r)
b = board()
run = [c for c in b["cards"] if c["col"] == "실행" and "그록 제목" in c["title"]]
dec = [c for c in b["cards"] if c["col"] == "결정" and "그록 제목" in c["title"]]
check("채택 → 실행 줄 생성(담당 Grok Build, 근거 포함)", run and run[0]["who"] == "Grok Build" and "60줄" in (run[0].get("body") or ""), run and (run[0]["who"], run[0].get("body", "")[:40]))
check("채택 → 결정 줄 생성", bool(dec))
g2 = next(c for c in b["cards"] if c["id"] == grok["id"])
check("의견 카드 verdict=채택", g2.get("verdict") == "채택")
s, r = call("POST", "/api/decide", {"id": grok["id"], "verdict": "채택"})
check("재판정 → 409", s == 409, r.get("error"))
cl = next(c for c in ops if c["who"] == "Claude")
s, r = call("POST", "/api/decide", {"id": cl["id"], "verdict": "반려"})
check("반려 200", s == 200 and r.get("ok"))
b = board()
check("반려는 실행 줄 안 만듦", not [c for c in b["cards"] if c["col"] == "실행" and "클로드 제목" in c["title"]])
s, r = call("POST", "/api/decide", {"id": "없는-id", "verdict": "채택"})
check("없는 id → 404", s == 404, r.get("error"))
s, r = call("POST", "/api/decide", {"id": grok["id"], "verdict": "이상"})
check("이상한 verdict → 400", s == 400, r.get("error"))
cmd = b["cards"][0]
s, r = call("POST", "/api/decide", {"id": cmd["id"], "verdict": "채택"})
check("명령 카드 판정 → 400", s == 400, r.get("error"))

# 5. 결정대기·질문 예/아니오
pend = [c for c in b["cards"] if c["col"] == "결정대기" and not c.get("verdict")]
check("결정대기 카드 있음(실 BOARD 복사본)", len(pend) >= 1, len(pend))
if pend:
    s, r = call("POST", "/api/decide", {"id": pend[0]["id"], "verdict": "반려"})
    check("결정대기 아니오 200", s == 200 and r.get("ok"))
    b = board()
    p2 = next((c for c in b["cards"] if c["col"] == "결정대기" and c["title"] == pend[0]["title"]), None)
    check("결정대기 카드 verdict 표시(줄이 바뀌어 id는 새로 남)", p2 is not None and p2.get("verdict") == "반려", p2 and p2.get("verdict"))
    md = open(os.path.join(T, "loop", "BOARD.md"), encoding="utf-8").read()
    check("BOARD.md 줄 끝에 → 아니오 [ts]", "→ 아니오 [" in md)
    check("결정 절에 아니오: 기록", "] 아니오: " in md)

# 6. 프로젝트 상태
s, r = call("POST", "/api/project-state", {"id": "petnna", "state": "보류", "note": "하네스"})
check("프로젝트 상태 200", s == 200 and r.get("ok") and r.get("pushed"), r)
b = board()
check("projects.petnna = 보류", (b["projects"].get("petnna") or {}).get("state") == "보류", b["projects"].get("petnna"))
s, r = call("POST", "/api/project-state", {"id": "petnna", "state": "진행"})
b = board()
check("같은 id 덮어쓰기(줄 1개)", (b["projects"].get("petnna") or {}).get("state") == "진행" and open(os.path.join(T, "loop", "BOARD.md"), encoding="utf-8").read().count("petnna: ") == 1)
s, r = call("POST", "/api/project-state", {"id": "petnna", "state": "대충"})
check("이상한 상태 → 400", s == 400, r.get("error"))
s, r = call("POST", "/api/project-state", {"id": "../x", "state": "진행"})
check("이상한 id → 400", s == 400, r.get("error"))

# 7. 조회 API
s, r = call("GET", "/api/projects?paths=petnna:projects/petnna,lab:")
rows = {x["id"]: x for x in r.get("rows", [])}
check("/api/projects 행 2개·sha", s == 200 and len(rows) == 2 and rows["petnna"]["sha"] and rows["lab"]["week"] >= 1, rows)
s, r = call("GET", "/api/status?path=loop&file=loop/BOARD.md")
check("/api/status sha·head 텍스트", s == 200 and r.get("sha") and r.get("head", "").startswith("#"), (r.get("sha"), r.get("head", "")[:20]))
s, r = call("GET", "/api/status?path=../../etc")
check("저장소 밖 경로 → 400", s == 400, r.get("error"))
s, r = call("GET", "/api/nope")
check("없는 경로 → 404", s == 404)

# 8. 원격 동기화·커밋 수
n1 = len(git("rev-list", "HEAD").splitlines())
local, remote = git("rev-parse", "HEAD"), git("rev-parse", "master", cwd=R)
check("로컬 HEAD == 원격 master", local == remote, (local[:7], remote[:7]))
check("커밋 수 증가(명령1+의견1+판정3+상태2)", n1 - n0 >= 7, n1 - n0)
check("작업 트리 깨끗", git("status", "--porcelain") == "", git("status", "--porcelain"))

# 9. 두 번째 명령: 의견 보관(archive) 후 새 수집
s, r = call("POST", "/api/command", {"text": "두 번째 하네스 명령"})
check("두 번째 명령 200", s == 200 and r.get("ok"), r.get("note"))
b = board()
check("이전 의견 카드 사라짐(보관)", not [c for c in b["cards"] if c["col"] == "의견" and c["status"] in ("ok", "fail")] or all(c.get("status") == "running" for c in b["cards"] if c["col"] == "의견"), [(c["who"], c["status"]) for c in b["cards"] if c["col"] == "의견"])
check("archive/ 생성", os.path.isdir(os.path.join(T, "loop", "opinions", "archive")))
for _ in range(20):
    time.sleep(1)
    if not board()["dispatch"]["running"]:
        break

fails = [r for r in results if not r[1]]
print("\n== %d passed, %d failed ==" % (len(results) - len(fails), len(fails)))
for name, _, d in fails:
    print("  FAIL:", name, d)
sys.exit(1 if fails else 0)
