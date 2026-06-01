import os
import glob
import re
import sys

ROOT = r"d:\ai_lab"

MAPPINGS = {
    "예원": "예원_CEO",
    "영숙": "영숙_비서",
    "루나": "루나_디렉터",
    "아린": "아린_관리자",
    "가희": "가희_검수관",
    "현빈": "현빈_전략가",
    "코다리": "코다리_개발자",
    "경수": "경수_수사관",
    "티모": "티모_디자이너",
    "케빈": "케빈_인프라",
    "로율": "로율_변호사"
}

# 1. Rename directories
skills_dir = os.path.join(ROOT, ".agent", "skills")
for old_name, new_name in MAPPINGS.items():
    old_path = os.path.join(skills_dir, old_name)
    new_path = os.path.join(skills_dir, new_name)
    if os.path.isdir(old_path) and not os.path.exists(new_path):
        os.rename(old_path, new_path)
        print(f"Renamed folder: {old_name} -> {new_name}")

# 2. Update python scripts and other text files that might reference ".agent/skills/old_name"
# We'll use simple search & replace for both forward slash and backward slash variations.
def update_file(filepath):
    try:
        with open(filepath, "r", encoding="utf-8") as f:
            content = f.read()
    except Exception:
        return
    
    orig_content = content
    for old_name, new_name in MAPPINGS.items():
        # Match common string formats in sys.path or OS joins
        # e.g., ".agent/skills/영숙_비서" -> ".agent/skills/영숙_비서"
        content = content.replace(f".agent/skills/{old_name}/", f".agent/skills/{new_name}/")
        content = content.replace(f".agent/skills/{old_name}\"", f".agent/skills/{new_name}\"")
        content = content.replace(f".agent/skills/{old_name}'", f".agent/skills/{new_name}'")
        
        content = content.replace(f".agent\\\\skills\\\\{old_name}\\\\", f".agent\\\\skills\\\\{new_name}\\\\")
        content = content.replace(f".agent\\\\skills\\\\{old_name}\"", f".agent\\\\skills\\\\{new_name}\"")
        content = content.replace(f".agent\\\\skills\\\\{old_name}'", f".agent\\\\skills\\\\{new_name}'")
        
        # OS path joins
        content = content.replace(f'\"skills\", \"{old_name}\"', f'\"skills\", \"{new_name}\"')
        content = content.replace(f'\'skills\', \'{old_name}\'', f'\'skills\', \'{new_name}\'')
    
    if orig_content != content:
        with open(filepath, "w", encoding="utf-8") as f:
            f.write(content)
        print(f"Updated references in {filepath}")

search_patterns = [
    os.path.join(ROOT, "*.py"),
    os.path.join(ROOT, "ai-team", "_shared", "*.py"),
    os.path.join(ROOT, "ai-team", "_shared", "*.md"),
    os.path.join(ROOT, "ai-team", "src", "*.ts"),
    os.path.join(ROOT, "ai-team", "README.md")
]

# Add all .py files in .agent/skills recursively
for new_name in MAPPINGS.values():
    search_patterns.append(os.path.join(skills_dir, new_name, "**", "*.py"))
    search_patterns.append(os.path.join(skills_dir, new_name, "*.md"))

import glob
for pattern in search_patterns:
    for filepath in glob.glob(pattern, recursive=True):
        if os.path.isfile(filepath):
            update_file(filepath)

print("Finished renaming and patching references.")
