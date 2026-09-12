#!/usr/bin/env python3
"""울온 개발 현황 보드. 표준 라이브러리만. 판단·실행은 하지 않는다."""
from __future__ import annotations

import json
import os
import re
import subprocess
import sys
from collections import Counter
from datetime import datetime, timezone, timedelta
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
COVERAGE = DOCS / "DESIGN_COVERAGE.md"
DEV_PLAN = DOCS / "DEVELOPMENT_PLAN.md"
GAME_DESIGN = DOCS / "GAME_DESIGN.md"
STOP = LOOP / "STOP"
LOG_DIR = ROOT / "logs"
STATE = LOG_DIR / "loop_state.json"
HTML = LOOP / "board.html"
ENV = LOOP / "env.sh"
CAPTURES = ROOT / "unity" / "Captures"

INBOX_LINE = re.compile(
    r"^- \[(?P<done>[ xX])\]"
    r"(?: · 바퀴#(?P<loop>\d+))?"
    r"(?P<urgent> \[긴급\])?"
    r"(?: \((?P<ts>[^)]+)\))?"
    r" (?P<text>.*)$"
)
SHOT_TICK = re.compile(
    r"`(?:unity/Captures/)?(loop\d+_[^`]+?\.(?:png|jpe?g|webp))`",
    re.I,
)
LOOP_HASH = re.compile(r"loop#(\d+)")
LOOP_FILE = re.compile(r"^loop(\d+)_.+\.(png|jpe?g|webp)$", re.I)
DUP_STEM = re.compile(r"-\d+$")
IMG_SUFFIX = {".png", ".jpg", ".jpeg", ".webp"}


def _env_int(key: str, default: int) -> int:
    if ENV.exists():
        for line in ENV.read_text(encoding="utf-8").splitlines():
            s = line.strip()
            if s.startswith(key + "="):
                raw = s.split("=", 1)[1].split("#", 1)[0].strip().strip("'\"")
                raw = re.sub(r"^\$\{[A-Z0-9_]+:-(\d+)\}$", r"\1", raw)
                try:
                    return int(raw)
                except ValueError:
                    pass
    try:
        return int(os.environ.get(key, default))
    except ValueError:
        return default


def load_env_port() -> int:
    return int(os.environ.get("BOARD_PORT", _env_int("BOARD_PORT", 8787)))


SYS_SCORE = {
    "원작 기준 합격": 100,
    "동작함": 75,
    "부분": 40,
    "미확인": 15,
    "없음": 0,
}
SYS_ORDER = ["원작 기준 합격", "동작함", "부분", "미확인", "없음"]


def parse_ts(s: str):
    if not s:
        return None
    raw = s.strip()
    if re.search(r"[+-]\d{4}$", raw):
        raw = raw[:-2] + ":" + raw[-2:]
    try:
        return datetime.fromisoformat(raw)
    except Exception:
        return None


def parse_systems(md: str) -> dict:
    rows = []
    in_table = False
    for line in md.splitlines():
        if line.startswith("#") and "시스템 상태" in line:
            in_table = True
            continue
        if not in_table:
            continue
        if line.startswith("## ") and "시스템 상태" not in line:
            break
        if not line.startswith("|"):
            continue
        cells = [c.strip() for c in line.split("|")[1:-1]]
        if len(cells) < 2 or cells[0] in {"시스템", "---"} or set(cells[1]) <= set("-: "):
            continue
        st = cells[1]
        rows.append(
            {
                "name": cells[0],
                "status": st,
                "note": cells[2] if len(cells) > 2 else "",
                "score": SYS_SCORE.get(st, 0),
            }
        )
    counts = {k: 0 for k in SYS_ORDER}
    counts.update(Counter(r["status"] for r in rows if r["status"] in counts))
    n = len(rows)
    score = round(sum(r["score"] for r in rows) / n) if n else 0
    return {"rows": rows, "counts": counts, "score": score, "total": n}


