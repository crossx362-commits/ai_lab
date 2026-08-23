#!/bin/bash
# launchd는 터미널 PATH를 상속하지 않으므로 절대 경로를 포함한다.

export PATH="/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin:/Users/junholee/.grok/bin:/Users/junholee/.cargo/bin"

# auto: 독립 작업이면 최대 3개 병렬, single: 한 개, parallel: 가능한 만큼 병렬
export LOOP_MODE="${LOOP_MODE:-auto}"
export LOOP_PROVIDERS="${LOOP_PROVIDERS:-claude,codex,grok}"
export LOOP_MAX_PARALLEL="${LOOP_MAX_PARALLEL:-3}"

# 강한 모델 고정. Claude는 Fable 사용량 소진/이용 불가 때만 Opus 5로 전환한다.
export LOOP_CLAUDE_MODEL="${LOOP_CLAUDE_MODEL:-fable}"
export LOOP_CLAUDE_FALLBACK_MODEL="${LOOP_CLAUDE_FALLBACK_MODEL:-opus5}"
export LOOP_CODEX_MODEL="${LOOP_CODEX_MODEL:-gpt-5.6-sol}"
export LOOP_CODEX_REASONING="${LOOP_CODEX_REASONING:-xhigh}"
export LOOP_CODEX_PLANNING_REASONING="${LOOP_CODEX_PLANNING_REASONING:-medium}"
export LOOP_GROK_MODEL="${LOOP_GROK_MODEL:-grok-4.6}"

export LOOP_MAX_TURNS="${LOOP_MAX_TURNS:-30}"
export LOOP_SESSION_TIMEOUT="${LOOP_SESSION_TIMEOUT:-1800}"
export LOOP_COOLDOWN="${LOOP_COOLDOWN:-10}"
export LOOP_IDLE_WAIT="${LOOP_IDLE_WAIT:-60}"
export LOOP_MAX_LOOPS="${LOOP_MAX_LOOPS:-0}"
export LOOP_PUSH="${LOOP_PUSH:-1}"

# off가 기본. clerical은 중복 문장 정리·형식 변환에만 쓰며 게임 판단에는 쓰지 않는다.
export LOOP_OLLAMA_MODE="${LOOP_OLLAMA_MODE:-off}"
