#!/usr/bin/env python3
"""agent_factory.py — 예원(CEO) 신규 에이전트 자동 생성 (단계 C, 메타 에이전트).

⚙️ 자율 생성 모드: 사람 승인 없이 예원이 스스로 신규 에이전트를 만들고 활성화한다.
승인 게이트는 없지만, 시스템이 깨지지 않게 하는 '자동 정합성 검사'는 유지한다
(이건 승인이 아니라 좀비/중복/폭주 방지용 — CLAUDE.md 필수 원칙):
  1. 일일 생성 상한(MAX_PER_DAY, 폭주 방지).
  2. 역할 중복 검사 — 기존 에이전트 키워드와 겹치면 거부(가짜/유령 방지).
  3. 백엔드(tools 폴더+SKILL.md) 없으면 registry 로드시 자동 retire.
  4. 신규 에이전트는 process.py 뮤텍스 락 의무 — 스캐폴드에 주입(좀비 방지).
  5. 생성 직후 시범 실행 통과해야 active — 실행 불가 스크립트는 자동 격리 유지.

흐름:
  auto_create(need) → 제안→생성→시범실행→자동 활성화 (승인 없이 한 번에)
  propose/create/promote → 수동 단계별 제어가 필요할 때 사용
"""
from __future__ import annotations

import json
import os
import re
import subprocess
import sys
from datetime import date, datetime
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

_here = Path(__file__).resolve().parent
AI_TEAM = _here.parents[2]
PROJECT_ROOT = AI_TEAM.parents[1]
sys.path.insert(0, str(AI_TEAM))

from _shared.registry import load_registry, active_agents, save_agent, get_agent  # noqa: E402
from _shared.llm import text as llm_text, is_available as llm_available  # noqa: E402

SKILLS_DIR = AI_TEAM / "skills"
FACTORY_LOG = PROJECT_ROOT / "output" / "cache" / "agent_factory_log.json"
# 폭주 방지용 일일 상한(승인 아님). 환경변수로 조절 가능.
MAX_PER_DAY = int(os.getenv("AI_TEAM_FACTORY_MAX_PER_DAY", "5"))


# ── 스캐폴드 템플릿 ──────────────────────────────────────────────
SKILL_MD_TMPL = """---
name: {display}
description: {role}
status: quarantined
created_by: yewon_factory
created_at: {ts}
---

# {display} 에이전트

> ⚠️ 자동 생성(격리) 상태. 사람 검증 후 활성화됩니다.

## 역할
{role}

## 도구
- `{slug}_main.py` — 진입점(스캐폴드). 실제 로직 구현 필요.

## 활성화 조건
1. 진입점 스크립트가 정상 실행(시범 1회)
2. 역할 중복 없음(skill_auditor 통과)
3. 사람 승인
"""

MAIN_PY_TMPL = '''#!/usr/bin/env python3
"""{display} 에이전트 진입점 (자동 생성).

표준 import 패턴 + 뮤텍스 락(좀비 방지) 적용.
기본 동작: LLM(_shared.llm) 기반 범용 처리 — 생성 즉시 동작한다.
역할 특화 로직이 필요하면 ROLE_SYSTEM 프롬프트/도구를 보강하면 된다.
"""
import os, sys
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

_here = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(_here, "..", "..", ".."))

from _shared.env import load_env
from _shared.process import ProcessLock
from _shared.llm import text as llm_text, is_available as llm_available

load_env()
AGENT = "{display}"
ROLE_SYSTEM = "당신은 '{display}' 에이전트입니다. 역할: {role}. 한국어로 간결히 답하세요."


def main(message: str = "") -> str:
    msg = message or "(빈 입력)"
    if llm_available():
        out = llm_text(msg, system=ROLE_SYSTEM, max_tokens=600, temperature=0.5, lm_first=True)
        result = f"[{{AGENT}}] {{out.strip()}}" if out else f"[{{AGENT}}] (LLM 미응답) 수신: {{msg}}"
    else:
        result = f"[{{AGENT}}] (LLM 미가용) 수신: {{msg}} — 역할: {role}"
    print(result)
    return result


if __name__ == "__main__":
    with ProcessLock("{slug}_main"):
        main(" ".join(sys.argv[1:]))
'''


