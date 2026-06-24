#!/bin/bash
# Python 찾기
for PY in /opt/homebrew/bin/python3 /opt/homebrew/bin/python3.12 /opt/homebrew/bin/python3.11 /usr/bin/python3; do
  if [ -x "$PY" ]; then
    PYTHON="$PY"
    break
  fi
done

echo "[영숙 launcher] Python: $PYTHON"

# 필수 패키지 확인 & 설치 (없는 것만)
for PKG in "telegram:python-telegram-bot" "cryptography:cryptography" "google.generativeai:google-generativeai"; do
  MOD="${PKG%%:*}"
  INST="${PKG##*:}"
  if ! $PYTHON -c "import $MOD" 2>/dev/null; then
    echo "[영숙 launcher] $INST 설치 중..."
    $PYTHON -m pip install "$INST" --break-system-packages -q
  fi
done

# 실행
exec $PYTHON -u "$(dirname "$0")/telegram_receiver.py"
