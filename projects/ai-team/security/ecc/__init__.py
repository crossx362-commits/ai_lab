"""
ECC (Everything Claude Code) Security Integration for ai_lab
=============================================================

AI 에이전트 보안 프레임워크 - 프롬프트 인젝션, 공급망 공격, 시크릿 노출 방지

주요 기능:
- AgentShield: 프롬프트 인젝션 스캐닝
- Security Hooks: 자동 보안 모니터링
- Dashboard: 보안 이벤트 시각화
- 시크릿 스캐닝 및 암호화

경수(Kyungsu) 수사관과 로율(Royul) 변호사 에이전트와 통합
"""

__version__ = "2.0.0-ailab"
__author__ = "ai_lab team"

from pathlib import Path

ECC_ROOT = Path(__file__).parent
AGENTSHIELD_PATH = ECC_ROOT / "agentshield"
SECURITY_HOOKS_PATH = ECC_ROOT / "security_hooks"
DASHBOARD_PATH = ECC_ROOT / "ecc_dashboard.py"
