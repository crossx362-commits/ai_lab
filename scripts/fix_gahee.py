import os

fpath = r"d:\ai_lab\.agent\skills\가희\tools\content_inspector.py"
with open(fpath, "r", encoding="utf-8") as f:
    content = f.read()

# Fix sys.path for _shared
if "sys.path.insert(0, os.path.join(_root, 'ai-team'))" not in content:
    content = content.replace("sys.path.insert(0, _root)", "sys.path.insert(0, _root)\nsys.path.insert(0, os.path.join(_root, 'ai-team'))")

# Fix tool-seeds paths
content = content.replace('"assets", "tool-seeds", "가희_검수관"', '".agent", "skills", "가희_검수관", "tools"')

with open(fpath, "w", encoding="utf-8") as f:
    f.write(content)

print("Gahee fixes applied.")
