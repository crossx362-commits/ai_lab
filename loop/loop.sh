#!/bin/bash
# 매 바퀴마다 헤드리스 세션을 "새로" 연다. 대화를 이어 붙이지 않는다.
#
# 운영 규칙 (오너 지시, PROMPT.md와 함께 매 바퀴 적용):
# - 상시 폴리싱: 코드 구멍 다음 이터레이션에는 UI·아트 폴리싱을 섞는다. 영지 화면·EstateYard는
#   docs/GAME_SPEC_ESTATE_BUILD.md대로. 할로우 나이트 화풍 강제는 취소.
# - 중복 리소스 재생성 금지 — 이미 있는 에셋은 다시 뽑지 않는다.

set -uo pipefail

DEPLOY_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# 자가검사 모드는 저장소 인자를 먹지 않는다 — 판정 함수만 쓰고 끝낸다(아래 후크).
SELFTEST_INFRA=""
if [ "${1:-}" = "--self-test-infra" ]; then SELFTEST_INFRA="${2:-}"; set --; fi
TARGET_REPO="${1:-$(cd "$DEPLOY_ROOT/.." && pwd)}"
TARGET_REPO="$(cd "$TARGET_REPO" && pwd)"

if [ -f "$DEPLOY_ROOT/env.sh" ]; then
  # shellcheck source=/dev/null
  source "$DEPLOY_ROOT/env.sh"
fi

MAX_LOOPS="${LOOP_MAX_LOOPS:-0}"
COOLDOWN="${LOOP_COOLDOWN:-10}"
MAX_TURNS="${LOOP_MAX_TURNS:-30}"
MAX_FAILS="${LOOP_MAX_FAILS:-3}"
PROMPT_FILE="$DEPLOY_ROOT/PROMPT.md"
STOP_FILE="$TARGET_REPO/loop/STOP"
AGENT_FILE="$TARGET_REPO/loop/agent"
STATUS_FILE="$TARGET_REPO/docs/STATUS.md"
MAIN_LOG="$TARGET_REPO/logs/loop_main.log"
GROK_MODEL="${LOOP_GROK_MODEL:-grok-4.6}"
OPENCODE_MODEL="${LOOP_OPENCODE_MODEL:-opencode/mimo-v2.5-free}"
# 실행기 체인(오너 지시 2026-08-25): 우선순위 없음 — 현재 돌리는 실행기를 소진될 때까지 쓰고,
# 소진되면 체인의 다음(링 회전)으로 넘어간다. 핀(codex/opencode 지정)은 전환 관여 없음.
PROVIDERS_CHAIN="${LOOP_PROVIDERS_CHAIN:-claude}"
# 체인에 실행기가 몇 개인지(빈 항목 제외). 1개면 「그 하나가 회복될 때까지 대기」 모드 —
# 소진돼도 다른 공급자로 넘어가지 않고 자멸하지도 않는다(오너 지시 2026-08-27:
# "클로드 소진 시 opencode 가지 말고 클로드 할당량 회복될 때까지 대기").
CHAIN_COUNT="$(printf '%s' "$PROVIDERS_CHAIN" | awk -F, '{c=0; for(i=1;i<=NF;i++){gsub(/[[:space:]]/,"",$i); if($i!="")c++} print c}')"
COUNCIL_EVERY="${LOOP_COUNCIL_EVERY:-4}"

