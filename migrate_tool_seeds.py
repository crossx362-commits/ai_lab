import os
import shutil
import re

BOT_FILE = r"d:\ai_lab\.agent\skills\예원\tools\telegram_bot.py"
TOOL_SEEDS_DIR = r"d:\ai_lab\ai-team\assets\tool-seeds"
AGENT_SKILLS_DIR = r"d:\ai_lab\.agent\skills"

mapping = {
    "루나_디렉터": "루나",
    "아린_관리자": "아린",
    "영숙_비서": "영숙",
    "가희_검수관": "가희",
    "현빈_전략가": "현빈",
    "예원_CEO": "예원",
    "코다리_개발자": "코다리",
    "경수_수사관": "경수",
    "티모_디자이너": "티모"
}

# 1. Update telegram_bot.py paths
with open(BOT_FILE, "r", encoding="utf-8") as f:
    content = f.read()

for old_name, new_name in mapping.items():
    # Replace os.path.join(..., "assets", "tool-seeds", "old_name", "src", ...) -> ".agent", "skills", "new_name", "tools", "src"
    # Actually, we can just do string replacements on the path components.
    
    # Replace standard file paths: "assets", "tool-seeds", "old_name" -> ".agent", "skills", "new_name", "tools"
    old_tuple = f'"assets", "tool-seeds", "{old_name}"'
    new_tuple = f'".agent", "skills", "{new_name}", "tools"'
    content = content.replace(old_tuple, new_tuple)
    
    # Some might use 'assets/tool-seeds/old_name'
    old_str = f"assets/tool-seeds/{old_name}"
    new_str = f".agent/skills/{new_name}/tools"
    content = content.replace(old_str, new_str)
    
    old_str_win = f"assets\\tool-seeds\\{old_name}"
    new_str_win = f".agent\\skills\\{new_name}\\tools"
    content = content.replace(old_str_win, new_str_win)

with open(BOT_FILE, "w", encoding="utf-8") as f:
    f.write(content)

print("Updated telegram_bot.py paths.")

# 2. Migrate leftover files
for old_name, new_name in mapping.items():
    old_dir = os.path.join(TOOL_SEEDS_DIR, old_name)
    new_dir = os.path.join(AGENT_SKILLS_DIR, new_name, "tools")
    
    if os.path.exists(old_dir):
        for root, dirs, files in os.walk(old_dir):
            for file in files:
                src_file = os.path.join(root, file)
                rel_path = os.path.relpath(src_file, old_dir)
                dest_file = os.path.join(new_dir, rel_path)
                
                os.makedirs(os.path.dirname(dest_file), exist_ok=True)
                if not os.path.exists(dest_file):
                    shutil.copy2(src_file, dest_file)
                    print(f"Migrated: {rel_path} -> {new_name}")

print("Migration complete.")