COV_MARK = {"①": ("구현", 100), "②": ("일부", 40), "③": ("없음", 0)}
P_HINTS = [
    (["persist-ready"], ("postgresql", "/ready", "저장 서비스", "persist")),
    (["persist-latest-wins"], ("json", "폴백", "최신 저장")),
    (["client-rebuild", "two-client-core"], ("13.1", "두 플레이어", "2인", "전용 서버", "외부 서버")),
    (["interest-mgmt"], ("관심 영역", "lod", "부하", "20~50")),
    (["ui-paperdoll"], ("paperdoll", "퀵바", "sfx", "vfx", "컨테이너 ux")),
]
ROADMAP_MATCH = [
    ("부트스트랩", "1."),
    ("Vertical Slice", "2."),
    ("온라인", "5."),
    ("스킬/아이템", "8."),
    ("채집/제작", "3."),
    ("경제/마을", "3."),
    ("월드/몬스터", "4."),
    ("콘텐츠/UX", "6."),
    ("안정화", "9."),
    ("Closed Alpha", "9."),
]


def _md_cells(line: str) -> list[str]:
    return [c.strip() for c in line.split("|")[1:-1]]


def parse_coverage(md: str) -> dict:
    chapters = []
    cur = None
    for line in md.splitlines():
        hm = re.match(r"^## (.+)$", line)
        if hm:
            if cur:
                chapters.append(cur)
            cur = {"title": hm.group(1).strip(), "items": []}
            continue
        if not cur or not line.startswith("|"):
            continue
        cells = _md_cells(line)
        if len(cells) < 2:
            continue
        if cells[0] in {"조항", "영역"} or cells[1] in {"판정"} or set(cells[1]) <= set("-: "):
            continue
        mark = next((k for k in COV_MARK if k in cells[1]), None)
        if not mark:
            continue
        note = cells[2] if len(cells) > 2 else ""
        deferred = "구멍이 아님" in note or "보류" in cells[0]
        label, score = COV_MARK[mark]
        cur["items"].append(
            {
                "name": cells[0],
                "mark": mark,
                "label": "보류" if deferred else label,
                "score": 0 if deferred else score,
                "deferred": deferred,
                "note": note,
            }
        )
    if cur:
        chapters.append(cur)
    counted = []
    for ch in chapters:
        live = [i for i in ch["items"] if not i["deferred"]]
        n = len(live)
        ch["score"] = round(sum(i["score"] for i in live) / n) if n else 0
        ch["n"] = n
        ch["full"] = sum(1 for i in live if i["mark"] == "①")
        ch["part"] = sum(1 for i in live if i["mark"] == "②")
        ch["none"] = sum(1 for i in live if i["mark"] == "③")
        counted.extend(live)
    n = len(counted)
    marks = Counter(i["mark"] for i in counted)
    score = round(sum(i["score"] for i in counted) / n) if n else 0
    return {
        "source": "docs/DESIGN_COVERAGE.md",
        "note": "① 화면에서 쓸 수 있음 · ② 일부 · ③ 없음. 2026-09-11 문서 판정(실행 재검증과 다를 수 있음).",
        "chapters": chapters,
        "score": score,
        "total": n,
        "full": marks.get("①", 0),
        "part": marks.get("②", 0),
        "none": marks.get("③", 0),
        "holes": [i for i in counted if i["mark"] == "③"],
        "partials": [i for i in counted if i["mark"] == "②"],
    }


def _chapter_score(chapters: list, prefix: str) -> int | None:
    hits = [c for c in chapters if c["title"].startswith(prefix)]
    if not hits:
        return None
    return hits[0]["score"]


def parse_roadmap(md: str, chapters: list) -> dict:
    rows = []
    in_sec = False
    for line in (md or "").splitlines():
        if line.startswith("## 13.3"):
            in_sec = True
            continue
        if in_sec and line.startswith("## "):
            break
        if not in_sec or not line.startswith("|"):
            continue
        cells = _md_cells(line)
        if len(cells) < 3 or cells[0] in {"기간"} or set(cells[0]) <= set("-: ~"):
            continue
        title = cells[1]
        prefix = next((p for key, p in ROADMAP_MATCH if key in title), None)
        pct = _chapter_score(chapters, prefix) if prefix else 0
        rows.append(
            {
                "when": cells[0],
                "title": title,
                "gate": cells[2],
                "pct": pct or 0,
                "state": "완료" if (pct or 0) >= 80 else ("진행" if (pct or 0) >= 40 else "남음"),
            }
        )
    n = len(rows) or 1
    done = sum(1 for r in rows if r["state"] == "완료")
    current = next((i for i, r in enumerate(rows) if r["state"] != "완료"), len(rows) - 1 if rows else 0)
    return {
        "source": "docs/GAME_DESIGN.md §13.3",
        "rows": rows,
        "pct": round(sum(r["pct"] for r in rows) / n),
        "done": done,
        "total": len(rows),
        "current": current,
    }


