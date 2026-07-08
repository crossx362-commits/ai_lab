#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""시장 국면 감지 — 은닉 마르코프 모델(HMM)로 KOSPI200 흐름을 상승/하락/횡보로 추정.

KODEX 200(069500)을 KOSPI 대용으로 일봉 수익률 시계열을 만들고,
3-state Gaussian HMM(numpy 자체 구현, Baum-Welch)으로 현재 숨은 국면을 추정한다.
소미 자동매매가 '하락 국면'에서 신규 매수를 보수적으로(중단) 가져가게 하는 데 쓴다.
"""

from __future__ import annotations

import json
import os
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

KOSPI_PROXY = "069500"   # KODEX 200 (KOSPI200 추종 ETF)
KOSDAQ_PROXY = "229200"  # KODEX 코스닥150 (코스닥150 추종 ETF)


def _fit_hmm(x, k: int = 3, n_iter: int = 40):
    """1차원 가우시안 HMM Baum-Welch(스케일드 forward-backward). 현재 상태 분포 반환."""
    import numpy as np

    x = np.asarray(x, dtype=float)
    T = len(x)
    if T < 20:
        return None
    # 초기화: 평균은 분위수, 분산은 전체분산, 전이행렬은 대각 우세
    mu = np.quantile(x, [(i + 0.5) / k for i in range(k)]).astype(float)
    var = np.full(k, float(x.var()) + 1e-6)
    A = np.full((k, k), 0.1 / (k - 1))
    np.fill_diagonal(A, 0.9)
    pi = np.full(k, 1.0 / k)

    gamma = None
    for _ in range(n_iter):
        B = (1.0 / np.sqrt(2 * np.pi * var)) * np.exp(-((x[:, None] - mu[None, :]) ** 2) / (2 * var))
        B = np.clip(B, 1e-300, None)
        # forward (scaled)
        alpha = np.zeros((T, k)); c = np.zeros(T)
        alpha[0] = pi * B[0]; c[0] = alpha[0].sum() or 1.0; alpha[0] /= c[0]
        for t in range(1, T):
            alpha[t] = (alpha[t - 1] @ A) * B[t]
            c[t] = alpha[t].sum() or 1.0
            alpha[t] /= c[t]
        # backward (scaled)
        beta = np.zeros((T, k)); beta[-1] = 1.0
        for t in range(T - 2, -1, -1):
            beta[t] = (A @ (B[t + 1] * beta[t + 1])) / (c[t + 1] or 1.0)
        gamma = alpha * beta
        gamma /= np.clip(gamma.sum(1, keepdims=True), 1e-300, None)
        # transitions
        xi = np.zeros((k, k))
        for t in range(T - 1):
            m = (alpha[t][:, None] * A) * (B[t + 1] * beta[t + 1])[None, :]
            s = m.sum()
            if s > 0:
                xi += m / s
        A = xi / np.clip(xi.sum(1, keepdims=True), 1e-300, None)
        pi = gamma[0]
        g = np.clip(gamma.sum(0), 1e-300, None)
        mu = (gamma * x[:, None]).sum(0) / g
        var = (gamma * (x[:, None] - mu[None, :]) ** 2).sum(0) / g + 1e-6

    return mu, gamma[-1]


def market_regime(proxy: str = KOSPI_PROXY) -> dict:
    """현재 시장 국면 추정 → {'regime': 'bull'|'bear'|'sideways'|'unknown', ...}.
    proxy: 지수 추종 ETF 코드 (기본 KOSPI200, 코스닥은 KOSDAQ_PROXY)."""
    try:
        import numpy as np
        kis = KISClient()
        dailies = kis.daily_prices(proxy, 70)
        closes = [num(d.get("stck_clpr")) for d in reversed(dailies) if num(d.get("stck_clpr"))]
        if len(closes) < 25:
            return {"regime": "unknown", "reason": "지수 데이터 부족"}
        arr = np.asarray(closes, dtype=float)
        rets = np.diff(np.log(arr)) * 100.0  # 일간 로그수익률(%)
        fit = _fit_hmm(rets, k=3)
        if fit is None:
            raise ValueError("HMM 적합 실패")
        mu, last = fit
        state = int(last.argmax())
        order = list(np.argsort(mu))  # 평균수익률 오름차순: [bear, sideways, bull]
        label = {order[0]: "bear", order[-1]: "bull"}
        regime = label.get(state, "sideways")
        return {
            "regime": regime,
            "confidence": round(float(last.max()), 2),
            "state_mean_ret": round(float(mu[state]), 3),
            "recent5_ret": round(float(rets[-5:].sum()), 2),
        }
    except Exception as exc:
        # HMM 실패 시 단순 추세 폴백 (파이프라인 안 깨지게).
        # 수정(2026-07-08): KIS 조회 등 rets 계산 전 단계에서 실패하면 rets가 미정의라
        # 폴백이 NameError로 조용히 또 실패 → 항상 unknown만 반환하며 폴백이 사실상 죽어있었음.
        if "rets" not in locals():
            return {"regime": "unknown", "reason": str(exc)}
        try:
            import numpy as np
            r = np.asarray(rets[-20:])
            m = float(r.mean())
            regime = "bull" if m > 0.1 else ("bear" if m < -0.1 else "sideways")
            return {"regime": regime, "reason": f"HMM 폴백(평균추세): {exc}"}
        except Exception:
            return {"regime": "unknown", "reason": str(exc)}


_REGIME_KR = {"bull": "🟢 상승 국면", "bear": "🔴 하락 국면", "sideways": "🟡 횡보 국면", "unknown": "❔ 판단 불가"}


def regime_label(regime: str) -> str:
    return _REGIME_KR.get(regime, regime)


# ── 신뢰도 게이트 국면(전환 안정화) ──
# 매 호출의 raw 추정은 경계값에서 흔들린다. 직전 확정 국면과 다른 국면이 나와도
# 신뢰도(confidence)가 임계값 이상일 때만 '전환 인정'한다. 미달이면 직전 국면 유지.
STABLE_FILE = PROJECT_ROOT / "output" / "cache" / "market_regime_stable.json"
CONF_MIN = float(os.getenv("SOMI_REGIME_CONF_MIN", "0.6"))


def _load_stable() -> dict:
    try:
        return json.loads(STABLE_FILE.read_text(encoding="utf-8"))
    except Exception:
        return {}


def _save_stable(state: dict) -> None:
    STABLE_FILE.parent.mkdir(parents=True, exist_ok=True)
    STABLE_FILE.write_text(json.dumps(state, ensure_ascii=False, indent=2), encoding="utf-8")


def stable_regime(proxy: str = KOSPI_PROXY, advance: bool = False,
                  threshold: float | None = None) -> dict:
    """신뢰도 게이트를 통과한 '확정 국면'을 반환.

    raw 국면이 직전 확정 국면과 다르고 confidence ≥ threshold(기본 0.6)일 때만 전환 인정.
    advance=True면 확정 국면 상태파일을 갱신(전환 시각·알림 담당 데몬만 사용).
    advance=False는 읽기 전용 — 결정(매수 게이트 등) 소비자는 이걸 쓴다(경쟁 쓰기 방지).
    반환: market_regime() dict + {regime(확정), raw_regime, changed, prev}.
    """
    thr = CONF_MIN if threshold is None else threshold
    raw = market_regime(proxy)
    rr = raw.get("regime", "unknown")
    conf = float(raw.get("confidence") or 0.0)
    state = _load_stable()
    prev = (state.get(proxy) or {}).get("regime")

    changed = False
    if rr == "unknown":
        stable = prev or "unknown"
    elif prev is None:
        stable = rr               # 최초 시드 — 알릴 전환 없음
    elif rr != prev and conf >= thr:
        stable = rr; changed = True
    else:
        stable = prev             # 동일하거나 신뢰도 미달 → 직전 국면 유지

    if advance and rr != "unknown" and (prev is None or changed):
        state[proxy] = {"regime": stable, "confidence": round(conf, 2)}
        _save_stable(state)

    out = dict(raw)
    out.update({"regime": stable, "raw_regime": rr, "changed": changed, "prev": prev})
    return out


def main() -> None:
    r = market_regime()
    print(f"시장 국면: {regime_label(r['regime'])}")
    print(f"  {r}")


if __name__ == "__main__":
    main()
