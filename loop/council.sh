#!/bin/bash
# 역할별 병렬 회의 — 자가학습 개선안 심의 (오너 2026-08-23).
# planner·builder·tester 세 에이전트를 "새" 헤드리스 세션으로 동시에 띄우고,
# 의장(chair)이 결론을 docs/meetings/COUNCIL_<ts>.md로 합친다.
# 대화를 이어 붙이지 않는다. STATUS.md는 자동 수정하지 않는다 (사람/루프 판단권 보호).

set -uo pipefail

DEPLOY_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TARGET_REPO="${1:-$(cd "$DEPLOY_ROOT/.." && pwd)}"
TARGET_REPO="$(cd "$TARGET_REPO" && pwd)"

if [ -f "$DEPLOY_ROOT/env.sh" ]; then
  # shellcheck source=/dev/null
  source "$DEPLOY_ROOT/env.sh"
fi

TS="$(date +%Y%m%d-%H%M%S)"
OUT_DIR="$TARGET_REPO/logs/council/$TS"
MEET_DIR="$TARGET_REPO/docs/meetings"
BIN="${LOOP_OPENCODE_BIN:-opencode}"
MODEL="${LOOP_OPENCODE_MODEL:-opencode/x-preview-f-free}"
COUNCIL_EVERY="${LOOP_COUNCIL_EVERY:-4}"
LOCK="$MEET_DIR/.council-lock"

mkdir -p "$OUT_DIR" "$MEET_DIR"

# 이중 소집 방지
if ! mkdir "$LOCK" 2>/dev/null; then
  echo "회의가 이미 진행 중 — 건너뛴다"
  exit 0
fi
trap 'rmdir "$LOCK" 2>/dev/null' EXIT

role_prompt() {
  local role="$1"
  cat <<EOF
너는 재와 별 프로젝트 정기 회의의 '$role' 역할 에이전트다. 비대화형 새 세션이다.
저장소 루트: $TARGET_REPO (거기서 작업하라)

먼저 읽어라: docs/feedback/PROPOSALS.md, docs/STATUS.md, 그리고 아래 역할 문서.
- planner: docs/GAME_DESIGN_ASHES_TO_STARS.md §22 로드맵 + docs/DESIGN.md — 큐 순서·범위가 로드맵과 일치하는지
- builder: git log --oneline -20 결과 + docs/GAME_WORKLOG.md 머리 — 반복되는 비효율·리스크·부채
- tester: loop/last_test_report.json + 최근 logs/ 당일 lap 로그 1개 — 검증 공정 구멍·회귀 위험

그 뒤 파일 하나만 쓰고 끝낸다:
$MEET_DIR/.council-part-$TS-$role.md

파일 형식(마크다운):
## $role
### 관찰 (최대 3건, 근거 파일·줄 명시)
### 제안 (각각 우선순위 상/중/하, PROPOSALS 항목 인용 시 원문 한 줄 포함)
### 회차 판정 요청 (이번 4바퀴 동안 우선할 것 1건)

다른 파일은 절대 수정하지 마라. 커밋하지 마라. 계획만 쓰면 실패다.
EOF
}

PIDS=()
for role in planner builder tester; do
  PF="$OUT_DIR/prompt-$role.md"
  role_prompt "$role" > "$PF"
  (
    cd "$TARGET_REPO" && \
    "$BIN" run -m "$MODEL" "$(cat "$PF")" > "$OUT_DIR/out-$role.log" 2>&1
  ) &
  PIDS+=($!)
done

FAIL=0
for p in "${PIDS[@]}"; do
  wait "$p" || FAIL=$((FAIL + 1))
done
echo "역할 세션 종료: 실패 ${FAIL}/3"

PARTS=("$MEET_DIR/.council-part-$TS-planner.md" "$MEET_DIR/.council-part-$TS-builder.md" "$MEET_DIR/.council-part-$TS-tester.md")
HAVE=0
for f in "${PARTS[@]}"; do [ -s "$f" ] && HAVE=$((HAVE + 1)); done

if [ "$HAVE" -eq 0 ]; then
  echo "역할 산출물 0건 — 의장 단계 생략"
  exit 1
fi

CHAIR_PROMPT="너는 재와 별 정기 회의 의장이다. 비대화형 새 세션이다. 저장소: $TARGET_REPO
읽어라: docs/feedback/PROPOSALS.md 전체와 회의 파트 ${HAVE}건:
$(printf '%s\n' "${PARTS[@]}" | sed 's/^/- /')
그리고 docs/STATUS.md 「다음 할 일」 목록.
의장 문서 하나를 써라: $MEET_DIR/COUNCIL-$TS.md
형식:
# 정기 회의 $TS (${COUNT:-n}바퀴 시점)
## 참석 (planner/builder/tester 산출 요약 각 2줄)
## 채택 개선안 (번호·근거·다음 바퀴 업무 후보로서의 위치)
## 보류 (사유 필수)
## 다음 바퀴 권고 1건 (STATUS 큐와 충돌하면 충돌 사실만 기록 — 큐를 직접 고치지 마라)
채택된 개선안은 docs/feedback/PROPOSALS.md 해당 줄 끝에 ✅<TS> 표식만 덧붙여라(줄 추가 금지).
보류된 것은 ⏸<TS> 표식. 다른 파일은 만지지 마라. 커밋은 하지 마라."

cd "$TARGET_REPO" && "$BIN" run -m "$MODEL" "$CHAIR_PROMPT" > "$OUT_DIR/out-chair.log" 2>&1
CHAIR_RC=$?

rm -f "${PARTS[@]}"

# 워크로그 말미에 한 줄 흔적 (append-only — 기존 내용 불변)
{
  echo ""
  echo "## 정기 회의 $TS"
  echo "- 역할 병렬 회의(planner·builder·tester+의장) 종료. 산출: docs/meetings/COUNCIL-$TS.md (rc=$CHAIR_RC, 파트 ${HAVE}/3)"
} >> "$TARGET_REPO/docs/GAME_WORKLOG.md"

echo "회의 완료: docs/meetings/COUNCIL-$TS.md (rc=$CHAIR_RC)"
