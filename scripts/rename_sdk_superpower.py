import os

ROOT = r"d:\ai_lab\.agent\skills"

yeongsuk_file = os.path.join(ROOT, "영숙", "SKILL.md")
loyul_file = os.path.join(ROOT, "로율", "SKILL.md")
gahee_file = os.path.join(ROOT, "가희", "SKILL.md")

def rename_skill(filepath):
    if not os.path.exists(filepath):
        return
    with open(filepath, "r", encoding="utf-8") as f:
        content = f.read()
    
    old_title = "## Google Antigravity SDK (슈퍼 파워 에이전트) 스킬"
    new_title = "## 슈퍼파워 스킬"
    
    if old_title in content:
        content = content.replace(old_title, new_title)
        
    with open(filepath, "w", encoding="utf-8") as f:
        f.write(content)

rename_skill(yeongsuk_file)
rename_skill(loyul_file)
rename_skill(gahee_file)

print("Renamed skill title to 슈퍼파워 스킬.")
