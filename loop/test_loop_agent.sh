#!/bin/bash

set -euo pipefail

TEST_ROOT="$(mktemp -d)"
trap 'rm -rf "$TEST_ROOT"' EXIT

mkdir -p "$TEST_ROOT/loop/logs" "$TEST_ROOT/docs/feedback" "$TEST_ROOT/projects/ashes-to-stars" "$TEST_ROOT/bin"
cp "$(dirname "$0")/loop.sh" "$TEST_ROOT/loop/loop.sh"
cp "$(dirname "$0")/runtime_state.py" "$TEST_ROOT/loop/runtime_state.py"

touch "$TEST_ROOT/docs/feedback/INBOX.md" \
      "$TEST_ROOT/docs/STATUS.md" \
      "$TEST_ROOT/docs/DESIGN.md" \
      "$TEST_ROOT/projects/ashes-to-stars/CLAUDE.md"

cat > "$TEST_ROOT/bin/claude" <<'FAKE_CLAUDE'
#!/bin/bash
touch "$TEST_ROOT/claude_called"
touch "$TEST_ROOT/loop/STOP"
FAKE_CLAUDE

cat > "$TEST_ROOT/bin/grok" <<'FAKE_GROK'
#!/bin/bash
printf '%s\n' "$@" > "$TEST_ROOT/grok_args"
if [ "${FAKE_MODE:-}" = "update_status" ]; then
  echo "fake grok handoff" >> "$TEST_ROOT/docs/STATUS.md"
fi
touch "$TEST_ROOT/grok_called"
touch "$TEST_ROOT/loop/STOP"
FAKE_GROK

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

chmod +x "$TEST_ROOT/bin/claude" "$TEST_ROOT/bin/codex" "$TEST_ROOT/bin/grok" \
  "$TEST_ROOT/loop/loop.sh" "$TEST_ROOT/loop/runtime_state.py"

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

rm -f "$TEST_ROOT/loop/STOP" "$TEST_ROOT/codex_called" "$TEST_ROOT/claude_called"
printf '%s\n' "codex" > "$TEST_ROOT/loop/agent"
PATH="$TEST_ROOT/bin:$PATH" \
TEST_ROOT="$TEST_ROOT" \
FAKE_MODE=update_status \
LOOP_COOLDOWN=0 \
env -u LOOP_AGENT \
bash "$TEST_ROOT/loop/loop.sh" > "$TEST_ROOT/agentfile.log" 2>&1

if [ ! -f "$TEST_ROOT/codex_called" ]; then
  echo "FAIL: loop/agent=codex 인데 Codex를 부르지 않았다"
  sed -n '1,80p' "$TEST_ROOT/agentfile.log"
  exit 1
fi
if [ -f "$TEST_ROOT/claude_called" ]; then
  echo "FAIL: loop/agent=codex 인데 Claude를 불렀다"
  exit 1
fi

echo "PASS: Codex 실행기는 새 세션에서 별도 승인 질문 없이 구현하도록 호출된다"
echo "PASS: loop/agent 파일이 LOOP_AGENT 미지정 시 실행기를 고른다"

rm -f "$TEST_ROOT/loop/STOP" "$TEST_ROOT/codex_called" "$TEST_ROOT/claude_called" "$TEST_ROOT/grok_called"
PATH="$TEST_ROOT/bin:$PATH" \
TEST_ROOT="$TEST_ROOT" \
FAKE_MODE=update_status \
LOOP_AGENT=grok \
LOOP_COOLDOWN=0 \
bash "$TEST_ROOT/loop/loop.sh" > "$TEST_ROOT/grok.log" 2>&1

if [ ! -f "$TEST_ROOT/grok_called" ]; then
  echo "FAIL: LOOP_AGENT=grok 인데 Grok을 부르지 않았다"
  sed -n '1,80p' "$TEST_ROOT/grok.log"
  exit 1
fi
if [ -f "$TEST_ROOT/codex_called" ] || [ -f "$TEST_ROOT/claude_called" ]; then
  echo "FAIL: LOOP_AGENT=grok 인데 다른 실행기를 불렀다"
  exit 1
fi
if ! grep -qx -- '--prompt-file' "$TEST_ROOT/grok_args"; then
  echo "FAIL: Grok 호출에 --prompt-file 이 없다 (stdin은 프롬프트가 아니다)"
  cat "$TEST_ROOT/grok_args"
  exit 1
fi
if ! grep -qx -- '--always-approve' "$TEST_ROOT/grok_args"; then
  echo "FAIL: Grok 루프가 도구 자동 승인을 켜지 않았다"
  cat "$TEST_ROOT/grok_args"
  exit 1
fi
if grep -qxE '(-c|--continue)' "$TEST_ROOT/grok_args"; then
  echo "FAIL: Grok 루프가 이전 세션을 이었다"
  exit 1
fi

echo "PASS: Grok 실행기는 새 세션·prompt-file·always-approve로 호출된다"
