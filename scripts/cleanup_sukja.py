import os
import re

def remove_sukja_lines(filepath, patterns):
    if not os.path.exists(filepath):
        return
    with open(filepath, "r", encoding="utf-8") as f:
        lines = f.readlines()
    
    new_lines = []
    skip = False
    for line in lines:
        if any(p in line for p in patterns):
            continue
        new_lines.append(line)
        
    with open(filepath, "w", encoding="utf-8") as f:
        f.writelines(new_lines)

# 1. Delete sukja_profile.json
profile_path = r"d:\ai_lab\.agent\config\sukja_profile.json"
if os.path.exists(profile_path):
    os.remove(profile_path)
    print("Deleted sukja_profile.json")

# 2. Update posting_scheduler.py
posting_file = r"d:\ai_lab\.agent\skills\영숙\tools\posting_scheduler.py"
with open(posting_file, "r", encoding="utf-8") as f:
    p_content = f.read()
# Remove the dictionary for sukja in POSTING_SCHEDULE
p_content = re.sub(r'\s*\{\s*"agent":\s*"숙자".*?\},', '', p_content, flags=re.DOTALL)
# Remove elif for sukja in _get_recent_results
p_content = re.sub(r'\s*elif "숙자" in agent:\s*results\["숙자"\] = meta.get\("title", ""\)', '', p_content)
# Remove comment at the top
p_content = p_content.replace("- 숙자: 블로그 포스팅 (오후 2시)\n", "")
with open(posting_file, "w", encoding="utf-8") as f:
    f.write(p_content)

# 3. Update content_inspector.py
inspector = r"d:\ai_lab\.agent\skills\가희\tools\content_inspector.py"
remove_sukja_lines(inspector, ["숙자 블로그 포스팅 검수", "\"action\": \"숙자\"", "# 3. Blog (숙자)", "Blog (숙자) 검수 중"])

# 4. Update duplicate_guard.py
dup_guard = r"d:\ai_lab\ai-team\_shared\duplicate_guard.py"
remove_sukja_lines(dup_guard, ["숙자 — Blog", "숙자가 최근 N일간"])
# Also replace "아린·숙자·루나" with "아린·루나"
if os.path.exists(dup_guard):
    with open(dup_guard, "r", encoding="utf-8") as f:
        dg = f.read()
    dg = dg.replace("아린·숙자·루나", "아린·루나")
    with open(dup_guard, "w", encoding="utf-8") as f:
        f.write(dg)

# 5. Update image_uploader.py
img_up = r"d:\ai_lab\ai-team\_shared\image_uploader.py"
if os.path.exists(img_up):
    with open(img_up, "r", encoding="utf-8") as f:
        iu = f.read()
    iu = iu.replace("아린·숙자(fix_missing_images", "아린(fix_missing_images")
    with open(img_up, "w", encoding="utf-8") as f:
        f.write(iu)

print("Sukja cleanup completed.")
