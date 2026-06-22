#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
위험 관리 모듈
일일 손실, 연속 손실, 포지션 수, 시간당 거래 횟수 제한
"""
import os
import json
import time
from pathlib import Path
from typing import Optional


class RiskGuard:
    """트레이더 위험 한도 관리"""

    def __init__(
        self,
        agent: str,
        workspace_root: str,
        daily_loss_limit_pct: float = -3.0,
        consecutive_loss_limit: int = 3,
        max_positions: int = 3,
        max_trades_per_hour: int = 5,
    ):
        """
        Args:
            agent: 에이전트 이름 (dave, leo)
            workspace_root: 작업 루트 경로
            daily_loss_limit_pct: 일일 손실 한도 (%)
            consecutive_loss_limit: 연속 손실 제한 (회)
            max_positions: 최대 동시 보유 수
            max_trades_per_hour: 시간당 최대 거래 횟수
        """
        self.agent = agent
        self.workspace_root = Path(workspace_root)
        self.daily_loss_limit_pct = daily_loss_limit_pct
        self.consecutive_loss_limit = consecutive_loss_limit
        self.max_positions = max_positions
        self.max_trades_per_hour = max_trades_per_hour

        # 상태 파일 경로
        self.state_dir = self.workspace_root / "output" / "trading_logs"
        self.state_dir.mkdir(parents=True, exist_ok=True)
        self.state_file = self.state_dir / f"{agent}_risk_state.json"

        # 상태 로드
        self.state = self._load_state()

    def _load_state(self) -> dict:
        """상태 파일 로드"""
        if self.state_file.exists():
            try:
                with open(self.state_file, "r", encoding="utf-8") as f:
                    return json.load(f)
            except Exception:
                pass

        # 기본 상태
        return {
            "daily_loss_pct": 0.0,
            "consecutive_losses": 0,
            "current_positions": 0,
            "trade_history": [],  # [(timestamp, ticker, action), ...]
            "last_reset_date": time.strftime("%Y-%m-%d"),
        }

    def _save_state(self):
        """상태 파일 저장"""
        try:
            with open(self.state_file, "w", encoding="utf-8") as f:
                json.dump(self.state, f, ensure_ascii=False, indent=2)
        except Exception as e:
            print(f"[RiskGuard] 상태 저장 실패: {e}")

    def _check_daily_reset(self):
        """일일 리셋 체크 (자정 넘어가면 초기화)"""
        today = time.strftime("%Y-%m-%d")
        if self.state["last_reset_date"] != today:
            self.state["daily_loss_pct"] = 0.0
            self.state["trade_history"] = []
            self.state["last_reset_date"] = today
            self._save_state()

    def can_trade(self) -> tuple[bool, Optional[str]]:
        """
        거래 가능 여부 체크

        Returns:
            (가능 여부, 불가 사유)
        """
        self._check_daily_reset()

        # 1. 일일 손실 한도
        if self.state["daily_loss_pct"] <= self.daily_loss_limit_pct:
            return False, f"일일 손실 한도 도달 ({self.state['daily_loss_pct']:.2f}% ≤ {self.daily_loss_limit_pct}%)"

        # 2. 연속 손실 제한
        if self.state["consecutive_losses"] >= self.consecutive_loss_limit:
            return False, f"연속 손절 {self.state['consecutive_losses']}회 - 30분 휴식 필요"

        # 3. 포지션 수 제한
        if self.state["current_positions"] >= self.max_positions:
            return False, f"최대 보유 {self.max_positions}개 도달 - 신규 진입 중단"

        # 4. 시간당 거래 제한
        now = time.time()
        recent_trades = [
            t for t in self.state["trade_history"]
            if now - t[0] < 3600  # 1시간 이내
        ]
        if len(recent_trades) >= self.max_trades_per_hour:
            return False, f"시간당 최대 {self.max_trades_per_hour}회 거래 제한"

        return True, None

    def record_trade(self, ticker: str, action: str, profit_pct: float = 0.0):
        """
        거래 기록

        Args:
            ticker: 티커
            action: BUY, SELL
            profit_pct: 수익률 (SELL 시)
        """
        now = time.time()
        self.state["trade_history"].append((now, ticker, action))

        if action == "BUY":
            self.state["current_positions"] += 1

        elif action == "SELL":
            self.state["current_positions"] = max(0, self.state["current_positions"] - 1)
            self.state["daily_loss_pct"] += profit_pct

            # 손실 처리
            if profit_pct < 0:
                self.state["consecutive_losses"] += 1
            else:
                # 익절 시 연속 손실 리셋
                self.state["consecutive_losses"] = 0

        self._save_state()

    def get_state(self) -> dict:
        """현재 상태 반환"""
        self._check_daily_reset()
        return self.state.copy()

    def reset_consecutive_losses(self):
        """연속 손실 리셋 (익절 시)"""
        self.state["consecutive_losses"] = 0
        self._save_state()


if __name__ == "__main__":
    # 테스트
    import tempfile
    temp_dir = tempfile.mkdtemp()

    guard = RiskGuard("test", temp_dir, daily_loss_limit_pct=-3.0, consecutive_loss_limit=3, max_positions=3)

    print("=== 위험 관리 테스트 ===")
    print(f"상태: {guard.get_state()}")

    # 거래 가능 체크
    can, reason = guard.can_trade()
    print(f"거래 가능: {can}, 사유: {reason}")

    # 매수 기록
    guard.record_trade("KRW-BTC", "BUY")
    print(f"매수 후 포지션: {guard.state['current_positions']}")

    # 손절 기록
    guard.record_trade("KRW-BTC", "SELL", profit_pct=-1.5)
    print(f"손절 후 일일손실: {guard.state['daily_loss_pct']:.2f}%")
    print(f"연속 손실: {guard.state['consecutive_losses']}")