# Claude↔Grok 사용량 자동전환 (오너 지시 2026-08-25). claude로 시작, 소진되면 grok, grok도
# 소진되면 다시 claude. 코드 오류·테스트 실패로는 전환하지 않는다 — 소진 판정은 오직
# board.py의 공식 사용량 API(claude_usage/grok_usage)와 랩 로그의 사후 폴백뿐이다.
AUTO_SWITCH="${LOOP_AUTO_SWITCH:-1}"
PROVIDER_RETRY_SECONDS="${PROVIDER_RETRY_SECONDS:-1800}"
MAX_PROVIDER_FAILURES="${MAX_PROVIDER_FAILURES:-6}"
PROVIDER_STATE_FILE="$TARGET_REPO/loop/provider.state"
BOARD_PY="$DEPLOY_ROOT/board.py"
EXHAUST_PATTERN="${LOOP_EXHAUST_PATTERN:-usage limit|quota exceeded|rate limit exceeded|out of credits|사용량.*(소진|초과)|로그인.*필요|please (log|sign) in|authentication required}"
# 공급자 인프라 장애 — 「인프라 실패 ≠ 이슈 실패」(CLAUDE.md 가드레일). 이런 바퀴는 우리 코드가
# 틀린 게 아니라 상대 서버가 죽은 것이므로 STATUS 미갱신으로 세면 안 된다. 2026-08-26 22:23·22:38
# 두 바퀴가 이 오류로 죽어 「미갱신 3회」에 걸려 루프 전체가 정상 종료했다.
INFRA_PATTERN="${LOOP_INFRA_PATTERN:-Endpoint is unavailable|Unexpected server error|UnknownError|Upstream request failed|502 Bad Gateway|503 Service|504 Gateway|overloaded_error|Internal server error}"
INFRA_MAX="${LOOP_INFRA_MAX:-6}"
INFRA_BACKOFF="${LOOP_INFRA_BACKOFF:-120}"
INFRA_COOLOFF="${LOOP_INFRA_COOLOFF:-1800}"

if [ ! -f "$PROMPT_FILE" ]; then
  PROMPT_FILE="$TARGET_REPO/loop/PROMPT.md"
fi

pick_agent() {
  if [ -n "${LOOP_AGENT:-}" ]; then
    echo "$LOOP_AGENT"
    return
  fi
  if [ -f "$AGENT_FILE" ]; then
    tr -d '[:space:]' < "$AGENT_FILE"
    return
  fi
  echo "${LOOP_PROVIDERS:-grok}" | awk -F, '{print $1}' | tr -d '[:space:]'
}

find_bin() {
  local name="$1"
  case "$name" in
    grok)
      if [ -n "${LOOP_GROK_BIN:-}" ] && [ -x "${LOOP_GROK_BIN}" ]; then echo "$LOOP_GROK_BIN"; return; fi
      ;;
    codex)
      if [ -n "${LOOP_CODEX_BIN:-}" ] && [ -x "${LOOP_CODEX_BIN}" ]; then echo "$LOOP_CODEX_BIN"; return; fi
      ;;
    claude)
      if [ -n "${LOOP_CLAUDE_BIN:-}" ] && [ -x "${LOOP_CLAUDE_BIN}" ]; then echo "$LOOP_CLAUDE_BIN"; return; fi
      ;;
    opencode)
      if [ -n "${LOOP_OPENCODE_BIN:-}" ] && [ -x "${LOOP_OPENCODE_BIN}" ]; then echo "$LOOP_OPENCODE_BIN"; return; fi
      ;;
  esac
  command -v "$name" 2>/dev/null || true
}

if ! [[ "$MAX_LOOPS" =~ ^[0-9]+$ && "$COOLDOWN" =~ ^[0-9]+$ && "$MAX_TURNS" =~ ^[0-9]+$ ]]; then
  echo "LOOP_MAX_LOOPS/LOOP_COOLDOWN/LOOP_MAX_TURNS는 0 이상의 정수여야 합니다." >&2
  exit 1
fi

mkdir -p "$TARGET_REPO/logs" "$TARGET_REPO/loop" "$TARGET_REPO/docs/feedback"
export PYTHONUNBUFFERED=1
COUNT=0
FAILS=0
INFRA_FAILS=0

wait_with_stop() {
  local remaining="$1"
  while [ "$remaining" -gt 0 ]; do
    [ -f "$STOP_FILE" ] && return 1
    sleep 1
    remaining=$((remaining - 1))
  done
  return 0
}

