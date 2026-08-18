#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""함대 배정 — 지금 이 인원이 **어느 프로젝트**에 붙어 있는가.

오너 지시(2026-08-18): "펫나 에이전트 재와별 개발에 모두 투입 시켜".

단일 소스는 `_shared/fleet_assignment.json`(평문·git 추적)이다. 암호화 파일에 두지 않는
이유는 이미 두 번 겪었다 — 기계+계정 파생 키로 암호화한 설정은 **기계 간 공유가 원천적으로
불가능**하고, 그때 각 기계는 서로 다른 값을 보면서 같은 값을 본다고 믿는다(2026-07-11).

이 모듈이 하는 일은 하나다: **배정되지 않은 프로젝트의 사이클을 코드로 막는다.**
문서에 "펫나는 지금 쉰다"라고 적는 것과 코드가 막는 것은 다르다(2026-07-11 단일 기계
가드가 한쪽 방향만 막고 있던 사고와 같은 계열).
"""

from __future__ import annotations

import json
from pathlib import Path

_PATH = Path(__file__).resolve().parent / "fleet_assignment.json"
DEFAULT_PROJECT = "petnna"


def read_assignment() -> dict:
    """배정 파일. 없거나 깨지면 빈 dict — 그때는 아무것도 막지 않는다(fail-open)."""
    try:
        return json.loads(_PATH.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError):
        return {}


def current_project() -> str:
    """지금 함대가 붙어 있는 프로젝트. 파일이 없으면 예전 기본값(펫나)."""
    data = read_assignment()
    value = str(data.get("project") or "").strip()
    return value or DEFAULT_PROJECT


def role_of(person: str) -> str:
    """그 사람이 지금 프로젝트에서 맡은 역할. 배정이 없으면 빈 문자열."""
    roles = read_assignment().get("roles") or {}
    return str(roles.get(person) or "").strip() if isinstance(roles, dict) else ""


def people_for(role: str) -> str:
    """역할 → 사람 이름(보고서·알림 표기용). 없으면 빈 문자열."""
    roles = read_assignment().get("roles") or {}
    if not isinstance(roles, dict):
        return ""
    for person, assigned in roles.items():
        if str(assigned).strip() == role:
            return str(person)
    return ""


def assignment_guard(agent: str, project: str = DEFAULT_PROJECT) -> bool:
    """이 사이클을 멈춰야 하면 True. 멈추는 이유를 **반드시 한 줄 남긴다**.

    조용히 종료하면 "데몬은 살아 있는데 아무 일도 안 한다"가 되어, 죽은 잡과 구분이
    안 된다(2026-07-10 `disabled`가 침묵장치로 뒤집힌 사고).
    """
    now = current_project()
    if now == project:
        return False
    print(f"[{agent}] 배정이 '{now}'라 '{project}' 사이클을 건너뛴다 "
          f"— 되돌리려면 _shared/fleet_assignment.json의 project를 고쳐라")
    return True


if __name__ == "__main__":
    data = read_assignment()
    print(f"배정 프로젝트: {current_project()} (since {data.get('since', '?')})")
    for person, role in (data.get("roles") or {}).items():
        print(f"  {person:4} → {role}")
