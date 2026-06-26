#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Somi price monitor — 우리기술 주가/거래량 급변동 실시간 감시"""

from __future__ import annotations

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
from somi_kis_reporter import KISClient, num, fmt_int, fmt_pct
from watchlist_manager import load_watchlist


load_env(str(PROJECT_ROOT))

DEFAULT_SYMBOL = "032820"
DEFAULT_NAME = "우리기술"
CHECK_INTERVAL = 60  # 1분마다 확인
RUN_LOG = PROJECT_ROOT / "output" / "bot_logs" / "somi_price_monitor.log"

# Global thresholds (can be modified by argparse)
PRICE_CHANGE_THRESHOLD = 3.0  # 등락률 3% 이상
VOLUME_RATIO_THRESHOLD = 2.0  # 거래량 평균 대비 2배 이상


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

            # 급변동 감지
            alerts = []

            if abs(change_pct) >= PRICE_CHANGE_THRESHOLD:
                direction = "급등" if change_pct > 0 else "급락"
                alerts.append(f"🚨 {direction} {change_pct:+.2f}%")

            if volume_ratio >= VOLUME_RATIO_THRESHOLD:
                alerts.append(f"📊 거래량 급증 {volume_ratio:.1f}배 (평균 대비)")

            # 알림 전송
            if alerts and self._can_send_alert():
                message = f"""⚠️ {self.name}({self.symbol}) 급변동 감지

{chr(10).join(alerts)}

현재가: {fmt_int(current_price)}원
등락률: {fmt_pct(change_pct)}
거래량: {fmt_int(volume)}주
시간: {datetime.now().strftime('%H:%M:%S')}"""

                if send(message):
                    self.last_alert_time = datetime.now()
                    log(f"알림 전송: {', '.join(alerts)}")

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
        help="등락률 알림 기준 (%)",
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