def _slugify(display: str) -> str:
    s = re.sub(r"[^0-9A-Za-z가-힣]+", "_", display).strip("_").lower()
    return s or "agent"


def _today_count() -> int:
    if not FACTORY_LOG.exists():
        return 0
    try:
        log = json.loads(FACTORY_LOG.read_text(encoding="utf-8"))
    except Exception:
        return 0
    today = date.today().isoformat()
    return sum(1 for e in log if e.get("date") == today and e.get("event") == "create")


def _log(event: str, detail: dict) -> None:
    FACTORY_LOG.parent.mkdir(parents=True, exist_ok=True)
    log = []
    if FACTORY_LOG.exists():
        try:
            log = json.loads(FACTORY_LOG.read_text(encoding="utf-8"))
        except Exception:
            log = []
    log.append({"date": date.today().isoformat(), "ts": datetime.now().isoformat(timespec="seconds"),
                "event": event, **detail})
    FACTORY_LOG.write_text(json.dumps(log[-100:], ensure_ascii=False, indent=2), encoding="utf-8")


def _overlap_keywords(keywords: list[str]) -> list[str]:
    """기존 활성 에이전트 키워드와 겹치는 항목."""
    existing = set()
    for m in active_agents().values():
        existing |= {k.lower() for k in m.get("keywords", [])}
    return [k for k in keywords if k.lower() in existing]


# ── propose: 명세 제안(생성 안 함) ───────────────────────────────
def propose(need: str) -> dict:
    """미해결 작업 설명 → 신규 에이전트 명세 제안(JSON). 생성하지 않는다."""
    prompt = (
        "현재 에이전트(예원=CEO, 영숙=비서, 봄이=펫나 QA, 수리=펫나 개발, 미오=디자인 리뷰, "
        "나무=기획, 백호=백엔드 감사, 테오=E2E 테스트)로 처리 불가한 작업이 있습니다.\n"
        f"미해결 작업: \"{need}\"\n\n"
        "이를 맡을 신규 에이전트 명세를 JSON으로 제안하세요(설명 금지):\n"
        '{"display":"한글이름","role":"한 줄 역할","keywords":["키워드",...]}'
    )
    spec = None
    if llm_available():
        raw = llm_text(prompt, json_mode=True, max_tokens=200, temperature=0.4, lm_first=True)  # 스펙 생성 — 올라마 우선(폴백 유지)
        if raw:
            a, b = raw.find("{"), raw.rfind("}")
            try:
                spec = json.loads(raw[a:b + 1])
            except Exception:
                spec = None
    if not spec:
        spec = {"display": "신규", "role": need[:60], "keywords": []}

    overlaps = _overlap_keywords(spec.get("keywords", []))
    spec["_overlaps"] = overlaps
    spec["_approval_required"] = True
    spec["_proposal_for"] = need[:120]
    _log("propose", {"need": need[:120], "spec": spec})
    return spec


