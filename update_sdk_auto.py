import os

ROOT = r"d:\ai_lab\.agent\skills"

yeongsuk_file = os.path.join(ROOT, "영숙", "SKILL.md")
loyul_file = os.path.join(ROOT, "로율", "SKILL.md")
gahee_file = os.path.join(ROOT, "가희", "SKILL.md")

def update_skill_auto(filepath):
    if not os.path.exists(filepath):
        return
    with open(filepath, "r", encoding="utf-8") as f:
        content = f.read()
    
    # We want to replace the ACTIVATE condition
    # Old: ACTIVATE this skill when the user wants to create, configure, or orchestrate Google Antigravity agents.
    # New: AUTOMATICALLY USE this skill and its tools whenever you need to break down complex tasks, orchestrate workflows, or write agent code. Do not wait for the user to explicitly request it.
    
    old_text1 = "ACTIVATE this skill when the user wants to create, configure, or orchestrate Google Antigravity agents."
    old_text2 = "ACTIVATE this skill when the user wants to"
    
    new_text = "AUTOMATICALLY USE this skill and its tools to spawn sub-agents, orchestrate workflows, and build systems on your own initiative whenever faced with a complex task. Do not wait for explicit user permission to use Antigravity."
    
    if old_text1 in content:
        content = content.replace(old_text1, new_text)
    elif old_text2 in content:
        # fallback regex or simple replace
        import re
        content = re.sub(r'ACTIVATE this skill when the user wants to.*agents\.', new_text, content)
        
    with open(filepath, "w", encoding="utf-8") as f:
        f.write(content)

update_skill_auto(yeongsuk_file)
update_skill_auto(loyul_file)
update_skill_auto(gahee_file)

print("Updated Antigravity skills to be used automatically.")
