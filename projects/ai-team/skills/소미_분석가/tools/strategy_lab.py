#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""strategy_lab.py — 한별: 전략 연구→백테스트 검증→모의 반영 자동 루프 (로컬 올라마).

오너 지시(2026-07-06): 주식 수익률 개선을 위한 '학습→개선→반영'을 루프화.
기존 예원 성장엔진(learn/tune/patch)과 별개 축 — 이건 '새 전략 발굴·검증' 전용.

한 사이클:
  1) 가설(hypothesize) — 전략 백로그에서 다음 검증 대상 선정. 로컬 올라마(lm_first=True)가
     과거 검증 이력을 보고 우선순위 판단(실패 시 '가장 오래 미검증' 결정적 폴백).
  2) 검증(validate)     — backtest 다기간(12·24개월) 3단 판정(✅채택/❌기각/🔸보류)을 그대로 사용
     (Bailey&López de Prado 다중검정 방어 — MIN_TRADES_SIGNIFICANT≥30·흑자·샤프>0).
  3) 반영(reflect)      — 통과/탈락 결과를 모의 설정(somi_paper_strategies.json)에 반영.
     ✅채택→활성, ❌기각→비활성(모의 가점 0), 🔸보류→불변. advisor가 이 설정을 읽어 가점 적용.

안전선(불변):
  - 코드 생성·수정 없음. '검증된 설정값(활성여부·가점 0~12)'만 바꾼다 → self_patch와 충돌·폭주 없음.
  - 모의(_is_paper) 전용. 실거래 경로·보수값은 advisor 쪽 _is_paper 가드라 원천 무관.
  - 가설은 backtest에 이미 구현된 변형 라벨(_STRATEGY_VARIANTS)만 — 임의 전략 실행 불가(화이트리스트).
  - LLM은 요약·우선순위 '보조'로만. 판정은 100% 백테스트 수치(3단 규칙)가 결정 — LLM이 채택 못 함.

실행:
  python strategy_lab.py --once     # 1회 사이클
  python strategy_lab.py --daemon    # 주 1회(백테스트 부하 큼) 자동