def _p_cards(title: str, cards: list) -> list:
    blob = title.lower()
    ids = []
    for idlist, keys in P_HINTS:
        if any(k in blob for k in keys):
            ids.extend(idlist)
    return [c for c in cards if c.get("id") in ids]


def parse_priority_plan(md: str, cards: list) -> list:
    items = []
    in_sec = False
    for line in (md or "").splitlines():
        if "이어서 진행할 순서" in line:
            in_sec = True
            continue
        if in_sec and line.startswith("## "):
            break
        if not in_sec or not line.startswith("|"):
            continue
        cells = _md_cells(line)
        if len(cells) < 3 or not re.match(r"^P[0-4]$", cells[0]):
            continue
        linked = _p_cards(cells[1], cards)
        if linked:
            statuses = [c.get("status") for c in linked]
            if "완료" in statuses and all(s == "완료" for s in statuses):
                st, pct = "완료", 100
            elif any(s in {"진행 중", "검증 중"} for s in statuses):
                st, pct = "진행 중", max(int(c.get("progress") or 0) for c in linked)
            elif any(s == "실패" for s in statuses):
                st, pct = "실패", max(int(c.get("progress") or 0) for c in linked)
            elif any(s == "막힘" for s in statuses):
                st, pct = "막힘", 0
            else:
                st, pct = "대기", 0
        else:
            st, pct = "대기", 0
        items.append(
            {
                "pri": cells[0],
                "title": cells[1],
                "evidence": cells[2],
                "status": st,
                "pct": pct,
                "cards": [c.get("id") for c in linked],
            }
        )
    return items


def wheel_progress(state: dict, timeout_min: int, sleep_between: int) -> dict:
    st = state.get("display_status") or state.get("status") or ""
    timeout_sec = max(1, timeout_min * 60)
    started = parse_ts(str(state.get("started_at") or ""))
    now = datetime.now(timezone.utc).astimezone()
    elapsed = 0
    if started:
        if started.tzinfo is None:
            started = started.replace(tzinfo=timezone(timedelta(hours=9)))
        elapsed = max(0, int((now - started).total_seconds()))
    if st == "running":
        pct = min(99, round(100 * elapsed / timeout_sec))
        return {
            "label": "이 바퀴 시간",
            "pct": pct,
            "elapsed_sec": elapsed,
            "limit_sec": timeout_sec,
            "remain_sec": max(0, timeout_sec - elapsed),
        }
    if st == "waiting":
        wait = int(state.get("wait_remaining_sec") or 0)
        sleep = max(1, sleep_between)
        return {
            "label": "다음 바퀴까지",
            "pct": min(100, round(100 * (sleep - wait) / sleep)),
            "elapsed_sec": sleep - wait,
            "limit_sec": sleep,
            "remain_sec": wait,
        }
    return {"label": "루프 정지", "pct": 0, "elapsed_sec": 0, "limit_sec": timeout_sec, "remain_sec": 0}


def card_progress(card: dict, state: dict, wheel: dict) -> int:
    st = card.get("status") or "대기"
    if st == "완료":
        return 100
    if st == "검증 중":
        return 85
    if st in {"실패", "막힘", "대기"}:
        return 0
    if st == "진행 중":
        fail = int(card.get("fail_streak") or 0)
        assigned = card.get("assigned_loop")
        cur = state.get("loop")
        run = (state.get("display_status") or state.get("status")) == "running"
        if run and assigned == cur:
            return min(90, max(12, 12 + int(0.78 * (wheel.get("pct") or 0))))
        if fail:
            return min(70, 20 + fail * 20)
        return 25
    return 0


