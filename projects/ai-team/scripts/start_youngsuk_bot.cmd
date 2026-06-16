@echo off
cd /d D:\ai_lab
set PYTHONUTF8=1
echo [%date% %time%] starting youngsuk >> output\bot_logs\youngsuk_cmd.log
python projects\ai-team\scripts\run_youngsuk_daemon.py >> output\bot_logs\youngsuk_cmd.log 2>&1
echo [%date% %time%] youngsuk exited %ERRORLEVEL% >> output\bot_logs\youngsuk_cmd.log
