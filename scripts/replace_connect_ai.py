import os

root_dir = r"d:\ai_lab\ai-team"
exclude_dirs = {".git", "node_modules", "out", "dist"}

def process_file(filepath):
    try:
        with open(filepath, "r", encoding="utf-8") as f:
            content = f.read()
    except Exception:
        return # Skip binary or unreadable files

    if "connect-ai" in content or "Connect AI" in content or "Connect-AI" in content or "connect_ai" in content:
        new_content = content.replace("connect-ai", "ai-team")
        new_content = new_content.replace("Connect AI", "AI Team")
        new_content = new_content.replace("Connect-AI", "AI-Team")
        new_content = new_content.replace("connect_ai", "ai_team")
        
        with open(filepath, "w", encoding="utf-8") as f:
            f.write(new_content)
        print(f"Updated: {filepath}")

for dirpath, dirnames, filenames in os.walk(root_dir):
    dirnames[:] = [d for d in dirnames if d not in exclude_dirs]
    for filename in filenames:
        if filename.endswith(('.json', '.ts', '.js', '.md', '.py', '.txt', '.tsx', '.html', '.css', '.bat', '.sh')):
            process_file(os.path.join(dirpath, filename))

print("Replacement complete.")
