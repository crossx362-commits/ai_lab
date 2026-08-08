#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""펫나 긴급 회의 — 큰 이슈 발생 시 전 에이전트 소집 (의장: 예원).

트리거(자동): 봄이 신규 P0/P1 발견 · 수리 3회 실패 보류 · 백호 신규 P1 계약 불일치.
수동: --topic "안건" [--context ...] [--priority P0|P1|P2]

진행:
1. 6인 전문가(봄이·수리·테오·백호·미오·나무)가 각자 헌장(SKILL.md)과 실데이터
   (QA 상태·감사 보고·테스트 결과·코드)를 읽고 독립 의견 제출 — 병렬, plan 모드(수정 불가)
2. 의장 예원이 의견을 종합해 결론·결정·액션아이템 도출
3. 액션아이템은 공유 백로그(source=회의)로 적재 → 수리가 실행(고위험은 [승인필요] 표시)
4. 회의록 output/qa/petnna/council/ 저장 + 텔레그램 요약

안전선: 회의는 조언·과제 생성만 한다(코드/DB 무수정). 같은 안건은 24시간 내 재소집 안 함.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parents[4]
AI_TEAM_ROOT = PROJECT_ROOT / "projects" / "ai-team"
sys.path.insert(0, str(AI_TEAM_ROOT))

from _shared.env import load_env  # noqa: E402
from _shared.telegram import send  # noqa: E402
from _shared.process import advisory_lock  # noqa: E402
from _shared.cc import run_claude, extract_json  # noqa: E402
from _shared.petnna_facts import VERIFY_BEFORE_PROPOSING  # noqa: E402
from _shared.backlog import (  # noqa: E402,F401
    all_known_titles,
    needs_human, AUTO_OWNERS, backlog_lock, waiting_lock,
    OWNER_CHOICES, HUMAN_OWNER, normalize_owner,
)

load_env(str(PROJECT_ROOT))

OUT_DIR = PROJECT_ROOT / "output" / "qa" / "petnna" / "council"
STATE = OUT_DIR / "state.json"
BACKLOG = PROJECT_ROOT / "output" / "qa" / "petnna" / "backlog.json"

# needs_human()·AUTO_OWNERS는 _shared/backlog.py가 단일 소스 — promote_approved_holds()도
# 같은 판정 함수(structurally_blocked)를 써야 사유가 늘 때마다 두 곳이 어긋나지 않는다
# (2026-07-11 같은 종류 사고 2연발: owner-불일치 → type-불일치, 그래서 아예 함수를
# backlog.py로 옮겨 이 파일은 재수출만 한다).
COOLDOWN_H = 24

PERSONAS = [
    ("봄이", "QA 검수관", "봄이_QA"),
    ("수리", "자동 개선 개발자", "수리_개발자"),
    ("테오", "E2E 테스트 엔지니어", "테오_테스트"),
    ("백호", "백엔드 지킴이", "백호_백엔드"),
    ("미오", "디자인 리뷰어", "미오_디자인"),
    ("나무", "기획 PM", "나무_기획"),
]


FAIL_SENTINEL = "(의견 수집 실패)"


def _topic_key(topic: str) -> str:
    return hashlib.md5(topic.encode("utf-8")).hexdigest()[:10]


def _is_recent_meeting(topic: str) -> bool:
    """순수 조회(부작용 없음) — 같은 안건이 쿨다운 내에 이미 열렸는가."""
    key = _topic_key(topic)
    try:
        state = json.loads(STATE.read_text(encoding="utf-8"))
    except Exception:
        state = {}
    last = state.get(key)
    if not last:
        return False
    from datetime import datetime as dt
    return (dt.now() - dt.fromisoformat(last)).total_seconds() < COOLDOWN_H * 3600


