#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""예원 — 펫나 일일 상품성·경쟁력 강화 아이디어 회의 (오너 지시 2026-08-04).

목표: 매일 정기로 전 에이전트(봄이·수리·테오·백호·미오·나무)를 소집해 시장 경쟁력을
높일 신규 기능·개선 아이디어를 발굴한다. 결론은 petnna_council.py의 표준 경로대로
액션아이템으로 공유 백로그에 적재되고, 수리가 이어서 구현한다.

petnna_council.convene()을 고정 안건으로 **직접 함수 호출**한다(CLI가 아님) — schedules.json
을 launchd로 변환하는 schedule_sync._program_args()는 command 문자열을 quote 없이 공백
split하므로, 여러 단어짜리 --topic 값을 그 문자열에 직접 넣으면 argv가 단어 단위로 깨진다.
그래서 이 전용 스크립트가 안건 텍스트를 코드 안에 고정해두고 인자 없이 실행된다.

동일 안건은 24시간 쿨다운(petnna_council.COOLDOWN_H)이 있어 정상적인 매일 1회 실행에는
걸리지 않는다 — 안건 텍스트를 매일 다르게 만들 필요가 없다(오히려 고정해야 쿨다운 키가
일관되게 맞물려 같은 날 중복 실행을 자연히 막아준다).
"""
import os
import sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from petnna_council import convene  # noqa: E402 (모듈 임포트 시점에 load_env()도 같이 실행됨)

TOPIC = "펫나 상품성·경쟁력 강화 아이디어 (일일 정기 회의)"
CONTEXT = (
    "목표(오너 지시 2026-08-04): 시장에서 펫나의 상품성을 높이고 경쟁력을 강화할 "
    "신규 기능·개선 아이디어를 발굴한다. 기존 백로그(대기·보류·완료 포함)와 최근 "
    "QA·디자인·기획 리포트를 먼저 참고해 이미 제안됐거나 진행 중인 것과 중복되지 "
    "않는 아이디어를 우선하라. 실행 가능성(현재 스택·인력 범위 안)을 반드시 함께 판단하라."
)

if __name__ == "__main__":
    convene(TOPIC, CONTEXT, "P2")
