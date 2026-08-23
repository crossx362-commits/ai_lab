#!/bin/bash
# ==============================================================================
# 자율 개발 루프 (Autonomous Development Loop)
#
# 주요 특징:
#   - 매 바퀴마다 대화 맥락이 없는 완전한 "새 세션"을 시작합니다.
#   - loop/PROMPT.md 지시서를 읽고 작업을 수행합니다.
#   - 모든 설정은 loop/env.sh 에서 관리합니다.
#   - loop/STOP 파일이 감지되면 즉시 또는 현재 바퀴 완료 후 안전하게 종료됩니다.
#   - 매 바퀴의 상세 로그는 logs/ 디렉토리에 타임스탬프와 함께 보관됩니다.
# ==============================================================================

set -uo pipefail

# 프로젝트 루트 디렉토리로 이동
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$ROOT"

# 환경설정 로드
ENV_FILE="$ROOT/loop/env.sh"
if [ -f "$ENV_FILE" ]; then
  # shellcheck source=/dev/null
  source "$ENV_FILE"
fi

# 기본값 보정
AGENT="${LOOP_AGENT:-codex}"
MODEL="${LOOP_MODEL:-default}"
MAX_TURNS="${LOOP_MAX_TURNS:-50}"
COOLDOWN="${LOOP_COOLDOWN:-10}"
MAX_LOOPS="${LOOP_MAX_LOOPS:-0}" # 0이면 무한 반복

STOP_FILE="$ROOT/loop/STOP"
PROMPT_FILE="$ROOT/loop/PROMPT.md"
LOG_DIR="$ROOT/logs"
MAIN_LOG="$LOG_DIR/loop_main.log"

mkdir -p "$LOG_DIR"

# 실행기 바이너리 탐색 함수
find_binary() {
  local name="$1"
  if command -v "$name" >/dev/null 2>&1; then
    command -v "$name"
    return 0
  fi
  for p in "/opt/homebrew/bin/$name" "/usr/local/bin/$name" "$HOME/.grok/bin/$name" "$HOME/.cargo/bin/$name"; do
    if [ -x "$p" ]; then
      printf '%s\n' "$p"
      return 0
    fi
  done
  return 1
}

LOOP_DESC="무한 반복"
if [ "${MAX_LOOPS}" -gt 0 ] 2>/dev/null; then
  LOOP_DESC="${MAX_LOOPS}회"
fi

echo "================================================================="
echo "🚀 자율 개발 루프 시작"
echo "   - 루트 경로: $ROOT"
echo "   - 에이전트 / 모델: $AGENT ($MODEL)"
echo "   - 최대 턴 수: $MAX_TURNS"
echo "   - 쿨다운 대기: ${COOLDOWN}초"
echo "   - 최대 바퀴 수: $LOOP_DESC"
echo "   - 중단 방법: touch loop/STOP"
echo "================================================================="

ITER=0

