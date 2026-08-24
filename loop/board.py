#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""재와 별 개발 보드 — 진행을 보고 체크하고 INBOX에 요청한다.

    python3 loop/board.py          → http://127.0.0.1:8766
    BOARD_HOST=127.0.0.1           이 기기만
    BOARD_PORT=8766

함대 FleetView(8765)와 별개다. 이 화면은 루프가 읽는 파일만 다룬다:
STATUS.md · DESIGN.md · feedback/INBOX.md · loop/HOLD·STOP·agent.
요청은 INBOX 「대기 중」에 붙고, 다음 이터레이션이 큐보다 먼저 읽는다.

오너 지시(2026-08-17) — 보드는 항상 이렇게 관리한다:
1. 채팅 지시도 남긴다. `python3 loop/board.py command "제목" "본문"`
2. 다음 할 일은 화면 위쪽. 끝난 프로토(V1~V4)는 접고 지금 단계를 보여 준다.
3. 칸·제목·설명은 짧은 한국어. INBOX 시각·조문·코드 이름을 그대로 올리지 않는다.
4. 끝난 외부 테스터는 두지 않는다. 지금 하는 사람(아나)만.
5. 끝난 일 갤러리에 검은 화면 PNG를 올리지 않는다.
6. **한 화면이 철칙.** 스크롤 없이 지금·다음·막힘이 보여야 한다. 막힌 것은 자세히에서만 풀고, 한눈에는 제목 3줄까지.
규칙을 바꾸려면 오너가 다시 말하기 전에는 되돌리지 마라.
"""
from __future__ import annotations

import hashlib
import json
import os
import re
import subprocess
import sys
import threading
import time
import urllib.error
import urllib.request
import webbrowser
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, quote, unquote, urlparse
from zoneinfo import ZoneInfo

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
PORT = int(os.getenv("BOARD_PORT", "8766"))
CHECKS_PATH = HERE / "board_checks.json"
DECISIONS_PATH = HERE / "board_decisions.json"
COMMANDS_PATH = HERE / "owner_commands.json"
COMMANDS_MAX = 80
TEST_REPORT_PATH = HERE / "last_test_report.json"
HANDOFF_STATE_PATH = HERE / "handoff_state.json"
PID_PATH = HERE / "loop.pid"
GROK_AUTH = Path.home() / ".grok" / "auth.json"
GROK_USAGE_CACHE = HERE / "grok_usage.cache.json"
CLAUDE_USAGE_CACHE = HERE / "claude_usage.cache.json"
CODEX_USAGE_CACHE = HERE / "codex_usage.cache.json"
CODEX_AUTH = Path.home() / ".codex" / "auth.json"
GROK_BILLING_URL = "https://cli-chat-proxy.grok.com/v1/billing?format=credits"
CLAUDE_USAGE_URL = "https://api.anthropic.com/api/oauth/usage"
CODEX_USAGE_URL = "https://chatgpt.com/backend-api/wham/usage"
_GROK_USAGE_TTL = 300
_USAGE_TTL = 300
_GROK_PRODUCTS = {
    "GrokBuild": "빌드",
    "GrokImagine": "이미지",
    "GrokAppBuilder": "앱빌더",
    "GrokChat": "채팅",
}
_usage_lock = threading.Lock()
_usage_mem: dict | None = None
_usage_at = 0.0
_git_sync_lock = threading.Lock()
_git_sync_last = {
    "busy": False,
    "ok": None,
    "action": "",
    "at": "",
    "message": "최근 동기화 없음",
}

CHOICES = {
    "do": "이걸로 진행 — 큐 맨 위에서 이것만 잡아라",
    "pass": "통과 — 완료로 내리고 다음으로 진행",
    "retry": "부족 — 이 항목을 다시 고쳐라",
    "skip": "건너뛰기 — 완료로 내리지 말고 다음 실행 가능 항목",
}

STATUS = ROOT / "docs" / "STATUS.md"
WORKLOG = ROOT / "docs" / "GAME_WORKLOG.md"
HANDOFF = ROOT / "docs" / "GAME_DEV_HANDOFF.md"
LOGS = ROOT / "logs"
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
    "loop/handoff_state.json",
    "loop/last_test_report.json",
    "loop/v4_playtest.py",
    "loop/v4_testers.json",
    "loop/v4_test_script.json",
    "loop/test_v4_playtest.py",
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
        # utf-8-sig: 다른 세션이 BOM을 붙여 저장해도 칸이 통째로 비지 않는다.
        return path.read_text(encoding="utf-8-sig")
    except OSError:
        return ""


def item_id(text: str) -> str:
    return hashlib.sha1(text.strip().encode("utf-8")).hexdigest()[:12]


_TITLE_HINTS = (
    (re.compile(r"지금 문제점"), "캐릭터·몹 움직임과 겹침"),
    (re.compile(r"^UI 퀄리티"), "화면이 아직 어색한 곳"),
    (re.compile(r"소비처 0곳|기획서\s*✅"), "기획만 있고 안 만든 기능"),
    (re.compile(r"글씨"), "글씨를 테두리 안에"),
    (re.compile(r"사용량"), "보드에 사용량 보이기"),
    (re.compile(r"보스.*애니|스프라이트 애니"), "보스 움직임 그림"),
    (re.compile(r"클래시오브클랜|영지.*건물"), "영지에서 건물 관리"),
    (re.compile(r"보스.*공격|공격을 안"), "보스가 때리게"),
    (re.compile(r"명령.*기록|할일 상위|할 일 상위"), "시킨 일을 보드에 남기기"),
)


def humanize_title(title: str, detail: str = "") -> str:
    """보드에 올리는 한 줄. INBOX 시각·조문·코드 이름은 뺀다."""
    t = re.sub(r"\s+", " ", title or "").strip()
    t = re.sub(r"^[📌⭐✅⚠]\s*", "", t)
    t = re.sub(r"^INBOX\s+\d{1,2}:\d{2}\s+", "", t)
    t = re.sub(r"\s*\(오너[^)]*\)\s*$", "", t)
    t = re.sub(r"\s*§[0-9.\-]+", "", t)
    for pat, label in _TITLE_HINTS:
        if pat.search(t):
            t = label
            break
    t = re.sub(r"`[^`]+`", "", t)
    t = re.sub(r"\s+", " ", t).strip(" ·,;—-")
    return _now_short(t, 36) if t else "할 일"


def humanize_detail(text: str, limit: int = 88) -> str:
    """설명은 한 줄. 코드·조문·로그 문장은 잘라 낸다."""
    t = text or ""
    t = re.sub(r"^>\s?", "", t, flags=re.M)
    leftover = re.findall(r"[^.。\n]{8,70}남음", t)
    t = re.sub(r"대기하지 말[고다요]?\s*", "", t)
    t = re.sub(r"큐\s*\d+번은[^.]*\.?\s*", "", t)
    t = re.sub(r"INBOX\s+\d{1,2}:\d{2}\s*", "", t)
    t = re.split(r"`|라 대화|사람 육안|루프는|생산 소비처|TDD/실행|네거티브", t, maxsplit=1)[0]
    t = re.sub(r"§[0-9.\-]+", "", t)
    t = re.sub(r"\*\*([^*]+)\*\*", r"\1", t)
    t = re.sub(r"\[[^\]]+\.png[^\]]*\]", "", t)
    t = re.sub(r"\bqa_[^\s]+\.png\b", "", t, flags=re.I)
    t = re.sub(r"`?[0-9a-f]{8,40}`?", "", t)
    t = re.sub(r"\b[A-Za-z][A-Za-z0-9_./-]*[A-Za-z0-9]\b", "", t)
    t = re.sub(r"/[A-Za-z][A-Za-z0-9_]*", "", t)
    t = re.sub(r"\d+-슬라이스\s*", "", t)
    t = re.sub(r"원장\s*§[^\n·]*", "", t)
    t = re.sub(r"근거 있음", "기획과 코드가 있다", t)
    t = re.sub(r"원장을 훑어 다음 구멍을 큐에 올린다",
               "기획서에서 아직 안 만든 기능을 찾는다", t)
    t = re.sub(r"^원장\s*[·,]\s*", "", t)
    t = re.sub(r"\s+", " ", t).strip(" ·,;—-\n")
    if leftover and (len(t) > limit or "닫음" in (text or "") or "·" in t[:20]):
        t = leftover[-1].strip()
    for sep in (". ", " · "):
        if sep in t:
            first = t.split(sep, 1)[0].strip()
            if len(first) >= 8:
                t = first
                break
    return _now_short(t, limit) if t else ""


def _plain_item(it: dict, detail_key: str = "detail") -> dict:
    out = dict(it)
    raw_t = str(out.get("title") or "")
    raw_d = str(out.get(detail_key) or out.get("body") or "")
    out["title"] = humanize_title(raw_t, raw_d)
    if detail_key in out:
        out[detail_key] = humanize_detail(raw_d)
    if "body" in out:
        out["body"] = humanize_detail(str(it.get("body") or ""), 120)
    return out


def _plain_list(items: list, detail_key: str = "detail") -> list:
    return [_plain_item(it, detail_key) for it in (items or [])]


def _heading_block(status: str, pred) -> str:
    """Return body of the first ## heading for which pred(heading) is true."""
    for m in re.finditer(r"^##\s+(.+?)\s*$", status, re.M):
        if not pred(m.group(1)):
            continue
        rest = status[m.end():]
        end = re.search(r"^## ", rest, re.M)
        return rest[: end.start()] if end else rest
    return ""


def parse_queue(status: str) -> list[dict]:
    """STATUS 「다음 할 일」 번호 목록 (구 포맷 + 2026-08-23 템플릿)."""
    def want(h: str) -> bool:
        if "다음 할 일" not in h:
            return False
        # Prefer numbered-list section over the markdown table section.
        if "큐" in h and "원장" not in h:
            return False
        return True

    block = _heading_block(status, want)
    if not block:
        # New template often names the only queue section with 큐.
        block = _heading_block(status, lambda h: "다음 할 일" in h)
    if not block:
        return []
    out = []
    for line in block.splitlines():
        parsed = parse_numbered_item(line)
        if parsed:
            out.append(parsed)
    return out


def parse_numbered_item(line: str) -> dict | None:
    """`1. **제목** (메모) — 설명`, checkbox `- [ ] 1. 제목`, or plain `1. 제목`."""
    raw = line.strip()
    # New template: "- [ ] 1. title" / "- [x] 1. title"
    raw = re.sub(r"^-\s*\[[ xX]\]\s*", "", raw)
    hit = re.match(
        r"^(\d+)\.\s+\*\*(.+?)\*\*(?:\s*\([^)]*\))?\s*(?:[—–-]\s*(.*))?$",
        raw,
    )
    if not hit:
        hit = re.match(
            r"^(\d+)\.\s+(.+?)(?:\s*[—–-]\s*(.*))?$",
            raw,
        )
        if not hit:
            return None
        title = hit.group(2).strip()
        detail = (hit.group(3) or "").strip()
        # skip empty template placeholders
        if not title or title in (".", "…", "..."):
            return None
    else:
        title = hit.group(2).strip()
        detail = (hit.group(3) or "").strip()
    if not title:
        return None
    return {
        "n": int(hit.group(1)),
        "id": item_id(title),
        "title": title,
        "detail": detail,
        "human": needs_human(title, detail),
    }


def parse_queue_table(status: str) -> list[dict]:
    """STATUS 하단 「다음 할 일 큐」 표. 취소선(완료) 행은 뺀다."""
    block = _heading_block(status, lambda h: "다음 할 일 큐" in h)
    if not block:
        return []
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
    block = _heading_block(status, lambda h: "다음 할 일 큐" in h)
    if not block:
        return []
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
            "goal": goal[:72],
            "gate": re.sub(r"<[^>]+>", "", gate)[:100],
            "state": state,
            "pct": pct,
        })
    return out


