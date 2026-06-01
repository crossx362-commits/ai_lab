import os
import re

bot_file = r"d:\ai_lab\.agent\skills\예원\tools\telegram_bot.py"
with open(bot_file, "r", encoding="utf-8") as f:
    content = f.read()

# Add _run_deepsearch in the New Agents block
import_block = """
def _run_deepsearch(topic):
    def background_task():
        try:
            import sys
            sys.path.insert(0, os.path.join(PROJECT_ROOT, ".agent", "skills", "현빈_전략가", "tools"))
            import deep_search_6h
            deep_search_6h.run_deep_research(topic)
        except Exception as e:
            send(f"현빈 딥서치 백그라운드 실행 실패: {e}")
            
    import threading
    threading.Thread(target=background_task, daemon=True).start()
    return f"📨 현빈이에게 6시간 딥서치를 지시했어! (주제: {topic or '기본 펫과나'}) 백그라운드에서 진행 상황을 알려줄게."
"""
if "_run_deepsearch" not in content:
    content = content.replace("def _run_hyunbin():", import_block + "\ndef _run_hyunbin():")

# Add to HELP_TEXT
old_help = """/research  — 현빈 비즈니스 리서치"""
new_help = """/research  — 현빈 일반 비즈니스 리서치
/deepsearch [주제] — 현빈 6시간 딥서치 (백그라운드)"""
if "/deepsearch" not in content:
    content = content.replace(old_help, new_help)

# Add to route()
route_addition = """
    elif text.startswith("/deepsearch"):
        parts = text.split(maxsplit=1)
        topic = parts[1] if len(parts) > 1 else None
        return _run_deepsearch(topic)
"""
if "elif text.startswith(\"/deepsearch\")" not in content:
    route_search = 'elif text.startswith("/research"):'
    content = content.replace(route_search, route_addition + "\n    " + route_search)

with open(bot_file, "w", encoding="utf-8") as f:
    f.write(content)

print("Bot routing updated with /deepsearch successfully.")