def build_viz(state: dict, cards: list, status_md: str, inbox: list, history: list, commits: list) -> dict:
    timeout_min = _env_int("LOOP_TIMEOUT_MIN", 90)
    sleep_between = _env_int("SLEEP_BETWEEN", 45)
    max_fail = _env_int("MAX_CONSEC_FAIL", 3)
    max_turns = _env_int("MAX_TURNS", 160)
    wheel = wheel_progress(state, timeout_min, sleep_between)
    enriched = []
    for c in cards:
        pct = card_progress(c, state, wheel)
        enriched.append({**c, "progress": pct})
    n = len(enriched) or 1
    board_pct = round(sum(c["progress"] for c in enriched) / n)
    by_status = Counter(c.get("status") or "대기" for c in cards)
    by_diff = Counter(c.get("difficulty") or "중" for c in cards)
    systems = parse_systems(status_md)
    today = state.get("today") or {}
    loops = int(today.get("loops") or 0)
    success = int(today.get("success") or 0)
    fail = int(today.get("fail") or 0)
    today_pct = round(100 * success / loops) if loops else 0
    hist = history[-20:]
    ok = sum(1 for r in hist if r.get("result") == "success")
    hist_pct = round(100 * ok / len(hist)) if hist else 0
    inbox_open = sum(1 for x in inbox if not x.get("done"))
    inbox_done = sum(1 for x in inbox if x.get("done"))
    consec = int(state.get("consec_fail") or 0)
    active = [c for c in enriched if c.get("status") in {"진행 중", "검증 중"}]
    cov_md = COVERAGE.read_text(encoding="utf-8") if COVERAGE.exists() else ""
    plan_md = DEV_PLAN.read_text(encoding="utf-8") if DEV_PLAN.exists() else ""
    game_md = GAME_DESIGN.read_text(encoding="utf-8") if GAME_DESIGN.exists() else ""
    coverage = parse_coverage(cov_md)
    roadmap = parse_roadmap(game_md, coverage.get("chapters") or [])
    priorities = parse_priority_plan(plan_md, enriched)
    return {
        "wheel": wheel,
        "board_pct": board_pct,
        "today_pct": today_pct,
        "hist_pct": hist_pct,
        "systems": systems,
        "coverage": coverage,
        "roadmap": roadmap,
        "priorities": priorities,
        "by_status": {k: by_status.get(k, 0) for k in ["대기", "진행 중", "검증 중", "완료", "실패", "막힘"]},
        "by_diff": {k: by_diff.get(k, 0) for k in ["하", "중", "상"]},
        "cards": enriched,
        "active": active,
        "inbox": {"open": inbox_open, "done": inbox_done, "total": inbox_open + inbox_done},
        "consec": {"n": consec, "max": max_fail, "pct": round(100 * consec / max(1, max_fail))},
        "max_turns": max_turns,
        "timeout_min": timeout_min,
        "commits_n": len(commits),
    }


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


def _loop_from_rel(rel: str):
    m = re.search(r"(?:^|/)loop(\d+)_", rel)
    return int(m.group(1)) if m else None


def _is_dup_shot(name: str) -> bool:
    return bool(DUP_STEM.search(Path(name).stem))


def _capture_index(capture_dir: Path) -> dict:
    out = {}
    if not capture_dir.exists():
        return out
    for p in capture_dir.iterdir():
        if not p.is_file() or p.suffix.lower() not in IMG_SUFFIX:
            continue
        out[f"unity/Captures/{p.name}"] = p
    return out