def _mark_meeting(topic: str) -> None:
    """실제로 회의가 열리기로 확정된 뒤(락 획득 후)에만 쿨다운을 기록한다.

    버그 발견·수정(2026-07-12, 자동 파이프라인 감사): 예전엔 이 기록이 ProcessLock
    획득 *전에* 일어났다 — 동시에 다른 안건의 회의 B가 트리거되면, B는 자기 topic을
    먼저 기록한 뒤 ProcessLock 충돌로 sys.exit(0)해 실제로는 한 번도 안 열렸는데
    24시간 재소집 금지 상태가 됐다(회의 없이 쿨다운만 소진). 락을 먼저 잡고 그
    안에서만 기록하도록 순서를 바꿔, 실제로 진행되는 회의만 쿨다운을 소진시킨다."""
    key = _topic_key(topic)
    try:
        state = json.loads(STATE.read_text(encoding="utf-8"))
    except Exception:
        state = {}
    state[key] = datetime.now().isoformat()
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    STATE.write_text(json.dumps(state, ensure_ascii=False, indent=1), encoding="utf-8")


def _clear_cooldown(topic: str) -> None:
    """인프라 전멸(전원 의견수집실패)로 끝난 회의는 쿨다운을 소진시키지 않는다 —
    안 그러면 그 이슈가 24h 동안 재소집 불가로 방치된다(수리의 '인프라 실패 미차감'과 동일 원칙)."""
    try:
        state = json.loads(STATE.read_text(encoding="utf-8"))
    except Exception:
        return
    state.pop(_topic_key(topic), None)
    STATE.write_text(json.dumps(state, ensure_ascii=False, indent=1), encoding="utf-8")


def _opinion(name: str, role: str, folder: str, topic: str, context: str, priority: str) -> tuple[str, str]:
    skill = AI_TEAM_ROOT / "skills" / folder / "SKILL.md"
    prompt = (
        f"너는 펫나 개발팀의 {name}({role})다. 너의 헌장은 {skill} 에 있다 — Read로 읽고 그 관점을 유지하라.\n\n"
        f"[긴급 회의 안건] (우선순위 {priority})\n{topic}\n상세: {context or '(없음)'}\n\n"
        "[참고 자료 — 판단에 필요한 것만 Read로 확인]\n"
        "- QA 상태: output/qa/petnna/qa_state.json / 보고서·감사·테스트 결과·백로그: output/qa/petnna/ 아래\n"
        "- 코드: projects/petnna/ / 대기 중 브랜치: git branch --list 'fix/petnna-*' 등은 회의록 컨텍스트 참고\n"
        "모르는 기술적 사실은 웹서치로 확인하라. 추측을 사실처럼 말하지 마라. 코드는 수정하지 마라.\n\n"
        + VERIFY_BEFORE_PROPOSING +
        "[출력 — 이 형식 그대로, 총 7줄 이내]\n"
        "입장: <한 줄 — 예: 즉시 수정 / 병합 권고 / 롤백 / 보류 / 구조 개선 필요>\n"
        "근거: <최대 3줄, 실제 확인한 데이터 기반>\n"
        "제안 액션: <구체적 1~2개, 없으면 '없음'>\n"
        "리스크: <한 줄>"
    )
    # Grep 필수(2026-08-08): VERIFY_BEFORE_PROPOSING이 "이미 있는지 Grep으로 확인하라"고
    # 요구하는데 도구가 없으면 그 규칙은 지킬 수 없는 말이 된다 — 회의 제안 5건이
    # 이미 구현된 것을 다시 만들라고 한 원인이다. plan 모드라 읽기 전용은 그대로다.
    ok, out = run_claude(prompt, PROJECT_ROOT, timeout=420,
                         allowed_tools="Read,Grep,Glob,WebSearch,WebFetch",
                         permission_mode="plan")
    return name, (out.strip()[:1200] if ok and out else FAIL_SENTINEL)


PENDING = OUT_DIR / "pending_topics.json"
MAX_DRAIN = 3   # 한 프로세스가 이어서 여는 대기 회의 수 상한(연쇄 폭주 방지)


