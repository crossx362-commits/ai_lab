#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""예원 — 펫나 백로그 위생 감사 (보류 항목 중복·해소 여부 재판단).

배경(2026-08-04): 오너 지시로 진행한 6인 회의가 "보류 39건 중 31건 폐기"를 권고했으나,
실데이터 재확인 결과 대부분(28건)은 방치가 아니라 이미 문서화된 정당한 사유(오너의
"당분간 나만" 결정·DB/인증 접촉·규모 판단 대기)로 보류돼 있었다 — 추측 기반 권고였다.
이 도구는 그 실수를 반복하지 않도록, 추측 대신 백로그 실데이터(gate·detail·다른 항목과의
중복 여부)만 근거로 예원(구독 클로드)이 재판단한다. 확신이 없으면 손대지 않는다(추측 금지
원칙, DIRECTIVES.md §1-7).

판단 범위(안전한 것만, 파괴적 작업 아님):
  1. **중복 제안** — 다른 항목(대기·완료·PR대기 포함)과 명백히 같은 기능을 가리키면
     하나만 남기고 나머지는 backlog.json에서 제거(git으로 언제든 복구 가능).
  2. **사유 해소** — gate/detail에 적힌 보류 사유가 최신 코드 상태로 볼 때 이미 풀렸다고
     "명백한 근거"로 확신할 때만 보류→대기로 되돌린다(promote_approved_holds와 동일한
     전환, 새 상태값을 만들지 않음 — "진행 무덤" 사고 2026-07-10 재발 방지).
  3. 오너 결정(예: "당분간 오너 단독 사용")·DB/인증 게이트·[승인필요] 태그가 붙은 항목은
     **절대 건드리지 않는다** — 오너만 풀 수 있는 잠금이다.

확신 없는 항목은 판단하지 않고 그대로 둔다(추측 금지) — 이 도구가 보고하는 것은
"손 못 댄 것"이 아니라 "실제로 처리한 것"뿐이다.

실행:
  python petnna_backlog_hygiene.py --check         # 판단만 출력(적용 없음)
  python petnna_backlog_hygiene.py --apply         # 판단 + 실제 적용(제거/승격)
  python petnna_backlog_hygiene.py --apply --send  # + 텔레그램 요약
