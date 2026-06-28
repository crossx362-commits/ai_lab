#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""몬테카를로 가격경로 시뮬레이션 — 디퓨전(미래 경로 분포)의 실용 대체.

종목 일봉 수익률을 부트스트랩(블록 샘플링)해 향후 N일 가격경로 수천 개를 생성하고,
진입가 기준 '목표가 먼저 도달 확률' vs '손절가 먼저 도달 확률', 기대수익/하방위험(VaR)을
계산한다. numpy만 사용(새 의존성 없음). 소미 매수판단에 '확률적 손익비'로 반영.
"""

from __future__ import annotations

import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parents[4]
AI_TEAM_ROOT = PROJECT_ROOT / "projects" / "ai-team"
sys.path.insert(0, str(AI_TEAM_ROOT))
sys.path.insert(0, str(SCRIPT_DIR))

from _shared.env import load_env  # noqa: E402
from somi_kis_reporter import KISClient, num  # noqa: E402

load_env(str(PROJECT_ROOT))

HORIZON = 5     # 향후 거래일 (소미 단기 예측과 동일 지평)
N_PATHS = 3000  # 시뮬 경로 수


def simulate(symbol: str, entry: float, stop: float, target: float,
             horizon: int = HORIZON, n_paths: int = N_PATHS) -> dict | None:
    """과거 수익률 부트스트랩으로 경로 생성 → 목표/손절 도달확률·기대수익 계산."""
    try:
        import numpy as np
        kis = KISClient()
        dailies = kis.daily_prices(symbol, 90)
        closes = [num(d.get("stck_clpr")) for d in reversed(dailies) if num(d.get("stck_clpr"))]
        if len(closes) < 30 or entry <= 0:
            return None
        arr = np.asarray(closes, dtype=float)
        rets = np.diff(np.log(arr))  # 일간 로그수익률
        if len(rets) < 20:
            return None

        rng = np.random.default_rng(abs(hash(symbol)) % (2**32))
        # 부트스트랩: 과거 수익률에서 horizon개씩 복원추출해 경로 생성
        sampled = rng.choice(rets, size=(n_paths, horizon), replace=True)
        paths = entry * np.exp(np.cumsum(sampled, axis=1))  # (n_paths, horizon)

        hit_target = np.zeros(n_paths, dtype=bool)
        hit_stop = np.zeros(n_paths, dtype=bool)
        for i in range(n_paths):
            p = paths[i]
            t_idx = np.argmax(p >= target) if (p >= target).any() else -1
            s_idx = np.argmax(p <= stop) if (p <= stop).any() else -1
            if t_idx == -1 and s_idx == -1:
                continue
            if s_idx == -1:
                hit_target[i] = True
            elif t_idx == -1:
                hit_stop[i] = True
            else:  # 둘 다 도달 → 먼저 닿은 것
                (hit_target if t_idx <= s_idx else hit_stop)[i] = True

        finals = paths[:, -1]
        p_target = float(hit_target.mean())
        p_stop = float(hit_stop.mean())
        exp_ret = float((finals / entry - 1).mean() * 100)        # 기대수익률(%)
        var5 = float(np.percentile(finals / entry - 1, 5) * 100)  # 5% VaR(%)
        win_rate = float((finals > entry).mean())                 # 종가>진입 비율
        return {
            "p_target": round(p_target, 3),
            "p_stop": round(p_stop, 3),
            "edge": round(p_target - p_stop, 3),   # 도달확률 우위
            "exp_ret_pct": round(exp_ret, 2),
            "var5_pct": round(var5, 2),
            "win_rate": round(win_rate, 3),
        }
    except Exception as exc:
        print(f"[mc] {symbol} 시뮬 실패: {exc}", file=sys.stderr)
        return None


def main() -> None:
    import argparse
    ap = argparse.ArgumentParser(description="몬테카를로 가격경로 시뮬")
    ap.add_argument("symbol")
    ap.add_argument("--entry", type=float, required=True)
    ap.add_argument("--stop", type=float, required=True)
    ap.add_argument("--target", type=float, required=True)
    args = ap.parse_args()
    print(simulate(args.symbol, args.entry, args.stop, args.target))


if __name__ == "__main__":
    main()
