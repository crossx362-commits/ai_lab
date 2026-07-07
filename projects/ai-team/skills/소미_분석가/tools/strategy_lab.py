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
CHAMPIONS = GROWTH / "strategy_champions.json"  # ✅채택된 새 스펙 — 실거래/라이브 승격 후보(사람·self_patch 실장 대기)
STATE = GROWTH / "strategy_lab_state.json"
PAPER_CFG = CACHE / "somi_paper_strategies.json"  # advisor가 읽는 모의 전략 활성/가점 설정

# 가설 라벨 → advisor의 모의 전략 키(활성/가점 반영 대상). 라벨은 backtest._STRATEGY_VARIANTS와 일치해야 함.
LIVE_MAP = {"+52주신고가+국면": "52w", "+거래량돌파+국면": "breakout"}
BONUS_MIN, BONUS_MAX = 0, 12
# 주1→일1 상향(오너 "연구 개선" 2026-07-07): 백로그 7건이 주1회론 7주 — 연구 처리량이 병목.
# 장 마감 후(16:30↑)에만 돌아 KIS 부하 무관. env SOMI_LAB_EVERY_DAYS로 조정.
RUN_EVERY_DAYS = int(os.getenv("SOMI_LAB_EVERY_DAYS", "1"))


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


def _verdict(cells: dict) -> str:
    """4단 판정(경쟁력 감사 2026-07-06 반영). 표본유의성 + 흑자 + 샤프>0 + **벤치마크(지수 단순보유)
    초과**까지 봐야 진짜 엣지. cells 각 metrics에 bench_sharpe/bench_ret 부착돼 있어야 함."""
    any_trades = insufficient = underperform = bench_fail = False
    for m in cells.values():
        if not m or not m.get("trades"):
            insufficient = True
            continue
        any_trades = True
        if not m.get("significant"):
            insufficient = True
        elif not (m.get("total_return", 0) > 0 and m.get("sharpe", 0) > 0):
            underperform = True
        elif m.get("bench_sharpe") is not None and m.get("sharpe", 0) <= m["bench_sharpe"]:
            bench_fail = True   # 표본충분·흑자지만 패시브(지수 단순보유) 위험조정 대비 초과수익 없음
    if not any_trades or (insufficient and not underperform and not bench_fail):
        return "🔸보류(표본부족)"
    if underperform:
        return "❌기각(성과미달)"
    if bench_fail:
        return "⚠️벤치미달(패시브 대비 초과수익 없음)"
    return "✅채택"


def _pass(hyp: dict, hold: int, periods: tuple[int, ...], expand: bool) -> tuple[str, dict]:
    """한 검증 패스 — 가설(기존 라벨 or 새 스펙) × 기간들 × (expand면 중소형 병합)로 백테스트 후 판정.
    각 기간에 벤치마크(지수 단순보유) 지표를 부착해 '패시브 대비 초과수익' 여부까지 판정에 반영."""
    saved = bt.UNIVERSE
    if expand:
        bt.UNIVERSE = {**bt.UNIVERSE, **bt.SMALL_UNIVERSE}
    try:
        cells = {}
        for mo in periods:
            if hyp.get("spec"):
                m = bt.run_levels(bt.make_spec_levels(hyp["spec"]), mo, 60, hold)
            else:
                m = bt._collect_variants(mo, 60, (hold,))[0].get((hyp["label"], hold), {})
            if m.get("trades"):
                bench = bt.benchmark_metrics(mo)   # 지수 단순보유(같은 잣대 샤프)
                m = {**m, "bench_sharpe": bench.get("sharpe"), "bench_ret": bench.get("ret")}
            cells[f"{mo}mo"] = m
    finally:
        bt.UNIVERSE = saved
    return _verdict(cells), cells


