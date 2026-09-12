#!/usr/bin/env python3
"""Stamp docs/STATUS.md after every wheel. Keeps the body (아트/시스템 표) intact."""
from __future__ import annotations

import argparse
import json
import re
import subprocess
from pathlib import Path

START = "<!-- loop-stamp:start -->"
END = "<!-- loop-stamp:end -->"
RESULT_KO = {"success": "성공", "fail": "실패", "timeout": "타임아웃"}


def git(root: Path, *args: str) -> str:
    try:
        return subprocess.check_output(
            ["git", *args],
            cwd=str(root),
            text=True,
            stderr=subprocess.DEVNULL,
        ).strip()
    except Exception:
        return ""


def inbox_open(root: Path) -> int:
    p = root / "docs" / "feedback" / "INBOX.md"
    if not p.exists():
        return 0
    n = 0
    in_items = False
    for line in p.read_text(encoding="utf-8").splitlines():
        if line.strip() == "## 항목":
            in_items = True
            continue
        if in_items and re.match(r"^- \[ \]", line):
            n += 1
    return n


def history_rows(root: Path, limit: int = 30) -> list[dict]:
    p = root / "logs" / "loop_history.jsonl"
    if not p.exists():
        return []
    rows = []
    for line in p.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line:
            continue
        try:
            rows.append(json.loads(line))
        except Exception:
            continue
    return rows[-limit:]


def wheel_commits(root: Path, started: str, ended: str) -> list[str]:
    if not started:
        return []
    out = git(
        root,
        "log",
        "--since",
        started,
        "--until",
        ended or started,
        "--pretty=%h %s",
        "--",
        ".",
    )
    return [ln for ln in out.splitlines() if ln.strip()]


def log_tail(path: Path, n: int = 8) -> str:
    if not path.exists():
        return ""
    lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
    skip = ("========", "사유:")
    body = [ln.strip() for ln in lines if ln.strip() and not ln.startswith(skip)]
    if not body:
        return ""
    return body[-1][:240]


def split_body(text: str) -> str:
    if END in text:
        return text.split(END, 1)[1].lstrip("\n")
    m = re.search(r"^## 아트 방식\s*$", text, re.M)
    if m:
        return text[m.start() :]
    m = re.search(r"^## ", text, re.M)
    if m:
        return text[m.start() :]
    return text


def build_stamp(args, root: Path) -> str:
    ko = RESULT_KO.get(args.result, args.result or "?")
    head = git(root, "log", "-1", "--pretty=%h %s", "--", ".")
    branch = git(root, "rev-parse", "--abbrev-ref", "HEAD")
    inbox = inbox_open(root)
    commits = wheel_commits(root, args.started, args.ended)
    summary = (args.reason or "").strip()
    if not summary and args.log:
        summary = log_tail(Path(args.log))
    if not summary:
        summary = "(세션이 본문을 남기지 않음)"
    hist = history_rows(root)
    # include this wheel if history was written first
    rows = []
    for row in hist:
        rows.append(
            f"| #{row.get('loop')} | {RESULT_KO.get(row.get('result'), row.get('result'))} "
            f"| {row.get('ended_at') or row.get('started_at') or ''} "
            f"| {row.get('model') or ''} | {row.get('elapsed_sec') or 0}s |"
        )
    if not any(f"| #{args.loop} |" in r for r in rows):
        rows.append(
            f"| #{args.loop} | {ko} | {args.ended} | {args.model} | {args.elapsed}s |"
        )
    commit_lines = "\n".join(f"- `{c}`" for c in commits) if commits else "- (이 바퀴 커밋 없음)"
    return (
        f"{START}\n"
        f"마지막 바퀴: **#{args.loop}** ({ko}) · {args.ended}\n"
        f"{summary}\n\n"
        f"- 모델 `{args.model}` · 경과 {args.elapsed}s · 세션 rc `{args.rc}`\n"
        f"- HEAD `{head}` · 브랜치 `{branch or '?'}`\n"
        f"- INBOX 미처리 **{inbox}**건\n"
        f"- 이 바퀴 커밋:\n{commit_lines}\n\n"
        f"## 바퀴 기록\n\n"
        f"| 바퀴 | 결과 | 시각 | 모델 | 경과 |\n"
        f"|---|---|---|---|---|\n"
        + "\n".join(rows[-30:])
        + f"\n{END}\n"
    )


SKELETON_BODY = """## 아트 방식

(0번째 바퀴에서 채움)

## 시스템 상태

| 시스템 | 상태 | 이번 근거 |
|---|---|---|
| (기획서 목록) | 미확인 | 아직 없음 |

## 진행 중이던 작업

없음.

## 발견한 문제

없음.

## 완료한 것 (이 바퀴)

없음.

## 지금 하는 것

없음.

## 다음 할 것 (우선순위 → board.json)

없음.

## 막힌 것 (사람 결정)

없음.
"""


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", required=True)
    ap.add_argument("--loop", required=True)
    ap.add_argument("--result", default="")
    ap.add_argument("--started", default="")
    ap.add_argument("--ended", default="")
    ap.add_argument("--elapsed", default="0")
    ap.add_argument("--model", default="")
    ap.add_argument("--reason", default="")
    ap.add_argument("--log", default="")
    ap.add_argument("--rc", default="")
    args = ap.parse_args()
    root = Path(args.root).resolve()
    path = root / "docs" / "STATUS.md"
    path.parent.mkdir(parents=True, exist_ok=True)
    prev = path.read_text(encoding="utf-8") if path.exists() else "# 울온 현황\n\n" + SKELETON_BODY
    if not prev.startswith("# "):
        prev = "# 울온 현황\n\n" + prev
    body = split_body(prev)
    if not body.startswith("## "):
        body = SKELETON_BODY
    title = "# 울온 현황\n\n"
    text = title + build_stamp(args, root) + "\n" + body.lstrip()
    if not text.endswith("\n"):
        text += "\n"
    path.write_text(text, encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
