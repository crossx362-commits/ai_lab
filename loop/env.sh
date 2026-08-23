#!/bin/bash
# loop/env.sh — 자율 개발 루프 환경 설정
#
# 이 파일은 loop.sh 가 실행될 때 자동으로 로드됩니다.
# 환경변수를 통해 외부에서 오버라이드할 수도 있습니다.

# [1] 실행기 및 모델 설정
# 옵션: claude, codex, grok
export LOOP_AGENT="${LOOP_AGENT:-claude}"
export LOOP_MODEL="${LOOP_MODEL:-claude-3-5-sonnet-20241022}"

# [2] 한 바퀴 최대 턴 수 (Max Turns per iteration)
export LOOP_MAX_TURNS="${LOOP_MAX_TURNS:-50}"

# [3] 바퀴 사이 대기 시간 (초 단위)
export LOOP_COOLDOWN="${LOOP_COOLDOWN:-10}"

# [4] 최대 바퀴 수 (0 또는 빈 값: 무한 반복, 1 이상의 정수: 해당 횟수 실행 후 종료)
export LOOP_MAX_LOOPS="${LOOP_MAX_LOOPS:-0}"

# [5] 환경변수 PATH (macOS / Linux 자동 실행 시 누락 방지)
export PATH="/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin:$HOME/.grok/bin:$HOME/.cargo/bin:$PATH"
