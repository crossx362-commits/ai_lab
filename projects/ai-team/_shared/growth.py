"""에이전트 공통 성장 루프 — 실행 기록·수집·개선제안 보관.

헌장: docs/AGENT_GROWTH_DOCTRINE.md
- record(): 매 실행 후 성장 기록 1건 적재 (output/growth/<agent>.jsonl)
- add_proposal()/list_proposals(): 개선 제안 보관(승인 전 적용 금지)
- collect()/summary(): 예원이 성장 기록을 수집·점검할 때 사용

핵심: 기록은 절대 본 작업을 방해하지 않는다(모든 함수 예외 안전).
"""

from __future__ import annotations

import json
from datetime import datetime
from pathlib import Path


def _root() -> Path:
    """ai_lab 루트 탐색 — .env 마커 기준(루트에만 존재, projects/ai-team의 CLAUDE.md 오인 방지)."""
    p = Path(__file__).resolve()
    for cand in p.parents:
        if (cand / ".env").exists() or (cand / ".env.encrypted").exists():
            return cand
    return p.parents[3]  # fallback: projects/ai-team/_shared/growth.py → ai_lab


GROWTH_DIR = _root() / "output" / "growth"
PROPOSALS_FILE = GROWTH_DIR / "proposals.json"


def _now() -> str:
    return datetime.now().strftime("%Y-%m-%d %H:%M:%S")


def record(agent: str, *, role: str = "", data: str = "", judgment: str = "",
           result: str = "", good: str = "", bad: str = "", uncertain: str = "",
           improve: str = "", scores: dict | None = None,
           memory: str | None = None, memory_confidence: str = "") -> None:
    """실행 후 성장 기록 1건 적재. 점수 dict 키: fit(25)/evidence(25)/efficiency(20)/risk(20)/brevity(10)."""
    try:
        GROWTH_DIR.mkdir(parents=True, exist_ok=True)
        sc = scores or {}
        total = sum(int(sc.get(k, 0)) for k in ("fit", "evidence", "efficiency", "risk", "brevity"))
        entry = {
            "ts": _now(), "agent": agent, "role": role,
            "data": data, "judgment": judgment, "result": result,
            "self_eval": {"good": good, "bad": bad, "uncertain": uncertain, "improve": improve},
            "scores": sc, "total": total,
            "memory": memory, "memory_confidence": memory_confidence,
        }
        with (GROWTH_DIR / f"{agent}.jsonl").open("a", encoding="utf-8") as f:
            f.write(json.dumps(entry, ensure_ascii=False) + "\n")
    except Exception:
        pass  # 기록 실패가 본 작업을 막지 않는다


def add_proposal(agent: str, problem: str, fix: str, effect: str = "",
                 risk: str = "", approval_needed: bool = True) -> None:
    """개선 제안 보관 — 승인 전 적용 금지. 예원이 수집해 사용자에 승인 요청."""
    try:
        GROWTH_DIR.mkdir(parents=True, exist_ok=True)
        items = list_proposals(unresolved_only=False)
        items.append({
            "ts": _now(), "agent": agent, "problem": problem, "fix": fix,
            "effect": effect, "risk": risk, "approval_needed": approval_needed,
            "status": "pending",
        })
        PROPOSALS_FILE.write_text(json.dumps(items, ensure_ascii=False, indent=2), encoding="utf-8")
    except Exception:
        pass


def list_proposals(unresolved_only: bool = True) -> list[dict]:
    try:
        items = json.loads(PROPOSALS_FILE.read_text(encoding="utf-8")) if PROPOSALS_FILE.exists() else []
        items = items if isinstance(items, list) else []
    except Exception:
        return []
    return [p for p in items if p.get("status") == "pending"] if unresolved_only else items


def collect(agent: str | None = None, limit: int = 50) -> list[dict]:
    """성장 기록 수집(최근순). agent 지정 시 해당 에이전트만. (예원용)"""
    out: list[dict] = []
    try:
        files = [GROWTH_DIR / f"{agent}.jsonl"] if agent else sorted(GROWTH_DIR.glob("*.jsonl"))
        for fp in files:
            if not fp.exists():
                continue
            for line in fp.read_text(encoding="utf-8").splitlines():
                try:
                    out.append(json.loads(line))
                except Exception:
                    continue
    except Exception:
        return []
    out.sort(key=lambda e: e.get("ts", ""), reverse=True)
    return out[:limit]


def summary() -> dict:
    """에이전트별 기록 수·평균 총점·최근 부족점(예원 점검용)."""
    recs = collect(limit=10_000)
    agg: dict[str, dict] = {}
    for r in recs:
        a = r.get("agent", "?")
        d = agg.setdefault(a, {"count": 0, "total_sum": 0, "last_ts": "", "recent_bad": []})
        d["count"] += 1
        d["total_sum"] += int(r.get("total", 0))
        if r.get("ts", "") > d["last_ts"]:
            d["last_ts"] = r["ts"]
        bad = r.get("self_eval", {}).get("bad")
        if bad and len(d["recent_bad"]) < 3:
            d["recent_bad"].append(bad)
    for a, d in agg.items():
        d["avg_total"] = round(d["total_sum"] / d["count"], 1) if d["count"] else 0
    return agg
