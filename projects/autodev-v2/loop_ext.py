#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Loop extensions installed from engine.py so the supervisor does not stall."""
from __future__ import annotations

import json
import re
from pathlib import Path
from typing import Any

HERE = Path(__file__).resolve().parent

ART_NOISE = (
    "ox-alpha", "폴리싱", "나노바나나", "시트 정수", "화풍 통일",
    "png 반입", "아틀라스", "재패커",
)

FILE_ANCHORS: dict[str, tuple[str, ...]] = {
    "estate": (
        "projects/ashes-to-stars/unity/Assets/_Game/Scripts/Runtime/EstateScreen.cs",
        "projects/ashes-to-stars/unity/Assets/_Game/Scripts/Runtime/EstateBuildings.cs",
        "projects/ashes-to-stars/unity/Assets/_Game/Scripts/Runtime/EstateBuild.cs",
        "projects/ashes-to-stars/unity/Assets/_Game/Scripts/Runtime/LifeSystem.cs",
    ),
    "formation": (
        "projects/ashes-to-stars/unity/Assets/Scripts/W3Party.cs",
        "projects/ashes-to-stars/unity/Assets/_Game/Scripts/Runtime/PartyScreen.cs",
        "projects/ashes-to-stars/unity/Assets/_Game/Scripts/Runtime/CharacterScreen.cs",
    ),
    "raid": (
        "projects/ashes-to-stars/unity/Assets/_Game/Scripts/Runtime/BossBattle.cs",
        "projects/ashes-to-stars/unity/Assets/Scripts/W3Party.cs",
    ),
    "combat": (
        "projects/ashes-to-stars/unity/Assets/Scripts/W3Party.cs",
        "projects/ashes-to-stars/unity/Assets/_Game/Scripts/Runtime/BossBattle.cs",
        "projects/ashes-to-stars/unity/Assets/_Game/Scripts/Runtime/RaceDef.cs",
    ),
    "character": (
        "projects/ashes-to-stars/unity/Assets/_Game/Scripts/Runtime/CharacterScreen.cs",
        "projects/ashes-to-stars/unity/Assets/_Game/Scripts/Runtime/RaceDef.cs",
        "projects/ashes-to-stars/unity/Assets/_Game/Scripts/Runtime/LifeSystem.cs",
    ),
}


def compact_status_next(cfg: dict[str, Any]) -> str:
    root = Path(cfg.get("_repo_root") or HERE.parents[1])
    p = root / "docs" / "STATUS.md"
    if not p.exists():
        return "(STATUS 없음)"
    text = p.read_text(encoding="utf-8", errors="replace")
    nxt = ""
    m = re.search(r"\*\*다음:\*\*\s*(.+)", text) or re.search(r"\*\*다음:\s*(.+)", text)
    if m:
        nxt = m.group(1).strip()[:400]
    numbered: list[str] = []
    in_next = False
    for ln in text.splitlines():
        if ln.startswith("## 다음 할 일"):
            in_next = True
            continue
        if in_next:
            if ln.startswith("## ") and not ln.startswith("## 다음"):
                break
            s = ln.strip()
            if re.match(r"^\d+\.", s):
                numbered.append(s[:220])
            if len(numbered) >= 3:
                break
    blob = nxt + " " + " ".join(numbered)
    artish = any(tok in blob.lower() for tok in ART_NOISE) or any(tok in blob for tok in ART_NOISE)
    warn = (
        "\n주의: 이 줄이 아트/폴리싱이면 작업으로 만들지 말고 "
        "영지→편성→전투→보상 플레이 기능을 우선한다."
        if artish else ""
    )
    parts = []
    if nxt:
        parts.append("최신 다음 한 줄: " + nxt)
    if numbered:
        parts.append("번호 목록:\n" + "\n".join(numbered))
    return (("\n".join(parts) if parts else "(다음 할 일 없음)") + warn)


def load_project_profile(cfg: dict[str, Any] | None = None) -> dict[str, Any]:
    name = str((cfg or {}).get("active_project") or "ashes-to-stars")
    path = HERE / "profiles" / f"{name}.json"
    if not path.is_file():
        return {}
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return {}
    return data if isinstance(data, dict) else {}


def install() -> None:
    import runner

    AUTODEV = runner.AUTODEV
    AUTODEV.compact_status_next = compact_status_next
    AUTODEV.FILE_ANCHORS = FILE_ANCHORS

    original_candidates = AUTODEV.candidate_files

    def candidate_files(cfg: dict[str, Any], task: dict[str, Any], verify_text: str = "") -> list[str]:
        found = list(original_candidates(cfg, task, verify_text))
        area = str(task.get("area") or "").strip().lower()
        maxn = int(cfg.get("max_candidate_files") or 5)
        for rel in FILE_ANCHORS.get(area, ()):
            if rel not in found:
                found.insert(0, rel)
        return found[:maxn]

    AUTODEV.candidate_files = candidate_files

    original_prompt = runner.director_prompt

    def director_prompt(cfg: dict[str, Any], st: dict[str, Any]) -> str:
        extra = (
            "중요 원칙:\n"
            "- 플레이 루프를 앞으로 민다: 영지(성장·전직·합성) → 출전 편성 → 필드/던전/보스 전투 → 보상 → 영지.\n"
            "- STATUS 전체와 아트/ox-alpha/폴리싱 로그는 작업이 아니다. 아트 다음 줄이 보여도 플레이 기능을 만든다.\n"
            "- loop/ 보드와 WORKLOG를 따라가지 않는다. AutoDev v2 state.json이 큐다.\n"
            f"[STATUS 다음 한 줄 — 참고만, 아트면 무시]\n{compact_status_next(cfg)}\n\n"
        )
        body = original_prompt(cfg, st)
        return body.replace("중요 원칙:\n", extra, 1)

    runner.director_prompt = director_prompt

    original_fill = runner.director_fill

    def seed_play_loop_if_empty(cfg: dict[str, Any], st: dict[str, Any]) -> bool:
        live = {
            "pending", "in_progress", "waiting_verification",
            "waiting_dependency", "running",
        }
        if any(isinstance(t, dict) and t.get("status") in live for t in st.get("tasks", [])):
            return False
        spec = load_project_profile(cfg).get("seed_tasks") or []
        if not isinstance(spec, list) or not spec:
            return False
        tasks = runner.normalize_guarded_tasks(cfg, st, spec)
        if not tasks:
            return False
        st.setdefault("tasks", []).extend(tasks)
        profile = load_project_profile(cfg)
        st["goal"] = st.get("goal") or str(profile.get("goal") or "자율 루프 전진")
        st["last_director_provider"] = "seed"
        print("[SEED] Director 없이 씨앗 작업 " + str(len(tasks)) + "개 투입: " + ", ".join(t["id"] for t in tasks))
        return True

    def director_fill(cfg: dict[str, Any], st: dict[str, Any]) -> bool:
        ok = original_fill(cfg, st)
        if ok:
            return True
        return seed_play_loop_if_empty(cfg, st)

    runner.director_fill = director_fill
    runner.seed_play_loop_if_empty = seed_play_loop_if_empty
    runner.load_project_profile = load_project_profile
    AUTODEV.director_fill = director_fill
    print("[LOOP_EXT] seed/profile/status compact installed")
