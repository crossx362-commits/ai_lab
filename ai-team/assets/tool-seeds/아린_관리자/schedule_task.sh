#!/bin/bash

# Get the absolute path of the current directory
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PYTHON_PATH=$(which python3)

if [ -z "$PYTHON_PATH" ]; then
    echo "❌ python3를 찾을 수 없습니다. 파이썬을 설치하거나 PATH 설정을 확인해주세요."
    exit 1
fi

CRON_JOB="30 18 * * * cd \"$DIR\" && \"$PYTHON_PATH\" auto_pipeline.py >> \"$DIR/daily_post.log\" 2>&1"

# Temporary file to store existing crontab
TMP_CRON=$(mktemp)

# Export current crontab, filtering out any existing job for this directory
crontab -l 2>/dev/null | grep -v "arin" > "$TMP_CRON"


# Append the new cron job
echo "$CRON_JOB" >> "$TMP_CRON"

# Install the new crontab
crontab "$TMP_CRON"
rm "$TMP_CRON"

echo "✅ 매일 18:30에 인스타그램 자동 업로드가 실행되도록 크론탭(crontab) 등록이 완료되었습니다!"
echo "📌 등록된 스케줄: $CRON_JOB"