def _validate(hyp: dict, hold: int) -> tuple[str, dict, str]:
    """자동 에스컬레이션 검증 — 표본부족이면 스스로 표본을 늘려 재검증(오너 지시 2026-07-06).
      0단계: 12·24개월 / 대형 40종목 (기본)
      1단계: 표본부족 → 24·36개월 / 대형 40종목 (히스토리 연장, 유니버스 성격 불변 우선)
      2단계: 여전히 부족 → 24·36개월 / 대형+중소형 60종목 (신호 breadth 확대)
    성격이 다른 유니버스 확대는 마지막 수단(대형↔중소형 전이는 부호가 뒤집힐 수 있어 기록에 stage 명시).
    반환: (verdict, cells, stage설명)."""
    ladders = [
        ((12, 24), False, "0:12·24mo/대형40"),
        ((24, 36), False, "1:24·36mo/대형40(기간연장)"),
        ((24, 36), True, "2:24·36mo/대형+중소형60(유니버스확대)"),
    ]
    last = ("🔸보류(표본부족)", {}, ladders[0][2])
    for periods, expand, tag in ladders:
        v, cells = _pass(hyp, hold, periods, expand)
        last = (v, cells, tag)
        if not v.startswith("🔸"):   # 채택/기각으로 결론나면 즉시 확정(표본 확보 성공)
            break
    return last


def _pick(backlog: list[dict], ledger: list[dict]) -> dict:
    """다음 검증 대상 — 로컬 올라마가 과거 이력 보고 우선순위 판단(실패 시 가장 오래 미검증 폴백).
    오너 지시 가설(source에 'owner')은 미검증이면 LLM 판단 없이 최우선(2026-07-07)."""
    owner_first = [h for h in backlog if not h.get("tested_ts") and "owner" in str(h.get("source", ""))]
    if owner_first:
        return owner_first[0]
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


# ── 새 전략 아이디어 자동생성(로컬 올라마) + 스펙 검증/클램프 ──────────────
_PARAM_BOUNDS = {  # 필터 타입별 파라미터 허용범위(LLM 생성 스펙을 여기로 클램프 — 폭주/무의미 방지)
    "breakout": {"window": (10, 40), "vol_mult": (1.0, 3.0)},
    "high52": {"pct": (0.90, 1.0)},
    "rsi_max": {"max": (50, 90)}, "rsi_min": {"min": (10, 50)},
    "rel_strength": {"lookback": (10, 60)}, "obv_rising": {"lookback": (5, 20)},
    "above_ma": {"n": (5, 120)},
    "pullback": {"min_dip": (0.01, 0.05), "max_dip": (0.06, 0.20)},
}


def _valid_spec(spec: dict) -> dict | None:
    """LLM 생성 스펙을 화이트리스트 타입·파라미터 범위로 정제. 유효 필터 1~4개면 반환, 아니면 None.
    base는 bt._SPEC_BASES 화이트리스트만(momentum|trend), 그 외/누락은 momentum.
    pullback 필터 포함 스펙은 trend 강제 — momentum base(돌파일 60+)와 눌림은 구조적 모순(거래 0건)."""
    base = spec.get("base") if spec.get("base") in bt._SPEC_BASES else "momentum"
    out = []
    for f in (spec.get("filters") or [])[:4]:
        typ = f.get("type")
        if typ not in bt._FILTER_TYPES:
            continue
        bounds = _PARAM_BOUNDS.get(typ, {})
        params = {}
        for k, (lo, hi) in bounds.items():
            v = f.get("params", {}).get(k)
            if v is None:
                continue
            try:
                params[k] = max(lo, min(hi, type(lo)(v)))
            except Exception:
                pass
        out.append({"type": typ, "params": params})
    if not out:
        return None
    if any(f["type"] == "pullback" for f in out):
        base = "trend"
    return {"base": base, "filters": out}


