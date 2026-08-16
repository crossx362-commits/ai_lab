#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""재와 별 개발 보드 — 진행을 보고 체크하고 INBOX에 요청한다.

    python3 loop/board.py          → http://127.0.0.1:8766
    BOARD_HOST=127.0.0.1           이 기기만
    BOARD_PORT=8766

함대 FleetView(8765)와 별개다. 이 화면은 루프가 읽는 파일만 다룬다:
STATUS.md · DESIGN.md · feedback/INBOX.md · loop/HOLD·STOP·agent.
요청은 INBOX 「대기 중」에 붙고, 다음 이터레이션이 큐보다 먼저 읽는다.
"""
from __future__ import annotations

import hashlib
import json
import os
import re
import subprocess
import sys
import threading
import webbrowser
from datetime import datetime
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import quote, unquote, urlparse

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
PORT = int(os.getenv("BOARD_PORT", "8766"))
CHECKS_PATH = HERE / "board_checks.json"
DECISIONS_PATH = HERE / "board_decisions.json"
PID_PATH = HERE / "loop.pid"

CHOICES = {
    "do": "이걸로 진행 — 큐 맨 위에서 이것만 잡아라",
    "pass": "통과 — 완료로 내리고 다음으로 진행",
    "retry": "부족 — 이 항목을 다시 고쳐라",
    "skip": "건너뛰기 — 완료로 내리지 말고 다음 실행 가능 항목",
}

STATUS = ROOT / "docs" / "STATUS.md"
DESIGN = ROOT / "docs" / "DESIGN.md"
GAME_DESIGN = ROOT / "docs" / "GAME_DESIGN_ASHES_TO_STARS.md"
INBOX = ROOT / "docs" / "feedback" / "INBOX.md"
QA_ROOT = ROOT / "output" / "qa" / "ashes-to-stars"

# 본문에 경로가 없을 때만. 키워드 → 실측 PNG.
_SHOT_HINTS = (
    (("강화", "+15", "대장간 둘째"), "smith_enhance_shots/qa_go_Estate_smith.png"),
    (("가죽", "흉갑", "대장간 첫"), "smith_shots/qa_go_Estate_smith.png"),
    (("편성", "명부"), "party_chrome_shots/qa_go:Party.png"),
    (("HUD", "초상을 키", "스킬을 붙"), "shots/qa_hunt.png"),
    (("쫄 소환", "소환피해"), "shots/qa_boss_summon_on.png"),
    (("V3", "한 판 종단", "보스 나머지"), "boss_run_shots/qa_boss.png"),
    (("전직", "1차", "슬롯"), "first_advancement/광전사_normal/qa_advancement.png"),
    (("2차", "각성", "초필"), "shots/qa_second_advancement.png"),
    (("실루엣", "매트릭스", "swarmer", "몹 AI"), "shots/mob_family_matrix.png"),
    (("V4", "삭제 루프", "와이프"), "v4_wipe_shots/qa_go:Result.png"),
    (("필드·탑", "헤더", "버튼 3상태"), "ui_chrome_shots/qa_go:Field.png"),
    (("아틀라스", "아이콘"), "ui_icon_shots/qa_go:Character.png"),
    (("수비", "월드맵"), "defense_shots/qa_go:WorldMap.png"),
    (("영지",), "shots/qa_estate.png"),
    (("캐릭터",), "char_sprite_shots/qa_go:Character.png"),
    (("던전",), "shots/qa_dungeon.png"),
    (("사냥",), "shots/qa_hunt.png"),
)

# 보드 커밋이 쓸 수 있는 경로. 시크릿·유니티 캐시·루프 로그는 절대 안 넣는다.
_COMMIT_ALLOW = (
    ".gitignore",
    "docs/",
    "loop/board.py",
    "loop/board.html",
    "loop/test_board.py",
    "loop/loop.sh",
    "projects/ashes-to-stars/art/",
    "projects/ashes-to-stars/unity/Assets/",
    "projects/ashes-to-stars/unity/ProjectSettings/",
    "projects/ashes-to-stars/CLAUDE.md",
    "tools/",
)
_COMMIT_DENY = (
    ".env",
    ".env.encrypted",
    "/Library/",
    "/Temp/",
    "/Logs/",
    "loop/logs/",
    "__pycache__",
    "unity_meas/",
)


def _read(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8")
    except OSError:
        return ""


def item_id(text: str) -> str:
    return hashlib.sha1(text.strip().encode("utf-8")).hexdigest()[:12]


def parse_queue(status: str) -> list[dict]:
    """STATUS 「다음 할 일」 번호 목록."""
    m = re.search(r"^## 다음 할 일[^\n]*\n", status, re.M)
    if not m:
        return []
    rest = status[m.end():]
    end = re.search(r"^## |\n최종 갱신:", rest)
    block = rest[: end.start()] if end else rest
    out = []
    for line in block.splitlines():
        parsed = parse_numbered_item(line)
        if parsed:
            out.append(parsed)
    return out


def parse_numbered_item(line: str) -> dict | None:
    """`1. **제목** (메모) — 설명` 또는 대시 없는 한 줄."""
    hit = re.match(
        r"^(\d+)\.\s+\*\*(.+?)\*\*(?:\s*\([^)]*\))?\s*(?:[—–-]\s*(.*))?$",
        line.strip(),
    )
    if not hit:
        return None
    title = hit.group(2).strip()
    detail = (hit.group(3) or "").strip()
    return {
        "n": int(hit.group(1)),
        "id": item_id(title),
        "title": title,
        "detail": detail,
        "human": needs_human(title, detail),
    }


def parse_queue_table(status: str) -> list[dict]:
    """STATUS 하단 「다음 할 일 큐」 표. 취소선(완료) 행은 뺀다."""
    m = re.search(r"^## 다음 할 일 큐[^\n]*\n", status, re.M)
    if not m:
        return []
    rest = status[m.end():]
    end = re.search(r"^## |\n### ", rest)
    block = rest[: end.start()] if end else rest
    out = []
    for line in block.splitlines():
        if "| ~~" in line or not line.startswith("|"):
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) < 2 or not cells[0].isdigit():
            continue
        body = cells[1]
        th = re.search(r"\*\*(.+?)\*\*", body)
        title = (th.group(1) if th else body).strip()
        if not title or title in ("항목", "#"):
            continue
        detail = cells[2] if len(cells) > 2 else body
        out.append({
            "n": int(cells[0]),
            "id": item_id("table:" + title),
            "title": title[:160],
            "detail": re.sub(r"<[^>]+>", "", detail)[:240],
            "human": needs_human(title, detail),
        })
    return out


def parse_queue_table_all(status: str) -> list[dict]:
    """큐 표 전체. 취소선 행도 남겨 완료/미완 비율을 센다."""
    m = re.search(r"^## 다음 할 일 큐[^\n]*\n", status, re.M)
    if not m:
        return []
    rest = status[m.end():]
    end = re.search(r"^## |\n### ", rest)
    block = rest[: end.start()] if end else rest
    out = []
    for line in block.splitlines():
        if not line.startswith("|"):
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) < 2:
            continue
        raw_n = re.sub(r"~+", "", cells[0]).strip()
        if not raw_n.isdigit():
            continue
        done = "| ~~" in line or cells[0].startswith("~~")
        body = re.sub(r"~+", "", cells[1])
        th = re.search(r"\*\*(.+?)\*\*", body)
        title = (th.group(1) if th else body).strip()
        title = re.sub(r"\s*✅.*$", "", title).strip()
        if not title or title in ("항목", "#"):
            continue
        detail = re.sub(r"~+", "", cells[2] if len(cells) > 2 else body)
        detail = re.sub(r"<[^>]+>", "", detail)
        blob = title + " " + detail
        blocked = (not done) and any(
            k in blob for k in ("소비 시스템이 없어", "유보", "지금 넣으면 오펀")
        )
        out.append({
            "n": raw_n,
            "title": title[:160],
            "detail": detail[:240],
            "done": done,
            "blocked": blocked,
        })
    return out


def parse_weeks(game_design: str) -> list[dict]:
    """원장 §21-4 주차별 표."""
    m = re.search(
        r"### 21-4\. 주차별 진행.*?\n\| 주차 \|[^\n]*\n\|[-| :]+\|\n(.*?)(?=\n\n|\n### |\Z)",
        game_design,
        re.S,
    )
    if not m:
        return []
    out = []
    for line in m.group(1).splitlines():
        if not line.startswith("|"):
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) < 3:
            continue
        week = re.sub(r"[~*]+", "", cells[0]).strip()
        if not re.match(r"W\d", week):
            continue
        goal = re.sub(r"[~*]+", "", cells[1]).strip()
        gate = cells[2]
        if "✅" in cells[0] + gate or ("완료" in gate and "미완" not in gate and "미통과" not in gate):
            state, pct = "done", 100
        elif any(k in gate for k in ("부분", "미완", "대기", "미통과")):
            state, pct = "partial", 50
        else:
            state, pct = "open", 0
        out.append({
            "id": week.split()[0],
            "label": week,
            "goal": goal[:48],
            "gate": re.sub(r"<[^>]+>", "", gate)[:80],
            "state": state,
            "pct": pct,
        })
    return out


def _v2_passed(status: str, decisions: dict) -> bool:
    if "V2 사람 판정 → 통과" in status:
        return True
    return any(
        str(v.get("title") or "").startswith("V2") and v.get("choice") == "pass"
        for v in (decisions or {}).values()
    )


def _v3_closed(design: str) -> bool:
    return any(m.get("done") and "V3" in m["title"] for m in parse_milestones(design))


def resource_bars() -> list[dict]:
    res = ROOT / "projects" / "ashes-to-stars" / "unity" / "Assets" / "Resources"
    if not res.is_dir():
        return []
    out = []
    for folder in ("sprites", "FX", "ui", "bg", "props", "ground"):
        d = res / folder
        if not d.is_dir():
            continue
        pngs = list(d.rglob("*.png"))
        kb = sum(p.stat().st_size for p in pngs) / 1024
        out.append({"id": folder, "n": len(pngs), "kb": round(kb, 1)})
    return out


def commit_spark(days: int = 14) -> list[dict]:
    try:
        raw = subprocess.check_output(
            [
                "git", "log", f"--since={days}.days",
                "--pretty=%ad", "--date=short",
                "--", "projects/ashes-to-stars", "docs/STATUS.md",
                "docs/DESIGN.md", "docs/GAME_DESIGN_ASHES_TO_STARS.md",
            ],
            cwd=ROOT, text=True, encoding="utf-8", timeout=8,
        )
    except (subprocess.CalledProcessError, OSError, subprocess.TimeoutExpired):
        return []
    counts: dict[str, int] = {}
    for line in raw.splitlines():
        day = line.strip()
        if day:
            counts[day] = counts.get(day, 0) + 1
    from datetime import timedelta
    today = datetime.now().date()
    out = []
    for i in range(days - 1, -1, -1):
        day = (today - timedelta(days=i)).isoformat()
        out.append({"day": day[5:], "n": counts.get(day, 0)})
    return out


def progress_charts(status: str | None = None, design: str | None = None,
                    game_design: str | None = None, decisions: dict | None = None) -> dict:
    """보드·이미지 공통. 숫자는 문서·파일·git에서만 온다."""
    status = status if status is not None else _read(STATUS)
    design = design if design is not None else _read(DESIGN)
    game_design = game_design if game_design is not None else _read(GAME_DESIGN)
    decisions = decisions if decisions is not None else load_decisions()

    v2 = _v2_passed(status, decisions)
    v3 = _v3_closed(design)
    rows = parse_queue_table_all(status)
    done_n = sum(1 for r in rows if r["done"])
    blocked_n = sum(1 for r in rows if r["blocked"])
    open_n = sum(1 for r in rows if not r["done"] and not r["blocked"])

    weeks = parse_weeks(game_design)
    for w in weeks:
        if w["id"] == "W2" and v2:
            w.update(state="done", pct=100, note="오너 통과")
        elif w["id"] == "W4" and v3:
            w.update(state="done", pct=100, note="V3 한 판 종단")
        elif w["id"] == "W6":
            w.update(state="open", pct=0, note="V4 70% 사람 관문")

    gates = [
        {"id": "V1", "label": "V1 성능", "pct": 100, "note": "W1 통과 · DOTS 불필요"},
        {"id": "V2", "label": "V2 조작감", "pct": 100 if v2 else 0,
         "note": "오너 보드 통과" if v2 else "사람 판정 남음"},
        {"id": "V3", "label": "V3 보스 한 판", "pct": 100 if v3 else 0,
         "note": "HP·페이즈·처치·층" if v3 else "한 판 미연결"},
        {"id": "V4a", "label": "V4 패배→삭제 경계", "pct": 100, "note": "자동 경계 닫힘"},
        {"id": "V4b", "label": "V4 외부 테스터 70%", "pct": 0,
         "note": "사람 관문 · 자동 완료 금지"},
    ]
    proto_done = sum(1 for g in gates if g["pct"] >= 100)
    roadmap = [
        {"id": "0", "label": "0. 프로토타입",
         "pct": round(100 * proto_done / max(len(gates), 1)),
         "note": f"관문 {proto_done}/{len(gates)} · 남은 건 V4 70%"},
        {"id": "1", "label": "1. 수직 슬라이스", "pct": 0, "note": "V4 이후"},
        {"id": "2", "label": "2. 온라인 기반", "pct": 0, "note": "V4 이후"},
        {"id": "3", "label": "3. 콘텐츠 확장", "pct": 0, "note": "V4 이후"},
        {"id": "4", "label": "4. 폴리시·베타", "pct": 0, "note": "V4 이후"},
        {"id": "5", "label": "5. 출시 준비", "pct": 0, "note": "V4 이후"},
    ]
    return {
        "gates": gates,
        "weeks": weeks,
        "queue": {"done": done_n, "open": open_n, "blocked": blocked_n, "total": len(rows)},
        "art": resource_bars(),
        "commits": commit_spark(),
        "roadmap": roadmap,
    }


def write_progress_png(path: Path, charts: dict | None = None) -> Path:
    """같은 집계를 한 장으로 저장한다. 보드와 숫자가 같아야 한다."""
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    charts = charts or progress_charts()
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    for name in ("Apple SD Gothic Neo", "AppleGothic", "Noto Sans CJK KR"):
        try:
            plt.rcParams["font.family"] = name
            break
        except Exception:
            continue
    plt.rcParams["axes.unicode_minus"] = False
    bg, ink, gold, ok, hold, muted = "#12100e", "#efe8d8", "#d4a24c", "#6cba7a", "#e0a050", "#9a9184"
    fig, axes = plt.subplots(2, 3, figsize=(13.2, 7.0), facecolor=bg)
    fig.suptitle("재와 별 · 개발 현황", color=gold, fontsize=16, fontweight="bold", y=0.98)

    def style(ax, title):
        ax.set_facecolor("#1b1814")
        ax.set_title(title, color=gold, fontsize=11, loc="left", pad=8)
        ax.tick_params(colors=muted, labelsize=9)
        for spine in ax.spines.values():
            spine.set_color("#2e2922")

    ax = axes[0, 0]
    style(ax, "프로토타입 관문")
    gates = charts["gates"]
    colors = [ok if g["pct"] >= 100 else hold if g["pct"] > 0 else "#3a342c" for g in gates]
    ax.barh([g["label"] for g in gates][::-1], [g["pct"] for g in gates][::-1], color=colors[::-1], height=0.55)
    ax.set_xlim(0, 100)
    ax.set_xlabel("%", color=muted)

    ax = axes[0, 1]
    style(ax, "로드맵")
    road = charts["roadmap"]
    ax.barh([r["label"] for r in road][::-1], [r["pct"] for r in road][::-1],
            color=[ok if r["pct"] >= 100 else gold if r["pct"] > 0 else "#3a342c" for r in road][::-1], height=0.55)
    ax.set_xlim(0, 100)

    ax = axes[0, 2]
    style(ax, "주차별 W1–W6")
    weeks = charts["weeks"]
    ax.barh([w["id"] for w in weeks][::-1], [w["pct"] for w in weeks][::-1],
            color=[ok if w["pct"] >= 100 else hold if w["pct"] > 0 else "#3a342c" for w in weeks][::-1], height=0.55)
    ax.set_xlim(0, 100)

    ax = axes[1, 0]
    style(ax, f"STATUS 큐 {charts['queue']['total']}항")
    q = charts["queue"]
    ax.barh(["큐"], [q["done"]], color=ok, height=0.35, label=f"완료 {q['done']}")
    ax.barh(["큐"], [q["open"]], left=[q["done"]], color=gold, height=0.35, label=f"열림 {q['open']}")
    ax.barh(["큐"], [q["blocked"]], left=[q["done"] + q["open"]], color=hold, height=0.35,
            label=f"선행 막힘 {q['blocked']}")
    ax.set_xlim(0, max(q["total"], 1))
    ax.legend(facecolor="#1b1814", edgecolor="#2e2922", labelcolor=ink, fontsize=8)
    ax.set_yticks([])

    ax = axes[1, 1]
    style(ax, "Resources PNG")
    art = charts["art"]
    if art:
        ax.barh([a["id"] for a in art][::-1], [a["n"] for a in art][::-1], color=gold, height=0.5)

    ax = axes[1, 2]
    style(ax, "14일 게임 커밋")
    spark = charts["commits"]
    if spark:
        ax.fill_between(range(len(spark)), [s["n"] for s in spark], color=gold, alpha=0.35)
        ax.plot(range(len(spark)), [s["n"] for s in spark], color=gold, linewidth=1.6)
        step = max(1, len(spark) // 4)
        ax.set_xticks(list(range(0, len(spark), step)))
        ax.set_xticklabels([spark[i]["day"] for i in range(0, len(spark), step)])
    ax.set_ylabel("커밋", color=muted)

    fig.tight_layout(rect=(0, 0, 1, 0.95))
    fig.savefig(path, dpi=140, facecolor=bg)
    plt.close(fig)
    return path


def parse_now_list(design: str) -> list[dict]:
    """원장 「지금 당장 할 일」 번호 목록."""
    m = re.search(r"^### 지금 당장 할 일[^\n]*\n", design, re.M)
    if not m:
        return []
    rest = design[m.end():]
    end = re.search(r"^## ", rest, re.M)
    block = rest[: end.start()] if end else rest
    out = []
    for line in block.splitlines():
        parsed = parse_numbered_item(line)
        if parsed:
            parsed["id"] = item_id("now:" + parsed["title"])
            out.append(parsed)
    return out


def needs_human(title: str, detail: str = "") -> bool:
    """오너가 창을 열거나 게임의 뼈대를 바꿀 때만 사람 관문.

    오너 2026-08-16 「진형현황 내 선택은 정말 중요한거 아니면 알아서」:
    대장간·레벨업·6초 캐스트·UI 배선은 선택이 아니다. 맨낱말 '사람'은
    인수인계 문장까지 잡아 카드를 11장 만든다.
    """
    text = f"{title} {detail}"
    if any(k in text for k in ("외부 테스터", "외부 판정", "70%")):
        return False
    if "V2" in text and any(k in text for k in ("사람", "체감", "피했다")):
        return True
    if "오너 선택" in text:
        return True
    return any(k in text for k in ("가챠", "사망 삭제 폐지", "캐릭터 삭제 폐지"))


def parse_results(status: str, limit: int = 8) -> list[dict]:
    """최근 이터 결과 블록. 최신이 앞."""
    found = []
    for m in re.finditer(
        r"> \*\*(이번 이터 결과|이전 이터 결과)(?:\([^)]*\))?:\s*([^*]+?)\*\*\s*(.*?)\s*(?=\n> \*\*|\n최종 갱신:|\n## |\Z)",
        status,
        re.S,
    ):
        kind, title, body = m.group(1), m.group(2).strip(" ."), m.group(3)
        body = re.sub(r"^>\s?", "", body, flags=re.M).strip()
        if not title:
            title = body.split("\n", 1)[0].strip(" .")
        commit = ""
        cm = re.search(r"`([0-9a-f]{8,40})`", body)
        if cm:
            commit = cm.group(1)[:8]
        found.append({
            "id": item_id(title + commit),
            "kind": kind,
            "title": title[:160],
            "body": body[:700],
            "commit": commit,
        })
    # 문서 위가 최신
    return found[:limit]


_SHOT_PATH = re.compile(
    r"(?:output/qa/ashes-to-stars/)?"
    r"((?:[\w.\-]+/)*[\w.:\-]+\.(?:png|jpe?g|webp))",
    re.I,
)
_SHOT_MIME = {
    ".png": "image/png",
    ".jpg": "image/jpeg",
    ".jpeg": "image/jpeg",
    ".webp": "image/webp",
}


def shot_file(rel: str) -> Path | None:
    rel = (rel or "").replace("\\", "/").lstrip("/")
    if not rel or ".." in rel.split("/"):
        return None
    full = (QA_ROOT / rel).resolve()
    try:
        full.relative_to(QA_ROOT.resolve())
    except ValueError:
        return None
    return full if full.is_file() else None


def read_shot(rel: str) -> tuple[bytes | None, str, str]:
    """보드가 보여줄 QA PNG만. 루트 밖·비이미지는 거절."""
    full = shot_file(rel)
    if full is None:
        return None, "", "없는 샷"
    ctype = _SHOT_MIME.get(full.suffix.lower(), "")
    if not ctype:
        return None, "", "이미지가 아님"
    try:
        return full.read_bytes(), ctype, ""
    except OSError as e:
        return None, "", str(e)


def mentioned_shots(text: str) -> list[str]:
    """본문에 적힌 PNG만. '옛 샷' 비교용 경로는 빼 둔다."""
    out: list[str] = []
    seen: set[str] = set()
    text = text or ""
    for m in _SHOT_PATH.finditer(text):
        rel = m.group(1)
        line_at = text.rfind("\n", 0, m.start()) + 1
        line = text[line_at:text.find("\n", m.end()) if text.find("\n", m.end()) >= 0 else len(text)]
        if "옛" in line[: m.start() - line_at]:
            continue
        if "legacy" in rel.lower():
            continue
        if rel in seen or shot_file(rel) is None:
            continue
        seen.add(rel)
        out.append(rel)
    return out


def hinted_shots(title: str, detail: str = "") -> list[str]:
    """경로가 없을 때만. 제목만 본다 — 본문 '안 연 것'이 V4·대장간을 훔친다."""
    blob = title or ""
    out: list[str] = []
    for keys, rel in _SHOT_HINTS:
        if any(k in blob for k in keys) and shot_file(rel) is not None:
            if rel not in out:
                out.append(rel)
            break
    return out


def summarize_done(body: str, limit: int = 220) -> str:
    skip = ("화면", "TDD", "네거티브", "정직한", "코드 `", "코드:", "검증")
    parts: list[str] = []
    for line in (body or "").splitlines():
        t = re.sub(r"^[-*>\s]+", "", line).strip()
        t = re.sub(r"\*\*(.+?)\*\*", r"\1", t)
        t = t.replace("**:", ":").replace("**", "")
        if not t or t.startswith(skip):
            continue
        parts.append(t)
        if sum(len(p) for p in parts) >= limit:
            break
    text = " ".join(parts)
    return text[:limit].rstrip()


def _title_seen(title: str, seen: set[str]) -> bool:
    for s in seen:
        if s in title or title in s:
            return True
    return False


def completed_posts(status: str, limit: int = 12) -> list[dict]:
    """완료된 개발 — STATUS 근거 + 실측 샷. 끝난 행만."""
    posts: list[dict] = []
    seen: set[str] = set()

    def add(title: str, detail: str, commit: str = "", extra: str = "") -> None:
        title = (title or "").strip()
        if not title or _title_seen(title, seen):
            return
        shots = mentioned_shots(detail + " " + extra) or hinted_shots(title, detail)
        posts.append({
            "id": item_id("done:" + title + commit),
            "title": title[:160],
            "detail": summarize_done(detail) or (detail or "")[:220],
            "commit": commit,
            "shots": [
                {"path": rel, "url": "/shots/" + quote(rel, safe="/")}
                for rel in shots[:3]
            ],
        })
        seen.add(title)

    for it in parse_results(status, limit=24):
        add(it["title"], it.get("body") or "", it.get("commit") or "")
    for row in parse_queue_table_all(status):
        if not row.get("done"):
            continue
        add(row["title"], row.get("detail") or "")
    return posts[:limit]


def parse_milestones(design: str) -> list[dict]:
    m = re.search(r"### 현재 핵심 미완.*?\n\n(.*?)(?=\n## |\Z)", design, re.S)
    if not m:
        return []
    out = []
    for line in m.group(1).splitlines():
        hit = re.match(r"^- \*\*(.+?)\*\*(.*)$", line.strip())
        if not hit:
            continue
        title, rest = hit.group(1).strip(), hit.group(2).strip(" —-")
        out.append({
            "id": item_id(title),
            "title": title,
            "detail": rest,
            "done": "✅" in line,
            "human": (not ("✅" in line)) and needs_human(title, rest),
        })
    return out


def parse_inbox(inbox: str) -> dict:
    waiting, done = [], []

    def section(name: str) -> str:
        m = re.search(rf"^## {re.escape(name)}[^\n]*\n", inbox, re.M)
        if not m:
            return ""
        rest = inbox[m.end():]
        nxt = re.search(r"^## ", rest, re.M)
        return rest[: nxt.start()] if nxt else rest

    def headings(block: str, dest: list) -> None:
        parts = re.split(r"^### ", block, flags=re.M)
        for part in parts[1:]:
            lines = part.strip().splitlines()
            if not lines:
                continue
            title = lines[0].strip()
            body = "\n".join(lines[1:]).strip()
            dest.append({
                "id": item_id(title),
                "title": title[:180],
                "body": body[:500],
            })

    headings(section("대기 중"), waiting)
    headings(section("처리됨 — 최신") or section("처리됨"), done)
    return {"waiting": waiting[:12], "done": done[:8]}


def parse_updated(status: str) -> str:
    m = re.search(r"^최종 갱신:\s*(.+)$", status, re.M)
    return m.group(1).strip() if m else ""


def load_checks() -> dict:
    try:
        data = json.loads(CHECKS_PATH.read_text(encoding="utf-8"))
        return data if isinstance(data, dict) else {}
    except (OSError, json.JSONDecodeError):
        return {}


def load_decisions() -> dict:
    try:
        data = json.loads(DECISIONS_PATH.read_text(encoding="utf-8"))
        return data if isinstance(data, dict) else {}
    except (OSError, json.JSONDecodeError):
        return {}


def save_decisions(data: dict) -> None:
    DECISIONS_PATH.write_text(
        json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )


def pending_choices(queue: list[dict], milestones: list[dict],
                    decisions: dict, extra: list[dict] | None = None) -> list[dict]:
    seen_titles = set()
    out = []
    sources = [(queue, "queue"), (extra or [], "plan"), (milestones, "milestone")]
    for src, kind in sources:
        for it in src:
            if it.get("done"):
                continue
            if not it.get("human"):
                continue
            prev = decisions.get(it["id"]) or {}
            if kind != "queue" and prev.get("choice") in ("pass", "skip"):
                continue
            key = re.sub(r"\s+", "", it["title"])
            if key in seen_titles:
                continue
            seen_titles.add(key)
            out.append({
                "id": it["id"],
                "title": it["title"],
                "detail": it.get("detail") or "",
                "kind": kind,
                "human": bool(it.get("human")),
                "last": prev.get("choice") or "",
            })
    return out


def rewrite_queue(status: str, items: list[dict], note: str = "") -> str:
    m = re.search(r"^## 다음 할 일[^\n]*\n", status, re.M)
    if not m:
        return status
    rest = status[m.end():]
    end = re.search(r"^## |\n최종 갱신:", rest)
    cut = end.start() if end else len(rest)
    block = rest[:cut]
    prose = []
    for line in block.splitlines():
        if re.match(r"^\d+\.\s+\*\*", line.strip()):
            continue
        if line.startswith("> **오너 선택"):
            continue
        prose.append(line)
    lines = [f"{i}. **{it['title']}** — {it['detail']}" for i, it in enumerate(items, 1)]
    body = "\n".join(lines)
    extra = "\n".join(prose).strip()
    note_line = f"\n{note.strip()}\n" if note.strip() else ""
    new_block = (body + "\n\n" + extra + "\n" + note_line).rstrip() + "\n\n"
    return status[: m.end()] + new_block + rest[cut:]


def apply_decision(item_id: str, choice: str, note: str = "") -> dict:
    if choice not in CHOICES:
        raise ValueError("이걸로 진행/완료/다시/보류 중에서 고르라")
    status = _read(STATUS)
    queue = parse_queue(status)
    miles = parse_milestones(_read(DESIGN))
    catalog = queue + parse_queue_table(status) + parse_now_list(_read(GAME_DESIGN)) + miles
    item = next((x for x in catalog if x["id"] == item_id), None)
    if not item:
        raise ValueError("그 항목을 찾지 못했다")
    stamp = datetime.now().strftime("%Y-%m-%d %H:%M")
    label = {"do": "이걸로 진행", "pass": "통과", "retry": "부족·다시", "skip": "보류"}[choice]
    body = (
        f"오너가 보드에서 **{label}**를 골랐다.\n"
        f"대상: {item['title']}\n"
        f"{CHOICES[choice]}.\n"
    )
    if note.strip():
        body += f"메모: {note.strip()[:400]}\n"
    if choice == "do":
        body += "큐 맨 위에서 이 항목만 잡아라. 다른 일을 먼저 하지 마라."
    elif choice == "pass":
        body += "이 항목을 완료로 내리고 큐의 다음 항목으로 진행하라. 자동검사로 통과를 선언한 것이 아니다."
    elif choice == "retry":
        body += "완료로 내리지 마라. 같은 항목을 고쳐서 다시 올려라."
    else:
        body += "완료로 내리지 마라. 이 항목은 보류하고 다음 실행 가능 항목을 잡아라."
    write_request(f"오너 판정 — {item['title']} ({label})", body)

    if choice == "do":
        remain = [q for q in queue if q["id"] != item_id]
        remain.insert(0, item)
        marker = f"> **오너 선택({stamp}): {item['title']} → 다음 할 일.**"
        STATUS.write_text(rewrite_queue(_read(STATUS), remain, marker), encoding="utf-8")
    elif choice in ("pass", "skip"):
        remain = [q for q in queue if q["id"] != item_id]
        marker = (
            f"> **오너 선택({stamp}): {item['title']} → {label}.**"
            + (f" {note.strip()[:80]}" if note.strip() else "")
        )
        STATUS.write_text(rewrite_queue(_read(STATUS), remain, marker), encoding="utf-8")

    rec = load_decisions()
    rec[item_id] = {
        "title": item["title"],
        "choice": choice,
        "at": stamp,
        "note": note.strip()[:200],
    }
    save_decisions(rec)
    return {"id": item_id, "title": item["title"], "choice": choice, "at": stamp}


def save_checks(data: dict) -> None:
    CHECKS_PATH.write_text(
        json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )


def write_request(title: str, body: str) -> str:
    title = re.sub(r"\s+", " ", title).strip()
    body = body.strip()
    if not title:
        raise ValueError("제목이 비어 있다")
    if len(title) > 80:
        title = title[:80]
    if len(body) > 4000:
        body = body[:4000]
    text = _read(INBOX) or (
        "# 오너 지시함 (INBOX) — 최우선\n\n## 대기 중\n"
    )
    stamp = datetime.now().strftime("%Y-%m-%d %H:%M")
    block = f"\n### 📌 {title} (오너, {stamp})\n\n{body or '(본문 없음)'}\n\n"
    marker = "## 대기 중"
    idx = text.find(marker)
    if idx < 0:
        text = text.rstrip() + "\n\n## 대기 중\n" + block
    else:
        nl = text.find("\n", idx)
        at = nl + 1 if nl >= 0 else idx + len(marker)
        text = text[:at] + block + text[at:]
    INBOX.parent.mkdir(parents=True, exist_ok=True)
    INBOX.write_text(text, encoding="utf-8")
    return stamp


def stuck_items(status: str, loop: dict | None = None,
                log_text: str | None = None) -> list[dict]:
    """루프가 멈췄거나, 선행이 없어 큐가 막힌 것만. 끝난 행은 안 올린다.

    오너가 일부러 밀어둔 것(외부 테스터·V4 70%)은 내 선택과 같이 빼 둔다.
    실패는 마지막 결과가 ❌일 때만 — 그 뒤 ✅가 있으면 이미 지나간 자리.
    """
    out: list[dict] = []
    flags = loop or {}
    for reason in flags.get("blocked") or []:
        out.append({
            "kind": "loop",
            "title": reason,
            "detail": "루프가 안 돈다. 계속 진행으로 푼다.",
        })
    main = log_text if log_text is not None else _read(HERE / "loop_main.log")
    last_fail = None
    last_fail_at = -1
    last_ok_at = -1
    for m in re.finditer(r"❌ #(\d+)", main):
        last_fail, last_fail_at = m.group(1), m.start()
    for m in re.finditer(r"✅ #(\d+)", main):
        last_ok_at = m.start()
    if last_fail is not None and last_fail_at > last_ok_at:
        out.append({
            "kind": "fail",
            "title": f"이터 #{last_fail} 실패",
            "detail": "루프 로그 마지막 실패. 원인 고치기 전에는 같은 자리를 반복한다.",
        })
    seen = set()
    for row in parse_queue_table_all(status):
        if not row.get("blocked"):
            continue
        key = row["title"]
        if key in seen:
            continue
        seen.add(key)
        out.append({
            "kind": "blocked",
            "title": row["title"],
            "detail": row.get("detail") or "선행 시스템이 없다",
        })
    for it in parse_queue(status):
        blob = f"{it['title']} {it.get('detail') or ''}"
        if any(k in blob for k in ("외부 테스터", "외부 판정", "70%")):
            continue
        if not any(k in blob for k in ("보류", "막힘", "거래서버", "유보", "완료로 내리지")):
            continue
        if it["title"] in seen:
            continue
        seen.add(it["title"])
        out.append({
            "kind": "parked",
            "title": it["title"],
            "detail": it.get("detail") or "",
        })
    return out


def loop_flags() -> dict:
    agent = _read(HERE / "agent").strip() or os.getenv("LOOP_AGENT", "grok")
    last_log = ""
    main = ROOT / "loop" / "loop_main.log"
    if main.is_file():
        lines = main.read_text(encoding="utf-8", errors="replace").splitlines()
        last_log = "\n".join(lines[-16:])
    latest_iter = ""
    log_dir = HERE / "logs"
    if log_dir.is_dir():
        iters = sorted(log_dir.glob("iter_*.log"), key=lambda p: p.stat().st_mtime)
        if iters:
            latest_iter = iters[-1].name
    hold = (HERE / "HOLD").exists()
    stop = (HERE / "STOP").exists()
    running = bool(find_loop_pids())
    blocked = []
    if stop:
        blocked.append("STOP")
    if hold:
        blocked.append("HOLD")
    if not running:
        blocked.append("루프 꺼짐")
    return {
        "agent": agent,
        "hold": hold,
        "stop": stop,
        "running": running,
        "blocked": blocked,
        "latest_iter": latest_iter,
        "log_tail": last_log,
        "now": current_work(running=running, hold=hold, stop=stop,
                            latest_iter=latest_iter, main_log=last_log),
    }


def _latest_iter_path() -> Path | None:
    log_dir = HERE / "logs"
    if not log_dir.is_dir():
        return None
    iters = sorted(log_dir.glob("iter_*.log"), key=lambda p: p.stat().st_mtime)
    return iters[-1] if iters else None


def infer_now_title(log_text: str, queue: list[dict], inbox_waiting: list[dict]) -> str:
    lines = [ln.strip() for ln in log_text.splitlines() if ln.strip()]
    keys = ("잡습", "잡고", "구현", "슬라이스", "고칩", "검증", "생성", "RED", "PASS", "커밋")
    for ln in reversed(lines):
        if any(k in ln for k in keys) and not ln.startswith("ERROR"):
            return ln[:180]
    if queue:
        return "큐 · " + queue[0]["title"]
    if inbox_waiting:
        title = inbox_waiting[0]["title"]
        title = re.sub(r"^[📌⭐✅]\s*", "", title)
        return "INBOX · " + title[:80]
    return "이터레이션 진행 중"


def current_work(running: bool, hold: bool, stop: bool,
                 latest_iter: str, main_log: str) -> dict:
    """지금 루프가 손에 든 일. 끝난 이터는 작업 중으로 안 속인다."""
    full_main = _read(HERE / "loop_main.log")
    latest = _latest_iter_path()
    iter_text = _read(latest) if latest else ""
    number, started = "", ""
    finished = False
    if latest:
        last = None
        for m in re.finditer(
            r"▶ 이터레이션 #(\d+)\s+(\d{2}:\d{2}:\d{2})\s+→\s+(\S+)",
            full_main,
        ):
            if m.group(3).endswith(latest.name):
                last = m
        if last:
            number, started = last.group(1), last.group(2)
            after = full_main[last.end():]
            finished = bool(re.search(
                rf"(?:✅|❌|⚠️|⏸)\s+#{{0,1}}{number}\b", after
            ))
    generating = _read(ROOT / "projects" / "ashes-to-stars" / "art" / ".generating").strip()
    queue = parse_queue(_read(STATUS))
    inbox = parse_inbox(_read(INBOX)).get("waiting") or []
    if stop:
        phase = "STOP"
        title = "정지됨 — 계속 진행을 누르면 다시 돈다"
    elif hold:
        phase = "HOLD"
        title = "HOLD — 다른 세션이 끝나는 중"
    elif running and latest and not finished:
        phase = "작업 중"
        title = infer_now_title(iter_text, queue, inbox)
    elif running:
        phase = "대기"
        title = "다음 이터를 기다리는 중"
    else:
        phase = "꺼짐"
        title = "루프가 꺼져 있다"
    if generating and phase == "작업 중":
        title = "아트 생성 · " + generating.splitlines()[0][:80]
    activity = iter_text.strip().splitlines()[-10:] if (phase == "작업 중" and iter_text) else []
    return {
        "phase": phase,
        "title": title,
        "iter": latest.name if latest else "",
        "number": number,
        "started": started,
        "generating": generating,
        "activity": activity,
    }


def find_loop_pids() -> list[int]:
    """loop.sh 본체만. 'loop.sh' 단독 검색은 래퍼·프롬프트에 걸린다."""
    try:
        raw = subprocess.check_output(
            ["ps", "-ax", "-o", "pid=,command="],
            text=True,
            encoding="utf-8",
            timeout=4,
        )
    except (OSError, subprocess.TimeoutExpired, subprocess.CalledProcessError):
        return []
    pids = []
    for line in raw.splitlines():
        line = line.strip()
        if "board.py" in line or "test_board" in line:
            continue
        # argv가 실제로 loop.sh 를 실행하는 줄만
        if not re.search(r"(?:^|\s)(?:/bin/)?(?:bash|sh)\s+\S*loop(?:/loop)?\.sh(?:\s|$)", line):
            continue
        try:
            pids.append(int(line.split(None, 1)[0]))
        except ValueError:
            continue
    return sorted(set(pids))


def start_loop() -> int:
    if find_loop_pids():
        return find_loop_pids()[0]
    log_path = HERE / "loop_main.log"
    with open(log_path, "a", encoding="utf-8") as log:
        log.write(f"\n▶ 보드에서 재개 {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n")
        log.flush()
        proc = subprocess.Popen(
            ["bash", str(HERE / "loop.sh")],
            cwd=str(ROOT),
            stdout=log,
            stderr=subprocess.STDOUT,
            start_new_session=True,
        )
    PID_PATH.write_text(str(proc.pid) + "\n", encoding="utf-8")
    return proc.pid


def resume_work() -> dict:
    """중단(HOLD/STOP/꺼짐)을 풀고 루프를 다시 돌린다."""
    set_flag("HOLD", False)
    set_flag("STOP", False)
    pids = find_loop_pids()
    started = False
    if not pids:
        pid = start_loop()
        pids = [pid]
        started = True
    return {"started": started, "pids": pids, "loop": loop_flags()}


def commit_allowed(path: str) -> bool:
    # lstrip("./")는 '.gitignore'의 앞점까지 깎는다 — removeprefix만 쓴다
    norm = path.replace("\\", "/").removeprefix("./")
    if any(bad in norm or norm.endswith(bad) or norm == bad.rstrip("/")
           for bad in _COMMIT_DENY):
        return False
    name = Path(norm).name
    if name in (".env", ".env.encrypted") or name.endswith(".log"):
        return False
    return any(norm == p.rstrip("/") or norm.startswith(p) for p in _COMMIT_ALLOW)


def dirty_files() -> list[dict]:
    try:
        raw = subprocess.check_output(
            ["git", "status", "--porcelain", "-u"],
            cwd=ROOT,
            text=True,
            encoding="utf-8",
            timeout=8,
        )
    except (OSError, subprocess.CalledProcessError, subprocess.TimeoutExpired):
        return []
    out = []
    for line in raw.splitlines():
        if len(line) < 4:
            continue
        code, rest = line[:2], line[3:]
        if " -> " in rest:
            rest = rest.split(" -> ", 1)[1]
        path = rest.strip().strip('"')
        out.append({
            "path": path,
            "code": code.strip() or "M",
            "allowed": commit_allowed(path),
        })
    return out


def commit_work(message: str) -> dict:
    files = [f["path"] for f in dirty_files() if f["allowed"]]
    if not files:
        raise ValueError("커밋할 허용 파일이 없다")
    msg = re.sub(r"\s+", " ", (message or "").strip())
    if not msg:
        msg = f"chore(game): 보드 커밋 ({len(files)}파일)"
    if len(msg) > 120:
        msg = msg[:120]
    # add와 commit을 한 호흡 — 스테이징 방치 금지
    subprocess.check_call(["git", "add", "--"] + files, cwd=ROOT, timeout=20)
    try:
        subprocess.check_call(["git", "commit", "-m", msg], cwd=ROOT, timeout=20)
    except subprocess.CalledProcessError:
        subprocess.call(["git", "reset", "HEAD", "--"] + files, cwd=ROOT)
        raise ValueError("커밋 실패 — 스테이징을 되돌렸다")
    head = subprocess.check_output(
        ["git", "log", "-1", "--pretty=format:%h %s"],
        cwd=ROOT, text=True, encoding="utf-8", timeout=5,
    ).strip()
    return {"hash": head.split()[0], "subject": head[len(head.split()[0]) + 1:], "files": files}


def recent_commits() -> list[dict]:
    try:
        raw = subprocess.check_output(
            [
                "git", "log", "--pretty=format:%h\t%ad\t%s",
                "--date=format:%m-%d %H:%M", "-12",
                "--", "projects/ashes-to-stars", "docs/STATUS.md",
                "docs/DESIGN.md", "docs/feedback/INBOX.md",
            ],
            cwd=ROOT,
            text=True,
            encoding="utf-8",
            timeout=8,
        )
    except (subprocess.CalledProcessError, OSError, subprocess.TimeoutExpired):
        return []
    out = []
    for line in raw.splitlines():
        parts = line.split("\t", 2)
        if len(parts) == 3:
            out.append({"hash": parts[0], "when": parts[1], "subject": parts[2]})
    return out


def build_state() -> dict:
    status = _read(STATUS)
    design = _read(DESIGN)
    inbox = _read(INBOX)
    checks = load_checks()
    decisions = load_decisions()
    queue = parse_queue(status)
    miles = parse_milestones(design)
    table = parse_queue_table(status)
    now_list = parse_now_list(_read(GAME_DESIGN))
    for it in now_list:
        for m in miles:
            if m.get("done") and (m["title"][:2] == it["title"][:2] or
                                  m["title"] in it["title"] or it["title"] in m["title"]):
                it["done"] = True
    extra = table + now_list
    flags = loop_flags()
    return {
        "updated": parse_updated(status),
        "queue": queue,
        "results": parse_results(status),
        "milestones": miles,
        "inbox": parse_inbox(inbox),
        "checks": checks,
        "decisions": decisions,
        "choices": pending_choices(queue, miles, decisions, extra),
        "loop": flags,
        "commits": recent_commits(),
        "git": dirty_files(),
        "charts": progress_charts(status, design, _read(GAME_DESIGN), decisions),
        "stuck": stuck_items(status, flags),
        "completed": completed_posts(status),
    }


def set_flag(name: str, on: bool) -> None:
    if name not in ("HOLD", "STOP"):
        raise ValueError("unknown flag")
    path = HERE / name
    if on:
        path.write_text("", encoding="utf-8")
    elif path.exists():
        path.unlink()


class Handler(BaseHTTPRequestHandler):
    def log_message(self, fmt: str, *args) -> None:
        return

    def _json(self, code: int, payload) -> None:
        body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Cache-Control", "no-store")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def _read_json(self) -> dict:
        n = int(self.headers.get("Content-Length") or 0)
        if n <= 0 or n > 200_000:
            return {}
        raw = self.rfile.read(n)
        try:
            data = json.loads(raw.decode("utf-8"))
        except (json.JSONDecodeError, UnicodeDecodeError):
            return {}
        return data if isinstance(data, dict) else {}

    def do_GET(self) -> None:
        path = urlparse(self.path).path
        if path in ("/", "/index.html"):
            html = HERE / "board.html"
            try:
                body = html.read_bytes()
            except OSError as e:
                body = f"board.html 없음: {e}".encode("utf-8")
                self.send_response(500)
            else:
                self.send_response(200)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.send_header("Cache-Control", "no-store")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)
            return
        if path == "/api/state":
            self._json(200, build_state())
            return
        if path.startswith("/shots/"):
            rel = unquote(path[len("/shots/"):])
            data, ctype, err = read_shot(rel)
            if data is None:
                self.send_response(404)
                self.end_headers()
                return
            self.send_response(200)
            self.send_header("Content-Type", ctype)
            self.send_header("Cache-Control", "private, max-age=120")
            self.send_header("Content-Length", str(len(data)))
            self.end_headers()
            self.wfile.write(data)
            return
        self.send_response(404)
        self.end_headers()

    def do_POST(self) -> None:
        path = urlparse(self.path).path
        data = self._read_json()
        try:
            if path == "/api/decide":
                decided = apply_decision(
                    str(data.get("id") or "").strip(),
                    str(data.get("choice") or "").strip(),
                    str(data.get("note") or ""),
                )
                resumed = None
                if data.get("resume", True):
                    resumed = resume_work()
                self._json(200, {"ok": True, **decided, "resume": resumed})
                return
            if path == "/api/request":
                stamp = write_request(
                    str(data.get("title") or ""),
                    str(data.get("body") or ""),
                )
                if data.get("resume"):
                    set_flag("HOLD", False)
                self._json(200, {"ok": True, "at": stamp})
                return
            if path == "/api/check":
                key = str(data.get("id") or "").strip()
                if not re.fullmatch(r"[0-9a-f]{8,16}", key):
                    self._json(400, {"ok": False, "error": "잘못된 id"})
                    return
                checks = load_checks()
                if data.get("done"):
                    checks[key] = {
                        "at": datetime.now().strftime("%Y-%m-%d %H:%M"),
                        "note": str(data.get("note") or "")[:120],
                    }
                else:
                    checks.pop(key, None)
                save_checks(checks)
                self._json(200, {"ok": True, "checks": checks})
                return
            if path == "/api/loop":
                action = str(data.get("action") or "")
                if action == "continue":
                    self._json(200, {"ok": True, **resume_work()})
                    return
                if action == "hold":
                    set_flag("HOLD", True)
                elif action == "unhold":
                    set_flag("HOLD", False)
                elif action == "stop":
                    set_flag("STOP", True)
                elif action == "unstop":
                    set_flag("STOP", False)
                else:
                    self._json(400, {"ok": False, "error": "알 수 없는 action"})
                    return
                self._json(200, {"ok": True, "loop": loop_flags()})
                return
            if path == "/api/commit":
                result = commit_work(str(data.get("message") or ""))
                self._json(200, {"ok": True, **result})
                return
        except ValueError as e:
            self._json(400, {"ok": False, "error": str(e)})
            return
        self.send_response(404)
        self.end_headers()


def _lan_ip() -> str:
    import socket
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    try:
        s.connect(("8.8.8.8", 80))
        return s.getsockname()[0]
    except OSError:
        return "127.0.0.1"
    finally:
        s.close()


def main() -> None:
    host = os.getenv("BOARD_HOST", "0.0.0.0")
    srv = ThreadingHTTPServer((host, PORT), Handler)
    print(f"재와 별 개발 보드  (ROOT={ROOT})")
    print(f"  이 기기: http://127.0.0.1:{PORT}/")
    if host != "127.0.0.1":
        print(f"  다른 기기: http://{_lan_ip()}:{PORT}/")
    print("  Ctrl+C 로 종료")
    threading.Timer(0.5, lambda: webbrowser.open(f"http://127.0.0.1:{PORT}/")).start()
    try:
        srv.serve_forever()
    except KeyboardInterrupt:
        print("\n종료")
        srv.shutdown()


if __name__ == "__main__":
    main()
