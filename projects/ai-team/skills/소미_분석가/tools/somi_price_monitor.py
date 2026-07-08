#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Somi price monitor — watchlist 등록 종목 주가/거래량 급변동 실시간 감시"""

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


SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parents[4]
AI_TEAM_ROOT = PROJECT_ROOT / "projects" / "ai-team"
sys.path.insert(0, str(AI_TEAM_ROOT))

from _shared.env import load_env
from _shared.notify import send
from _shared.process import ProcessLock
from _shared import growth
from somi_kis_reporter import KISClient, num, fmt_int, fmt_pct
from watchlist_manager import load_watchlist


load_env(str(PROJECT_ROOT))

# run_multi()는 watchlist 종목만 감시한다. 아래 기본값은 단일 인스턴스
# 호출 시의 폴백일 뿐 정기 감시 대상이 아니다.
DEFAULT_SYMBOL = ""
DEFAULT_NAME = ""
CHECK_INTERVAL = 60  # 1분마다 확인
RUN_LOG = PROJECT_ROOT / "output" / "bot_logs" / "somi_price_monitor.log"
# 강한 신호 → 어드바이저 즉시 매수검토 트리거(이벤트 드리븐). 어드바이저 데몬이 소비 후 삭제.
TRIGGER_FILE = PROJECT_ROOT / "output" / "cache" / "somi_trigger.json"

# Global thresholds (can be modified by argparse)
PRICE_CHANGE_THRESHOLD = 3.0  # 전일대비 등락률(참고용 — 더 이상 단독 트리거 아님)
VOLUME_RATIO_THRESHOLD = 2.0  # 거래량 평균 대비 2배 이상
# 진짜 '급변동' = 짧은 시간 내 급변. 전일대비 누적이 아니라 최근 N분 변동률로 감지.
RAPID_MOVE_THRESHOLD = 2.0     # 최근 MOVE_WINDOW 내 ±2% 이상 변동 시 급변동
MOVE_WINDOW_SEC = 300          # 급변동 판정 시간창(5분)


def _is_paper() -> bool:
    return os.getenv("KIS_PAPER", "false").strip().lower() in {"1", "true", "yes", "y"}


# 지수 급락 실측 즉시 경보(2026-07-08 사고: 코스피 장중 -8.2% 서킷브레이커를 웹검색 LLM이 놓쳐
# 하루 늦게 보고). 긴급 시장경보는 뉴스가 아니라 '시세 실측'이 1차 — LLM·웹검색 무관하게 작동.
# 지수 직접시세 대신 프록시 ETF(KODEX200/코스닥150) 전일대비 사용(속보감시와 동일 방식).
INDEX_PROXY = {"코스피": "069500", "코스닥": "229200"}
CRASH_STAGES = tuple(float(x) for x in os.getenv("SOMI_INDEX_CRASH_STAGES", "-5,-8").split(","))
CRASH_STATE = PROJECT_ROOT / "output" / "cache" / "index_crash_state.json"


def index_crash_watch(kis: KISClient) -> None:
    """코스피/코스닥 급락 단계(-5% 사이드카권, -8% 서킷브레이커권) 진입 시 즉시 텔레그램.
    지수·단계당 하루 1회(파일 상태 — 데몬 재시작에도 중복 없음). 매 사이클 호출(1분)."""
    today = datetime.now().strftime("%Y-%m-%d")
    try:
        st = json.loads(CRASH_STATE.read_text(encoding="utf-8"))
    except Exception:
        st = {}
    if st.get("date") != today:
        st = {"date": today, "sent": {}}
    changed = False
    for label, code in INDEX_PROXY.items():
        try:
            chg = num(kis.quote(code).get("prdy_ctrt"))
        except Exception:
            continue
        done = st["sent"].get(label, [])
        for stage in sorted(CRASH_STAGES, reverse=True):
            if chg <= stage and stage not in done:
                tag = "서킷브레이커권" if stage <= -8 else "사이드카권"
                ok = send(f"🚨 [지수 급락 — 실측] {label} {chg:+.2f}% ({tag} {stage:+.0f}% 진입)\n"
                          f"보유 포지션·신규 매수 점검 필요. (시세 기반 즉시 경보 — 뉴스 대기 없음)")
                log(f"지수 급락 경보: {label} {chg:+.2f}% (단계 {stage}, 전송 {'성공' if ok else '실패 — 다음 사이클 재시도'})")
                if not ok:
                    continue   # 전송 실패면 미기록 → 1분 뒤 재시도(전송 성공만 하루 1회 소진)
                done.append(stage)
                changed = True
        st["sent"][label] = done
    if changed:
        CRASH_STATE.parent.mkdir(parents=True, exist_ok=True)
        CRASH_STATE.write_text(json.dumps(st, ensure_ascii=False), encoding="utf-8")