# ── create: 승인 후 실제 생성 (격리) ─────────────────────────────
def create(spec: dict, approved: bool = False) -> str:
    if not approved:
        return ("⛔ 승인 필요: 이 함수는 사람 승인(approved=True) 없이는 에이전트를 만들지 않습니다.\n"
                f"제안 명세: {json.dumps(spec, ensure_ascii=False)}")

    if _today_count() >= MAX_PER_DAY:
        return f"⛔ 일일 생성 상한({MAX_PER_DAY}) 초과 — 오늘은 더 만들 수 없습니다."

    display = str(spec.get("display") or "").strip()
    role = str(spec.get("role") or "").strip()
    keywords = [k for k in spec.get("keywords", []) if isinstance(k, str)]
    if not display or not role:
        return "⛔ display/role 누락 — 생성 거부."

    overlaps = _overlap_keywords(keywords)
    if overlaps:
        _log("reject_overlap", {"display": display, "overlaps": overlaps})
        return f"⛔ 역할 중복으로 거부 — 기존 에이전트와 겹치는 키워드: {overlaps}"

    slug = _slugify(display)
    folder_name = f"{display}_자동"
    folder = SKILLS_DIR / folder_name
    if folder.exists():
        return f"⛔ 이미 존재: {folder_name}"

    ts = datetime.now().isoformat(timespec="seconds")
    (folder / "tools").mkdir(parents=True, exist_ok=True)
    (folder / "SKILL.md").write_text(
        SKILL_MD_TMPL.format(display=display, role=role, slug=slug, ts=ts), encoding="utf-8")
    (folder / "tools" / f"{slug}_main.py").write_text(
        MAIN_PY_TMPL.format(display=display, role=role, slug=slug), encoding="utf-8")

    agent_id = slug
    save_agent(agent_id, {
        "display": display, "folder": folder_name, "role": role, "keywords": keywords or [display],
        "tools": [{"name": "main", "desc": role, "script": f"skills/{folder_name}/tools/{slug}_main.py",
                   "args": ["{message}"], "timeout": 60}],
        "daemons": {}, "scheduled": {},
        "status": "quarantined", "created_by": "yewon_factory", "created_at": ts,
    })
    _log("create", {"display": display, "agent_id": agent_id, "folder": folder_name})
    return (f"✅ 격리 상태로 생성: [{display}] (id={agent_id})\n"
            f"  폴더: skills/{folder_name}/\n"
            f"  상태: quarantined — 시범 실행+승인 후 promote 로 활성화하세요.")


# ── promote: 시범 실행 통과 시 active 승격 ───────────────────────
def promote(agent_id: str, approved: bool = False) -> str:
    meta = get_agent(agent_id)
    if not meta:
        return f"⛔ 없는 에이전트: {agent_id}"
    if meta.get("status") != "quarantined":
        return f"ℹ️ 승격 대상 아님(현재 status={meta.get('status')})."
    if not meta.get("_folder_exists"):
        return "⛔ 백엔드 폴더 없음 — 승격 불가(자동 retire 대상)."
    if not approved:
        return "⛔ 승인 필요: promote(agent_id, approved=True)."
    meta = {k: v for k, v in meta.items() if not k.startswith("_")}
    meta["status"] = "active"
    save_agent(agent_id, meta)
    _log("promote", {"agent_id": agent_id})
    return f"✅ 활성화: {agent_id} → active. (notify/agent_controller 데몬 등록은 수동 확인 권장)"


def _trial_run(agent_id: str) -> tuple[bool, str]:
    """생성된 스캐폴드를 1회 시범 실행 — 정상 종료해야 승격."""
    meta = get_agent(agent_id)
    if not meta or not meta.get("tools"):
        return False, "도구 없음"
    script = AI_TEAM / meta["tools"][0]["script"]
    if not script.exists():
        return False, "스크립트 없음"
    nowin = {"creationflags": subprocess.CREATE_NO_WINDOW} if sys.platform == "win32" else {}
    try:
        r = subprocess.run(
            [sys.executable, str(script), "시범 실행 점검"],
            cwd=str(PROJECT_ROOT), capture_output=True, text=True,
            encoding="utf-8", errors="replace", timeout=30,
            env={**os.environ, "PYTHONUTF8": "1", "SUPPRESS_TELEGRAM": "true"}, **nowin,
        )
        return (r.returncode == 0), (r.stdout or r.stderr or "").strip()[:200]
    except Exception as e:
        return False, str(e)[:200]


def auto_create(need: str) -> dict:
    """승인 없이 자율 생성: 제안→생성→시범실행→자동 활성화.

    반환: {"ok":bool, "agent_id":str|None, "status":str, "message":str}
    자동 정합성 검사(상한·중복·시범실행)만 게이트로 둔다.
    """
    spec = propose(need)
    spec.pop("_overlaps", None); spec.pop("_approval_required", None); spec.pop("_proposal_for", None)

    msg = create(spec, approved=True)   # 자율: 내부적으로 승인 처리(자동 정합성 검사는 그대로)
    if not msg.startswith("✅"):
        _log("auto_reject", {"need": need[:120], "reason": msg[:120]})
        return {"ok": False, "agent_id": None, "status": "rejected", "message": msg}

    agent_id = _slugify(str(spec.get("display")))
    ok, detail = _trial_run(agent_id)
    if not ok:
        _log("auto_trial_fail", {"agent_id": agent_id, "detail": detail})
        return {"ok": False, "agent_id": agent_id, "status": "quarantined",
                "message": f"⚠️ 생성됨(격리) — 시범 실행 실패로 활성화 보류: {detail}"}

    pmsg = promote(agent_id, approved=True)
    _log("auto_activate", {"agent_id": agent_id})
    return {"ok": True, "agent_id": agent_id, "status": "active",
            "message": f"🤖 예원 자율 생성·활성화 완료: [{spec.get('display')}] (id={agent_id})\n  {pmsg}"}