def _queue_topic(topic: str, context: str, priority: str) -> None:
    """다른 안건의 회의가 진행 중이라 지금 열 수 없는 안건을 대기열에 남긴다.

    예전엔 `ProcessLock`이 충돌 시 `sys.exit(0)`을 불러 **두 번째 회의가 흔적도 없이
    사라졌다**(2026-07-12 파이프라인 감사 지적). 트리거 쪽(봄이·백호·수리)은 Popen으로
    띄우기만 하므로 그 소멸을 알 수 없었고, 재소집 경로도 없었다.
    """
    with waiting_lock("petnna_council_queue", 10.0, label="회의대기열"):
        try:
            items = json.loads(PENDING.read_text(encoding="utf-8"))
        except Exception:
            items = []
        if any(_topic_key(i.get("topic", "")) == _topic_key(topic) for i in items):
            return   # 같은 안건이 이미 줄 서 있다
        items.append({"topic": topic, "context": context, "priority": priority,
                      "queued": datetime.now().isoformat()})
        PENDING.parent.mkdir(parents=True, exist_ok=True)
        PENDING.write_text(json.dumps(items, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"[회의] 다른 회의 진행 중 — 대기열에 적재: {topic[:60]}")


def _pop_topic() -> dict | None:
    with waiting_lock("petnna_council_queue", 10.0, label="회의대기열"):
        try:
            items = json.loads(PENDING.read_text(encoding="utf-8"))
        except Exception:
            return None
        if not items:
            return None
        head = items.pop(0)
        PENDING.write_text(json.dumps(items, ensure_ascii=False, indent=1), encoding="utf-8")
        return head


def convene(topic: str, context: str, priority: str) -> None:
    if _is_recent_meeting(topic):
        print(f"[회의] 동일 안건 {COOLDOWN_H}시간 내 개최됨 — 생략: {topic[:60]}")
        return
    with advisory_lock("petnna_council") as got:
        if not got:
            # 죽지 않는다 — 줄을 서고, 진행 중인 회의가 끝나면서 우리 안건을 열어준다.
            _queue_topic(topic, context, priority)
            return
        if not _hold_meeting(topic, context, priority):
            return   # 클로드가 죽어 있다 — 대기열을 건드리면 줄 선 안건까지 태운다
        # 내 회의가 끝났으니 그 사이 밀린 안건을 이어서 연다. 락은 아직 내가 쥐고 있어
        # 새 프로세스가 끼어들지 않고, 상한(MAX_DRAIN)으로 연쇄 폭주를 막는다.
        for _ in range(MAX_DRAIN):
            nxt = _pop_topic()
            if not nxt:
                break
            if _is_recent_meeting(nxt["topic"]):
                continue
            print(f"[회의] 대기열 소진 — 이어서 개최: {nxt['topic'][:60]}")
            if not _hold_meeting(nxt["topic"], nxt.get("context", ""), nxt.get("priority", "P1")):
                # 인프라 실패 — 꺼낸 안건을 되돌려 놓는다. 안 그러면 큐에서만 빠지고
                # 아무도 다시 열지 않아, 고치려던 '흔적 없는 유실'이 그대로 재현된다.
                _queue_topic(nxt["topic"], nxt.get("context", ""), nxt.get("priority", "P1"))
                break


def _hold_meeting(topic: str, context: str, priority: str) -> bool:
    """실제 회의 1회. 호출자가 이미 petnna_council 락을 쥐고 있어야 한다.

    반환: 회의가 실제로 열렸으면 True, 인프라 사유(클로드 전멸)로 못 열었으면 False.
    호출자는 False면 대기열 소진을 멈춘다 — 클로드가 죽어 있으면 다음 안건도
    똑같이 실패하고, 그렇게 소진된 안건은 큐에서 빠진 채 사라진다.
    """
    # 락을 실제로 잡은 뒤에만 쿨다운을 기록한다 — 락 충돌로 회의가 안 열렸는데
    # 24h 봉쇄되는 사고를 막는다(위 주석 참고).
    _mark_meeting(topic)
    print(f"[{datetime.now()}] 🏛️ 긴급 회의 소집 — {topic[:80]}")
    with ThreadPoolExecutor(max_workers=3) as ex:
        futures = [ex.submit(_opinion, n, r, f, topic, context, priority)
                   for n, r, f in PERSONAS]
        opinions = [fu.result() for fu in futures]

    if all(op == FAIL_SENTINEL for _, op in opinions):
        # 전원 실패 = 개별 판단이 아니라 그 시점 클로드 호출 자체가 전멸(레이트리밋/타임아웃).
        # 이슈 탓이 아니므로 쿨다운을 소진시키지 않고 다음 트리거에서 바로 재시도 가능하게 한다.
        _clear_cooldown(topic)
        print(f"[{datetime.now()}] 회의 전원 의견수집 실패(인프라) — 쿨다운 미소진, 재시도 대기: {topic[:80]}")
        return False

    opinion_text = "\n\n".join(f"### {name}\n{op}" for name, op in opinions)
    ok, out = run_claude(
        "너는 펫나 개발팀 CEO 예원, 이 긴급 회의의 의장이다. 군더더기 없이 핵심만.\n\n"
        f"[안건] (우선순위 {priority})\n{topic}\n상세: {context or '(없음)'}\n\n"
        f"[전문가 의견]\n{opinion_text}\n\n"
        "[할 일]\n"
        "1. 의견을 종합해 회의 결론을 5줄 이내로 내라(의견 충돌은 명시하고 판정하라).\n"
        "2. 결정 사항을 번호 목록으로.\n"
        "3. 마지막에 실행 액션아이템을 JSON 배열로: "
        "[{\"title\": \"...\", \"detail\": \"...\", \"priority\": \"P1|P2|P3\", "
        f"\"type\": \"기능|디자인|기획|테스트|백엔드\", \"owner\": \"{'|'.join(OWNER_CHOICES)}\"}}] "
        "— owner는 위 목록에서만 고른다. 목록에 없는 에이전트(나무·봄이 등)는 백로그를 "
        "읽지 않아 배정해도 아무도 집지 못한다. 구현 담당이 애매하면 '수리', 오너가 "
        "직접 판단해야 하면 '사람'을 쓴다. "
        "— 자동 실행이 위험한 항목은 title 앞에 [승인필요]를 붙여라. 액션이 없으면 빈 배열.",
        PROJECT_ROOT, timeout=420, allowed_tools="Read", permission_mode="plan")
    verdict = out.strip() if ok and out else "(의장 종합 실패)"
    if not (ok and out):
        # 전문가 의견은 살아있는데 의장 합성만 인프라 문제로 실패 — 의견은 회의록에 보존하되
        # 쿨다운은 소진시키지 않아 다음 트리거에서 곧바로 재종합 시도가 가능하게 한다.
        _clear_cooldown(topic)
        print(f"[{datetime.now()}] 의장 종합 실패(인프라) — 쿨다운 미소진, 의견은 보존")

    # 액션아이템 → 백로그(source=회의). [승인필요]·소비자 없는 owner 항목은 적재만 하고 자동 루프가 안 집도록 보류 상태.
    actions = extract_json(verdict) or []
    added = 0
    # 백로그 읽기~쓰기만 공용 락으로 감싼다 — convene 전체를 감싸면 안에 있는
    # 6인 병렬 LLM 호출(수분)이 락을 붙들어 다른 도구를 전부 막는다(2026-08-02).
    with backlog_lock():
        try:
            data = json.loads(BACKLOG.read_text(encoding="utf-8"))
        except FileNotFoundError:
            data = {"items": []}
        except Exception as e:
            # 파일은 있는데 파싱 실패(다른 프로세스의 non-atomic write 도중 읽었을 가능성) —
            # 빈 dict로 대체해 아래에서 통째로 덮어쓰면 기존 백로그 전체가 소실된다
            # (자동 파이프라인 감사 도구가 발견, 2026-07-12). 액션아이템 적재는 건너뛰되
            # 회의록·텔레그램은 이미 만들어진 대로 계속 진행 — 기존 백로그 파일 보존.
            print(f"[회의] 백로그 읽기 실패(파일 손상 가능) — 액션아이템 적재 건너뜀: {e}")
            data = None
        if data is not None:
            # 아카이브된 완료 항목의 제목까지 대조한다 — 활성 파일만 보면 이미 만든 기능을
            # 다시 제안한다(2026-08-04 아카이브 분리 시 같이 처리).
            existing = all_known_titles(BACKLOG, data)
            # id 충돌 방지 — stamp가 분 단위라 같은 분에 다른 안건 회의가 열리면 서로 다른
            # 액션아이템이 같은 id를 갖는다. 수리 dev_state가 id로 조회하므로 attempts가
            # 뒤섞인다. 나무·미오는 2026-07-12에 고쳤는데 회의만 빠져 있었다(비대칭).
            existing_ids = {i.get("id") for i in data["items"]}
            for a in actions if isinstance(actions, list) else []:
                title = (a.get("title") or "").strip()
                if not title or title in existing:
                    continue
                # owner 정규화 — 목록 밖 owner를 그대로 적재하면 아무도 안 집는 유령이 된다
                # (2026-08-04 근본 수리: 프롬프트 목록과 AUTO_OWNERS가 어긋나 10건 누적).
                _owner, _onote = normalize_owner(a.get("owner", ""))
                human = needs_human(title, _owner, a.get("detail") or "",
                                    a.get("type", "기획"))
                seq = added
                new_id = f"회의_{datetime.now():%Y%m%d%H%M}_{seq}"
                while new_id in existing_ids:
                    seq += 1
                    new_id = f"회의_{datetime.now():%Y%m%d%H%M}_{seq}"
                existing_ids.add(new_id)
                data["items"].append({
                    "id": new_id,
                    "title": title[:120], "detail": (a.get("detail") or "")[:500],
                    "priority": a.get("priority") if a.get("priority") in ("P1", "P2", "P3") else "P2",
                    "type": a.get("type", "기획"), "source": "회의",
                    "status": "보류" if human else "대기",
                    "owner": _owner, "created": datetime.now().isoformat(),
                    **({"owner_note": _onote} if _onote else {}),
                    **({"gate": "사람 판단 트랙"} if _owner == HUMAN_OWNER else {}),
                })
                added += 1
            BACKLOG.parent.mkdir(parents=True, exist_ok=True)
            BACKLOG.write_text(json.dumps(data, ensure_ascii=False, indent=1), encoding="utf-8")

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    minutes = OUT_DIR / f"minutes_{datetime.now():%Y%m%d_%H%M}.md"
    minutes.write_text(
        f"# 펫나 긴급 회의록 — {datetime.now():%Y-%m-%d %H:%M}\n\n"
        f"## 안건 ({priority})\n{topic}\n\n상세: {context or '(없음)'}\n\n"
        f"## 전문가 의견\n{opinion_text}\n\n## 의장(예원) 종합\n{verdict}\n\n"
        f"## 백로그 적재: {added}건 (source=회의, 승인필요 항목은 보류 상태)\n",
        encoding="utf-8")
    print(f"[{datetime.now()}] 회의 종료 — 회의록 {minutes}, 액션 {added}건 적재")
    # 결론 첫 부분만 텔레그램 (전문은 회의록)
    head = verdict.split("```")[0].strip()[:1200]
    send(f"🏛️ 펫나 긴급 회의 결과 — {topic[:70]}\n\n{head}\n\n"
         f"액션 {added}건 백로그 적재 · 📄 {minutes}")
    return True


def main() -> None:
    ap = argparse.ArgumentParser(description="펫나 긴급 회의 (전 에이전트 소집)")
    ap.add_argument("--topic", required=True, help="회의 안건")
    ap.add_argument("--context", default="", help="상세 컨텍스트")
    ap.add_argument("--priority", default="P1", choices=["P0", "P1", "P2"])
    args = ap.parse_args()
    convene(args.topic, args.context, args.priority)


if __name__ == "__main__":
    main()
