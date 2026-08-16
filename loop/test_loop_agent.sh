#!/bin/bash

set -euo pipefail

TEST_ROOT="$(mktemp -d)"
trap 'rm -rf "$TEST_ROOT"' EXIT

mkdir -p "$TEST_ROOT/loop/logs" "$TEST_ROOT/docs/feedback" "$TEST_ROOT/projects/ashes-to-stars" "$TEST_ROOT/bin"
cp "$(dirname "$0")/loop.sh" "$TEST_ROOT/loop/loop.sh"

touch "$TEST_ROOT/docs/feedback/INBOX.md" \
      "$TEST_ROOT/docs/STATUS.md" \
      "$TEST_ROOT/docs/DESIGN.md" \
      "$TEST_ROOT/projects/ashes-to-stars/CLAUDE.md"

cat > "$TEST_ROOT/bin/claude" <<'FAKE_CLAUDE'
#!/bin/bash
touch "$TEST_ROOT/claude_called"
touch "$TEST_ROOT/loop/STOP"
FAKE_CLAUDE

cat > "$TEST_ROOT/bin/codex" <<'FAKE_CODEX'
#!/bin/bash
printf '%s\n' "$@" > "$TEST_ROOT/codex_args"
cat > "$TEST_ROOT/codex_prompt"
case "$(cat "$TEST_ROOT/codex_prompt")" in
  *"별도 승인 질문 없이 구현"*) touch "$TEST_ROOT/autonomous_prompt_seen" ;;
esac
if [ "${FAKE_MODE:-}" = "update_status" ]; then
  echo "fake iteration handoff" >> "$TEST_ROOT/docs/STATUS.md"
fi
touch "$TEST_ROOT/codex_called"
touch "$TEST_ROOT/loop/STOP"
FAKE_CODEX

chmod +x "$TEST_ROOT/bin/claude" "$TEST_ROOT/bin/codex" "$TEST_ROOT/loop/loop.sh"

set +e
PATH="$TEST_ROOT/bin:$PATH" \
TEST_ROOT="$TEST_ROOT" \
FAKE_MODE=missing_status \
LOOP_AGENT=codex \
LOOP_MAX_FAILS=1 \
LOOP_COOLDOWN=0 \
bash "$TEST_ROOT/loop/loop.sh" > "$TEST_ROOT/missing_status.log" 2>&1
MISSING_STATUS_RESULT=$?
set -e

if [ "$MISSING_STATUS_RESULT" -eq 0 ]; then
  echo "FAIL: STATUS.md를 갱신하지 않은 정상 종료를 완료로 판정했다"
  exit 1
fi

if ! grep -q 'STATUS.md 갱신 없음' "$TEST_ROOT/missing_status.log"; then
  echo "FAIL: STATUS.md 미갱신 실패 원인이 로그에 남지 않았다"
  exit 1
fi

rm -f "$TEST_ROOT/loop/STOP"
PATH="$TEST_ROOT/bin:$PATH" \
TEST_ROOT="$TEST_ROOT" \
FAKE_MODE=update_status \
LOOP_AGENT=codex \
LOOP_MAX_FAILS=1 \
LOOP_COOLDOWN=0 \
bash "$TEST_ROOT/loop/loop.sh" > "$TEST_ROOT/output.log" 2>&1

if [ ! -f "$TEST_ROOT/codex_called" ]; then
  echo "FAIL: LOOP_AGENT=codex가 Codex 실행기를 호출하지 않았다"
  sed -n '1,80p' "$TEST_ROOT/output.log"
  exit 1
fi

if [ -f "$TEST_ROOT/claude_called" ]; then
  echo "FAIL: LOOP_AGENT=codex인데 Claude 실행기가 호출됐다"
  exit 1
fi

if grep -qxE '(resume|--continue)' "$TEST_ROOT/codex_args"; then
  echo "FAIL: Codex 루프가 이전 세션을 이어서 실행했다"
  exit 1
fi

if ! grep -qx 'danger-full-access' "$TEST_ROOT/codex_args"; then
  echo "FAIL: Codex 루프가 Git 커밋·Unity 실제 창 실행 권한으로 호출되지 않았다"
  exit 1
fi

if [ ! -f "$TEST_ROOT/autonomous_prompt_seen" ]; then
  echo "FAIL: 무인 루프 프롬프트에 사전 승인된 구현 지시가 없다"
  exit 1
fi

echo "PASS: Codex 실행기는 새 세션에서 별도 승인 질문 없이 구현하도록 호출된다"