def _gen_specs(ledger: list[dict], n: int = 1) -> list[dict]:
    """로컬 올라마가 새 전략 아이디어(스펙)를 생성 — 프리미티브 조합. 정제 통과분만 가설로 반환."""
    try:
        from _shared.llm import text
        done = [x.get("label") for x in ledger[-20:]]
        prompt = (
            "너는 퀀트 전략 설계자다. 아래 '필터 프리미티브'만 2~3개 조합해 새 매매전략 후보를 만들어라.\n"
            f"필터 타입: {', '.join(bt._FILTER_TYPES)}\n"
            "각 파라미터 예: breakout{window:20,vol_mult:1.5} high52{pct:0.98} rsi_max{max:70} "
            "rsi_min{min:30} rel_strength{lookback:20} obv_rising{lookback:10} above_ma{n:20} "
            "pullback{min_dip:0.02,max_dip:0.12}\n"
            "base 선택: \"momentum\"(돌파형 — 당일 급등·거래량 돌파일에만 진입) | "
            "\"trend\"(추세질 — 정배열 상승추세면 진입, pullback 등 눌림형은 반드시 trend).\n"
            "원칙: base에 얹는 '진입 조건'. 서로 보완되는 조합(추세+과열회피 등). 기존과 다른 새 조합 우선.\n"
            f"이미 검증한 전략(중복 회피): {done}\n"
            f'JSON만: {{"strategies": [{{"name": "짧은이름", "base": "momentum|trend", '
            f'"filters": [{{"type": "...", "params": {{...}}}}]}}]}} ({n}개)'
        )
        raw = text(prompt, json_mode=True, max_tokens=400, temperature=0.5, lm_first=True)  # 로컬 올라마
        data = json.loads(raw[raw.find("{"):raw.rfind("}") + 1])
    except Exception as e:
        print(f"스펙 생성 실패(스킵): {e}")
        return []
    out = []
    for s in (data.get("strategies") or [])[:n]:
        spec = _valid_spec(s)
        if not spec:
            continue
        name = str(s.get("name") or "gen")[:30]
        sig = json.dumps(spec, sort_keys=True, ensure_ascii=False)
        out.append({"id": f"spec:{name}:{abs(hash(sig)) % 100000}", "label": f"[스펙]{name}",
                    "spec": spec, "hold": 10, "note": sig[:80], "source": "ollama-gen", "tested_ts": ""})
    return out


def _apply_to_paper(label: str, verdict: str) -> str:
    """검증 결과를 advisor가 읽는 모의 설정에 반영 — 매핑된 라이브 전략만. 코드 수정 없음(설정값만).
    새 스펙 전략(LIVE_MAP 밖)은 라이브 진입로직이 없으므로 자동반영 안 함 — 이력·챔피언보드로만 승격 후보."""
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

    # 미검증 가설이 부족하면 로컬 올라마가 새 전략 아이디어(스펙)를 생성해 백로그 보충(중복 id 제외).
    untested = [h for h in backlog if not h.get("tested_ts")]
    if len(untested) < 2:
        have = {h["id"] for h in backlog}
        for g in _gen_specs(ledger, n=2):
            if g["id"] not in have:
                backlog.append(g)
        _save(BACKLOG, backlog)

    pick = _pick(backlog, ledger)
    hold = int(pick.get("hold", 10))
    verdict, cells, stage = _validate(pick, hold)

    # 결과 기간이 에스컬레이션에 따라 달라지므로(12·24 또는 24·36) 셀 전체를 요약 저장(벤치마크 포함)
    period_summary = {k: {kk: v.get(kk) for kk in
                          ("trades", "total_return", "sharpe", "significant", "bench_sharpe", "bench_ret")}
                      for k, v in cells.items()}
    entry = {"ts": datetime.now().strftime("%Y-%m-%d %H:%M"), "id": pick["id"],
             "label": pick["label"], "hold": hold, "verdict": verdict, "stage": stage,
             "spec": pick.get("spec"), "periods": period_summary}
    ledger.append(entry)
    _save(LEDGER, ledger[-200:])

    for h in backlog:
        if h["id"] == pick["id"]:
            h["tested_ts"] = entry["ts"]
    _save(BACKLOG, backlog)

    applied = _apply_to_paper(pick["label"], verdict)
    # ✅채택된 '새 스펙'은 라이브 진입로직이 없으므로 챔피언보드에 승격후보로 기록(사람/self_patch가 실장).
    if verdict.startswith("✅") and pick.get("spec"):
        champs = _load(CHAMPIONS, [])
        champs.append({"ts": entry["ts"], "label": pick["label"], "spec": pick["spec"],
                       "hold": hold, "periods": period_summary, "stage": stage})
        _save(CHAMPIONS, champs[-50:])

    detail = " · ".join(f"{k} {m.get('trades', 0)}건·{m.get('total_return', 0)}%·샤프{m.get('sharpe', 0)}"
                        for k, m in cells.items())
    summary = (f"🔬 [전략랩] {pick['label']} (보유 {hold}일) → {verdict}\n"
               f"검증단계 {stage}\n{detail}\n반영: {applied}")
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
