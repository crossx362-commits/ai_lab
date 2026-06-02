@echo off
cd /d "d:\ai_lab"
set PYTHONIOENCODING=utf-8
:loop
python ".agent\skills\예원\tools\telegram_bot.py" >> ".agent\skills\예원\tools\telegram_bot.log" 2>&1
echo [%date% %time%] Bot exited, restarting in 30s... >> ".agent\skills\예원\tools\telegram_bot.log"
timeout /t 30 /nobreak >nul
goto loop