def parse_roadmap_table(game_design: str) -> list[dict]:
    """원장 §22 「개발 로드맵 — 프로토타입 이후」."""
    m = re.search(
        r"개발 로드맵[^\n]*\n.*?\n\| 단계 \|[^\n]*\n\|[-| :]+\|\n(.*?)(?=\n\n|\n- |\n### |\Z)",
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
        label = re.sub(r"[*]+", "", cells[0]).strip()
        num = re.match(r"(\d+)", label)
        if not num:
            continue
        out.append({
            "id": num.group(1),
            "label": label[:40],
            "pct": 0,
            "note": re.sub(r"<[^>]+>", "", cells[-1])[:120],
        })
    return out


# 원장 §22-1 범위 + §13-2 건물 7종 + 확정 단축/격자. 근거 없는 완료는 올리지 않는다.
_SLICE_ITEMS = (
    ("keep", "본성 레벨", ("본성 레벨", "본성 건설", "EstateBuild")),
    ("mine", "광산 생산", ("광산 적립", "광산 생산", "EstateMine")),
    ("warehouse", "창고 적립·용량", ("창고 용량", "창고 적립", "창고")),
    ("smith", "대장간·제작", ("대장간",)),
    ("mausoleum", "영묘", ("영묘",)),
    ("barracks", "수비대", ("수비 배치", "수비대")),
    ("defense", "방어 건물 4종", ("방어 건물", "EstateDefense")),
    ("timer", "건설 시간", ("건설 시간",)),
    ("first_job", "전직 1차", ("전직 1차", "전직 시스템")),
    ("gear", "장비·제작", ("장비 제작", "가죽", "흉갑")),
    ("t1t3", "티어 1~3", ("티어 1~3", "T1~T3")),
    ("tower30", "탑 30층", ("탑 30층까지", "30층까지 성장", "티어 1~3 전체")),
    ("grid", "격자 8×8", ("8×8", "격자 배치")),
    ("shorten", "단축 50%", ("단축 50%", "단축 50")),
)

_SLICE_OPEN = ("다음", "미완", "안 넣음", "아직", "측정 안", "해금 게이트", "해금」")


def _sentence_closed(blob: str, needles: tuple[str, ...]) -> bool:
    for sent in re.split(r"(?<!\d)\.|\n", blob):
        if not any(n in sent for n in needles):
            continue
        if any(w in sent for w in _SLICE_OPEN):
            continue
        title = re.search(r"^\s*\d+\.\s+\*\*(.+?)\*\*", sent)
        if title and "✅" not in sent and "~~" not in sent:
            if any(n in title.group(1) for n in needles):
                continue
        if any(w in sent for w in ("✅", "닫힘", "닫음", "완료", "PASS", "적립", "이터 결과")):
            return True
    return False


def slice_checks(status: str, design: str, game_design: str = "") -> list[dict]:
    """기획서 수직 슬라이스 범위를 STATUS·DESIGN 근거로 체크한다."""
    blob = f"{status}\n{design}"
    out = []
    for sid, label, needles in _SLICE_ITEMS:
        done = _sentence_closed(blob, needles)
        out.append({
            "id": item_id("slice:" + sid),
            "title": label,
            "detail": "원장 §22·§13" + (" · 근거 있음" if done else " · 아직"),
            "done": done,
            "human": False,
        })
    return out


def mark_now_closed(now_list: list[dict], design: str, status: str,
                    decisions: dict) -> list[dict]:
    """기획서 「지금 당장」이 이미 닫힌 관문을 다시 열지 않게 한다."""
    v2 = _v2_passed(status, decisions)
    v3 = _v3_closed(design)
    v4 = v4_released(status, decisions)
    miles = parse_milestones(design)
    for it in now_list:
        title = it.get("title") or ""
        if v2 and title.startswith("V2"):
            it["done"] = True
        if v3 and title.startswith("V3"):
            it["done"] = True
        if v4 and title.startswith("V4"):
            it["done"] = True
        for m in miles:
            if m.get("done") and (m["title"][:2] == title[:2]
                                  or m["title"] in title or title in m["title"]):
                it["done"] = True
    return now_list


def dummy_human_report() -> dict:
    """오너 2026-08-24 사람 관문 더미 보고서. ROOT를 옮겨 테스트가 실파일을 안 읽게 한다."""
    p = ROOT / "output" / "qa" / "ashes-to-stars" / "v4_playtest_dummy" / "dummy_report.json"
    if not p.is_file():
        return {}
    try:
        data = json.loads(p.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return {}
    if not data.get("dummy") or not data.get("closes_human_gates"):
        return {}
    return data


def _dummy_verdict_pass(gate: str) -> bool:
    v = (dummy_human_report().get("verdict") or {}).get(gate) or {}
    return bool(v.get("pass"))


def _v2_passed(status: str, decisions: dict) -> bool:
    if _dummy_verdict_pass("V2"):
        return True
    if "V2 사람 판정 → 통과" in status:
        return True
    return any(
        str(v.get("title") or "").startswith("V2") and v.get("choice") == "pass"
        for v in (decisions or {}).values()
    )


def _v3_closed(design: str) -> bool:
    return any(m.get("done") and "V3" in m["title"] for m in parse_milestones(design))


def _v4_human_passed(status: str, decisions: dict | None) -> bool:
    """테스터 70% 통과. skip·실측 세션은 통과가 아니다."""
    if re.search(r"V4.{0,24}70%.{0,24}→\s*통과", status or ""):
        return True
    for v in (decisions or {}).values():
        title = str(v.get("title") or "")
        if "V4" in title and "70%" in title and v.get("choice") == "pass":
            return True
    return False


def _v4_owner_skipped(status: str, decisions: dict | None) -> bool:
    """오너가 사람 70%를 넘김. 옛 보류(skip)와 다르다."""
    if re.search(r"V4.{0,24}70%.{0,24}→\s*넘김", status or ""):
        return True
    for v in (decisions or {}).values():
        title = str(v.get("title") or "")
        note = str(v.get("note") or "")
        if "V4" in title and "70%" in title and "넘김" in note:
            return True
    return False


def v4_released(status: str = "", decisions: dict | None = None) -> bool:
    return (_v4_human_passed(status, decisions)
            or _v4_owner_skipped(status, decisions)
            or _dummy_verdict_pass("V4"))


def v4_gate_pct(st: dict | None = None, decisions: dict | None = None,
                status: str = "") -> int:
    """V4b 진행. 키트·세션은 숫자를 올리고, 사람 70% 전에는 90이 상한이다.

    오너가 「넘어가」면 100이지만 테스터 통과로 기록하지 않는다.
    """
    if v4_released(status, decisions):
        return 100
    st = st if st is not None else playtest_state()
    target = 10
    ran = min(int(st.get("ran") or 0), target)
    deleted = min(int(st.get("deleted") or 0), target)
    continued = min(int(st.get("continued") or 0), target)
    pct = 0
    if int(st.get("n") or 0) >= target:
        pct += 20
    pct += round(25 * ran / target)
    pct += round(25 * deleted / target)
    pct += round(20 * continued / target)
    return min(90, pct)


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
        elif w["id"] == "W5" and v4_released(status, decisions):
            w.update(state="done", pct=100, note="V4 관문 열림")
        elif w["id"] == "W6":
            if v4_released(status, decisions):
                if _v4_owner_skipped(status, decisions):
                    wnote = "오너 넘김"
                elif _dummy_verdict_pass("V4"):
                    wnote = "더미 관문 통과"
                else:
                    wnote = "사람 70% 통과"
                w.update(state="done", pct=100, note=wnote)
            else:
                w.update(state="open", pct=0, note="V4 70% 사람 관문")

    v4b_pct = v4_gate_pct(decisions=decisions, status=status)
    v4b_note = v4_playtest_note(status=status, decisions=decisions)
    gates = [
        {"id": "V1", "label": "V1 성능", "pct": 100, "note": "W1 통과 · DOTS 불필요"},
        {"id": "V2", "label": "V2 조작감", "pct": 100 if v2 else 0,
         "note": ("더미 관문 통과 · 실측 아님" if _dummy_verdict_pass("V2")
                  else ("오너 보드 통과" if v2 else "사람 판정 남음"))},
        {"id": "V3", "label": "V3 보스 한 판", "pct": 100 if v3 else 0,
         "note": "HP·페이즈·처치·층" if v3 else "한 판 미연결"},
        {"id": "V4a", "label": "V4 패배→삭제 경계", "pct": 100, "note": "자동 경계 닫힘"},
        {"id": "V4b", "label": "V4 외부 테스터 70%", "pct": v4b_pct, "note": v4b_note},
    ]
    proto_pct = round(sum(g["pct"] for g in gates) / max(len(gates), 1))
    proto_closed = sum(1 for g in gates if g["pct"] >= 100)
    if any(g["pct"] < 100 for g in gates):
        proto_pct = min(proto_pct, 90)
    if _v4_owner_skipped(status, decisions):
        proto_note = "오너가 사람 70%를 넘김 · 측정 안 함"
    elif proto_pct >= 100:
        proto_note = f"관문 {proto_closed}/{len(gates)} 닫힘"
    else:
        proto_note = f"관문 평균 · {proto_closed}/{len(gates)}닫힘 · 사람 70% 전 상한 90"
    slice_rows = slice_checks(status, design, game_design)
    slice_done = sum(1 for r in slice_rows if r["done"])
    slice_n = max(len(slice_rows), 1)
    slice_pct = round(100 * slice_done / slice_n) if slice_rows else 0
    opened = v4_released(status, decisions)
    roadmap = parse_roadmap_table(game_design) or [
        {"id": "0", "label": "0. 프로토타입", "pct": 0, "note": "§21"},
        {"id": "1", "label": "1. 수직 슬라이스", "pct": 0, "note": "§22"},
        {"id": "2", "label": "2. 온라인 기반", "pct": 0, "note": "§22"},
        {"id": "3", "label": "3. 콘텐츠 확장", "pct": 0, "note": "§22"},
        {"id": "4", "label": "4. 폴리시·베타", "pct": 0, "note": "§22"},
        {"id": "5", "label": "5. 출시 준비", "pct": 0, "note": "§22"},
    ]
    for stage in roadmap:
        if stage["id"] == "0":
            stage.update(pct=proto_pct, note=proto_note)
        elif stage["id"] == "1":
            if opened:
                stage.update(
                    pct=slice_pct,
                    note=f"원장 범위 {slice_done}/{len(slice_rows)} 닫힘",
                )
            else:
                stage.update(pct=0, note="V4 관문 후")
        elif not opened:
            stage.update(pct=0, note=stage.get("note") or "V4 관문 후")
    live = parse_queue(status)
    if live:
        live_blocked = sum(1 for q in live if any(
            k in (q.get("detail") or "") for k in ("소비 시스템이 없어", "유보", "지금 넣으면 오펀")))
        queue_stat = {
            "done": 0, "open": max(0, len(live) - live_blocked),
            "blocked": live_blocked, "total": len(live),
        }
    else:
        queue_stat = {"done": done_n, "open": open_n, "blocked": blocked_n, "total": len(rows)}
    current = pick_current_stage(roadmap)
    focus = focus_bars(current, gates, slice_rows)
    for w in weeks:
        if not w.get("note"):
            w["note"] = w.get("gate") or w.get("goal") or ""
    return {
        "gates": gates,
        "focus": focus,
        "current": current,
        "weeks": weeks,
        "queue": queue_stat,
        "art": resource_bars(),
        "commits": commit_spark(),
        "roadmap": roadmap,
        "slice": slice_rows,
        "slice_pct": slice_pct,
    }


# 원장 §22 단계 번호 → 보드 라벨. 오너 2026-08-18로 온라인이 알파 뒤(3번)로 갔다.
_STAGE_LABELS = {
    "0": "프로토타입",
    "1": "마을·탑·장비",
    "2": "직업·층 확장",
    "3": "온라인",
    "4": "다듬기",
    "5": "출시",
}


def pick_current_stage(roadmap: list[dict]) -> dict:
    """끝난 단계는 건너뛰고, 아직 안 끝난 첫 단계를 지금으로 둔다."""
    empty = {"id": "0", "label": "프로토타입", "pct": 0, "note": "", "proto_done": False}
    if not roadmap:
        return empty
    proto = next((s for s in roadmap if str(s.get("id")) == "0"), roadmap[0])
    proto_done = (proto.get("pct") or 0) >= 100
    for s in roadmap:
        if (s.get("pct") or 0) < 100:
            return {
                "id": str(s.get("id") or "0"),
                "label": _STAGE_LABELS.get(str(s.get("id")), _bare_stage(s.get("label"))),
                "pct": int(s.get("pct") or 0),
                "note": s.get("note") or "",
                "proto_done": proto_done,
            }
    last = roadmap[-1]
    return {
        "id": str(last.get("id") or "5"),
        "label": _STAGE_LABELS.get(str(last.get("id")), _bare_stage(last.get("label"))),
        "pct": int(last.get("pct") or 0),
        "note": last.get("note") or "끝",
        "proto_done": True,
    }


def _bare_stage(label: str | None) -> str:
    return re.sub(r"^\d+\.\s*", "", label or "").strip() or "다음 단계"


def focus_bars(current: dict, gates: list[dict], slice_rows: list[dict]) -> list[dict]:
    """지금 단계의 막대. 프로토가 끝나면 V1~V4 대신 다음 단계 항목을 보여 준다."""
    if not current.get("proto_done") or str(current.get("id")) == "0":
        return list(gates)
    if str(current.get("id")) == "1":
        done_n = sum(1 for r in slice_rows if r.get("done"))
        bars = [{
            "id": "slice-done",
            "label": f"끝낸 것 {done_n}/{len(slice_rows)}",
            "pct": 100 if slice_rows else 0,
            "note": "끝",
        }]
        for r in slice_rows:
            if r.get("done"):
                continue
            bars.append({
                "id": str(r.get("id") or r.get("title") or "open"),
                "label": r.get("title") or "남음",
                "pct": 0,
                "note": str(r.get("detail") or "남음")[:80],
            })
        return bars
    return [{
        "id": str(current.get("id") or "next"),
        "label": current.get("label") or "다음",
        "pct": int(current.get("pct") or 0),
        "note": current.get("note") or "",
    }]


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
    cur = charts.get("current") or {}
    style(ax, "지금 · " + (cur.get("label") or "프로토타입"))
    gates = charts.get("focus") or charts["gates"]
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


_SHOT_BLACK_CACHE: dict[tuple[str, int, int], bool] = {}


def shot_is_black(path: Path, mean_max: float = 14.0, dark_min: float = 0.90) -> bool:
    """거의 검은 화면은 끝난 일 증거로 쓰지 않는다.
    같은 파일(mtime·크기 불변) 재판정은 메모 캐시로 건너뛴다 — 상태 1건마다
    QA 샷 수십 장을 다시 디코딩해 렌더가 수 초로 늘어 서버 테스트 3초
    타임아웃 flake(테스트스위트:FAIL 재발)의 근본 원인이었다 (2026-08-24)."""
    try:
        st = path.stat()
    except OSError:
        return False
    key = (str(path), st.st_mtime_ns, st.st_size)
    hit = _SHOT_BLACK_CACHE.get(key)
    if hit is not None:
        return hit
    try:
        from PIL import Image
    except ImportError:
        return False
    try:
        im = Image.open(path).convert("RGB")
    except (OSError, ValueError):
        return False
    im = im.resize((80, 45))
    pix = im.load()
    w, h = im.size
    n = w * h
    if n <= 0:
        return True
    total = 0
    dark = 0
    for y in range(h):
        for x in range(w):
            r, g, b = pix[x, y][:3]
            s = r + g + b
            total += s
            if s <= 24:
                dark += 1
    mean = total / (3 * n)
    result = mean < mean_max and (dark / n) >= dark_min
    if len(_SHOT_BLACK_CACHE) > 1024:
        _SHOT_BLACK_CACHE.clear()
    _SHOT_BLACK_CACHE[key] = result
    return result


def usable_shot(rel: str) -> bool:
    full = shot_file(rel)
    return full is not None and not shot_is_black(full)


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
        if rel in seen or not usable_shot(rel):
            continue
        seen.add(rel)
        out.append(rel)
    return out


def hinted_shots(title: str, detail: str = "") -> list[str]:
    """경로가 없을 때만. 제목만 본다 — 본문 '안 연 것'이 V4·대장간을 훔친다."""
    blob = title or ""
    out: list[str] = []
    for keys, rel in _SHOT_HINTS:
        if any(k in blob for k in keys) and usable_shot(rel):
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


def playtest_state() -> dict:
    """V4 테스터 10명. 세션 JSON이 있으면 실측, 없으면 키트만."""
    kit_raw = _read(HERE / "v4_testers.json")
    testers = []
    try:
        testers = (json.loads(kit_raw) or {}).get("testers") or []
    except json.JSONDecodeError:
        testers = []
    sess_path = ROOT / "output" / "qa" / "ashes-to-stars" / "v4_playtest" / "sessions.json"
    sessions = []
    ran_at = ""
    if sess_path.is_file():
        try:
            blob = json.loads(sess_path.read_text(encoding="utf-8-sig"))
            sessions = blob.get("sessions") or []
            ran_at = blob.get("ran_at") or ""
        except (OSError, json.JSONDecodeError):
            sessions = []
    by_id = {s.get("id"): s for s in sessions if s.get("id")}
    rows = []
    for t in testers:
        s = by_id.get(t.get("id") or "") or {}
        rows.append({
            "id": t.get("id") or "",
            "tester": t.get("name") or s.get("tester") or "",
            "favorite": t.get("favorite") or s.get("favorite") or "",
            "job": t.get("job") or "",
            "first": t.get("first") or "",
            "minutes": t.get("minutes") or s.get("minutes") or 0,
            "level": s.get("level"),
            "deleted": bool(s.get("deleted")),
            "continued": bool(s.get("continued")),
            "living": s.get("living"),
            "gear": bool(s.get("gear")) if "gear" in s else bool(t.get("gear")),
            "path": s.get("continue_path") or "",
            "ran": bool(s),
            "ongoing": bool(t.get("ongoing")),
        })
    hist_n = len(sessions)
    hist_del = sum(1 for s in sessions if s.get("deleted"))
    hist_cont = sum(1 for s in sessions if s.get("continued"))
    script = load_playtest_script()
    return {
        "n": max(len(rows), hist_n),
        "ran": hist_n if hist_n else sum(1 for r in rows if r["ran"]),
        "deleted": hist_del if hist_n else sum(1 for r in rows if r["deleted"]),
        "continued": hist_cont if hist_n else sum(1 for r in rows if r["continued"]),
        "human_70": (dummy_human_report().get("human_70")
                     or script.get("human_70") or "pending"),
        "dummy": dummy_human_report(),
        "ran_at": ran_at,
        "sessions": rows,
        "script": script,
        "doc": "docs/V4_EXTERNAL_PLAYTEST.md",
        "sheet": "docs/feedback/playtest_sheet.md",
    }


def load_playtest_script() -> dict:
    raw = _read(HERE / "v4_test_script.json")
    try:
        data = json.loads(raw) if raw else {}
    except json.JSONDecodeError:
        return {}
    gates = data.get("gates") or []
    return {
        "title": data.get("title") or "",
        "human_70": data.get("human_70") or "pending",
        "fail_reasons": data.get("fail_reasons") or [],
        "gates": [
            {
                "id": g.get("id") or "",
                "title": g.get("title") or "",
                "pass": g.get("pass") or "",
                "testers": g.get("testers") or [],
                "steps": [
                    {
                        "id": s.get("id") or "",
                        "do": s.get("do") or "",
                        "record": s.get("record") or "",
                    }
                    for s in (g.get("steps") or [])
                ],
            }
            for g in gates
        ],
    }


def v4_playtest_note(st: dict | None = None, status: str = "",
                    decisions: dict | None = None) -> str:
    if _v4_owner_skipped(status, decisions):
        return "오너가 사람 70%를 넘김 · 측정 안 함"
    if _dummy_verdict_pass("V4"):
        return "더미 관문 통과 · 실측 아님"
    if dummy_human_report().get("human_70") == "dummy-fail":
        return "더미 관문 FAIL · 사람 대기 종료"
    if _v4_human_passed(status, decisions):
        return "사람 70% 통과"
    st = st if st is not None else playtest_state()
    pct = v4_gate_pct(st, decisions=decisions, status=status)
    if st["ran"] >= 10 and st["deleted"] >= 10:
        return f"10세션 삭제 실측 {pct}% · 사람 70% 대기"
    if st["n"] == 10:
        return f"테스터 10명 키트 {pct}% · 세션 대기"
    return "사람 관문 · 자동 완료 금지"


def parse_history_table(status: str) -> list[dict]:
    """STATUS 「최근 완료 내역 (History)」 표. 최신이 앞."""
    block = _heading_block(
        status,
        lambda h: ("완료" in h and ("History" in h or "내역" in h))
        or h.strip().startswith("최근 완료"),
    )
    if not block:
        return []
    out: list[dict] = []
    for line in block.splitlines():
        line = line.strip()
        if not line.startswith("|"):
            continue
        cells = [c.strip() for c in line.strip("|").split("|")]
        joined = "".join(cells)
        if set(joined) <= set("-:| ") or any(
            k in line for k in ("작업 내용", "검증 결과", "일시")
        ):
            continue
        if len(cells) < 3:
            continue
        if len(cells) >= 4:
            lap, when, title, verify = cells[0], cells[1], cells[2], cells[3]
        else:
            when, title, verify = cells[0], cells[1], cells[2]
            lap = ""
        title = re.sub(r"\*\*([^*]+)\*\*", r"\1", title).strip()
        if not title or title in ("—", "-", "…", "..."):
            continue
        commit = ""
        cm = re.search(r"`([0-9a-f]{7,40})`|\b([0-9a-f]{7,40})\b", verify or "")
        if cm:
            commit = (cm.group(1) or cm.group(2) or "")[:8]
        bits = [x for x in (when, verify) if x and x not in ("—", "-")]
        if lap and lap not in ("—", "-"):
            bits.append("바퀴 " + lap)
        out.append({
            "title": title[:160],
            "detail": " · ".join(bits)[:300],
            "commit": commit,
            "body": (verify or "")[:300],
        })
    return out


def completed_posts(status: str, limit: int = 12) -> list[dict]:
    """완료된 개발 — History 표 + STATUS 결과 + 실측 샷. 끝난 행만."""
    posts: list[dict] = []
    seen: set[str] = set()

    def add(title: str, detail: str, commit: str = "", extra: str = "") -> None:
        title = (title or "").strip()
        if not title or _title_seen(title, seen):
            return
        shots = [rel for rel in (mentioned_shots(detail + " " + extra)
                                 or hinted_shots(title, detail)) if usable_shot(rel)]
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

    for it in parse_history_table(status):
        add(it["title"], it.get("detail") or it.get("body") or "", it.get("commit") or "")
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
        data = json.loads(CHECKS_PATH.read_text(encoding="utf-8-sig"))
        return data if isinstance(data, dict) else {}
    except (OSError, json.JSONDecodeError):
        return {}


def load_decisions() -> dict:
    try:
        data = json.loads(DECISIONS_PATH.read_text(encoding="utf-8-sig"))
        return data if isinstance(data, dict) else {}
    except (OSError, json.JSONDecodeError):
        return {}



def load_handoff_state() -> dict:
    """코디네이터 핸드오프 상태. 없거나 깨져도 보드가 죽지 않게 기본값."""
    default = {
        "phase": "idle",
        "updated": "",
        "last_commit": "",
        "last_files": [],
        "review": "",
        "note": "준호 완료 보고 대기",
    }
    try:
        raw = json.loads(HANDOFF_STATE_PATH.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError):
        return dict(default)
    if not isinstance(raw, dict):
        return dict(default)
    phase = str(raw.get("phase") or "idle").strip()
    if phase not in ("idle", "awaiting_review", "pass", "fail"):
        phase = "idle"
    files = raw.get("last_files") or []
    if not isinstance(files, list):
        files = []
    return {
        "phase": phase,
        "updated": str(raw.get("updated") or ""),
        "last_commit": str(raw.get("last_commit") or ""),
        "last_files": [str(x) for x in files if str(x).strip()],
        "review": str(raw.get("review") or ""),
        "note": str(raw.get("note") or default["note"]),
    }


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
    write_request(f"오너 판정 — {item['title']} ({label})", body, source="decide")

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


def load_test_report() -> dict:
    """마지막 검증 묶음. 파일이 없거나 깨지면 칸을 비운다."""
    try:
        data = json.loads(TEST_REPORT_PATH.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError):
        return {}
    if not isinstance(data, dict):
        return {}
    items = []
    for raw in data.get("items") or []:
        if not isinstance(raw, dict):
            continue
        name = str(raw.get("name") or "").strip()
        if not name:
            continue
        items.append({
            "name": name[:80],
            "ok": bool(raw.get("ok")),
            "note": str(raw.get("note") or "").strip()[:200],
        })
    if not items and not data.get("summary"):
        return {}
    return {
        "at": str(data.get("at") or "")[:24],
        "ok": bool(data.get("ok")),
        "summary": str(data.get("summary") or "").strip()[:120],
        "items": items[:24],
    }


def load_commands() -> list[dict]:
    try:
        data = json.loads(COMMANDS_PATH.read_text(encoding="utf-8-sig"))
        return data if isinstance(data, list) else []
    except (OSError, json.JSONDecodeError):
        return []


def record_command(title: str, body: str = "", source: str = "board",
                   status: str = "open") -> dict:
    """오너 명령을 보드 기록에 남긴다. INBOX와 별개 — 처리돼도 목록에서 안 사라진다."""
    title = re.sub(r"\s+", " ", title).strip()
    if not title:
        raise ValueError("제목이 비어 있다")
    if len(title) > 80:
        title = title[:80]
    body = (body or "").strip()[:400]
    if source not in ("board", "chat", "decide"):
        source = "board"
    if status not in ("open", "done"):
        status = "open"
    rec = {
        "at": datetime.now().strftime("%Y-%m-%d %H:%M"),
        "title": title,
        "body": body,
        "source": source,
        "status": status,
    }
    rows = [rec] + [r for r in load_commands() if isinstance(r, dict)]
    COMMANDS_PATH.write_text(
        json.dumps(rows[:COMMANDS_MAX], ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    return rec


def write_request(title: str, body: str, source: str = "board") -> str:
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
    record_command(title, body, source=source, status="open")
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


def _hhmm(ts: float) -> str:
    if not ts:
        return ""
    return datetime.fromtimestamp(ts).strftime("%H:%M:%S")


def _main_log_path() -> Path:
    current = ROOT / "logs" / "loop_main.log"
    legacy = HERE / "loop_main.log"
    return current if current.exists() or not legacy.exists() else legacy


def loop_flags() -> dict:
    agent = os.getenv("LOOP_PROVIDERS", "claude · codex · grok").replace(",", " · ")
    main = _main_log_path()
    latest = _latest_iter_path()
    latest_iter = latest.name if latest else ""
    main_at = main.stat().st_mtime if main.is_file() else 0.0
    iter_at = latest.stat().st_mtime if latest else 0.0
    # 메인 로그가 이터 로그보다 낡으면(파이프로 띄운 루프 등) 이터 로그를 보여 준다.
    # 그래야 화면이 "지금"을 말한다 — 낡은 메인 로그를 그대로 걸어 두지 않는다.
    tail_src = latest if (latest and iter_at > main_at) else (main if main.is_file() else None)
    last_log = ""
    if tail_src is not None:
        lines = tail_src.read_text(encoding="utf-8", errors="replace").splitlines()
        last_log = "\n".join(lines[-16:])
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
        "log_from": tail_src.name if tail_src is not None else "",
        "log_at": _hhmm(max(main_at, iter_at)),
        "log_age_sec": int(max(0.0, datetime.now().timestamp() - max(main_at, iter_at))),
        "now": current_work(running=running, hold=hold, stop=stop,
                            latest_iter=latest_iter, main_log=last_log),
    }


def _latest_iter_path() -> Path | None:
    logs_root = ROOT / "logs"
    iters = list(logs_root.glob("*/lap-*.log")) if logs_root.is_dir() else []
    old_log_dir = HERE / "logs"
    if not iters and old_log_dir.is_dir():
        iters = list(old_log_dir.glob("iter_*.log"))
    iters.sort(key=lambda p: p.stat().st_mtime)
    return iters[-1] if iters else None


_NOW_SKIP = re.compile(
    r"이터레이션을 시작|지시대로|STATUS\.md|인수인계|함정 목록|DIRECTIVES|"
    r"먼저 읽|대조합|큐 1|사람 육안|오너 보류|대기하지|최근 커밋|"
    r"한 일|남긴 것|다음 세션|큐에만 올리|\*\*코드\*\*|증거"
)
_NOW_WORK = re.compile(
    r"구현|넣겠|고치|고칩|붙이|나누|찍|검증|커밋|배치|"
    r"SelfCheck|만듭|바꿉|나눕|검사기|동기화"
)


def _now_sentences(text: str) -> list[str]:
    blob = re.sub(r"\s+", " ", text or "").strip()
    parts = re.split(r"(?<=[다요])\.\s*|(?<=니다)\.\s*|(?<=까)\.\s*", blob)
    out = []
    for part in parts:
        part = part.strip(" .")
        if len(part) >= 8:
            out.append(part)
    return out


def _now_ing(text: str) -> str:
    text = re.sub(r"하겠습니다$", "는 중", text)
    text = re.sub(r"합니다$", "하는 중", text)
    return text


def _now_short(text: str, limit: int = 52) -> str:
    text = re.sub(r"\s+", " ", text).strip(" .")
    if len(text) <= limit:
        return text
    cut = text[:limit]
    for sep in (" · ", " — ", ", ", " "):
        i = cut.rfind(sep)
        if i >= 16:
            return cut[:i].rstrip(" ·,—")
    return cut.rstrip()


def infer_now_title(log_text: str, queue: list[dict], inbox_waiting: list[dict]) -> str:
    """지금 손에 든 일 한 줄. 읽기·계획 로그는 제목으로 안 쓴다."""
    sents = _now_sentences(log_text)
    work = [s for s in sents if _NOW_WORK.search(s) and not _NOW_SKIP.search(s)]
    if work:
        return _now_short(_now_ing(work[-1]))
    if inbox_waiting:
        title = re.sub(r"^[📌⭐✅]\s*", "", inbox_waiting[0]["title"])
        title = re.sub(r"\s*\(오너[^)]*\)\s*$", "", title)
        return _now_short(title) + " 하는 중"
    if queue:
        return _now_short(queue[0]["title"]) + " 하는 중"
    return "다음 일을 고르는 중"


def current_work(running: bool, hold: bool, stop: bool,
                 latest_iter: str, main_log: str) -> dict:
    """지금 루프가 손에 든 일. 끝난 이터는 작업 중으로 안 속인다."""
    full_main = _read(_main_log_path())
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
    if latest and not started:
        # 메인 로그가 낡아도 시작 시각은 이터 로그 이름에 있다(iter_YYYYmmdd_HHMMSS.log).
        m = re.search(r"iter_\d{8}_(\d{2})(\d{2})(\d{2})", latest.name)
        if m:
            started = ":".join(m.groups())
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
        title = "그림 만드는 중 · " + generating.splitlines()[0][:40]
    return {
        "phase": phase,
        "title": title,
        "iter": latest.name if latest else "",
        "number": number,
        "started": started,
        "generating": generating,
        "activity": [],
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
        if not re.search(r"(?:^|\s)(?:/bin/)?(?:bash|sh)\s+.+?loop(?:/loop)?\.sh(?:\s|$)", line):
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
    # 로그는 loop.sh가 직접 tee 한다 — 여기서 또 리다이렉트하면 두 벌로 쌓인다.
    proc = subprocess.Popen(
        ["bash", str(HERE / "loop.sh")],
        cwd=str(ROOT),
        stdout=subprocess.DEVNULL,
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
    return any(
        norm.startswith(p) if p.endswith("/") else norm == p
        for p in _COMMIT_ALLOW
    )


def _dirty_files(strict: bool = False) -> tuple[list[dict], str]:
    try:
        raw = subprocess.check_output(
            ["git", "status", "--porcelain=v1", "-z", "--untracked-files=all"],
            cwd=ROOT,
            timeout=8,
        )
    except (OSError, subprocess.CalledProcessError, subprocess.TimeoutExpired) as e:
        message = "Git 작업 트리 상태 확인 실패"
        if strict:
            raise ValueError(message) from e
        return [], message
    out = []
    records = raw.split(b"\0")
    i = 0
    while i < len(records):
        record = records[i]
        i += 1
        if len(record) < 4:
            continue
        code = record[:2].decode("ascii", errors="replace")
        path = os.fsdecode(record[3:])
        source_path = ""
        if "R" in code or "C" in code:
            if i < len(records):
                source_path = os.fsdecode(records[i])
                i += 1
        if code == "??":
            kind = "untracked"
        elif "D" in code:
            kind = "deleted"
        elif "R" in code or "C" in code:
            kind = "renamed"
        elif "A" in code:
            kind = "added"
        else:
            kind = "modified"
        allowed = commit_allowed(path)
        if source_path:
            allowed = allowed and commit_allowed(source_path)
        out.append({
            "path": path,
            "code": code.strip() or "M",
            "allowed": allowed,
            "kind": kind,
            "staged": code[0] not in (" ", "?"),
            "unstaged": code[1] != " ",
            "source_path": source_path,
        })
    return out, ""


def dirty_files() -> list[dict]:
    files, _error = _dirty_files()
    return files


def _git_commit_info(ref: str) -> dict:
    if not ref:
        return {"hash": "", "when": "", "subject": ""}
    try:
        raw = subprocess.check_output(
            [
                "git", "log", "-1", "--pretty=format:%h%x09%ad%x09%s",
                "--date=format:%m-%d %H:%M", ref,
            ],
            cwd=ROOT, text=True, encoding="utf-8", timeout=8,
            stderr=subprocess.DEVNULL,
        ).strip()
    except (OSError, subprocess.CalledProcessError, subprocess.TimeoutExpired):
        raw = ""
    parts = raw.split("\t", 2)
    if len(parts) != 3:
        return {"hash": "", "when": "", "subject": ""}
    return {"hash": parts[0], "when": parts[1], "subject": parts[2]}


def git_detail(strict: bool = False) -> dict:
    """현재 worktree와 추적 원격의 차이를 보드 표시용으로 계산한다."""
    branch_error = ""
    try:
        branch = subprocess.check_output(
            ["git", "rev-parse", "--abbrev-ref", "HEAD"],
            cwd=ROOT, text=True, encoding="utf-8", timeout=5,
            stderr=subprocess.DEVNULL,
        ).strip()
    except (OSError, subprocess.CalledProcessError, subprocess.TimeoutExpired) as e:
        if strict:
            raise ValueError("Git 브랜치 확인 실패") from e
        branch = ""
        branch_error = "Git 브랜치 확인 실패"

    upstream_error = ""
    try:
        upstream = subprocess.check_output(
            ["git", "rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}"],
            cwd=ROOT, text=True, encoding="utf-8", timeout=5,
            stderr=subprocess.DEVNULL,
        ).strip()
    except subprocess.CalledProcessError:
        upstream = ""
    except (OSError, subprocess.TimeoutExpired) as e:
        if strict:
            raise ValueError("Git 원격 추적 확인 실패") from e
        upstream = ""
        upstream_error = "Git 원격 추적 확인 실패"

    ahead = behind = 0
    relation_error = ""
    if upstream:
        try:
            raw = subprocess.check_output(
                ["git", "rev-list", "--left-right", "--count", f"HEAD...{upstream}"],
                cwd=ROOT, text=True, encoding="utf-8", timeout=8,
                stderr=subprocess.DEVNULL,
            ).strip().split()
            if len(raw) == 2:
                ahead, behind = int(raw[0]), int(raw[1])
        except (OSError, subprocess.CalledProcessError, subprocess.TimeoutExpired, ValueError) as e:
            if strict:
                raise ValueError("Git 원격 차이 확인 실패") from e
            ahead = behind = 0
            relation_error = "Git 원격 차이 확인 실패"

    files, worktree_error = _dirty_files(strict=strict)
    counts = {key: 0 for key in ("modified", "added", "deleted", "renamed", "untracked")}
    for item in files:
        counts[item["kind"]] += 1
    allowed = sum(1 for item in files if item["allowed"])
    error = branch_error or upstream_error or relation_error or worktree_error
    if error:
        status = error
    elif not branch:
        status = "Git 상태 확인 불가"
    elif not upstream:
        status = "원격 추적 없음"
    elif ahead and behind:
        status = f"로컬 {ahead}개·원격 {behind}개로 갈라짐"
    elif behind:
        status = f"원격 {behind}개 앞섬"
    elif ahead:
        status = f"로컬 {ahead}개 앞섬"
    else:
        status = "origin과 같음"
    change_id = hashlib.sha1(json.dumps(sorted(
        (item["code"], item["path"], item["source_path"], item["allowed"])
        for item in files
    ), ensure_ascii=True, separators=(",", ":")).encode("utf-8")).hexdigest()[:12]
    return {
        "ok": not error,
        "error": error,
        "branch": branch,
        "upstream": upstream,
        "ahead": ahead,
        "behind": behind,
        "diverged": ahead > 0 and behind > 0,
        "status": status,
        "changed": len(files),
        "allowed": allowed,
        "blocked": len(files) - allowed,
        "staged": sum(1 for item in files if item["staged"]),
        "unstaged": sum(1 for item in files if item["unstaged"]),
        "change_id": change_id,
        "counts": counts,
        "files": files,
        "local": _git_commit_info("HEAD"),
        "remote": _git_commit_info(upstream),
    }


def git_summary() -> dict:
    summary = git_detail()
    summary.pop("files", None)
    return summary


def _commit_work(message: str) -> dict:
    dirty, _error = _dirty_files(strict=True)
    staged_blocked = [
        item for item in dirty if item.get("staged") and not item.get("allowed")
    ]
    if staged_blocked:
        raise ValueError(
            f"제외 파일 {len(staged_blocked)}개가 이미 스테이징되어 있다 — "
            "다른 커밋에 섞지 않는다")
    files = [f["path"] for f in dirty if f["allowed"]]
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
        # --only: 검사 뒤 다른 세션이 stage해도 이 경로 밖 파일은 새 커밋에 넣지 않는다.
        subprocess.check_call(
            ["git", "commit", "-m", msg, "--only", "--"] + files,
            cwd=ROOT, timeout=20,
        )
    except subprocess.CalledProcessError:
        subprocess.call(["git", "reset", "HEAD", "--"] + files, cwd=ROOT)
        raise ValueError("커밋 실패 — 스테이징을 되돌렸다")
    head = subprocess.check_output(
        ["git", "log", "-1", "--pretty=format:%h %s"],
        cwd=ROOT, text=True, encoding="utf-8", timeout=5,
    ).strip()
    return {"hash": head.split()[0], "subject": head[len(head.split()[0]) + 1:], "files": files}


def commit_work(message: str) -> dict:
    """수동 커밋도 동기화와 같은 잠금으로 index·HEAD 경합을 막는다."""
    if not _git_sync_lock.acquire(blocking=False):
        raise ValueError("다른 깃 작업이 이미 진행 중이다")
    try:
        return _commit_work(message)
    finally:
        _git_sync_lock.release()


def push_state() -> dict:
    """origin보다 몇 커밋 앞서 있나. origin이 없거나 못 읽으면 조용히 0."""
    try:
        branch = subprocess.check_output(
            ["git", "rev-parse", "--abbrev-ref", "HEAD"],
            cwd=ROOT, text=True, encoding="utf-8", timeout=5).strip()
        raw = subprocess.check_output(
            ["git", "rev-list", "--count", f"origin/{branch}..HEAD"],
            cwd=ROOT, text=True, encoding="utf-8", timeout=8,
            stderr=subprocess.DEVNULL).strip()
        ahead = int(raw or 0)
    except (subprocess.CalledProcessError, OSError, subprocess.TimeoutExpired, ValueError):
        return {"branch": "", "ahead": 0}
    return {"branch": branch, "ahead": ahead}


def _push_work() -> dict:
    """지금 브랜치를 origin으로 올린다. **강제 푸시는 하지 않는다** — 남의 커밋을 지운다.

    보드 버튼 전용(오너 지시 2026-08-18). 사람이 눌렀을 때만 돈다 — 자동 루프는 이걸 안 부른다.
    거절(non-fast-forward)이면 그대로 사유를 돌려준다. 여기서 pull·rebase를 대신 하지 않는다:
    작업트리에 다른 세션의 변경이 있는 상태에서 자동 rebase는 사고가 된다.
    """
    state = push_state()
    branch = state.get("branch") or ""
    if not branch or branch == "HEAD":
        raise ValueError("브랜치가 없다(detached HEAD) — 푸시하지 않는다")
    if state.get("ahead", 0) <= 0:
        raise ValueError("올릴 커밋이 없다")
    env = dict(os.environ)
    env["GIT_TERMINAL_PROMPT"] = "0"
    try:
        out = subprocess.run(
            ["git", "push", "origin", branch],
            cwd=ROOT, text=True, encoding="utf-8", timeout=180,
            capture_output=True, env=env)
    except (OSError, subprocess.TimeoutExpired) as e:
        raise ValueError(f"푸시 실패 — {e}") from e
    if out.returncode != 0:
        tail = (out.stderr or out.stdout or "").strip().splitlines()
        why = tail[-1] if tail else f"exit {out.returncode}"
        raise ValueError(f"푸시 거절 — {why[:160]}")
    after = push_state()
    return {"branch": branch, "pushed": state["ahead"], "ahead": after.get("ahead", 0)}


def push_work() -> dict:
    """수동 푸시도 동기화와 같은 잠금으로 원격 변경 경합을 막는다."""
    if not _git_sync_lock.acquire(blocking=False):
        raise ValueError("다른 깃 작업이 이미 진행 중이다")
    try:
        return _push_work()
    finally:
        _git_sync_lock.release()


def _fetch_origin(branch: str) -> None:
    """원격 상태만 갱신한다. worktree와 브랜치는 바꾸지 않는다."""
    env = dict(os.environ)
    env["GIT_TERMINAL_PROMPT"] = "0"
    try:
        out = subprocess.run(
            [
                "git", "fetch", "--prune", "--no-tags",
                "--no-recurse-submodules", "origin", branch,
            ],
            cwd=ROOT, text=True, encoding="utf-8", timeout=180,
            capture_output=True, env=env,
        )
    except (OSError, subprocess.TimeoutExpired) as e:
        raise ValueError(f"원격 확인 실패 — {e}") from e
    if out.returncode != 0:
        tail = (out.stderr or out.stdout or "").strip().splitlines()
        why = tail[-1] if tail else f"exit {out.returncode}"
        raise ValueError(f"원격 확인 실패 — {why[:160]}")


def _sync_work(message: str = "") -> dict:
    """원격을 확인한 뒤 허용 변경을 한 커밋으로 묶어 origin에 올린다."""
    before = git_detail(strict=True)
    branch = before.get("branch") or ""
    if not branch or branch == "HEAD":
        raise ValueError("브랜치가 없다(detached HEAD) — 동기화하지 않는다")
    upstream = before.get("upstream") or ""
    if not upstream:
        raise ValueError(f"origin/{branch} 원격 추적이 없다 — 동기화하지 않는다")
    if not upstream.startswith("origin/"):
        raise ValueError(f"origin이 아닌 원격({upstream})을 추적 중이다 — 동기화하지 않는다")
    expected_upstream = f"origin/{branch}"
    if upstream != expected_upstream:
        raise ValueError(
            f"현재 브랜치와 다른 원격 브랜치({upstream})를 추적 중이다 — "
            "동기화하지 않는다")
    _fetch_origin(branch)
    checked = git_detail(strict=True)
    if checked.get("behind", 0) > 0:
        raise ValueError(
            f"원격이 {checked['behind']}개 앞서 있다 — 자동 병합하지 않는다")
    staged_blocked = [
        item for item in checked.get("files", [])
        if item.get("staged") and not item.get("allowed")
    ]
    if staged_blocked:
        raise ValueError(
            f"제외 파일 {len(staged_blocked)}개가 이미 스테이징되어 있다 — "
            "다른 커밋에 섞지 않는다")

    commit = None
    if checked.get("allowed", 0) > 0:
        commit = _commit_work(message)

    pushed = 0
    state = git_detail(strict=True)
    if state.get("ahead", 0) > 0:
        pushed = _push_work()["pushed"]
    return {
        "action": "synced" if commit or pushed else "noop",
        "branch": branch,
        "commit": commit,
        "pushed": pushed,
        "at": datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
        "git": git_detail(strict=True),
    }


def sync_work(message: str = "") -> dict:
    """동기화 요청을 직렬화해 여러 탭의 중복 커밋·푸시를 막는다."""
    if not _git_sync_lock.acquire(blocking=False):
        raise ValueError("깃 동기화가 이미 진행 중이다")
    _git_sync_last.update({
        "busy": True, "ok": None, "action": "running",
        "message": "원격 확인 중…",
    })
    try:
        result = _sync_work(message)
    except Exception as e:
        _git_sync_last.update({
            "busy": False,
            "ok": False,
            "action": "error",
            "at": datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
            "message": str(e),
        })
        raise
    else:
        message_out = (
            "이미 최신 상태" if result["action"] == "noop"
            else f"동기화 완료 · {result['pushed']}개 올림"
        )
        _git_sync_last.update({
            "busy": False,
            "ok": True,
            "action": result["action"],
            "at": result["at"],
            "message": message_out,
        })
        return result
    finally:
        _git_sync_lock.release()


def git_sync_status() -> dict:
    return dict(_git_sync_last)


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


def _grok_token() -> str:
    try:
        raw = json.loads(GROK_AUTH.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return ""
    if not isinstance(raw, dict):
        return ""
    for rec in raw.values():
        if isinstance(rec, dict):
            key = rec.get("key") or rec.get("access_token")
            if isinstance(key, str) and key:
                return key
    return ""


def fetch_grok_billing(token: str) -> dict:
    req = urllib.request.Request(
        GROK_BILLING_URL,
        headers={
            "Authorization": "Bearer " + token,
            "Accept": "application/json",
            "x-grok-client-mode": "cli",
        },
        method="GET",
    )
    with urllib.request.urlopen(req, timeout=12) as resp:
        data = json.loads(resp.read().decode("utf-8"))
    if not isinstance(data, dict):
        raise ValueError("billing not object")
    return data


def _fmt_period(iso: str) -> str:
    if not iso:
        return ""
    try:
        dt = datetime.fromisoformat(iso.replace("Z", "+00:00"))
        local = dt.astimezone(ZoneInfo("Asia/Seoul"))
        return f"{local.month}/{local.day}"
    except (ValueError, TypeError, OSError):
        return iso[:10]


def summarize_grok_billing(raw: dict, fetched_at: str = "",
                           error: str | None = None, stale: bool = False) -> dict:
    cfg = raw.get("config") if isinstance(raw, dict) else None
    if not isinstance(cfg, dict):
        cfg = {}
    used = cfg.get("creditUsagePercent")
    try:
        used_pct = float(used)
    except (TypeError, ValueError):
        used_pct = None
    remain_pct = None if used_pct is None else max(0.0, round(100.0 - used_pct, 1))
    if used_pct is not None:
        used_pct = round(used_pct, 1)
    period = cfg.get("currentPeriod") if isinstance(cfg.get("currentPeriod"), dict) else {}
    ptype = str(period.get("type") or "")
    period_label = "이번 주" if "WEEKLY" in ptype else ("이번 달" if "MONTH" in ptype else "")
    products = []
    for row in cfg.get("productUsage") or []:
        if not isinstance(row, dict):
            continue
        pid = str(row.get("product") or "")
        try:
            pct = float(row.get("usagePercent"))
        except (TypeError, ValueError):
            continue
        products.append({
            "id": pid,
            "label": _GROK_PRODUCTS.get(pid, pid),
            "used_pct": round(pct, 1),
        })
    return {
        "ok": used_pct is not None and not error,
        "used_pct": used_pct,
        "remain_pct": remain_pct,
        "period": period_label,
        "period_start": _fmt_period(str(period.get("start") or cfg.get("billingPeriodStart") or "")),
        "period_end": _fmt_period(str(period.get("end") or cfg.get("billingPeriodEnd") or "")),
        "products": products,
        "fetched_at": fetched_at,
        "stale": stale,
        "error": error,
    }


def _load_usage_disk() -> dict | None:
    try:
        data = json.loads(GROK_USAGE_CACHE.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError):
        return None
    return data if isinstance(data, dict) else None


def _save_usage_disk(data: dict) -> None:
    try:
        GROK_USAGE_CACHE.write_text(
            json.dumps(data, ensure_ascii=False), encoding="utf-8")
    except OSError:
        pass


def grok_usage(now: float | None = None, fetch=None, force: bool = False) -> dict:
    """그록 주간 한도. 실패해도 캐시가 있으면 그걸 보여 준다."""
    global _usage_mem, _usage_at
    import time
    now = time.time() if now is None else now
    fetch = fetch or fetch_grok_billing
    with _usage_lock:
        if (not force and _usage_mem is not None
                and now - _usage_at < _GROK_USAGE_TTL):
            return _usage_mem
        token = _grok_token()
        if not token:
            cached = _usage_mem or _load_usage_disk()
            if cached:
                out = dict(cached)
                out["stale"] = True
                out["ok"] = False
                out["error"] = out.get("error") or "그록 로그인 없음"
                _usage_mem, _usage_at = out, now
                return out
            return summarize_grok_billing({}, error="그록 로그인 없음")
        try:
            raw = fetch(token)
            out = summarize_grok_billing(
                raw, fetched_at=datetime.now().strftime("%H:%M"))
            if out.get("remain_pct") is None:
                raise ValueError("billing missing usage percent")
            _usage_mem, _usage_at = out, now
            _save_usage_disk(out)
            return out
        except urllib.error.HTTPError as e:
            err = "그록 로그인 만료" if e.code in (401, 403) else f"사용량 HTTP {e.code}"
        except (urllib.error.URLError, TimeoutError, ValueError, json.JSONDecodeError, OSError):
            err = "사용량을 못 읽음"
        cached = _usage_mem or _load_usage_disk()
        if cached:
            out = dict(cached)
            out["stale"] = True
            out["ok"] = False
            out["error"] = err
            _usage_mem, _usage_at = out, now
            return out
        return summarize_grok_billing({}, error=err)


def _first_access_token(obj) -> str:
    if isinstance(obj, dict):
        for k in ("accessToken", "access_token"):
            v = obj.get(k)
            if isinstance(v, str) and v:
                return v
        for v in obj.values():
            t = _first_access_token(v)
            if t:
                return t
    return ""


def _claude_token() -> str:
    try:
        raw = subprocess.check_output(
            ["security", "find-generic-password", "-s", "Claude Code-credentials", "-w"],
            text=True, timeout=6, stderr=subprocess.DEVNULL,
        )
    except (OSError, subprocess.CalledProcessError, subprocess.TimeoutExpired):
        return ""
    try:
        data = json.loads(raw)
    except json.JSONDecodeError:
        return ""
    return _first_access_token(data)


def fetch_claude_usage(token: str) -> dict:
    req = urllib.request.Request(
        CLAUDE_USAGE_URL,
        headers={
            "Authorization": "Bearer " + token,
            "Accept": "application/json",
            "anthropic-beta": "oauth-2025-04-20",
        },
        method="GET",
    )
    with urllib.request.urlopen(req, timeout=12) as resp:
        data = json.loads(resp.read().decode("utf-8"))
    if not isinstance(data, dict):
        raise ValueError("claude usage not object")
    return data


def _window_used(block) -> float | None:
    if not isinstance(block, dict):
        return None
    try:
        return float(block.get("utilization"))
    except (TypeError, ValueError):
        return None


def summarize_claude_usage(raw: dict, fetched_at: str = "",
                           error: str | None = None, stale: bool = False) -> dict:
    products = []
    for key, label in (("five_hour", "5시간"), ("seven_day", "주간")):
        used = _window_used((raw or {}).get(key))
        if used is None:
            continue
        products.append({"id": key, "label": label, "used_pct": round(used, 1)})
    used_pct = None
    if products:
        used_pct = max(p["used_pct"] for p in products)
    remain_pct = None if used_pct is None else max(0.0, round(100.0 - used_pct, 1))
    week = (raw or {}).get("seven_day") if isinstance(raw, dict) else None
    reset = ""
    if isinstance(week, dict):
        reset = _fmt_period(str(week.get("resets_at") or ""))
    return {
        "ok": used_pct is not None and not error,
        "used_pct": used_pct,
        "remain_pct": remain_pct,
        "period": "주간" if any(p["id"] == "seven_day" for p in products) else "5시간",
        "period_start": "",
        "period_end": reset,
        "products": products,
        "plan": "Max",
        "fetched_at": fetched_at,
        "stale": stale,
        "error": error,
    }


_claude_mem: dict | None = None
_claude_at = 0.0
_claude_lock = threading.Lock()


def claude_usage(now: float | None = None, fetch=None, force: bool = False) -> dict:
    """클로드 구독(Max) 5시간·주간. 실패해도 캐시가 있으면 그걸 보여 준다."""
    global _claude_mem, _claude_at
    import time
    now = time.time() if now is None else now
    fetch = fetch or fetch_claude_usage
    with _claude_lock:
        if (not force and _claude_mem is not None
                and now - _claude_at < _USAGE_TTL):
            return _claude_mem
        token = _claude_token()
        if not token:
            cached = _claude_mem or _load_named_usage(CLAUDE_USAGE_CACHE)
            if cached:
                out = dict(cached)
                out["stale"] = True
                out["ok"] = False
                out["error"] = out.get("error") or "클로드 로그인 없음"
                _claude_mem, _claude_at = out, now
                return out
            return summarize_claude_usage({}, error="클로드 로그인 없음")
        try:
            raw = fetch(token)
            out = summarize_claude_usage(
                raw, fetched_at=datetime.now().strftime("%H:%M"))
            if out.get("remain_pct") is None:
                raise ValueError("claude usage missing percent")
            _claude_mem, _claude_at = out, now
            _save_named_usage(CLAUDE_USAGE_CACHE, out)
            return out
        except urllib.error.HTTPError as e:
            err = "클로드 로그인 만료" if e.code in (401, 403) else f"클로드 HTTP {e.code}"
        except (urllib.error.URLError, TimeoutError, ValueError, json.JSONDecodeError, OSError):
            err = "클로드 사용량을 못 읽음"
        cached = _claude_mem or _load_named_usage(CLAUDE_USAGE_CACHE)
        if cached:
            out = dict(cached)
            out["stale"] = True
            out["ok"] = False
            out["error"] = err
            _claude_mem, _claude_at = out, now
            return out
        return summarize_claude_usage({}, error=err)


def _codex_auth() -> tuple[str, str]:
    try:
        data = json.loads(CODEX_AUTH.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return "", ""
    if not isinstance(data, dict):
        return "", ""
    tokens = data.get("tokens") if isinstance(data.get("tokens"), dict) else {}
    tok = tokens.get("access_token") or ""
    acct = tokens.get("account_id") or ""
    return (tok if isinstance(tok, str) else ""), (acct if isinstance(acct, str) else "")


def fetch_codex_usage(token: str, account_id: str = "") -> dict:
    headers = {
        "Authorization": "Bearer " + token,
        "Accept": "application/json",
    }
    if account_id:
        headers["ChatGPT-Account-Id"] = account_id
    req = urllib.request.Request(CODEX_USAGE_URL, headers=headers, method="GET")
    with urllib.request.urlopen(req, timeout=12) as resp:
        data = json.loads(resp.read().decode("utf-8"))
    if not isinstance(data, dict):
        raise ValueError("codex usage not object")
    return data


def _fmt_unix(ts) -> str:
    try:
        n = int(ts)
    except (TypeError, ValueError):
        return ""
    if n <= 0:
        return ""
    try:
        dt = datetime.fromtimestamp(n, tz=timezone.utc).astimezone(ZoneInfo("Asia/Seoul"))
        return f"{dt.month}/{dt.day}"
    except (OverflowError, OSError, ValueError):
        return ""


def _window_seconds_label(seconds) -> str:
    try:
        n = int(seconds)
    except (TypeError, ValueError):
        return ""
    if n >= 500_000:
        return "이번 주"
    if n >= 10_000:
        return "5시간"
    if n > 0:
        return f"{max(1, n // 3600)}시간"
    return ""


def summarize_codex_usage(raw: dict, fetched_at: str = "",
                          error: str | None = None, stale: bool = False) -> dict:
    rl = (raw or {}).get("rate_limit") if isinstance(raw, dict) else None
    if not isinstance(rl, dict):
        rl = {}
    win = rl.get("primary_window") if isinstance(rl.get("primary_window"), dict) else {}
    used_pct = None
    try:
        used_pct = float(win.get("used_percent"))
    except (TypeError, ValueError):
        used_pct = None
    remain_pct = None if used_pct is None else max(0.0, round(100.0 - used_pct, 1))
    if used_pct is not None:
        used_pct = round(used_pct, 1)
    period = _window_seconds_label(win.get("limit_window_seconds"))
    products = []
    if used_pct is not None:
        products.append({
            "id": "primary",
            "label": period or "한도",
            "used_pct": used_pct,
        })
    sec = rl.get("secondary_window") if isinstance(rl.get("secondary_window"), dict) else None
    if sec:
        try:
            s_used = round(float(sec.get("used_percent")), 1)
            products.append({
                "id": "secondary",
                "label": _window_seconds_label(sec.get("limit_window_seconds")) or "보조",
                "used_pct": s_used,
            })
        except (TypeError, ValueError):
            pass
    plan = str((raw or {}).get("plan_type") or "")
    plan_label = {"plus": "Plus", "pro": "Pro", "free": "Free", "team": "Team"}.get(plan, plan)
    return {
        "ok": used_pct is not None and not error,
        "used_pct": used_pct,
        "remain_pct": remain_pct,
        "period": period,
        "period_start": "",
        "period_end": _fmt_unix(win.get("reset_at")),
        "products": products,
        "plan": plan_label,
        "fetched_at": fetched_at,
        "stale": stale,
        "error": error,
    }


_codex_mem: dict | None = None
_codex_at = 0.0
_codex_lock = threading.Lock()


def _load_named_usage(path: Path) -> dict | None:
    try:
        data = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError):
        return None
    return data if isinstance(data, dict) else None


def _save_named_usage(path: Path, data: dict) -> None:
    try:
        path.write_text(json.dumps(data, ensure_ascii=False), encoding="utf-8")
    except OSError:
        pass


def codex_usage(now: float | None = None, fetch=None, force: bool = False) -> dict:
    """코덱스(ChatGPT Plus) 한도. 실패해도 캐시가 있으면 그걸 보여 준다."""
    global _codex_mem, _codex_at
    import time
    now = time.time() if now is None else now
    fetch = fetch or (lambda token, account_id="": fetch_codex_usage(token, account_id))
    with _codex_lock:
        if (not force and _codex_mem is not None
                and now - _codex_at < _USAGE_TTL):
            return _codex_mem
        token, account_id = _codex_auth()
        if not token:
            cached = _codex_mem or _load_named_usage(CODEX_USAGE_CACHE)
            if cached:
                out = dict(cached)
                out["stale"] = True
                out["ok"] = False
                out["error"] = out.get("error") or "코덱스 로그인 없음"
                _codex_mem, _codex_at = out, now
                return out
            return summarize_codex_usage({}, error="코덱스 로그인 없음")
        try:
            raw = fetch(token, account_id)
            out = summarize_codex_usage(
                raw, fetched_at=datetime.now().strftime("%H:%M"))
            if out.get("remain_pct") is None:
                raise ValueError("codex usage missing percent")
            _codex_mem, _codex_at = out, now
            _save_named_usage(CODEX_USAGE_CACHE, out)
            return out
        except urllib.error.HTTPError as e:
            err = "코덱스 로그인 만료" if e.code in (401, 403) else f"코덱스 HTTP {e.code}"
        except (urllib.error.URLError, TimeoutError, ValueError, json.JSONDecodeError, OSError, TypeError):
            err = "코덱스 사용량을 못 읽음"
        cached = _codex_mem or _load_named_usage(CODEX_USAGE_CACHE)
        if cached:
            out = dict(cached)
            out["stale"] = True
            out["ok"] = False
            out["error"] = err
            _codex_mem, _codex_at = out, now
            return out
        return summarize_codex_usage({}, error=err)



def parse_worklog_todos(text: str, limit: int = 80) -> list[dict]:
    """GAME_WORKLOG 「아직 안 한 것」 번호 목록."""
    m = re.search(r"^##\s+아직 안 한 것[^\n]*\n(.*?)(?=^##\s|\Z)", text, re.M | re.S)
    if not m:
        return []
    out = []
    for line in m.group(1).splitlines():
        hit = re.match(r"^(\d+)\.\s+\*\*(.+?)\*\*\s*(?:—|-)?\s*(.*)$", line.strip())
        if not hit:
            hit = re.match(r"^(\d+)\.\s+(.+)$", line.strip())
            if not hit:
                continue
            title, detail = hit.group(2).strip(), ""
        else:
            title, detail = hit.group(2).strip(), (hit.group(3) or "").strip()
        if not title:
            continue
        out.append({
            "n": int(hit.group(1)),
            "id": item_id("wl:" + title),
            "title": title[:160],
            "detail": detail[:300],
        })
        if len(out) >= limit:
            break
    return out


def parse_blockers(status: str) -> list[dict]:
    """STATUS 「막힌 것」 불릿."""
    m = re.search(r"^##\s+막힌[^\n]*\n(.*?)(?=^##\s|\Z)", status, re.M | re.S)
    if not m:
        return []
    out = []
    for line in m.group(1).splitlines():
        line = line.strip()
        if not line.startswith("-"):
            continue
        body = line.lstrip("- ").strip()
        if not body:
            continue
        title = body.split("—")[0].split("-")[0].strip()[:120]
        out.append({
            "id": item_id("blk:" + title),
            "title": title,
            "detail": body[:300],
        })
    return out


def _lap_role_error(stem: str, raw: str) -> str:
    """역할 로그에서 Claude/API 오류 메시지 추출. JSON result 우선."""
    for line in raw.splitlines():
        line = line.strip()
        if not line.startswith("{"):
            continue
        try:
            obj = json.loads(line)
        except json.JSONDecodeError:
            continue
        if not isinstance(obj, dict):
            continue
        result = obj.get("result")
        status = obj.get("api_error_status")
        is_err = bool(obj.get("is_error"))
        terminal = str(obj.get("terminal_reason") or "")
        if not (is_err or status or terminal == "api_error"):
            continue
        msg = result.strip() if isinstance(result, str) and result.strip() else ""
        if msg:
            return msg[:220]
        if status:
            return f"API {status}"
        if terminal:
            return terminal[:120]
    low = raw.lower()
    if stem.endswith("claude") and (
        "weekly limit" in low or "hit your weekly" in low or "주간 한도" in raw
        or '"api_error_status":429' in raw
    ):
        return "Claude 주간 한도"
    if stem.endswith("claude") and '"is_error":true' in raw and "api_error" in low:
        return "Claude API 오류"
    if "traceback (most recent" in low:
        return "예외"
    return ""


def latest_lap_info() -> dict:
    """logs/YYYY-MM-DD/ 아래 최신 바퀴 폴더와 role 로그 요약."""
    logs = ROOT / "logs"
    if not logs.is_dir():
        return {"ok": False, "error": "logs 없음"}
    dated = sorted([p for p in logs.iterdir() if p.is_dir() and re.match(r"\d{4}-\d{2}-\d{2}$", p.name)])
    if not dated:
        return {"ok": False, "error": "날짜 로그 없음"}
    day = dated[-1]
    laps = sorted(
        [p for p in day.iterdir() if p.is_dir() and not p.name.startswith("smoke")],
        key=lambda p: p.stat().st_mtime,
    )
    if not laps:
        # flat lap-*.log only
        laps_files = sorted(day.glob("lap-*.log"), key=lambda p: p.stat().st_mtime)
        return {
            "ok": True,
            "day": day.name,
            "id": laps_files[-1].name if laps_files else "",
            "path": str(day.relative_to(ROOT)) if laps_files else str(day),
            "roles": [],
            "errors": [],
            "mtime": _hhmm(laps_files[-1].stat().st_mtime) if laps_files else "",
        }
    lap = laps[-1]
    roles = []
    errors = []
    for f in sorted(lap.glob("*.log")):
        raw = _read(f)
        tail = "\n".join(raw.strip().splitlines()[-8:]) if raw.strip() else ""
        err = _lap_role_error(f.stem, raw)
        roles.append({
            "name": f.stem,
            "bytes": f.stat().st_size,
            "tail": humanize_detail(tail, 220) if tail else "(비어 있음)",
            "error": err,
        })
        if err:
            errors.append(f"{f.stem}: {err}")
    return {
        "ok": True,
        "day": day.name,
        "id": lap.name,
        "path": str(lap.relative_to(ROOT)),
        "roles": roles,
        "errors": errors,
        "mtime": _hhmm(lap.stat().st_mtime),
    }

def provider_health() -> dict:
    """env LOOP_PROVIDERS + 간단 바이너리/한도 힌트."""
    raw = os.environ.get("LOOP_PROVIDERS", "claude,codex,grok")
    names = [x.strip() for x in raw.replace("·", ",").split(",") if x.strip()]
    # also check App Support env if present
    app_env = Path.home() / "Library/Application Support/AI Lab Autonomous Loop/env.sh"
    if app_env.is_file():
        for line in app_env.read_text(encoding="utf-8", errors="replace").splitlines():
            if line.startswith("export LOOP_PROVIDERS="):
                val = line.split("=", 1)[1].split("#", 1)[0].strip().strip('"').strip("'")
                m = re.search(r"\$\{LOOP_PROVIDERS:-([^}]+)\}", val)
                if m:
                    val = m.group(1)
                names = [x.strip().strip('"').strip("'") for x in val.split(",") if x.strip()]
    return {"providers": names, "note": "Claude 주간 한도 시 codex·grok만 쓰는 게 안전"}




def make_status_snip(status: str, limit: int = 400) -> str:
    """보드용 요약 — 큐 맨 위·막힘·History 한 줄을 우선."""
    bits: list[str] = []
    q = parse_queue(status)
    if q:
        bits.append("다음: " + (q[0].get("title") or ""))
        if len(q) > 1:
            bits.append("외 " + str(len(q) - 1) + "건")
    hist = parse_history_table(status)
    if hist:
        bits.append("완료: " + (hist[0].get("title") or ""))
    blockers = parse_blockers(status)
    if blockers:
        bits.append("막힘: " + (blockers[0].get("title") or ""))
    if bits:
        return humanize_detail(" · ".join(bits), limit)
    return humanize_detail(status[:1200], limit)


def _tcp_open(port: int, host: str = "127.0.0.1", timeout: float = 0.5) -> bool:
    import socket
    s = socket.socket()
    s.settimeout(timeout)
    try:
        s.connect((host, port))
        return True
    except OSError:
        return False
    finally:
        s.close()


def _pgrep(pattern: str) -> bool:
    try:
        r = subprocess.run(["pgrep", "-f", pattern], capture_output=True, timeout=5)
        return r.returncode == 0
    except (OSError, subprocess.SubprocessError):
        return False


def mcp_health() -> dict:
    """에이전트가 쓰는 MCP 연결 상태 — 유니티(6400)·블렌더(앱+브리지 9876)."""
    unity = _tcp_open(6400)
    blender_app = _pgrep(r"Blender\.app/Contents/MacOS/Blender")
    blender_bridge = _tcp_open(9876) if blender_app else False
    return {
        "unity": unity,
        "blender_app": blender_app,
        "blender_bridge": blender_bridge,
        "note": ("유니티 MCP 연결됨" if unity else "유니티 에디터 꺼짐/브리지 없음")
        + " · "
        + ("블렌더 브리지 연결됨" if blender_bridge else "블렌더 꺼짐(3D 작업 시 켜라)"),
    }


_ENV_LINE = re.compile(r"^export (LOOP_[A-Z_]+)=")


def _env_defaults(path: Path) -> dict[str, str]:
    out: dict[str, str] = {}
    if not path.is_file():
        return out
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        m = _ENV_LINE.match(line.strip())
        if not m:
            continue
        val = line.split("=", 1)[1].split("#", 1)[0].strip()
        dm = re.search(r"\$\{[^:}]+:-([^}]*)\}", val)
        out[m.group(1)] = (dm.group(1) if dm else val).strip("\"'")
    return out


def runner_info() -> dict:
    """루프 실행기 — 어떤 에이전트·모델로 도는지 + 살아 있는 세션 수."""
    vals: dict[str, str] = {}
    for base in (Path.home() / "Library/Application Support/AI Lab Autonomous Loop/env.sh",
                 HERE / "env.sh"):
        vals.update(_env_defaults(base))
    live_opencode = _pgrep(r"opencode run -m")
    live_grok = _pgrep(r"grok .*--model") or _pgrep(r"/grok --model")
    return {
        "agent": vals.get("LOOP_AGENT", "?"),
        "model": vals.get("LOOP_OPENCODE_MODEL", ""),
        "council_every": vals.get("LOOP_COUNCIL_EVERY", "4"),
        "live": {"opencode": live_opencode, "grok": live_grok},
    }


def council_info() -> dict:
    """최근 정기 회의 문서."""
    meet = ROOT / "docs" / "meetings"
    files = sorted(meet.glob("COUNCIL_*.md"), key=lambda p: p.stat().st_mtime) if meet.is_dir() else []
    latest = files[-1] if files else None
    info: dict = {"count": len(files), "latest": "", "when": "", "title": ""}
    if latest:
        info["latest"] = str(latest.relative_to(ROOT))
        ts = datetime.fromtimestamp(latest.stat().st_mtime, ZoneInfo("Asia/Seoul"))
        info["when"] = ts.strftime("%m-%d %H:%M")
        head = latest.read_text(encoding="utf-8", errors="replace").splitlines()
        for line in head[:6]:
            if line.startswith("#"):
                info["title"] = humanize_title(line.lstrip("# ").strip())
                break
    return info


def proposals_info() -> dict:
    """자가학습 개선안 수함대 — 미처리(표식 없음) 건수와 최근 1건."""
    path = ROOT / "docs" / "feedback" / "PROPOSALS.md"
    total = open_count = 0
    last = ""
    if path.is_file():
        for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
            t = line.strip()
            if not t.startswith("- [") and not re.match(r"^- \d{4}-", t):
                continue
            if t.startswith("- [시각]") or "없음:" in t:
                continue
            total += 1
            if "✅" not in t and "⏸" not in t:
                open_count += 1
                last = t.lstrip("- ")
    return {"total": total, "open": open_count, "last": humanize_detail(last)}


def keeper_info() -> dict:
    """보드 지킴이 최근 검증 결과 — loop/board_keeper.json."""
    path = HERE / "board_keeper.json"
    if not path.is_file():
        return {"ok": None, "when": "", "failed": []}
    try:
        d = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return {"ok": None, "when": "", "failed": ["기록 파싱 실패"]}
    return {
        "ok": bool(d.get("ok")),
        "when": str(d.get("at", "")),
        "failed": [str(x) for x in (d.get("failed") or [])][:4],
        "warns": [str(x) for x in (d.get("warns") or [])][:2],
    }


def _usages_parallel(budget: float = 3.0) -> dict:
    """사용량 3종(grok·claude·codex)을 병렬로, 전체 예산(기본 3초) 안에서만 기다린다.
    느리면 그 항목만 빈 값으로 보여 주고 화면 전체가 막히는 일을 없앤다 (2026-08-23).
    2026-08-24(보드응답:000 재발 차단): 옛 구현은 항목당 result(timeout=6)가 순차 누적되어
    최악 18초, with 블록 종료 시 잔여 스레드까지 기다려 실질 상한이 없었다.
    마감시간 하나로 묶고 예산 초과분은 기다리지 않는다 — 남은 스레드는 각자 타임아웃 안에서
    조용히 끝나며 다음 요청이 쓸 메모리 캐시만 채우고 간다."""
    import concurrent.futures
    out: dict[str, dict] = {}
    jobs = {"grok": grok_usage, "claude": claude_usage, "codex": codex_usage}
    deadline = time.monotonic() + budget
    pool = concurrent.futures.ThreadPoolExecutor(max_workers=len(jobs))
    try:
        futs = {key: pool.submit(fn) for key, fn in jobs.items()}
        for key, fut in futs.items():
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                out[key] = {}
                continue
            try:
                out[key] = fut.result(timeout=remaining)
            except Exception:
                out[key] = {}
    finally:
        pool.shutdown(wait=False)
    return out


def _live_stamp() -> str:
    """보드가 화면에 그리는 입력들의 지문. 하나라도 바뀌면 SSE가 즉시 refresh를 보낸다."""
    paths = [
        STATUS, INBOX, WORKLOG, DESIGN, GAME_DESIGN,
        ROOT / "logs" / "loop_main.log",
        ROOT / ".git" / "HEAD",
        ROOT / ".git" / "refs" / "heads" / "master",
        HERE / "board_keeper.json",
        HERE / "STOP",
        HERE / "HOLD",
        HERE / "last_test_report.json",
        ROOT / "output" / "qa" / "ashes-to-stars" / "v4_playtest_dummy" / "dummy_report.json",
    ]
    day = ROOT / "logs" / datetime.now().strftime("%Y-%m-%d")
    if day.is_dir():
        laps = list(day.glob("lap-*.log"))
        if laps:
            try:
                paths.append(max(laps, key=lambda p: p.stat().st_mtime))
            except OSError:
                pass
    bits: list[str] = []
    for p in paths:
        try:
            st = p.stat()
            bits.append(f"{st.st_mtime_ns}:{st.st_size}")
        except OSError:
            bits.append("-")
    return ",".join(bits)


def build_state() -> dict:
    status = _read(STATUS)
    design = _read(DESIGN)
    inbox = _read(INBOX)
    checks = load_checks()
    decisions = load_decisions()
    queue = parse_queue(status)
    miles = parse_milestones(design)
    table = parse_queue_table(status)
    now_list = mark_now_closed(
        parse_now_list(_read(GAME_DESIGN)), design, status, decisions)
    extra = table + now_list
    flags = loop_flags()
    now = dict(flags.get("now") or {})
    if now.get("title"):
        now["title"] = humanize_title(now["title"])
        flags = dict(flags)
        flags["now"] = now
    inbox_box = parse_inbox(inbox)
    inbox_box["waiting"] = _plain_list(inbox_box.get("waiting") or [], "body")
    inbox_box["done"] = _plain_list(inbox_box.get("done") or [], "body")
    git = git_summary()
    worklog = _read(WORKLOG)
    lap = latest_lap_info()
    usages = _usages_parallel()
    return {
        "updated": parse_updated(status),
        "queue": _plain_list(queue),
        "queue_table": _plain_list(table),
        "results": _plain_list(parse_results(status), "body"),
        "milestones": _plain_list(miles),
        "inbox": inbox_box,
        "checks": checks,
        "decisions": decisions,
        "choices": _plain_list(pending_choices(queue, miles, decisions, extra)),
        "loop": flags,
        "commits": recent_commits(),
        "push": {"branch": git.get("branch", ""), "ahead": git.get("ahead", 0)},
        "git": git,
        "sync": git_sync_status(),
        "charts": progress_charts(status, design, _read(GAME_DESIGN), decisions),
        "slice": _plain_list(slice_checks(status, design, _read(GAME_DESIGN))),
        "stuck": _plain_list(stuck_items(status, flags)),
        "blockers": _plain_list(parse_blockers(status)),
        "worklog": _plain_list(parse_worklog_todos(worklog)),
        "lap": lap,
        "handoff": load_handoff_state(),
        "providers": provider_health(),
        "mcp": mcp_health(),
        "runner": runner_info(),
        "council": council_info(),
        "proposals": proposals_info(),
        "keeper": keeper_info(),
        "completed": _plain_list(completed_posts(status)),
        "playtest": playtest_state(),
        "grok": usages.get("grok", {}),
        "claude": usages.get("claude", {}),
        "codex": usages.get("codex", {}),
        "commands": _plain_list(load_commands()[:40], "body"),
        "tests": load_test_report(),
        "status_snip": make_status_snip(status),
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

    def _events(self, once: bool = False) -> None:
        """파일·깃·바퀴 로그가 바뀌면 바로 신호를 보낸다. 브라우저 타이머에 의존하지 않는다."""
        self.send_response(200)
        self.send_header("Content-Type", "text/event-stream; charset=utf-8")
        self.send_header("Cache-Control", "no-cache")
        self.send_header("X-Accel-Buffering", "no")
        self.end_headers()
        limit = 1 if once else 1200  # 0.5초 × 1200 ≈ 10분 뒤 EventSource가 재연결
        stamp = ""
        last_emit = -10**9
        try:
            for sequence in range(limit):
                now = _live_stamp()
                changed = now != stamp
                stamp = now
                heartbeat = (sequence - last_emit) >= 10
                if sequence == 0 or changed or heartbeat:
                    last_emit = sequence
                    payload = (
                        f"event: refresh\n"
                        f"data: {{\"sequence\":{sequence},\"changed\":{str(changed).lower()}}}\n\n"
                    ).encode("utf-8")
                    self.wfile.write(payload)
                    self.wfile.flush()
                if once:
                    return
                time.sleep(0.5)
        except (BrokenPipeError, ConnectionAbortedError, ConnectionResetError):
            return

    def _post_is_trusted(self) -> bool:
        """브라우저 form/교차 출처 요청이 로컬 Git을 바꾸지 못하게 한다."""
        if self.headers.get_content_type() != "application/json":
            self._json(415, {"ok": False, "error": "JSON 요청만 허용한다"})
            return False
        origin = (self.headers.get("Origin") or "").strip()
        if not origin:
            return True  # 로컬 CLI·테스트 클라이언트
        parsed = urlparse(origin)
        host = (self.headers.get("Host") or "").strip().lower()
        if parsed.scheme not in ("http", "https") or parsed.netloc.lower() != host:
            self._json(403, {"ok": False, "error": "다른 출처의 변경 요청을 거부한다"})
            return False
        return True

    def do_GET(self) -> None:
        parsed = urlparse(self.path)
        path = parsed.path
        if path in ("/", "/index.html"):
            html = HERE / "board.html"
            try:
                body = html.read_bytes()
            except OSError as e:
                body = f"board.html 없음: {e}".encode("utf-8")
                self.send_response(500)
            else:
                # 첫 화면에 상태를 직접 심는다 — 브라우저 fetch가 어떤 사정으로
                # 막혀도 보드는 반드시 그려진다 (2026-08-23, 조용한 빈 화면 재발 방지)
                try:
                    seed = json.dumps(build_state(), ensure_ascii=False).replace("<", "\\u003c")
                    injected = (f'<script>window.__STATE__={seed};</script>'
                                ).encode("utf-8")
                    marker = b"<script>"
                    idx = body.find(marker)
                    if idx != -1:
                        body = body[:idx] + injected + body[idx:]
                except Exception:
                    pass  # 심기 실패 시에도 기존 방식(fetch)으로 동작한다
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
        if path == "/api/git":
            self._json(200, {
                "ok": True,
                "git": git_detail(),
                "sync": git_sync_status(),
            })
            return
        if path == "/api/events":
            self._events(parse_qs(parsed.query).get("once") == ["1"])
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
        if not self._post_is_trusted():
            return
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
            if path == "/api/sync":
                result = sync_work(str(data.get("message") or ""))
                self._json(200, {"ok": True, **result})
                return
            if path == "/api/push":
                self._json(200, {"ok": True, **push_work()})
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
    host = os.getenv("BOARD_HOST", "127.0.0.1")
    srv = ThreadingHTTPServer((host, PORT), Handler)
    print(f"재와 별 개발 보드  (ROOT={ROOT})")
    print(f"  이 기기: http://127.0.0.1:{PORT}/")
    if host != "127.0.0.1":
        print(f"  다른 기기: http://{_lan_ip()}:{PORT}/")
    print("  Ctrl+C 로 종료")
    if os.getenv("BOARD_NO_BROWSER") != "1" and sys.stdin.isatty():
        threading.Timer(0.5, lambda: webbrowser.open(f"http://127.0.0.1:{PORT}/")).start()
    try:
        srv.serve_forever()
    except KeyboardInterrupt:
        print("\n종료")
        srv.shutdown()


def cli_command(argv: list[str]) -> int:
    """python3 loop/board.py command 제목 [본문] [--source chat] [--status done] [--inbox]"""
    args = argv[2:]
    source = "chat"
    status = "done"
    inbox = False
    positional: list[str] = []
    i = 0
    while i < len(args):
        a = args[i]
        if a == "--source" and i + 1 < len(args):
            source = args[i + 1]
            i += 2
            continue
        if a == "--status" and i + 1 < len(args):
            status = args[i + 1]
            i += 2
            continue
        if a == "--inbox":
            inbox = True
            i += 1
            continue
        positional.append(a)
        i += 1
    if not positional:
        print("제목이 비어 있다", file=sys.stderr)
        return 2
    title = positional[0]
    body = positional[1] if len(positional) > 1 else ""
    if inbox or status == "open":
        stamp = write_request(title, body, source=source)
        rec = {"at": stamp, "title": title[:80], "body": body[:400],
               "source": source, "status": "open", "inbox": True}
    else:
        rec = record_command(title, body, source=source, status=status)
    print(json.dumps(rec, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "command":
        raise SystemExit(cli_command(sys.argv))
    main()
