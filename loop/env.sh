#!/bin/bash
# launchd는 터미널 PATH를 상속하지 않으므로 절대 경로를 포함한다.

export PATH="/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin:/Users/junholee/.grok/bin:/Users/junholee/.cargo/bin:/Users/junholee/.opencode/bin"

# session: 매 바퀴 새 세션 (기본, 오너 명세). coordinator: agent_runner 병렬.
export LOOP_MODE="${LOOP_MODE:-session}"
# 오너 2026-08-24: 실행기는 그록. (INBOX 「그록만」 · 코덱스 안 씀)
# 비워두면 loop/agent 파일이 실행기를 정한다(README 규칙). 기본값을 넣으면 파일 지정이 죽는다.
export LOOP_AGENT="${LOOP_AGENT:-}"
export LOOP_PROVIDERS="${LOOP_PROVIDERS:-grok}"
# 실행기 체인(오너 지시 2026-08-27): claude 단독. 「클로드 소진 시 opencode 등으로 넘어가지
# 말고, 클로드 할당량이 회복될 때까지 대기」. 체인이 1개면 loop.sh가 소진 시 자멸하지 않고
# PROVIDER_RETRY_SECONDS마다 회복을 무한 재확인한다(CHAIN_COUNT<=1 분기).
# 다시 다중 공급자로 되돌리려면 여기에 쉼표로 실행기를 추가하면 옛 링 전환이 자동 복원된다.
export LOOP_PROVIDERS_CHAIN="${LOOP_PROVIDERS_CHAIN:-claude}"
export LOOP_MAX_PARALLEL="${LOOP_MAX_PARALLEL:-3}"

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
# 그 모델이 서버 장애로 죽으면 이 순서로 갈아탄다(모델 단위 페일오버). 공급자는 그대로 두고
# 모델만 바꾸므로 대체 공급자가 없는 상황에서도 바퀴가 계속 돈다.
export LOOP_OPENCODE_MODELS="${LOOP_OPENCODE_MODELS:-opencode/mimo-v2.5-free,opencode/hy3-free,opencode/nemotron-3.5-lightning-free,opencode/x-preview-f-free}"

# 자가학습 회의 — N바퀴마다 역할(planner·builder·tester) 병렬 회의 소집 (오너 2026-08-23)
export LOOP_COUNCIL_EVERY="${LOOP_COUNCIL_EVERY:-4}"
# GameFullCheck 전수 — N바퀴마다 1회 (회의 20260827-081437 채택 #3). Unity 없으면 스킵.
export LOOP_FULLCHECK_EVERY="${LOOP_FULLCHECK_EVERY:-4}"

# Claude↔Grok 사용량 자동전환 (오너 2026-08-25). 기본은 Claude로 시작, 소진되면 Grok, Grok도
# 소진되면 다시 Claude — pick_agent()가 claude/grok을 고를 때만 적용된다(codex/opencode로
# 수동 지정하면 이 전환은 관여하지 않는다). 소진 판정은 board.py의 공식 사용량 API
# (claude_usage/grok_usage)가 1순위, 랩 로그 문구 매치는 사후 폴백일 뿐이다.
export LOOP_AUTO_SWITCH="${LOOP_AUTO_SWITCH:-1}"
# 둘 다 소진이면 이만큼(초) 기다렸다 재확인한다 — 빠른 왕복 금지.
export PROVIDER_RETRY_SECONDS="${PROVIDER_RETRY_SECONDS:-1800}"
# 위 대기를 몇 번 반복해도 계속 둘 다 소진이면 STOP을 찍는 안전판(사용량 확인 자체가
# 고장났을 가능성 포함 — 무한 대기 방지).
export MAX_PROVIDER_FAILURES="${MAX_PROVIDER_FAILURES:-6}"
# 전환 상태는 loop/provider.state(git 미추적)에만 남는다 — 대화가 아니라 파일이 기억한다.

export LOOP_MAX_TURNS="${LOOP_MAX_TURNS:-60}"
export LOOP_SESSION_TIMEOUT="${LOOP_SESSION_TIMEOUT:-1800}"
export LOOP_COOLDOWN="${LOOP_COOLDOWN:-10}"
export LOOP_IDLE_WAIT="${LOOP_IDLE_WAIT:-60}"
export LOOP_MAX_LOOPS="${LOOP_MAX_LOOPS:-0}"
export LOOP_PUSH="${LOOP_PUSH:-1}"

# off가 기본. clerical은 중복 문장 정리·형식 변환에만 쓰며 게임 판단에는 쓰지 않는다.
export LOOP_OLLAMA_MODE="${LOOP_OLLAMA_MODE:-off}"
