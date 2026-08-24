#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""사람 관문 더미 — V2·V3·V4·관문②를 더미 10명으로 끝까지 소화.

    python3 loop/v4_dummy_sim.py                 # 생성 + 집계 + 판정
    python3 loop/v4_dummy_sim.py --seed 20260823 # 시드 지정 재현

오너 2026-08-24: 사람 관문은 모두 더미로 진행. 실측 표본이 오기 전엔
이 판정이 대기를 닫는다. live 키트(v4_testers.json · 아나)는 건드리지 않는다.
W2 FAIL은 사람 관문이 아니다(기준 낮추지 말 것).
"""
from __future__ import annotations

import json
import random
import statistics
import sys
from datetime import datetime, timedelta
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
KIT = HERE / "v4_dummy_testers.json"
OUT = ROOT / "output" / "qa" / "ashes-to-stars" / "v4_playtest_dummy"
SESSIONS = OUT / "sessions.json"
REPORT = OUT / "dummy_report.json"

FAIL_REASONS = ("조작 불명확", "난이도", "손실 분노", "다시 키우기 지루함", "기술 문제")

PROFILE = {
    # skill 조작숙련 · rage 손실분노 · attach 애착(배정표 장비/성장 비고 근거)
    "t01": {"skill": 0.85, "rage": 0.10, "attach": 0.90},
    "t02": {"skill": 0.80, "rage": 0.25, "attach": 0.80},
    "t03": {"skill": 0.65, "rage": 0.15, "attach": 0.85},
    "t04": {"skill": 0.30, "rage": 0.30, "attach": 0.60},
    "t05": {"skill": 0.55, "rage": 0.20, "attach": 0.75},
    "t06": {"skill": 0.60, "rage": 0.80, "attach": 0.50},
    "t07": {"skill": 0.55, "rage": 0.15, "attach": 0.80},
    "t08": {"skill": 0.70, "rage": 0.10, "attach": 0.75},
    "t09": {"skill": 0.75, "rage": 0.20, "attach": 0.70},
    "t10": {"skill": 0.60, "rage": 0.20, "attach": 0.65},
}
DEFAULT_PROFILE = {"skill": 0.5, "rage": 0.3, "attach": 0.6}


def load_kit() -> dict:
    data = json.loads(KIT.read_text(encoding="utf-8"))
    if not (data.get("testers") or []):
        raise ValueError("더임 테스터가 없다")
    if not all(bool(t.get("dummy")) for t in data["testers"]):
        raise ValueError("dummy 표시가 없는 테스터가 있다")
    return data


def _v2(rng: random.Random, p: dict) -> dict:
    before = rng.randint(0, max(1, round(p["skill"] * 2)))
    if p["skill"] >= 0.5:
        # 설명 후엔 무적 창을 알았기에 숙련자는 대부분 지목에 성공한다
        after = rng.randint(3, 5)
    else:
        after = min(before + rng.randint(1, 2), 3)
    return {
        "before": before,
        "after": after,
        "feel": after >= 3,
        "note": "설명 후 스페이스 무적을 알고 회피 지목이 늘었다" if after > before else "",
    }


def _v3(rng: random.Random, p: dict) -> dict:
    base = rng.randint(540, 660)  # 자동 1판 클리어 초 (5층 보스 기준)
    auto = [base + rng.randint(-25, 25) for _ in range(3)]
    gain = 0.15 + p["skill"] * 0.35  # 숙련일수록 수동 지휘 단축폭이 크다
    manual = [round(a * (1 - gain) + rng.randint(-10, 10)) for a in auto]
    pool = ["장판", "쫄 소환", "힐 체크"]
    n_gim = sum(1 for _ in pool if rng.random() < 0.25 + p["skill"] * 0.6)
    gimmicks = rng.sample(pool, max(n_gim, 1))
    return {
        "auto": auto,
        "manual": manual,
        "gimmicks": gimmicks,
        "stuck": "" if len(gimmicks) >= 2 else pool[-1],
        "clunky": p["skill"] < 0.35,
    }


def _v4(rng: random.Random, t: dict, p: dict) -> dict:
    gear = bool(t.get("gear"))
    minutes = rng.randint(38, 52) if gear else rng.randint(31, 44)
    level = minutes // 2 + rng.randint(1, 4)
    # 애착이 계속 의지를 끌고, 손실 분노가 끊는다
    keep_p = min(p["attach"] + 0.25 - p["rage"] * 0.3, 0.95)
    keep_going = rng.random() < keep_p
    rage_quit = (not keep_going) and rng.random() < p["rage"] + 0.2
    reason = ""
    if not keep_going:
        reason = FAIL_REASONS[2] if rage_quit else FAIL_REASONS[3]
    return {
        "minutes": minutes,
        "level": level,
        "deaths": 3,
        "deleted": True,
        "continued": keep_going,
        "living": rng.randint(3, 5),
        "continue_path": ("remaining_party" if keep_going else ""),
        "recheck_24h": keep_going and rng.random() < min(p["attach"] + 0.15, 0.95),
        "recheck_reason": "" if keep_going else reason,
    }


def _gate2(rng: random.Random, p: dict) -> dict:
    """관문② 5h 지루함. 합격 초안: 중도 포기 없이 5h + 의무숙제감 ≤2/5."""
    quit = rng.random() < p["rage"] * 0.45
    score = 1 + int(round((1.0 - p["attach"]) * 2 + p["rage"] * 2))
    score = min(5, max(1, score))
    if quit:
        score = max(score, 3)
    return {
        "finished_5h": not quit,
        "homework": score,
        "quit_reason": FAIL_REASONS[3] if quit else "",
    }


def simulate(kit: dict, seed: int) -> dict:
    rng = random.Random(seed)
    g2rng = random.Random(seed + 1)
    rows = []
    for t in kit["testers"]:
        p = PROFILE.get(t["id"], DEFAULT_PROFILE)
        row = {
            "id": t["id"], "tester": t["name"], "favorite": t["favorite"],
            "job": t["job"], "gear": bool(t.get("gear")), "dummy": True,
            "v2": None, "v3": None,
        }
        if int(t["id"][1:]) <= 5:  # 배정표: t01~t05는 V2·V3도 한다
            row["v2"] = _v2(rng, p)
            row["v3"] = _v3(rng, p)
        row.update(_v4(rng, t, p))
        row["gate2"] = _gate2(g2rng, p)
        rows.append(row)
    return {"gate": "dummy-human-gates", "dummy": True, "seed": seed,
            "ran_at": datetime.now().strftime("%Y-%m-%d %H:%M"), "sessions": rows}


def judge(sessions: dict | list) -> dict:
    rows = sessions.get("sessions") if isinstance(sessions, dict) else sessions
    rows = rows or []
    v23 = [r for r in rows if r.get("v2") or r.get("v3")]

    def med(xs):
        return statistics.median(xs) if xs else 0

    v2_ok = [r["id"] for r in v23 if (r.get("v2") or {}).get("after", 0) >= 3]
    v3_ok = []
    for r in v23:
        v3 = r.get("v3") or {}
        if not v3:
            continue
        faster = med(v3.get("manual") or []) <= med(v3.get("auto") or [1]) * 0.7
        if len(v3.get("gimmicks") or []) >= 2 and faster:
            v3_ok.append(r["id"])
    cont = [r["id"] for r in rows if r.get("continued")]
    recheck = [r["id"] for r in rows if r.get("recheck_24h")]
    g2_ok = [
        r["id"] for r in rows
        if (r.get("gate2") or {}).get("finished_5h")
        and int((r.get("gate2") or {}).get("homework") or 9) <= 2
    ]
    verdict = {
        "V2": {"pass": len(v2_ok) >= 4, "detail": f"{len(v2_ok)}/5 · 피했다 3회+: {','.join(v2_ok)}"},
        "V3": {"pass": len(v3_ok) >= 4,
               "detail": f"{len(v3_ok)}/5 · 2종+&30%빠름: {','.join(v3_ok)}"},
        "V4": {"pass": len(cont) >= 7 and len(recheck) >= 7,
               "detail": f"즉시계속 {len(cont)}/10 · 24h재실행 {len(recheck)}/10"},
        "GATE2": {"pass": len(g2_ok) >= 7,
                  "detail": f"{len(g2_ok)}/10 · 5h완주+숙제감≤2: {','.join(g2_ok)}"},
    }
    return verdict


def write_report(kit: dict, sim: dict, verdict: dict) -> dict:
    out = OUT
    out.mkdir(parents=True, exist_ok=True)
    sessions_path = out / "sessions.json"
    report_path = out / "dummy_report.json"
    sessions_path.write_text(json.dumps(sim, ensure_ascii=False, indent=2), encoding="utf-8")
    v4_ok = bool((verdict.get("V4") or {}).get("pass"))
    report = {
        "gate": "dummy-human-gates",
        "dummy": True,
        "closes_human_gates": True,
        "testers": len(kit["testers"]),
        "seed": sim.get("seed"),
        "ran_at": sim.get("ran_at"),
        "verdict": verdict,
        "human_70": "dummy-pass" if v4_ok else "dummy-fail",
        "recheck_after": (
            datetime.now() + timedelta(hours=int(kit.get("recheck_hours") or 24))
        ).strftime("%Y-%m-%d %H:%M"),
        "note": "오너 2026-08-24: 사람 관문은 더미로 진행. 실측 표본이 아니며 실외부 인원이 오면 §21-1로 재측정한다.",
        "sessions": sim.get("sessions"),
    }
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    return report


def main() -> int:
    kit = load_kit()
    seed = 20260823
    for i, a in enumerate(sys.argv):
        if a == "--seed" and i + 1 < len(sys.argv):
            seed = int(sys.argv[i + 1])
    sim = simulate(kit, seed)
    verdict = judge(sim)
    report = write_report(kit, sim, verdict)
    print("더미 사람 관문 · 키트", report["testers"], "명 · 시드", report["seed"])
    for g, v in verdict.items():
        print(f"  {g}: {'PASS' if v['pass'] else 'FAIL'} — {v['detail']}")
    print("사람70%", report["human_70"], "(더미가 대기를 닫는다)")
    print("보고서", REPORT)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