status_stamp() {
  if [ -f "$STATUS_FILE" ]; then
    cksum "$STATUS_FILE" | awk '{print $1"-"$2}'
  else
    echo none
  fi
}

other_provider() {
  case "$1" in
    claude) echo grok ;;
    grok) echo claude ;;
    *) echo "$1" ;;
  esac
}

# 체인에서 find_bin 가능하고 usage_check가 exhausted가 아닌 첫 실행기를 고른다.
# start <name>: 링 순회 시작점(보통 현재 실행기) — 그 자체부터 검사해 스티키 유지.
# exclude <name>: 후보 제외(랩 로그 소진 직후 재선택 방지).
# echo: 실행기명 | 전부 소진·부재면 빈 문자열(return 1).
pick_from_chain() {
  local exclude="${1:-}" start="${2:-}" p bin check order
  if [ -n "$start" ]; then
    order="$(printf '%s' "$PROVIDERS_CHAIN" | awk -F, -v s="$start" '
      { n=split($0,a,","); idx=0
        for(i=1;i<=n;i++){ gsub(/ /,"",a[i]); if(a[i]==s) idx=i }
        if(idx<1) idx=1
        out=""
        for(i=0;i<n;i++) out=out (i?",":"") a[((idx-1+i)%n)+1]
        print out }')"
  else
    order="$PROVIDERS_CHAIN"
  fi
  local IFS=','
  for p in $order; do
    p="$(printf '%s' "$p" | tr -d '[:space:]')"
    [ -z "$p" ] && continue
    [ -n "$exclude" ] && [ "$p" = "$exclude" ] && continue
    bin="$(find_bin "$p")"
    [ -z "$bin" ] && continue
    check="$(usage_check "$p")"
    [ "$check" = "exhausted" ] && continue
    echo "$p"
    return 0
  done
  return 1
}

# 공식 사용량 API(board.py usage)로 소진 여부를 확인한다. echo: ok | exhausted | unknown
# unknown은 "조회 자체가 실패"(네트워크/키체인 일시 오류) — 소진으로 오판하지 않고
# 이전 provider를 그대로 쓴다(fail-open).
usage_check() {
  local name="$1"
  python3 "$BOARD_PY" usage "$name" 2>/dev/null | python3 -c '
import json, sys
raw = sys.stdin.read()
try:
    d = json.loads(raw)
except Exception:
    print("unknown"); sys.exit()
err = d.get("error")
if err and "로그인 없음" in err:
    print("exhausted"); sys.exit()
remain = d.get("remain_pct")
if d.get("ok") and remain is not None and remain <= 0:
    print("exhausted"); sys.exit()
if err:
    print("unknown"); sys.exit()
print("ok")
'
}

provider_state_read_current() {
  python3 -c '
import json, sys
path = sys.argv[1]
try:
    with open(path, encoding="utf-8") as f:
        cur = (json.load(f) or {}).get("current")
except Exception:
    cur = None
print(cur or "claude")
' "$PROVIDER_STATE_FILE"
}

provider_state_write() {
  # provider_state_write <current> <reason>
  python3 -c '
import json, sys, time
path, current, reason = sys.argv[1:4]
try:
    with open(path, encoding="utf-8") as f:
        d = json.load(f)
except Exception:
    d = {}
d["current"] = current
d["last_switch_at"] = time.strftime("%Y-%m-%d %H:%M:%S")
d["last_switch_reason"] = reason
with open(path, "w", encoding="utf-8") as f:
    json.dump(d, f, ensure_ascii=False, indent=2)
' "$PROVIDER_STATE_FILE" "$1" "$2"
}