"""
from __future__ import annotations

import json
import os
import sys
import time
from datetime import datetime
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

_here = Path(__file__).resolve().parent
AI_TEAM = _here.parents[2]
ROOT = AI_TEAM.parents[1]
sys.path.insert(0, str(AI_TEAM))
sys.path.insert(0, str(_here))

from _shared.env import load_env       # noqa: E402
from _shared.notify import send        # noqa: E402
from _shared.process import ProcessLock  # noqa: E402

load_env(str(ROOT))

import backtest as bt                  # noqa: E402 — 검증 엔진·변형 라벨 단일 소스

CACHE = ROOT / "output" / "cache"
GROWTH = ROOT / "output" / "growth"
BACKLOG = GROWTH / "strategy_backlog.json"    # 검증 대기 가설
LEDGER = GROWTH / "strategy_lab.json"         # 검증 결과 이력
STATE = GROWTH / "strategy_lab_state.json"
PAPER_CFG = CACHE / "somi_paper_strategies.json"  # advisor가 읽는 모의 전략 활성/가점 설정

# 가설 라벨 → advisor의 모의 전략 키(활성/가점 반영 대상). 라벨은 backtest._STRATEGY_VARIANTS와 일치해야 함.
LIVE_MAP = {"+52주신고가+국면": "52w", "+거래량돌파+국면": "breakout"}
BONUS_MIN, BONUS_MAX = 0, 12
RUN_EVERY_DAYS = 7


def _load(p: Path, default):
    try:
        return json.loads(p.read_text(encoding="utf-8")) if p.exists() else default
    except Exception:
        return default


def _save(p: Path, data) -> None:
    p.parent.mkdir(parents=True, exist_ok=True)
    tmp = p.with_name(p.name + ".tmp")
    tmp.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    os.replace(tmp, p)


def _seed_backlog() -> list[dict]:
    """초기 백로그 — backtest에 구현된 검증 대상 변형(웹 연구 2026-07-05 근거 포함).
    새 아이디어는 이 파일에 {label(backtest 변형 라벨), hold, note, source} 추가로 주입(웹리서치 결과 등)."""
    seed = [
        {"label": "+52주신고가+국면", "hold": 10, "note": "장기 신고가 편승(웹: best-performing)", "source": "web 2026-07-05"},
        {"label": "+거래량돌파+국면", "hold": 10, "note": "turtle/Donchian 20일 돌파+거래량", "source": "web 2026-07-05"},
        {"label": "+돌파+상대강도", "hold": 10, "note": "돌파+주도주 필터", "source": "실험 2026-07-05"},
        {"label": "+돌파+상대강도+RSI", "hold": 10, "note": "돌파+주도주+과매수회피(실게이트선 표본부족 주의)", "source": "실험 2026-07-05"},
        {"label": "+상대강도+국면", "hold": 10, "note": "시장 초과강세 단독", "source": "실험"},
        {"label": "+RSI+상대강도+국면", "hold": 10, "note": "복합 필터", "source": "실험"},
    ]
    for h in seed:
        h["id"] = f'{h["label"]}@{h["hold"]}'
        h["tested_ts"] = ""
    return seed


def _verdict(m12: dict, m24: dict) -> tuple[str, dict]:
    """backtest.validate_strategies와 동일한 3단 판정 규칙을 단일 라벨에 적용."""
    cells = {"12mo": m12, "24mo": m24}
    any_trades = insufficient = underperform = False
    for m in (m12, m24):
        if not m or not m.get("trades"):
            insufficient = True
            continue
        any_trades = True
        if not m.get("significant"):
            insufficient = True
        elif not (m.get("total_return", 0) > 0 and m.get("sharpe", 0) > 0):
            underperform = True
    if not any_trades or (insufficient and not underperform):
        v = "🔸보류(표본부족)"
    elif underperform:
        v = "❌기각(성과미달)"
    else:
        v = "✅채택"
    return v, cells


def _validate(label: str, hold: int) -> tuple[str, dict]:
    """단일 가설을 12·24개월로 검증. KIS 일봉 로드(수 분 소요)."""
    r12 = bt._collect_variants(12, 60, (hold,))[0].get((label, hold), {})
    r24 = bt._collect_variants(24, 60, (hold,))[0].get((label, hold), {})
    return _verdict(r12, r24)


def _pick(backlog: list[dict], ledger: list[dict]) -> dict:
    """다음 검증 대상 — 로컬 올라마가 과거 이력 보고 우선순위 판단(실패 시 가장 오래 미검증 폴백)."""
    fallback = sorted(backlog, key=lambda h: h.get("tested_ts", ""))[0]
    try:
        from _shared.llm import text
        recent = [{"id": x["id"], "verdict": x["verdict"]} for x in ledger[-10:]]
        prompt = (
            "너는 퀀트 전략 검증 우선순위 결정자다. 아래 백로그에서 '다음에 백테스트로 검증할' 가설 1개의 id만 골라라.\n"
            "원칙: 오래 미검증(tested_ts 빈값/과거)·과거 🔸보류(표본부족)로 재검증 가치 있는 것 우선, 최근 ❌기각은 후순위.\n"
            'JSON만: {"id": "<정확한 id>"}\n\n'
            f"[백로그] {json.dumps([{k: h.get(k) for k in ('id', 'tested_ts', 'note')} for h in backlog], ensure_ascii=False)}\n"
            f"[최근 결과] {json.dumps(recent, ensure_ascii=False)}"
        )
        raw = text(prompt, json_mode=True, max_tokens=120, temperature=0.2, lm_first=True)  # 로컬 올라마 우선
        pid = json.loads(raw[raw.find("{"):raw.rfind("}") + 1]).get("id")
        return next((h for h in backlog if h["id"] == pid), fallback)
    except Exception:
        return fallback


def _apply_to_paper(label: str, verdict: str) -> str:
    """검증 결과를 advisor가 읽는 모의 설정에 반영 — 매핑된 라이브 전략만. 코드 수정 없음(설정값만)."""
    key = LIVE_MAP.get(label)
    if not key:
        return f"(라이브 매핑 없음 — 이력만 기록)"
    cfg = _load(PAPER_CFG, {})
    strat = cfg.setdefault("strategies", {}).setdefault(key, {"enabled": True, "bonus": 10})
    before = dict(strat)
    if verdict.startswith("✅"):
        strat["enabled"] = True
        strat["bonus"] = max(BONUS_MIN, min(BONUS_MAX, int(strat.get("bonus", 10)) or 10))
    elif verdict.startswith("❌"):
        strat["enabled"] = False   # 성과 미달 전략은 모의 가점 끔(데이터 오염 방지)
    # 🔸보류는 불변(표본부족 = 판단 불가)
    strat["bonus"] = max(BONUS_MIN, min(BONUS_MAX, int(strat.get("bonus", 10))))
    cfg["ts"] = datetime.now().strftime("%Y-%m-%d %H:%M")
    _save(PAPER_CFG, cfg)
    return f"{key}: {before}→{{enabled:{strat['enabled']},bonus:{strat['bonus']}}}"


def run_once() -> str:
    backlog = _load(BACKLOG, None)
    if not backlog:
        backlog = _seed_backlog()
        _save(BACKLOG, backlog)
    ledger = _load(LEDGER, [])

    pick = _pick(backlog, ledger)
    label, hold = pick["label"], int(pick.get("hold", 10))
    verdict, cells = _validate(label, hold)

    entry = {"ts": datetime.now().strftime("%Y-%m-%d %H:%M"), "id": pick["id"],
             "label": label, "hold": hold, "verdict": verdict,
             "m12": {k: cells["12mo"].get(k) for k in ("trades", "total_return", "sharpe", "significant")},
             "m24": {k: cells["24mo"].get(k) for k in ("trades", "total_return", "sharpe", "significant")}}
    ledger.append(entry)
    _save(LEDGER, ledger[-200:])

    for h in backlog:
        if h["id"] == pick["id"]:
            h["tested_ts"] = entry["ts"]
    _save(BACKLOG, backlog)

    applied = _apply_to_paper(label, verdict)

    m24 = cells["24mo"]
    summary = (f"🔬 [전략랩] {label} (보유 {hold}일) → {verdict}\n"
               f"24개월: {m24.get('trades', 0)}건·{m24.get('total_return', 0)}%·샤프 {m24.get('sharpe', 0)}\n"
               f"반영: {applied}")
    try:
        send(summary)
    except Exception:
        pass
    print(summary)
    return summary


def daemon() -> None:
    with ProcessLock("strategy_lab"):
        print(f"[{datetime.now()}] 🔬 전략랩 데몬 시작 (주 1회 검증)")
        while True:
            st = _load(STATE, {})
            last = st.get("last_run", "")
            due = True
            if last:
                try:
                    due = (datetime.now() - datetime.strptime(last, "%Y-%m-%d %H:%M")).days >= RUN_EVERY_DAYS
                except Exception:
                    due = True
            # 평일 장 마감 후(16:30↑)에만 — KIS 부하·시세 안정
            now = datetime.now()
            if due and now.weekday() < 5 and now.strftime("%H:%M") >= "16:30":
                try:
                    run_once()
                    st["last_run"] = now.strftime("%Y-%m-%d %H:%M")
                    _save(STATE, st)
                except Exception as exc:
                    print(f"[{now}] 사이클 오류: {exc}")
            time.sleep(600)


def main() -> None:
    if "--daemon" in sys.argv:
        daemon()
    elif "--once" in sys.argv:
        run_once()
    else:
        print(json.dumps({"backlog": _load(BACKLOG, []), "paper_cfg": _load(PAPER_CFG, {}),
                          "ledger_tail": _load(LEDGER, [])[-3:]}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
