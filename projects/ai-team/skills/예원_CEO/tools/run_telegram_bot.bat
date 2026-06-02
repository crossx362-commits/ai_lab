@echo off
cd /d "d:\ai_lab"
set PYTHONIOENCODING=utf-8
:loop
python ".agent\skills\영숙_비서\tools\telegram_receiver.py" >> ".agent\skills\영숙_비서\tools\telegram_receiver.log" 2>&1
echo [%date% %time%] Bot exited, restarting in 30s... >> ".agent\skills\영숙_비서\tools\telegram_receiver.log"
timeout /t 30 /nobreak >nul
goto loop
