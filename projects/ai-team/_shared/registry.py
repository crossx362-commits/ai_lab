#!/usr/bin/env python3
"""registry.py — 에이전트 단일 진실 소스(SSOT).

설계 근거: skill_auditor 는 이미 skills/ 폴더를 동적 발견하지만,
dispatcher·notify·agents.ts·harness 는 에이전트를 하드코딩한다.
이 모듈이 둘을 잇는다.

산출 = (1) output/cache/agent_registry.json 의 메타  +  (2) skills/ 폴더 실측 발견.
JSON에 없지만 폴더+SKILL.md 가 있으면 자동 발견 항목으로 합류한다(가짜 방지용 status 추적).

비파괴 원칙: 기존 notify.py / agents.ts 를 수정하지 않는다. 그들은 이 모듈을
'읽어 쓸 수' 있도록 어댑터(as_notify_dicts)를 제공할 뿐이다.
"""
from __future__ import annotations

import json
import os
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

_here = Path(__file__).resolve().parent          # _shared
AI_TEAM = _here.parent                            # projects/ai-team
PROJECT_ROOT = AI_TEAM.parents[1]                 # ai_lab
SKILLS_DIR = AI_TEAM / "skills"
REGISTRY_JSON = PROJECT_ROOT / "output" / "cache" / "agent_registry.json"

VALID_STATUS = {"active", "quarantined", "retired"}


def _display_from_folder(folder: str) -> str:
    return folder.split("_", 1)[0]


def _load_json() -> dict:
    if REGISTRY_JSON.exists():
        try:
            return json.loads(REGISTRY_JSON.read_text(encoding="utf-8"))
        except Exception as e:
            print(f"[registry] JSON 파싱 실패, 발견만 사용: {e}")
    return {"agents": {}}


def _discover_folders() -> dict[str, str]:
    """skills/ 에서 SKILL.md 보유 폴더 발견 → {folder: display}."""
    found: dict[str, str] = {}
    if not SKILLS_DIR.exists():
        return found
    for entry in sorted(os.listdir(SKILLS_DIR)):
        folder = SKILLS_DIR / entry
        if (folder / "SKILL.md").exists():
            found[entry] = _display_from_folder(entry)
    return found


def load_registry(include_inactive: bool = False) -> dict[str, dict]:
    """병합된 에이전트 레지스트리 반환 → {agent_id: meta}.

    - JSON 메타가 1차. folder 기준으로 실측 발견과 매칭.
    - JSON엔 없지만 폴더는 있는 에이전트 → status='discovered'(미등록)로 합류.
    - JSON엔 있지만 폴더가 사라진 에이전트 → status='retired' 강등(가짜/유령 방지).
    """
    data = _load_json().get("agents", {})
    discovered = _discover_folders()                  # {folder: display}
    folder_to_id = {meta.get("folder"): aid for aid, meta in data.items() if meta.get("folder")}

    merged: dict[str, dict] = {}

    # 1) JSON 등록 에이전트
    for aid, meta in data.items():
        m = dict(meta)
        folder = m.get("folder")
        m["_folder_exists"] = bool(folder and (SKILLS_DIR / folder / "SKILL.md").exists())
        if not m["_folder_exists"] and m.get("status") == "active":
            m["status"] = "retired"   # 백엔드 없는 에이전트는 자동 강등
        m.setdefault("status", "active")
        merged[aid] = m

    # 2) 폴더는 있는데 JSON에 없는 에이전트 → 미등록 발견
    for folder, display in discovered.items():
        if folder in folder_to_id:
            continue
        aid = display.lower()
        if aid in merged:
            aid = folder.lower()
        merged[aid] = {
            "display": display, "folder": folder, "role": "(미등록 — JSON 메타 없음)",
            "keywords": [display], "tools": [], "daemons": {}, "scheduled": {},
            "status": "discovered", "created_by": "unknown", "_folder_exists": True,
        }

    if include_inactive:
        return merged
    return {aid: m for aid, m in merged.items() if m.get("status") == "active"}


def active_agents() -> dict[str, dict]:
    return load_registry(include_inactive=False)


def get_agent(agent_id: str) -> dict | None:
    reg = load_registry(include_inactive=True)
    key = (agent_id or "").lower()
    if key in reg:
        return reg[key]
    for aid, meta in reg.items():
        if meta.get("display") == agent_id or key in [k.lower() for k in meta.get("keywords", [])]:
            return meta
    return None


def route_by_keyword(message: str) -> str | None:
    """키워드 매칭으로 에이전트 id 추정 (LLM 폴백용)."""
    low = (message or "").lower()
    for aid, meta in active_agents().items():
        for kw in meta.get("keywords", []):
            if kw.lower() in low:
                return aid
    return None


def tools_for(agent_id: str) -> list[dict]:
    meta = get_agent(agent_id)
    return list(meta.get("tools", [])) if meta else []


def as_notify_dicts() -> tuple[dict, dict, dict]:
    """notify.py 호환 어댑터 → (CONTINUOUS_DAEMONS, SCHEDULED_SERVICES, LABELS).

    notify.py 를 수정하지 않고도, 원하면 이걸 import 해 점진 이행할 수 있다.
    """
    daemons, scheduled, labels = {}, {}, {}
    for meta in active_agents().values():
        display, role = meta.get("display", "?"), meta.get("role", "")
        for k, v in (meta.get("daemons") or {}).items():
            daemons[k] = v
            labels[k] = f"{display} ({role[:20]})"
        for k, v in (meta.get("scheduled") or {}).items():
            scheduled[k] = v
            labels.setdefault(k, f"{display} ({role[:20]})")
    return daemons, scheduled, labels


def save_agent(agent_id: str, meta: dict) -> None:
    """레지스트리에 에이전트 추가/갱신 (agent_factory 가 사용). 원자적 쓰기로 손상 방지."""
    raw = _load_json()
    raw.setdefault("agents", {})[agent_id] = meta
    REGISTRY_JSON.parent.mkdir(parents=True, exist_ok=True)
    tmp = REGISTRY_JSON.with_name(REGISTRY_JSON.name + ".tmp")
    tmp.write_text(json.dumps(raw, ensure_ascii=False, indent=2), encoding="utf-8")
    os.replace(tmp, REGISTRY_JSON)  # 같은 FS 원자적 교체 → 크래시 시 부분쓰기/손상 방지


def _cli() -> None:
    import argparse
    p = argparse.ArgumentParser(description="에이전트 레지스트리 점검")
    p.add_argument("--all", action="store_true", help="비활성 포함 전체")
    p.add_argument("--json", action="store_true", help="JSON 출력")
    args = p.parse_args()
    reg = load_registry(include_inactive=args.all)
    if args.json:
        print(json.dumps(reg, ensure_ascii=False, indent=2))
        return
    print(f"📇 에이전트 레지스트리 ({len(reg)}명)")
    for aid, m in reg.items():
        tn = len(m.get("tools", []))
        flag = "" if m.get("_folder_exists") else "  ⚠️폴더없음"
        print(f"  [{m.get('status'):10}] {aid:12} {m.get('display','?'):6} tools={tn}  {m.get('role','')[:40]}{flag}")


if __name__ == "__main__":
    _cli()
