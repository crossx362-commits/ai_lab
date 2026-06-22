#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
점수 정규화 모듈 (0~100 통일 체계)
데이브/레오 모두 동일한 0~100 점수 체계 사용
"""


def normalize_score(raw_score: float, max_raw_score: float = 20.0) -> int:
    """
    원시 점수를 0~100 범위로 정규화

    Args:
        raw_score: 원시 점수 (0~max_raw_score)
        max_raw_score: 원시 점수 최댓값 (기본 20)

    Returns:
        0~100 범위 정수
    """
    if max_raw_score <= 0:
        return 0

    # 0~100 선형 변환
    normalized = (raw_score / max_raw_score) * 100

    # 0~100 범위 클램핑
    normalized = max(0, min(100, normalized))

    return int(normalized)


def denormalize_score(normalized_score: int, max_raw_score: float = 20.0) -> float:
    """
    정규화된 점수를 원시 점수로 역변환

    Args:
        normalized_score: 정규화 점수 (0~100)
        max_raw_score: 원시 점수 최댓값 (기본 20)

    Returns:
        원시 점수 (0~max_raw_score)
    """
    return (normalized_score / 100.0) * max_raw_score


if __name__ == "__main__":
    # 테스트
    test_cases = [
        (0, 20),    # 0점 → 0
        (3, 20),    # 3점 → 15
        (10, 20),   # 10점 → 50
        (15, 20),   # 15점 → 75
        (20, 20),   # 20점 → 100
    ]

    print("=== 점수 정규화 테스트 ===")
    for raw, max_raw in test_cases:
        normalized = normalize_score(raw, max_raw)
        print(f"원시 {raw:2d}/{max_raw} → 정규화 {normalized:3d}/100")