"""
import json
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
from _shared.cc import run_claude, extract_json  # noqa: E402
from _shared.backlog import backlog_lock  # noqa: E402

_root = find_root(_here)
load_env(_root)  # 누락 시 launchd 잡이 TELEGRAM_BOT_TOKEN 등을 못 읽어 전송 조용히 실패(2026-07-11 교훈)

BACKLOG = os.path.join(_root, "output", "qa", "petnna", "backlog.json")
APPLY = "--apply" in sys.argv
SEND_TG = "--send" in sys.argv

# 오너만 풀 수 있는 잠금 — 이 문구가 detail/gate에 있으면 이 도구는 절대 건드리지 않는다.
_HARD_LOCK_MARKERS = (
    "당분간", "[승인필요]", "DB/인증", "오너 결정", "오너 판단", "오너 우선순위",
    "가입이 오너", "restrict_signups", "사람 판단",
)


def _is_hard_locked(item: dict) -> bool:
    text = f"{item.get('gate') or ''} {item.get('detail') or ''} {item.get('title') or ''}"
    return any(m in text for m in _HARD_LOCK_MARKERS)


def _load() -> dict:
    return json.loads(Path(BACKLOG).read_text(encoding="utf-8"))


def _ask_yewon(hold_items: list[dict], all_titles: list[str]) -> list[dict]:
    """예원에게 보류 항목의 중복·해소 여부만 판단하게 한다. 확신 없으면 넘기라고 명시."""
    hold_block = "\n".join(
        f"- id={it['id']} | title={it.get('title','')} | gate={(it.get('gate') or '')[:150]}"
        for it in hold_items
    )
    titles_block = "\n".join(f"- {t}" for t in all_titles[:300])
    prompt = (
        "너는 펫나(반려동물 웹앱) 개발팀 CEO '예원'이다. 아래 [보류 항목] 각각에 대해 "
        "딱 두 가지만 판단하라. 그 외에는 아무것도 하지 마라.\n\n"
        "1) duplicate: 이 항목이 [전체 항목 제목 목록]의 다른 어떤 항목과 명백히 같은 기능을 "
        "가리키는가? (같으면 그 제목을 duplicate_of에 적어라)\n"
        "2) resolved: 이 항목의 gate(보류 사유)가 실제로 이미 해소됐다고 '명백한 근거'로 "
        "확신하는가? 애매하면 false로 두어라 — 추측하지 마라.\n\n"
        "[중요] 확신 없는 항목은 duplicate=false, resolved=false로 두고 reason에 왜 확신이 "
        "없는지 적어라. 근거 없이 true를 주는 것이 이 작업에서 가장 위험한 실수다.\n\n"
        "[보류 항목]\n" + hold_block + "\n\n"
        "[전체 항목 제목 목록(중복 대조용)]\n" + titles_block + "\n\n"
        '반드시 JSON 배열만 출력: '
        '[{"id":"...","duplicate":true|false,"duplicate_of":"<제목 또는 빈문자열>",'
        '"resolved":true|false,"reason":"<한 줄 근거>"}]'
    )
    ok, out = run_claude(prompt, cwd=_root, timeout=300)
    if not ok:
        return []
    parsed = extract_json(out)
    return parsed if isinstance(parsed, list) else []


def audit() -> tuple[list[str], list[str], list[str]]:
    """반환: (제거된 중복 id 목록, 대기로 승격된 id 목록, 판단 불가로 넘긴 id 목록)"""
    data = _load()
    items = data.get("items", [])
    hold = [it for it in items if it.get("status") == "보류"]
    lockable = [it for it in hold if not _is_hard_locked(it)]
    skipped_locked = [it["id"] for it in hold if _is_hard_locked(it)]
    if not lockable:
        return [], [], skipped_locked

    all_titles = [it.get("title", "") for it in items if it.get("title")]
    verdicts = _ask_yewon(lockable, all_titles)
    verdict_by_id = {v.get("id"): v for v in verdicts if isinstance(v, dict)}

    removed_ids, promoted_ids, skipped_ids = [], [], list(skipped_locked)
    if not APPLY:
        for it in lockable:
            v = verdict_by_id.get(it["id"])
            if v and (v.get("duplicate") or v.get("resolved")):
                (removed_ids if v.get("duplicate") else promoted_ids).append(it["id"])
            else:
                skipped_ids.append(it["id"])
        return removed_ids, promoted_ids, skipped_ids

    with backlog_lock():
        data = _load()  # 락 안에서 재로드 — 판단 이후 다른 도구가 먼저 썼을 수 있다
        items = data.get("items", [])
        kept = []
        for it in items:
            v = verdict_by_id.get(it.get("id"))
            if it.get("status") == "보류" and v and v.get("duplicate") and v.get("duplicate_of"):
                removed_ids.append(it["id"])
                continue  # 배열에서 제거 = 폐기(새 상태값을 만들지 않는다)
            if it.get("status") == "보류" and v and v.get("resolved") and not v.get("duplicate"):
                it["status"] = "대기"
                it["updated"] = datetime.now().isoformat()
                it["promotion_note"] = f"예원 위생감사 자동 승격: {v.get('reason','')}"
                promoted_ids.append(it["id"])
            elif it.get("status") == "보류" and not _is_hard_locked(it):
                skipped_ids.append(it.get("id", "?"))
            kept.append(it)
        data["items"] = kept
        Path(BACKLOG).write_text(json.dumps(data, ensure_ascii=False, indent=1), encoding="utf-8")
    return removed_ids, promoted_ids, skipped_ids


def main() -> None:
    removed, promoted, skipped = audit()
    lines = [f"[{datetime.now():%Y-%m-%d %H:%M}] 🧹 예원 백로그 위생감사"
              + (" (적용됨)" if APPLY else " (판단만·미적용)")]
    lines.append(f"  중복 제거: {len(removed)}건" + (" — " + ", ".join(removed) if removed else ""))
    lines.append(f"  사유해소 승격(보류→대기): {len(promoted)}건"
                 + (" — " + ", ".join(promoted) if promoted else ""))
    lines.append(f"  판단 보류(확신 없음/오너 잠금): {len(skipped)}건")
    print("\n".join(lines))
    if SEND_TG and (removed or promoted):
        send("[예원] 백로그 위생감사\n" + "\n".join(lines[1:]))


if __name__ == "__main__":
    main()