provider_state_mark_retry() {
  # provider_state_mark_retry <name> — 그 실행기가 소진 감지된 시각을 기록만 한다(참고용).
  python3 -c '
import json, sys, time
path, name = sys.argv[1:3]
try:
    with open(path, encoding="utf-8") as f:
        d = json.load(f)
except Exception:
    d = {}
d[f"{name}_retry_after"] = time.strftime("%Y-%m-%d %H:%M:%S")
with open(path, "w", encoding="utf-8") as f:
    json.dump(d, f, ensure_ascii=False, indent=2)
' "$PROVIDER_STATE_FILE" "$1"
}

# 랩 로그에서 사후 소진 신호를 찾는다(usage_check가 못 잡는 세션 도중 소진 대비 폴백).
detect_exhaustion_in_log() {
  local logfile="$1"
  [ -f "$logfile" ] && grep -qiE "$EXHAUST_PATTERN" "$logfile"
}

# 공급자 서버 장애로 죽은 바퀴인가 — 마지막 40줄만 본다(작업 중 인용한 문구를 장애로 오독하지
# 않기 위해서다. 실제 장애는 세션 끝에서 터진다).
detect_infra_failure_in_log() {
  local logfile="$1"
  [ -f "$logfile" ] || return 1
  tail -40 "$logfile" | grep -qiE "$INFRA_PATTERN"
}

# 모델 단위 페일오버 — 지금 쓰는 opencode 모델이 서버 장애면 목록의 다음 모델로 갈아탄다.
# 2026-08-27 01:10 실측: x-preview-f-free만 죽어 있고 mimo·big-pickle은 정상이었는데, 모델을
# 고정해 둔 탓에 3시간 동안 바퀴가 한 번도 못 돌았다. 공급자가 하나뿐일 때의 마지막 방어선이다.
rotate_opencode_model() {
  [ "$AGENT" = "opencode" ] || return 0
  local list="${LOOP_OPENCODE_MODELS:-}"
  [ -n "$list" ] || return 0
  local first="" next="" found=0 m
  local IFS=,
  for m in $list; do
    [ -n "$m" ] || continue
    [ -z "$first" ] && first="$m"
    if [ "$found" = "1" ]; then next="$m"; break; fi
    [ "$m" = "$OPENCODE_MODEL" ] && found=1
  done
  [ -z "$next" ] && next="$first"                  # 목록 끝이면 처음으로 순환
  [ "$next" = "$OPENCODE_MODEL" ] && return 0      # 후보가 하나뿐이면 그대로
  echo "opencode 모델 전환: $OPENCODE_MODEL → $next (서버 장애)" | tee -a "$MAIN_LOG" "$LAP_LOG"
  OPENCODE_MODEL="$next"
}

# 자가검사용 후크 — `bash loop/loop.sh --self-test-infra <로그파일>`이면 판정만 하고 끝낸다
# (종료 0=공급자 장애 · 1=아니다). 네거티브 컨트롤 없는 통과를 만들지 않기 위한 장치다.
if [ -n "$SELFTEST_INFRA" ]; then
  detect_infra_failure_in_log "$SELFTEST_INFRA" && exit 0 || exit 1
fi

run_session() {
  local agent="$1"
  local bin="$2"
  local header="너는 비대화형 자율 루프다. 별도 승인 질문 없이 구현한다. 대화를 이어 붙이지 않는다. loop/PROMPT.md 다섯 절을 따른다."

  case "$agent" in
    grok)
      cd "$TARGET_REPO" || return 3
      "$bin" \
        --cwd "$TARGET_REPO" \
        --model "$GROK_MODEL" \
        --prompt-file "$PROMPT_FILE" \
        --always-approve \
        --permission-mode auto \
        --max-turns "$MAX_TURNS" \
        --no-subagents \
        --output-format plain
      ;;
    codex)
      {
        printf '%s\n\n' "$header"
        if [ -f "$PROMPT_FILE" ]; then cat "$PROMPT_FILE"; fi
      } | "$bin" exec --ephemeral --ignore-user-config \
            --sandbox danger-full-access \
            --skip-git-repo-check \
            -
      ;;
    claude)
      "$bin" -p "$(printf '%s\n\n' "$header"; [ -f "$PROMPT_FILE" ] && cat "$PROMPT_FILE")" \
        --permission-mode acceptEdits \
        --no-session-persistence \
        --output-format text
      ;;
    opencode)
      # 비대화형 단발 세션. resume 없음. 권한·MCP는 저장소 opencode.json이 결정한다.
      local prompt
      prompt="$(printf '%s\n\n' "$header"; [ -f "$PROMPT_FILE" ] && cat "$PROMPT_FILE")"
      cd "$TARGET_REPO" || return 3
      "$bin" run \
        ${OPENCODE_MODEL:+-m "$OPENCODE_MODEL"} \
        "$prompt"
      ;;
    *)
      echo "알 수 없는 실행기: $agent" >&2
      return 2
      ;;
  esac
}

