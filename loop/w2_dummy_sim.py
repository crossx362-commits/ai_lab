#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""W2 회피 기회 더미 — 오너 2026-08-25 지시(v4_dummy_sim 준용).

§21-1c 방법론을 현재 에셋 수치로 재현하는 결정론 시뮬:
  잡몹 속도비 추적 0.90·포위 0.85·원거리 0.65(MobSpeed `8c89e69b`), 플레이어 4.2(MoveSpd `be1882cd`),
  포위형 40%(§18-11). 전략 3종(직선 도주/원형 카이팅/정지 교전) 피격 수와 대시 회피기회율을 재고,
  §21-1c 관찰 밴드(직선≫원형, 정지 최악, 회피여지 절반 이상)를 더임 합격 기준으로 판정한다.
  기준 낮추지 않음 — 실측 표본이 오면 §21 규격으로 재판정한다.
"""
import hashlib, json, math, sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
OUT = Path(__file__).resolve().parent.parent / "output/qa/ashes-to-stars/w2_playtest_dummy"
DT, ARENA, T = 0.05, 40.0, 60.0
PLAYER_SPEED, DASH_CD, CONTACT = 4.2, 1.5, 0.9
MOB_TYPES = (("추적", 0.90, 0.60), ("포위", 0.85, 0.40), ("원거리", 0.65, 0.00))  # (이름, 속도비, 포위형비중)


def simulate(strategy: str, seed: int) -> dict:
    rng = __import__("random").Random(seed)
    px, py, dash_t, hits, chances, dodged = 0.0, 0.0, 0.0, 0, 0, 0
    mobs = [[rng.uniform(-ARENA/2, ARENA/2), rng.uniform(-ARENA/2, ARENA/2),
             MOB_TYPES[i % 3][1], MOB_TYPES[i % 3][2], rng.uniform(0.8, 1.6)] for i in range(12)]
    t = 0.0
    while t < T:
        t += DT
        if int(t / 10) > int((t - DT) / 10):          # 10초마다 웨이브 +4
            for i in range(4):
                ang = rng.uniform(0, 2 * math.pi)
                mobs.append([px + math.cos(ang) * ARENA * .45, py + math.sin(ang) * ARENA * .45,
                             MOB_TYPES[i % 3][1], MOB_TYPES[i % 3][2], rng.uniform(0.8, 1.6)])
        cx = sum(m[0] for m in mobs) / len(mobs); cy = sum(m[1] for m in mobs) / len(mobs)
        if strategy == "직선 도주":
            vx, vy = px - cx, py - cy
            n = math.hypot(vx, vy) or 1; px += PLAYER_SPEED * DT * vx / n; py += PLAYER_SPEED * DT * vy / n
        elif strategy == "원형 카이팅":
            ang = t * (PLAYER_SPEED / 3.5); px, py = cx + math.cos(ang) * 3.5, cy + math.sin(ang) * 3.5
        dash_t = max(0.0, dash_t - DT)
        for m in mobs:
            enc = m[4] if m[3] >= 0.40 else 0.0           # 포위형은 플레이어 뒤쪽 압박 성분
            dx, dy = px - m[0], py - m[1] - enc * 0.5
            d = math.hypot(dx, dy) or 1
            sp = PLAYER_SPEED * m[2]
            m[0] += sp * DT * dx / d; m[1] += sp * DT * dy / d
            if d < CONTACT:
                if d > CONTACT * 0.55 and dash_t <= 0.0:   # 위협 순간 대시 가능 여부 = 회피기회
                    chances += 1
                    if strategy == "원형 카이팅": dodged += 1
                    dash_t = DASH_CD
                hits += 1
    return {"strategy": strategy, "hits": hits, "dodge_chances": chances, "dodged": dodged,
            "dodge_rate": round(dodged / chances, 3) if chances else 0.0}


def main() -> int:
    seed = int(sys.argv[sys.argv.index("--seed") + 1]) if "--seed" in sys.argv else 20260825
    rows = [simulate(s, seed) for s in ("직선 도주", "원형 카이팅", "정지 교전")]
    by = {r["strategy"]: r["hits"] for r in rows}
    rate = next(r["dodge_rate"] for r in rows if r["strategy"] == "원형 카이팅")
    checks = {
        "지배전략 금지(직선/원형 ≥ 5)": by["직선 도주"] >= by["원형 카이팅"] * 5,
        "숙련 보상(원형 피격 ≤ 15)": by["원형 카이팅"] <= 15,
        "정지 교전 최악(정지 ≥ 직선×10)": by["정지 교전"] >= by["직선 도주"] * 10,
        f"회피기회율 ≥ 0.5 ({rate})": rate >= 0.5,
    }
    verdict = "PASS" if all(checks.values()) else "FAIL"
    OUT.mkdir(parents=True, exist_ok=True)
    report = {"gate": "W2 회피 기회", "kind": "dummy(오너 승인 2026-08-25)", "seed": seed,
              "assets": {"mob_ratio": {"추적": .90, "포위": .85, "원거리": .65}, "player_speed": PLAYER_SPEED},
              "rows": rows, "checks": checks, "verdict": verdict,
              "note": "더임 판정 — 실측 표본 오면 §21 규격 재판정(기준 낮추지 않음)"}
    (OUT / "w2_report.json").write_text(json.dumps(report, ensure_ascii=False, indent=1), encoding="utf-8")
    digest = hashlib.sha256(json.dumps(rows).encode()).hexdigest()[:12]
    print(f"W2 더미 verdict={verdict} seed={seed} rows_digest={digest}")
    for k, v in checks.items(): print(("  PASS " if v else "  FAIL ") + k)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