while true; do
  # [1] STOP 파일 사전 체크
  if [ -f "$STOP_FILE" ]; then
    echo "⏹ [STOP 감지] loop/STOP 파일이 확인되어 루프를 안전하게 정지합니다."
    echo "   재개하려면: rm loop/STOP"
    exit 0
  fi

  # [2] 최대 바퀴 수 체크
  if [ "${MAX_LOOPS}" -gt 0 ] 2>/dev/null && [ "$ITER" -ge "$MAX_LOOPS" ]; then
    echo "🏁 [완료] 지정된 최대 바퀴 수(${MAX_LOOPS}회)를 모두 완료하여 루프를 마칩니다."
    exit 0
  fi

  ITER=$((ITER + 1))
  TS=$(date +%Y%m%d_%H%M%S)
  ITER_LOG="$LOG_DIR/loop_${TS}_iter${ITER}.log"

  echo ""
  echo "─────────────────────────────────────────────────────────────────"
  echo "▶ 바퀴 #${ITER} 시작 ($(date '+%Y-%m-%d %H:%M:%S'))"
  echo "   로그 파일: $ITER_LOG"
  echo "─────────────────────────────────────────────────────────────────"

  # PROMPT.md 존재 확인
  if [ ! -f "$PROMPT_FILE" ]; then
    echo "❌ 오류: 프롬프트 파일 ($PROMPT_FILE)을 찾을 수 없습니다." | tee -a "$ITER_LOG"
    exit 1
  fi

  RESULT=0

  # [3] 에이전트별 새 헤드리스 세션 실행 (대화 맥락 이어붙이지 않음)
  if [ "$AGENT" = "codex" ]; then
    CODEX_BIN=$(find_binary codex) || { echo "❌ codex 실행기를 찾을 수 없습니다." | tee -a "$ITER_LOG"; RESULT=127; }
    if [ "$RESULT" -eq 0 ]; then
      cat "$PROMPT_FILE" | "$CODEX_BIN" exec --ephemeral --sandbox danger-full-access \
        --cd "$ROOT" --color never - > >(tee "$ITER_LOG" >> "$MAIN_LOG") 2>&1
      RESULT=$?
    fi
  elif [ "$AGENT" = "grok" ]; then
    GROK_BIN=$(find_binary grok) || { echo "❌ grok 실행기를 찾을 수 없습니다." | tee -a "$ITER_LOG"; RESULT=127; }
    if [ "$RESULT" -eq 0 ]; then
      "$GROK_BIN" --prompt-file "$PROMPT_FILE" \
        --cwd "$ROOT" \
        --always-approve \
        --permission-mode bypassPermissions \
        --output-format plain \
        --no-plan \
        --max-turns "$MAX_TURNS" > >(tee "$ITER_LOG" >> "$MAIN_LOG") 2>&1
      RESULT=$?
    fi
  else
    # 기본: Claude CLI 헤드리스 실행
    CLAUDE_BIN=$(find_binary claude) || { echo "❌ claude 실행기를 찾을 수 없습니다." | tee -a "$ITER_LOG"; RESULT=127; }
    if [ "$RESULT" -eq 0 ]; then
      cat "$PROMPT_FILE" | "$CLAUDE_BIN" -p --permission-mode acceptEdits \
        --allowedTools \
          "Bash(*)" \
          "GlobTool(*)" \
          "GrepTool(*)" \
          "ReadFileTool(*)" \
          "EditFileTool(*)" \
          "WriteFileTool(*)" \
        > >(tee "$ITER_LOG" >> "$MAIN_LOG") 2>&1
      RESULT=$?
    fi
  fi

  # [4] 바퀴 실행 결과 판정 및 출력
  if [ "$RESULT" -eq 0 ]; then
    echo "✅ 바퀴 #${ITER} 성공적으로 완료되었습니다."
  else
    echo "⚠️ 바퀴 #${ITER} 실행 완료 (종료 코드: $RESULT)"
  fi

  # [5] 바퀴 종료 후 STOP 파일 사후 체크
  if [ -f "$STOP_FILE" ]; then
    echo "⏹ [STOP 감지] loop/STOP 파일이 확인되어 다음 바퀴를 진행하지 않고 정지합니다."
    exit 0
  fi

  # [6] 최대 바퀴 수 재확인
  if [ "${MAX_LOOPS}" -gt 0 ] 2>/dev/null && [ "$ITER" -ge "$MAX_LOOPS" ]; then
    echo "🏁 [완료] 지정된 최대 바퀴 수(${MAX_LOOPS}회)를 완료했습니다."
    exit 0
  fi

  # [7] 쿨다운 대기
  if [ "$COOLDOWN" -gt 0 ]; then
    echo "⏳ 다음 바퀴까지 ${COOLDOWN}초 대기 중... (중단: touch loop/STOP)"
    sleep "$COOLDOWN"
  fi
done
