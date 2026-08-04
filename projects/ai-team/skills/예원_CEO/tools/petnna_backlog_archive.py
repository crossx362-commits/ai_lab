#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""예원 — 펫나 백로그 아카이브 (오래된 완료 항목 분리, 매일 04:30).

배경(2026-08-04 오너 지시): backlog.json이 440건 353KB까지 커졌고 그중 376건(85%)이
'완료'였다. 백로그에 쓰는 도구가 7곳인데 전부 파일 전체를 읽고 통째로 덮어쓰므로,
죽은 완료 기록이 매 사이클 왕복하는 순수 낭비이고 동시쓰기 경합 창도 그만큼 넓어진다.

이 스크립트는 얇은 실행기다 — 실제 로직은 `_shared/backlog.archive_completed()`에 있다
(같은 로직을 두 곳에 만들지 않는다, DIRECTIVES §3-4). 락·무손실·중복대조 보존은
그 함수가 책임지고, 회귀 테스트는 `tests/test_backlog_archive.py`(네거티브 컨트롤 포함).

안전성:
  · 완료(status=='완료')만 옮긴다 — 대기·보류·PR대기는 절대 안 건드린다.
  · 최근 KEEP_RECENT_DONE(40)건은 활성에 남긴다 — recent_reviewed_items()가 읽는 창.
  · 아카이브된 제목은 all_known_titles()로 **중복 적재 판정에 계속 참여**한다.
    (이걸 빠뜨리면 이미 만든 기능을 미오·나무가 매주 다시 제안한다 — 2026-07-25
     AI_AGENT_FRIENDS 사고와 같은 계열: 목록을 옮기며 소비자를 확인 안 한 실수.)
  · 임계 미만이면 아무것도 안 하고 0을 반환한다(파일도 안 만든다).

실행:
  python petnna_backlog_archive.py          # 아카이브 실행
  python petnna_backlog_archive.py --send   # + 텔레그램(옮긴 게 있을 때만)
"""
import os
import sys
from datetime import datetime
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

_here = os.path.dirname(os.path.abspath(__file__))
_ai_team_root = os.path.abspath(os.path.join(_here, "..", "..", ".."))
if _ai_team_root not in sys.path:
    sys.path.insert(0, _ai_team_root)

from _shared.env import find_root, load_env  # noqa: E402
from _shared.telegram import send  # noqa: E402
from _shared.backlog import archive_completed, archive_path, KEEP_RECENT_DONE  # noqa: E402

_root = find_root(_here)
load_env(_root)  # 누락 시 launchd 잡이 TELEGRAM_BOT_TOKEN을 못 읽어 전송 조용히 실패(2026-07-11 교훈)

BACKLOG = os.path.join(_root, "output", "qa", "petnna", "backlog.json")
SEND_TG = "--send" in sys.argv


def main() -> None:
    moved = archive_completed(BACKLOG, keep_recent=KEEP_RECENT_DONE)
    stamp = f"[{datetime.now():%Y-%m-%d %H:%M}] 📦 예원 백로그 아카이브"
    if not moved:
        print(f"{stamp} — 옮길 완료 항목 없음(최근 {KEEP_RECENT_DONE}건 이하)")
        return
    active_kb = Path(BACKLOG).stat().st_size // 1024
    ap = archive_path(BACKLOG)
    archive_kb = ap.stat().st_size // 1024 if ap.exists() else 0
    msg = (f"{stamp} — 완료 {moved}건 분리\n"
           f"  활성 backlog.json {active_kb}KB / 아카이브 {archive_kb}KB")
    print(msg)
    if SEND_TG:
        send(f"📦 예원 백로그 아카이브 — 완료 {moved}건 분리\n"
             f"활성 {active_kb}KB / 아카이브 {archive_kb}KB", silent=True)


if __name__ == "__main__":
    main()
