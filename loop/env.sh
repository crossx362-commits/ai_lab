#!/bin/bash
# launchd는 터미널 PATH를 상속하지 않으므로 절대 경로를 포함한다.

export PATH="/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin:/Users/junholee/.grok/bin:/Users/junholee/.cargo/bin:/Users/junholee/.opencode/bin"

# session: 매 바퀴 새 세션 (기본, 오너 명세). coordinator: agent_runner 병렬.
export LOOP_MODE="${LOOP_MODE:-session}"
# 오너 2026-08-24: 실행기는 그록. (INBOX 「그록만」 · 코덱스 안 씀)
# 비워두면 loop/agent 파일이 실행기를 정한다(README 규칙). 기본값을 넣으면 파일 지정이 죽는다.
export LOOP_AGENT="${LOOP_AGENT:-}"
export LOOP_PROVIDERS="${LOOP_PROVIDERS:-grok}"
export LOOP_MAX_PARALLEL="${LOOP_MAX_PARALLEL:-1}"

# 강한 모델 고정. Claude는 Fable 사용량 소진/이용 불가 때만 Opus 5로 전환한다.
export LOOP_CLAUDE_MODEL="${LOOP_CLAUDE_MODEL:-fable}"
export LOOP_CLAUDE_FALLBACK_MODEL="${LOOP_CLAUDE_FALLBACK_MODEL:-opus5}"
export LOOP_CODEX_MODEL="${LOOP_CODEX_MODEL:-gpt-5.6-sol}"
export LOOP_CODEX_REASONING="${LOOP_CODEX_REASONING:-xhigh}"
export LOOP_CODEX_PLANNING_REASONING="${LOOP_CODEX_PLANNING_REASONING:-medium}"
export LOOP_GROK_MODEL="${LOOP_GROK_MODEL:-grok-4.6}"
# opencode 모델. 2026-08-27 01:10 실측: x-preview-f-free만 서버 오류(UnknownError)로 죽어 있고
# 다른 모델은 정상이었다 — 3시간 동안 바퀴가 한 번도 못 돈 원인. 살아 있고 무료이며 긴 컨텍스트
# 과제도 27초에 정답을 낸 mimo-v2.5-free를 1순위로 바꾼다.
export LOOP_OPENCODE_MODEL="${LOOP_OPENCODE_MODEL:-opencode/mimo-v2.5-free}"
# GameFullCheck 전수 — N바퀴마다 1회 (회의 20260827-081437 채택 #3). Unity 없으면 스킵.
export LOOP_FULLCHECK_EVERY="${LOOP_FULLCHECK_EVERY:-4}"

# 선택한 AI가 소진되면 바꾸지 않고 무료 사용량 조회만 이 간격으로 재확인한다.
export PROVIDER_RETRY_SECONDS="${PROVIDER_RETRY_SECONDS:-1800}"
export LOOP_HEARTBEAT_SECONDS="${LOOP_HEARTBEAT_SECONDS:-30}"
export LOOP_RECOVERY_RETRY_SECONDS="${LOOP_RECOVERY_RETRY_SECONDS:-900}"
export LOOP_RECOVERY_PROVIDERS="${LOOP_RECOVERY_PROVIDERS:-codex,claude,grok,opencode}"

export LOOP_MAX_TURNS="${LOOP_MAX_TURNS:-60}"
export LOOP_SESSION_TIMEOUT="${LOOP_SESSION_TIMEOUT:-1800}"
export LOOP_COOLDOWN="${LOOP_COOLDOWN:-10}"
export LOOP_IDLE_WAIT="${LOOP_IDLE_WAIT:-60}"
export LOOP_MAX_LOOPS="${LOOP_MAX_LOOPS:-0}"
export LOOP_PUSH="${LOOP_PUSH:-1}"

# off가 기본. clerical은 중복 문장 정리·형식 변환에만 쓰며 게임 판단에는 쓰지 않는다.
export LOOP_OLLAMA_MODE="${LOOP_OLLAMA_MODE:-off}"