def log(message: str) -> None:
    RUN_LOG.parent.mkdir(parents=True, exist_ok=True)
    line = f"[{datetime.now().strftime('%Y-%m-%d %H:%M:%S')}] {message}"
    print(line, flush=True)
    with RUN_LOG.open("a", encoding="utf-8") as file:
        file.write(line + "\n")


class PriceMonitor:
    def __init__(self, symbol: str = DEFAULT_SYMBOL, name: str = DEFAULT_NAME):
        self.symbol = symbol
        self.name = name
        self.kis = KISClient()
        self.last_alert_time = None
        self.alert_cooldown = 600  # 10분 쿨다운 (같은 알림 반복 방지)
        self.baseline_volume = None
        self.price_window: list[tuple[datetime, float]] = []  # (시각, 가격) — 최근 N분 급변동 판정용

    def check_market_hours(self) -> bool:
        """장 운영 시간인지 확인 (평일 09:00-15:30)"""
        now = datetime.now()
        if now.weekday() >= 5:  # 토요일(5), 일요일(6)
            return False
        hour = now.hour
        minute = now.minute
        if hour < 9 or (hour == 15 and minute > 30) or hour > 15:
            return False
        return True

    def update_baseline(self) -> None:
        """20일 평균 거래량 업데이트"""
        try:
            dailies = self.kis.daily_prices(self.symbol, 30)
            if len(dailies) > 1:
                rows = dailies[1:21]
                self.baseline_volume = (
                    sum(num(row.get("acml_vol")) for row in rows) / len(rows) if rows else 0
                )
                log(f"20일 평균 거래량 업데이트: {int(self.baseline_volume):,}주")
        except Exception as exc:
            log(f"평균 거래량 업데이트 실패: {exc}")

    def check_alert(self) -> None:
        """현재가/거래량 확인 후 급변동 감지"""
        try:
            quote = self.kis.quote(self.symbol)
            if not quote:
                return

            current_price = num(quote.get("stck_prpr"))
            change_pct = num(quote.get("prdy_ctrt"))
            volume = num(quote.get("acml_vol"))

            if not current_price:
                return

            # 거래량 비율 계산
            volume_ratio = 0
            if self.baseline_volume and self.baseline_volume > 0:
                volume_ratio = volume / self.baseline_volume

            # 최근 N분 변동률 계산 — 진짜 '급변동'(전일대비 누적이 아니라 짧은 시간 급변)
            now = datetime.now()
            self.price_window.append((now, current_price))
            self.price_window = [(t, p) for t, p in self.price_window
                                 if (now - t).total_seconds() <= MOVE_WINDOW_SEC]
            ref_price = self.price_window[0][1]
            window_change = ((current_price - ref_price) / ref_price * 100) if ref_price else 0.0

            # 급변동 감지 — 트리거는 '최근 N분 급변'만 (종일 반복 알림 방지)
            triggered = abs(window_change) >= RAPID_MOVE_THRESHOLD
            direction = "급등" if window_change > 0 else "급락"
            mins = MOVE_WINDOW_SEC // 60
            # 신호 등급(헌장): 거래량 동반·변동폭으로 단순변동/관심신호/강한신호 구분
            if abs(window_change) >= RAPID_MOVE_THRESHOLD + 1 and volume_ratio >= VOLUME_RATIO_THRESHOLD:
                grade, nxt = "강한 신호", "매수 제안 에이전트로 전달 권고"
            elif volume_ratio >= 1.5:
                grade, nxt = "관심 신호", "추가 확인 필요"
            else:
                grade, nxt = "단순 변동", "추격 금지 · 관망"

            # 알림 전송 (헌장 [급변동 알림] 형식 + 신호등급)
            if triggered and self._can_send_alert():
                vol_txt = f"{volume_ratio:.1f}배" if volume_ratio else "확인불가"
                message = f"""[급변동 알림]
- 종목: {self.name}({self.symbol})
- 변동률: {mins}분새 {direction} {window_change:+.2f}% (전일대비 {change_pct:+.2f}%)
- 신호 등급: {grade}
- 이유: 거래량 {vol_txt} · 현재가 {fmt_int(current_price)}원
- 다음 전달 대상: {nxt}"""

                # 사용자 지시(2026-07-02): 급변동 알림은 텔레그램 전송 안 함 — 매수 체결 메시지만 전송.
                # 감지·성장기록·콘솔로그는 유지(신호 파이프라인 영향 없음). 재활성화: SOMI_ALERT_TELEGRAM=true
                tg_on = os.getenv("SOMI_ALERT_TELEGRAM", "false").lower() in {"1", "true", "yes"}
                # 강한 신호는 문구 전달이 아니라 실제 연동 — 어드바이저가 다음 정시 슬롯(최대 15분)을
                # 기다리지 않고 즉시 매수검토를 돌도록 트리거 파일 기록(급등주 알파 감쇠 대응).
                # 모의 한정 완화(2026-07-06): '강한 신호' 외 '관심 신호'(중간등급)도
                # 어드바이저로 전달 → 체결 표본 확보. '단순 변동'은 여전히 제외. 실거래는 강한 신호만(불변).
                _paper_relay = _is_paper() and grade == "관심 신호"
                if grade == "강한 신호" or _paper_relay:
                    try:
                        import json as _json
                        TRIGGER_FILE.write_text(_json.dumps({
                            "ts": datetime.now().isoformat(timespec="seconds"),
                            "symbol": self.symbol, "name": self.name,
                            "change": f"{window_change:+.2f}%/{mins}분", "grade": grade,
                        }, ensure_ascii=False), encoding="utf-8")
                        log(f"어드바이저 트리거 기록 — {self.name} 강한 신호")
                    except Exception as exc:
                        log(f"트리거 기록 실패: {exc}")
                if (send(message) if tg_on else True):
                    self.last_alert_time = datetime.now()
                    log(f"급변동 감지{'·알림 전송' if tg_on else '(텔레그램 억제)'}: {direction} {window_change:+.2f}% [{grade}]")
                    growth.record(
                        "somi_monitor", role="실시간 급변동 감시",
                        data=f"{self.name}({self.symbol}) {window_change:+.2f}%/{mins}분",
                        judgment=f"급변동 감지 — {grade}", result="알림 전송" if tg_on else "감지 기록(전송 억제)",
                        good="단시간 델타 + 신호등급 분류",
                        bad=("강한신호 외엔 전달 보류 검토" if grade != "강한 신호" else ""),
                        scores={"fit": 22, "evidence": 19, "efficiency": 19, "risk": 17, "brevity": 9},
                    )

        except Exception as exc:
            log(f"모니터링 오류: {exc}")

    def _can_send_alert(self) -> bool:
        """쿨다운 체크 (동일 알림 반복 방지)"""
        if not self.last_alert_time:
            return True
        elapsed = (datetime.now() - self.last_alert_time).total_seconds()
        return elapsed >= self.alert_cooldown

    def run_multi() -> None:
        """여러 종목 실시간 모니터링"""
        with ProcessLock("somi_price_monitor"):
            log("소미 주가 감시 시작 (다중 종목 모드)")
            log(f"감지 기준: 등락률 ±{PRICE_CHANGE_THRESHOLD}%, 거래량 {VOLUME_RATIO_THRESHOLD}배")

            monitors = {}  # {symbol: PriceMonitor}
            kis_idx = KISClient()   # 지수 급락 감시 전용(사이클마다 재생성 방지)

            while True:
                try:
                    # 장 시간 체크
                    if not PriceMonitor("", "").check_market_hours():
                        log("장외 시간 - 대기 중...")
                        time.sleep(300)
                        continue

                    # watchlist 로드 (변경사항 반영)
                    watchlist = load_watchlist()

                    # 새로운 종목 추가
                    for symbol, name in watchlist.items():
                        if symbol not in monitors:
                            monitors[symbol] = PriceMonitor(symbol, name)
                            log(f"감시 추가: {name}({symbol})")

                    # 제거된 종목 삭제
                    removed = [s for s in monitors if s not in watchlist]
                    for symbol in removed:
                        name = monitors[symbol].name
                        del monitors[symbol]
                        log(f"감시 제거: {name}({symbol})")

                    # 평균 거래량 업데이트 (1시간마다)
                    now = datetime.now()
                    if now.minute == 0:
                        for monitor in monitors.values():
                            monitor.update_baseline()

                    # 지수 급락 실측 경보(사이드카/서킷권) — watchlist와 무관하게 항상
                    try:
                        index_crash_watch(kis_idx)
                    except Exception as exc:
                        log(f"지수 급락 감시 오류: {exc}")

                    # 각 종목 급변동 체크
                    for monitor in monitors.values():
                        monitor.check_alert()

                    # 대기
                    time.sleep(CHECK_INTERVAL)

                except KeyboardInterrupt:
                    log("모니터링 중단")
                    break
                except Exception as exc:
                    log(f"예외 발생: {exc}")
                    time.sleep(60)


def main() -> int:
    import argparse

    parser = argparse.ArgumentParser(description="소미 주식 실시간 감시 (다중 종목)")
    parser.add_argument(
        "--price-threshold",
        type=float,
        default=3.0,
        help="등락률 알림 기준 (%%)",
    )
    parser.add_argument(
        "--volume-threshold",
        type=float,
        default=2.0,
        help="거래량 배수 알림 기준",
    )
    args = parser.parse_args()

    global PRICE_CHANGE_THRESHOLD, VOLUME_RATIO_THRESHOLD
    PRICE_CHANGE_THRESHOLD = args.price_threshold
    VOLUME_RATIO_THRESHOLD = args.volume_threshold

    PriceMonitor.run_multi()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
