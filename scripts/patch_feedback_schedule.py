import os
import re

bot_file = r"d:\ai_lab\.agent\skills\예원\tools\telegram_bot.py"
with open(bot_file, "r", encoding="utf-8") as f:
    content = f.read()

feedback_loop_code = """
def _ceo_feedback_loop():
    \"\"\"CEO(예원): 매일 오후 8시(KST) 유튜브 피드백/평가 자동 실행\"\"\"
    import datetime
    import time
    KST_TZ = datetime.timezone(datetime.timedelta(hours=9))
    while True:
        now = datetime.datetime.now(KST_TZ)
        target = now.replace(hour=20, minute=0, second=0, microsecond=0)
        if now >= target:
            target += datetime.timedelta(days=1)
        wait_seconds = (target - now).total_seconds()
        time.sleep(wait_seconds)
        try:
            res = cmd_evaluate()
            send(f"👑 예원: 오늘의 유튜브 피드백(RL 성과 평가) 결과를 가져왔어! 📊\\n\\n{res}")
        except Exception as e:
            print(f"  [CEO 피드백 스케줄러] 오류: {e}")
"""

# Insert the loop before `def main():` or near the other loops
if "_ceo_feedback_loop" not in content:
    content = content.replace("def main():", feedback_loop_code + "\ndef main():")

# Start the thread in main()
thread_start_code = """
    threading.Thread(target=_ceo_feedback_loop, daemon=True).start()
"""
if "_ceo_feedback_loop" in feedback_loop_code and "target=_ceo_feedback_loop" not in content:
    # Find `threading.Thread(target=_ceo_report_loop, daemon=True).start()`
    content = content.replace(
        "threading.Thread(target=_ceo_report_loop, daemon=True).start()",
        "threading.Thread(target=_ceo_report_loop, daemon=True).start()\n" + thread_start_code
    )

with open(bot_file, "w", encoding="utf-8") as f:
    f.write(content)

print("CEO Feedback Schedule added successfully.")