def collect_reports(status_md: str, cards: list, capture_dir: Path) -> dict:
    """STATUS에 적힌 경로 + 완료 바퀴의 loopN_ 샷만. MCP 중복(-1)과 ulon_anim_*는 제외."""
    files = _capture_index(capture_dir)
    groups: dict[int, dict] = {}

    def ensure(loop: int, title: str = "", note: str = "", system: str = "") -> dict:
        g = groups.setdefault(
            loop,
            {
                "loop": loop,
                "title": title,
                "system": system,
                "note": note,
                "card_id": "",
                "shots": [],
                "_seen": set(),
            },
        )
        if title and not g["title"]:
            g["title"] = title
        if system and not g["system"]:
            g["system"] = system
        if note and len(note) > len(g.get("note") or ""):
            g["note"] = note
        return g

    def add_shot(loop: int, rel: str, caption: str, system: str = "", title: str = "") -> None:
        if loop is None:
            return
        g = ensure(loop, title=title, note=caption, system=system)
        if rel in g["_seen"]:
            return
        g["_seen"].add(rel)
        p = files.get(rel)
        g["shots"].append(
            {
                "path": rel,
                "name": Path(rel).name,
                "caption": caption or Path(rel).stem,
                "missing": p is None,
                "mtime": p.stat().st_mtime if p else 0,
            }
        )

    cited_loops = set()
    systems = parse_systems(status_md or "")
    for row in systems.get("rows") or []:
        note = row.get("note") or ""
        ticks = [f"unity/Captures/{name}" for name in SHOT_TICK.findall(note)]
        loops = [int(x) for x in LOOP_HASH.findall(note)]
        for rel in ticks:
            loop = _loop_from_rel(rel)
            if loop is None and loops:
                loop = loops[0]
            if loop is None:
                continue
            cited_loops.add(loop)
            add_shot(loop, rel, note, system=row.get("name") or "")
        for loop in loops:
            if loop in cited_loops:
                continue
            cited_loops.add(loop)
            for rel, p in files.items():
                if _is_dup_shot(p.name):
                    continue
                m = LOOP_FILE.match(p.name)
                if m and int(m.group(1)) == loop:
                    add_shot(loop, rel, note, system=row.get("name") or "")

    by_loop_card = {}
    for c in cards or []:
        n = c.get("completed_loop")
        if n is None or c.get("status") != "완료":
            continue
        by_loop_card[int(n)] = c

    for n, c in by_loop_card.items():
        ensure(n, title=c.get("title") or "", note=c.get("rationale") or "")
        groups[n]["card_id"] = c.get("id") or ""
        if n in cited_loops and groups[n]["shots"]:
            continue
        for rel, p in files.items():
            if _is_dup_shot(p.name):
                continue
            m = LOOP_FILE.match(p.name)
            if m and int(m.group(1)) == n:
                add_shot(
                    n,
                    rel,
                    groups[n].get("note") or (c.get("rationale") or ""),
                    title=c.get("title") or "",
                )

    out_groups = []
    for loop in sorted(groups, reverse=True):
        g = groups[loop]
        g.pop("_seen", None)
        g["shots"].sort(key=lambda s: (s.get("missing", False), s.get("name") or ""))
        if not g["shots"]:
            continue
        out_groups.append(g)
    n_shots = sum(len(g["shots"]) for g in out_groups)
    n_missing = sum(1 for g in out_groups for s in g["shots"] if s.get("missing"))
    return {
        "note": "STATUS.md에 적힌 샷과 완료 바퀴의 loopN_ 파일만. MCP 중복(-1)과 바퀴 번호 없는 파일은 넣지 않음.",
        "groups": out_groups,
        "shot_count": n_shots,
        "missing_count": n_missing,
    }


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


def pid_alive(pid) -> bool:
    try:
        os.kill(int(pid), 0)
        return True
    except Exception:
        return False


def persist_alpha_ready() -> dict:
    """파일 ok:true 가 현재 준비가 아니다. /ready 실측만."""
    tools = ROOT / "tools"
    if str(tools) not in sys.path:
        sys.path.insert(0, str(tools))
    try:
        import alpha_ready

        return alpha_ready.judge(ROOT / "data" / "alpha_status.json")
    except Exception as e:
        return {
            "ok": False,
            "stale_file": False,
            "file": {"present": False, "ok_claim": False, "body": {}},
            "live": {"ok": False, "http": 0, "body": {}, "error": str(e)},
            "reason": "판정 실패: " + str(e),
        }


def build_state() -> dict:
    state = read_json(STATE, {})
    if state.get("status") == "running" and not pid_alive(state.get("pid")):
        state = dict(state)
        state["display_status"] = "off"
    else:
        state = dict(state)
        state["display_status"] = state.get("status") or "off"
    board = read_json(BOARD, {"updated_at": "", "cards": []})
    cards = board.get("cards") or []
    inbox = parse_inbox()
    status = STATUS.read_text(encoding="utf-8") if STATUS.exists() else ""
    assets = ASSETS.read_text(encoding="utf-8") if ASSETS.exists() else ""
    commits = git_log(30)
    history = history_rows(50)
    blocked = sum(1 for c in cards if c.get("status") == "막힘")
    viz = build_viz(state, cards, status, inbox, history, commits)
    reports = collect_reports(status, cards, CAPTURES)
    return {
        "loop_state": state,
        "board": board,
        "inbox": inbox,
        "status_md": status,
        "assets_md": assets,
        "commits": commits,
        "history": history,
        "previews": asset_previews(),
        "reports": reports,
        "stop": STOP.exists(),
        "warnings": {
            "blocked": blocked,
            "license_unknown": license_unknown_count(assets),
            "no_commit_streak": no_commit_streak(history, commits),
        },
        "alpha_ready": persist_alpha_ready(),
        "max_consec_fail": viz["consec"]["max"],
        "viz": viz,
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

    def _allowed_file(self, p: Path) -> bool:
        for allowed in ((ROOT / "assets").resolve(), CAPTURES.resolve()):
            try:
                p.relative_to(allowed)
                return True
            except ValueError:
                continue
        return False

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
        if not self._allowed_file(p):
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
