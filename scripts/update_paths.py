import os
import re

bot_file = r"d:\ai_lab\.agent\skills\예원\tools\telegram_bot.py"
with open(bot_file, "r", encoding="utf-8") as f:
    content = f.read()

# Fix PROJECT_ROOT
content = content.replace(
    'PROJECT_ROOT = os.path.abspath(os.path.join(_here, "..", ".."))',
    'PROJECT_ROOT = os.path.abspath(os.path.join(_here, "..", "..", "..", ".."))'
)

# Fix tool-seeds paths mapping
path_map = {
    '"루나_디렉터"': '", ".agent", "skills", "루나_디렉터", "tools"',
    '"아린_관리자"': '", ".agent", "skills", "아린_관리자", "tools"',
    '"현빈_전략가"': '", ".agent", "skills", "현빈_전략가", "tools"',
    '"영숙_비서"': '", ".agent", "skills", "영숙_비서", "tools"',
    '"예원_CEO"': '", ".agent", "skills", "예원_CEO", "tools"',
    '"코다리_개발자"': '", ".agent", "skills", "코다리_개발자", "tools"',
    '"가희_검수관"': '", ".agent", "skills", "가희_검수관", "tools"',
    '"경수_수사관"': '", ".agent", "skills", "경수_수사관", "tools"'
}

for old, new in path_map.items():
    content = content.replace(f'", "assets", "tool-seeds", {old}', new)

# Fix PID_FILE
content = content.replace(
    'os.path.join(PROJECT_ROOT, ".agent", "tools", "telegram_bot.pid")',
    'os.path.join(_here, "telegram_bot.pid")'
)

# Fix LOG_PATHS
content = content.replace(
    'os.path.join(PROJECT_ROOT, ".agent", "tools", "telegram_bot.log")',
    'os.path.join(_here, "telegram_bot.log")'
)

with open(bot_file, "w", encoding="utf-8") as f:
    f.write(content)

print("Paths updated successfully.")