# ── 승인 대기 제안 (설계문서 단계 C: 사람 승인 게이트) ────────────
AGENT_PROPOSAL = PROJECT_ROOT / "output" / "cache" / "agent_proposal.json"


def save_proposal(spec: dict, need: str) -> None:
    AGENT_PROPOSAL.parent.mkdir(parents=True, exist_ok=True)
    AGENT_PROPOSAL.write_text(
        json.dumps({"spec": spec, "need": need,
                    "ts": datetime.now().isoformat(timespec="seconds")}, ensure_ascii=False, indent=2),
        encoding="utf-8")


def load_proposal() -> dict | None:
    if not AGENT_PROPOSAL.exists():
        return None
    try:
        return json.loads(AGENT_PROPOSAL.read_text(encoding="utf-8"))
    except Exception:
        return None


def clear_proposal() -> None:
    try:
        AGENT_PROPOSAL.unlink()
    except Exception:
        pass


def approve_pending() -> str:
    """대기 중인 신규 에이전트 제안을 승인 → 생성 + 시범실행 + 활성화."""
    p = load_proposal()
    if not p:
        return "승인 대기 중인 신규 에이전트 제안이 없어요."
    spec = {k: v for k, v in p.get("spec", {}).items() if not k.startswith("_")}
    msg = create(spec, approved=True)
    if not msg.startswith("✅"):
        clear_proposal()
        return msg
    agent_id = _slugify(str(spec.get("display")))
    ok, detail = _trial_run(agent_id)
    if not ok:
        clear_proposal()
        return f"⚠️ 생성됨(격리) — 시범 실행 실패로 활성화 보류: {detail}"
    pmsg = promote(agent_id, approved=True)
    clear_proposal()
    return f"✅ 신규 에이전트 활성화: [{spec.get('display')}] (id={agent_id})\n  {pmsg}"


def reject_pending() -> str:
    p = load_proposal()
    if not p:
        return "대기 중인 신규 에이전트 제안이 없어요."
    clear_proposal()
    _log("reject_human", {"display": p.get("spec", {}).get("display")})
    return f"신규 에이전트 제안을 거절했어요: {p.get('spec', {}).get('display')}"


def _cli() -> None:
    import argparse
    p = argparse.ArgumentParser(description="에이전트 팩토리 (승인 게이트)")
    sub = p.add_subparsers(dest="cmd")
    pa = sub.add_parser("auto"); pa.add_argument("need")
    pp = sub.add_parser("propose"); pp.add_argument("need")
    pc = sub.add_parser("create"); pc.add_argument("spec_json"); pc.add_argument("--approve", action="store_true")
    pr = sub.add_parser("promote"); pr.add_argument("agent_id"); pr.add_argument("--approve", action="store_true")
    sub.add_parser("list")
    args = p.parse_args()

    if args.cmd == "auto":
        print(auto_create(args.need)["message"])
    elif args.cmd == "propose":
        print(json.dumps(propose(args.need), ensure_ascii=False, indent=2))
    elif args.cmd == "create":
        print(create(json.loads(args.spec_json), approved=args.approve))
    elif args.cmd == "promote":
        print(promote(args.agent_id, approved=args.approve))
    elif args.cmd == "list":
        for aid, m in load_registry(include_inactive=True).items():
            print(f"  [{m.get('status'):11}] {aid:14} {m.get('display','?')}  ({m.get('created_by')})")
    else:
        p.print_help()


if __name__ == "__main__":
    _cli()