STARTUP_AGENT="$(pick_agent)"
if [ "$AUTO_SWITCH" = "1" ] && { [ "$STARTUP_AGENT" = "claude" ] || [ "$STARTUP_AGENT" = "grok" ]; }; then
  echo "자율 개발 루프 시작: target=$TARGET_REPO agent=auto(${PROVIDERS_CHAIN}, 시작=$(provider_state_read_current)) max=${MAX_LOOPS:-0}"
else
  BIN="$(find_bin "$STARTUP_AGENT")"
  if [ -z "$BIN" ]; then
    echo "실행기를 찾지 못했다: $STARTUP_AGENT" >&2
    exit 1
  fi
  echo "자율 개발 루프 시작: target=$TARGET_REPO agent=$STARTUP_AGENT max=${MAX_LOOPS:-0}"
fi

PROVIDER_WAIT_ROUNDS=0

while true; do
  if [ -f "$STOP_FILE" ]; then
    echo "STOP 확인 — 새 바퀴를 시작하지 않고 정상 종료합니다."
    exit 0
  fi
  if [ "$MAX_LOOPS" -gt 0 ] && [ "$COUNT" -ge "$MAX_LOOPS" ]; then
    echo "지정된 ${MAX_LOOPS}바퀴를 마쳤습니다."
    exit 0
  fi

  AGENT="$(pick_agent)"
  case "$AGENT" in
    codex|opencode)
      : ;;  # 수동 핀 — 전환 관여 없음(설계 유지)
    *)
      if [ "$AUTO_SWITCH" = "1" ]; then
        STATE_CURRENT="$(provider_state_read_current)"
        PICKED="$(pick_from_chain "" "$STATE_CURRENT")"
        if [ -z "$PICKED" ]; then
          PROVIDER_WAIT_ROUNDS=$((PROVIDER_WAIT_ROUNDS + 1))
          provider_state_mark_retry "$STATE_CURRENT"
          if [ "$CHAIN_COUNT" -le 1 ]; then
            # 단독 공급자 대기 모드 — 넘어갈 곳이 없다. 회복될 때까지 무한 대기(자멸 안 함).
            # usage_check는 fail-open이라 조회 실패는 'unknown'→여기로 안 온다. 여기 도달은
            # 진짜 소진(remain<=0)이나 '로그인 없음'뿐이므로 대기가 정답이다(오너 지시).
            echo "$STATE_CURRENT 소진 — ${PROVIDER_RETRY_SECONDS}초 쉬고 할당량 회복 재확인(대기 ${PROVIDER_WAIT_ROUNDS}회, 종료 안 함·오너 지시)" | tee -a "$MAIN_LOG"
          else
            echo "체인 전체 소진(${PROVIDER_WAIT_ROUNDS}/${MAX_PROVIDER_FAILURES}) — ${PROVIDER_RETRY_SECONDS}초 대기 후 재확인" | tee -a "$MAIN_LOG"
            if [ "$PROVIDER_WAIT_ROUNDS" -ge "$MAX_PROVIDER_FAILURES" ]; then
              echo "체인 소진 대기 ${MAX_PROVIDER_FAILURES}회 초과 — 정상 종료합니다(사용량 확인 자체가 고장났을 가능성 포함)." | tee -a "$MAIN_LOG"
              touch "$STOP_FILE"
              exit 0
            fi
          fi
          wait_with_stop "$PROVIDER_RETRY_SECONDS" || exit 0
          continue
        fi
        if [ "$PICKED" != "$STATE_CURRENT" ]; then
          echo "$STATE_CURRENT 소진/부재 감지 — $PICKED(으)로 전환" | tee -a "$MAIN_LOG"
          provider_state_write "$PICKED" "usage_check: $STATE_CURRENT exhausted"
        fi
        AGENT="$PICKED"
        PROVIDER_WAIT_ROUNDS=0
      fi
      ;;
  esac
  BIN="$(find_bin "$AGENT")"
  if [ -z "$BIN" ]; then
    echo "실행기를 찾지 못했다: $AGENT" | tee -a "$MAIN_LOG"
    exit 1
  fi

  DATE_DIR="$(date +%Y-%m-%d)"
  LAP_ID="$(date +%Y%m%d-%H%M%S)-$((COUNT + 1))"
  LAP_DIR="$TARGET_REPO/logs/$DATE_DIR"
  LAP_LOG="$LAP_DIR/lap-$LAP_ID.log"
  mkdir -p "$LAP_DIR"

  echo "바퀴 시작: $LAP_ID agent=$AGENT" | tee -a "$MAIN_LOG" "$LAP_LOG"
  BEFORE="$(status_stamp)"

  # 새 세션 — resume/continue 없음
  run_session "$AGENT" "$BIN" >> "$LAP_LOG" 2>&1
  RESULT=$?
  AFTER="$(status_stamp)"

  echo "바퀴 종료: $LAP_ID code=$RESULT" | tee -a "$MAIN_LOG" "$LAP_LOG"

  EXHAUSTED_THIS_LAP=0
  if [ "$AUTO_SWITCH" = "1" ] && { [ "$AGENT" = "claude" ] || [ "$AGENT" = "grok" ]; } \
      && detect_exhaustion_in_log "$LAP_LOG"; then
    EXHAUSTED_THIS_LAP=1
    NEXT_PROVIDER="$(pick_from_chain "$AGENT" "$AGENT")"
    [ -z "$NEXT_PROVIDER" ] && NEXT_PROVIDER="$(other_provider "$AGENT")"
    echo "랩 로그에서 소진 신호 감지 — $AGENT 소진, 다음 바퀴부터 $NEXT_PROVIDER (FAILS 미증가)" | tee -a "$MAIN_LOG" "$LAP_LOG"
    provider_state_write "$NEXT_PROVIDER" "log-detect: $AGENT exhaustion phrase in $LAP_ID"
  fi

  INFRA_THIS_LAP=0
  if [ "$EXHAUSTED_THIS_LAP" = "0" ] && [ "$RESULT" -ne 0 ] && detect_infra_failure_in_log "$LAP_LOG"; then
    INFRA_THIS_LAP=1
    INFRA_FAILS=$((INFRA_FAILS + 1))
    if [ "$INFRA_FAILS" -ge "$INFRA_MAX" ]; then
      # 상대 서버가 오래 죽어 있는 상황이다. 여기서 종료하면 오너가 가장 싫어하는 「루프가
      # 안 돈다」가 된다(loop_watch가 살리기까지 최대 90분 공백). 대신 길게 쉬고 다시 본다.
      echo "공급자 장애 ${INFRA_MAX}회 연속 — ${INFRA_COOLOFF}초 길게 쉬고 재확인합니다(종료하지 않음)." \
        | tee -a "$MAIN_LOG" "$LAP_LOG"
      wait_with_stop "$INFRA_COOLOFF" || { echo "STOP 감지 — 종료" | tee -a "$MAIN_LOG"; exit 0; }
      INFRA_FAILS=0
    else
      rotate_opencode_model
      BACKOFF=$((INFRA_BACKOFF * INFRA_FAILS))
      echo "공급자 서버 장애로 죽은 바퀴 ($AGENT · ${INFRA_FAILS}/${INFRA_MAX}) — FAILS 미증가, ${BACKOFF}초 후 재시도" \
        | tee -a "$MAIN_LOG" "$LAP_LOG"
      wait_with_stop "$BACKOFF" || { echo "STOP 감지 — 종료" | tee -a "$MAIN_LOG"; exit 0; }
    fi
  fi

  if [ "$EXHAUSTED_THIS_LAP" = "1" ] || [ "$INFRA_THIS_LAP" = "1" ]; then
    : # 우리 코드가 틀린 게 아니라 공급자 사정이다 — FAILS/COUNT를 건드리지 않는다.
  elif [ "$AFTER" = "$BEFORE" ]; then
    echo "STATUS.md 갱신 없음" | tee -a "$MAIN_LOG" "$LAP_LOG"
    FAILS=$((FAILS + 1))
    if [ "$FAILS" -ge "$MAX_FAILS" ]; then
      echo "STATUS.md 미갱신이 ${MAX_FAILS}회 — 정상 종료합니다(재기동 루프 방지)." | tee -a "$MAIN_LOG" "$LAP_LOG"
      touch "$STOP_FILE"
      exit 0
    fi
  elif [ "$RESULT" -eq 0 ]; then
    COUNT=$((COUNT + 1))
    FAILS=0
    INFRA_FAILS=0   # 한 바퀴라도 정상이면 공급자는 살아난 것이다
    # 자가학습 회의 — N바퀴마다 역할별 병렬 회의를 소집한다 (오너 2026-08-23).
    # 어떤 에이전트든 loop/COUNCIL_NOW 파일을 만들면 다음 바퀴 끝에 즉시 소집된다.
    if [ -f "$TARGET_REPO/loop/COUNCIL_NOW" ]; then
      rm -f "$TARGET_REPO/loop/COUNCIL_NOW"
      echo "즉시 회의 소집 신호 감지" | tee -a "$MAIN_LOG" "$LAP_LOG"
      bash "$DEPLOY_ROOT/council.sh" "$TARGET_REPO" >> "$LAP_LOG" 2>&1 || true
    fi
    if [ "$COUNT" -gt 0 ] && [ "$COUNCIL_EVERY" -gt 0 ] && [ $((COUNT % COUNCIL_EVERY)) -eq 0 ]; then
      echo "회의 소집: ${COUNT}바퀴 완료 — 역할 병렬 회의 시작" | tee -a "$MAIN_LOG" "$LAP_LOG"
      bash "$DEPLOY_ROOT/council.sh" "$TARGET_REPO" >> "$LAP_LOG" 2>&1 \
        || echo "회의 실패 — 루프는 계속" | tee -a "$MAIN_LOG" "$LAP_LOG"
    fi
    # 속도 레인 적립분(autonomous/integration)을 master로 흡수
    bash "$DEPLOY_ROOT/merge_integration.sh" "$TARGET_REPO" >> "$MAIN_LOG" 2>&1 || true
  else
    echo "세션 비정상 종료 (code=$RESULT)" | tee -a "$MAIN_LOG" "$LAP_LOG"
    FAILS=$((FAILS + 1))
    if [ "$FAILS" -ge "$MAX_FAILS" ]; then
      exit "$RESULT"
    fi
  fi

  if [ -f "$STOP_FILE" ]; then
    echo "현재 바퀴 완료 후 STOP 확인 — 정상 종료합니다." | tee -a "$MAIN_LOG"
    exit 0
  fi

  wait_with_stop "$COOLDOWN" || exit 0
done
