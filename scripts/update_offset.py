import os
import re

bot_file = r"d:\ai_lab\.agent\skills\예원\tools\telegram_bot.py"
with open(bot_file, "r", encoding="utf-8") as f:
    content = f.read()

# Replace offset initialization
old_offset = """
    offset = 0
    processed_messages = set()  # 중복 메시지 방지 (message_id 추적)
"""
new_offset = """
    OFFSET_FILE = os.path.join(_here, "telegram_offset.txt")
    offset = 0
    if os.path.exists(OFFSET_FILE):
        try:
            with open(OFFSET_FILE, "r") as f:
                offset = int(f.read().strip())
        except:
            pass

    processed_messages = set()  # 중복 메시지 방지 (message_id 추적)
"""

content = content.replace(old_offset, new_offset)

# Replace offset update loop
old_update = """
        for upd in updates:
            offset = upd["update_id"] + 1
"""
new_update = """
        for upd in updates:
            offset = upd["update_id"] + 1
            try:
                with open(OFFSET_FILE, "w") as f:
                    f.write(str(offset))
            except:
                pass
"""
content = content.replace(old_update, new_update)

with open(bot_file, "w", encoding="utf-8") as f:
    f.write(content)

print("Offset logic updated successfully.")
