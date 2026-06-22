#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
LLM 판단 캐시 모듈
점수 변화 10점 미만 시 재호출 금지, 쿨다운 관리
"""
import json
import time
from pathlib import Path
from typing import Optional


class LLMCache:
    """LLM 판단 캐시 및 재호출 조건 관리"""

    def __init__(
        self,
        agent: str,
        workspace_root: str,
        cooldown_seconds: int = 900,
        score_delta_trigger: int = 10,
    ):
        """
        Args:
            agent: 에이전트 이름 (dave, leo)
            workspace_root: 작업 루트 경로
            cooldown_seconds: LLM 재호출 쿨다운 (초)
            score_delta_trigger: 재호출 허용 최소 점수 변화
        """
        self.agent = agent
        self.workspace_root = Path(workspace_root)
        self.cooldown_seconds = cooldown_seconds
        self.score_delta_trigger = score_delta_trigger

        # 캐시 파일 경로
        self.cache_dir = self.workspace_root / "output" / "trading_logs"
        self.cache_dir.mkdir(parents=True, exist_ok=True)
        self.cache_file = self.cache_dir / f"{agent}_llm_cache.json"

        # 캐시 로드
        self.cache = self._load_cache()

    def _load_cache(self) -> dict:
        """캐시 파일 로드"""
        if self.cache_file.exists():
            try:
                with open(self.cache_file, "r", encoding="utf-8") as f:
                    return json.load(f)
            except Exception:
                pass
        return {}

    def _save_cache(self):
        """캐시 파일 저장"""
        try:
            with open(self.cache_file, "w", encoding="utf-8") as f:
                json.dump(self.cache, f, ensure_ascii=False, indent=2)
        except Exception as e:
            print(f"[LLMCache] 캐시 저장 실패: {e}")

    def should_call_llm(
        self,
        ticker: str,
        current_score: int,
        volume_spike: float = 1.0,
    ) -> tuple[bool, Optional[str]]:
        """
        LLM 호출 필요 여부 판단

        Args:
            ticker: 티커
            current_score: 현재 점수 (0~100)
            volume_spike: 거래량 급증 배수

        Returns:
            (호출 필요 여부, 캐시 사유)
        """
        now = time.time()

        # 캐시 없음 → 호출
        if ticker not in self.cache:
            return True, None

        cached = self.cache[ticker]
        last_call_time = cached.get("timestamp", 0)
        last_score = cached.get("score", 0)
        last_decision = cached.get("decision", "")

        # 1. 쿨다운 체크
        elapsed = now - last_call_time
        if elapsed < self.cooldown_seconds:
            remaining = self.cooldown_seconds - elapsed
            return False, f"쿨다운 {remaining:.0f}초 남음"

        # 2. 점수 변화 체크
        score_delta = abs(current_score - last_score)
        if score_delta < self.score_delta_trigger:
            # 예외: 거래량 급증 (1.5배 이상)
            if volume_spike < 1.5:
                return False, f"점수 변화 {score_delta}점 < {self.score_delta_trigger}점"

        # 3. HOLD 캐시 재사용
        if last_decision == "HOLD" and score_delta < self.score_delta_trigger:
            return False, "HOLD 캐시 재사용"

        return True, None

    def cache_decision(
        self,
        ticker: str,
        score: int,
        decision: str,
        percentage: int = 0,
        reason: str = "",
    ):
        """
        LLM 판단 결과 캐싱

        Args:
            ticker: 티커
            score: 점수 (0~100)
            decision: BUY, SELL, HOLD
            percentage: 포지션 비율
            reason: 판단 이유
        """
        self.cache[ticker] = {
            "timestamp": time.time(),
            "score": score,
            "decision": decision,
            "percentage": percentage,
            "reason": reason,
        }
        self._save_cache()

    def get_cached_decision(self, ticker: str) -> Optional[dict]:
        """캐시된 판단 반환"""
        return self.cache.get(ticker)

    def invalidate_cache(self, ticker: str):
        """특정 티커 캐시 무효화"""
        if ticker in self.cache:
            del self.cache[ticker]
            self._save_cache()

    def clear_all(self):
        """전체 캐시 초기화"""
        self.cache = {}
        self._save_cache()


if __name__ == "__main__":
    # 테스트
    import tempfile
    temp_dir = tempfile.mkdtemp()

    cache = LLMCache("test", temp_dir, cooldown_seconds=300, score_delta_trigger=10)

    print("=== LLM 캐시 테스트 ===")

    # 첫 호출 → True
    should_call, reason = cache.should_call_llm("KRW-BTC", current_score=50)
    print(f"첫 호출: {should_call}, 사유: {reason}")

    # 캐싱
    cache.cache_decision("KRW-BTC", score=50, decision="BUY", percentage=10, reason="테스트")

    # 쿨다운 중 → False
    should_call, reason = cache.should_call_llm("KRW-BTC", current_score=52)
    print(f"쿨다운 중: {should_call}, 사유: {reason}")

    # 점수 변화 작음 → False
    time.sleep(1)
    cache.cache["KRW-BTC"]["timestamp"] = time.time() - 400  # 쿨다운 해제
    should_call, reason = cache.should_call_llm("KRW-BTC", current_score=55)
    print(f"점수 변화 5점: {should_call}, 사유: {reason}")

    # 점수 변화 큼 → True
    should_call, reason = cache.should_call_llm("KRW-BTC", current_score=65)
    print(f"점수 변화 15점: {should_call}, 사유: {reason}")

    # 거래량 급증 → True
    should_call, reason = cache.should_call_llm("KRW-BTC", current_score=55, volume_spike=2.0)
    print(f"거래량 2배 급증: {should_call}, 사유: {reason}")
