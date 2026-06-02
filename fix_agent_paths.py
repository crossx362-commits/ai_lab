#!/usr/bin/env python3
import os, re

AI_TEAM = "/Users/junholee/ai_lab/projects/ai-team"

FILE_DEPTHS = {
    "skills/루나_디렉터/tools/audit_output.py": 3,
    "skills/루나_디렉터/tools/veo_video_maker.py": 3,
    "skills/루나_디렉터/tools/lyria_music_gen.py": 3,
    "skills/루나_디렉터/tools/update_titles.py": 3,
    "skills/케빈_인프라/tools/vercel_manager.py": 3,
    "skills/가희_검수관/tools/fix_issues.py": 3,
    "skills/아린_관리자/tools/uploader.py": 3,
    "skills/코다리_개발자/tools/ollama_health_check.py": 3,
    "skills/코다리_개발자/tools/lint_test.py": 3,
    "skills/코다리_개발자/tools/telegram_health_check.py": 3,
    "skills/코다리_개발자/tools/agent_health_check.py": 3,
    "skills/코다리_개발자/tools/instagram_token_refresher.py": 3,
    "skills/코다리_개발자/tools/mermaid_generator.py": 3,
    "skills/루나_디렉터/tools/src/update_scheduled_descriptions.py": 4,
}

REPLACEMENTS = [
    ('".agent", "memory", "upload_history.json"',
     '"reports", "history", "upload_history.json"'),
    ('".agent", "memory", "luna_research.json"',
     '"reports", "research", "luna_research.json"'),
    ('".agent", "memory", "arin_research.json"',
     '"reports", "research", "arin_research.json"'),
    ('".agent", "memory", "hyunbin_research.json"',
     '"reports", "research", "hyunbin_research.json"'),
    ('".agent", "memory", "gahee_inspection_log.jsonl"',
     '"reports", "learning", "gahee_inspection_log.jsonl"'),
    ('".agent", "skills", "영숙_비서", "tools", "telegram_receiver.py"',
     '"projects", "ai-team", "skills", "영숙_비서", "tools", "telegram_receiver.py"'),
    ('".agent", "skills", "영숙_비서", "tools", "telegram_receiver.log"',
     '"projects", "ai-team", "skills", "영숙_비서", "tools", "telegram_receiver.log"'),
    ('".agent", "credentials", "youtube_token.pickle"',
     '"projects", "ai-team", "skills", "루나_디렉터", "tools", "youtube_token.pickle"'),
    ('".agent", "credentials", "youtube_token_update.pickle"',
     '"projects", "ai-team", "skills", "루나_디렉터", "tools", "youtube_token_update.pickle"'),
    ('".agent", "credentials", "token_sukja.pickle"',
     '"projects", "ai-team", "skills", "루나_디렉터", "tools", "token_sukja.pickle"'),
    ('".agent", "credentials", "client_secret.json"',
     '"projects", "ai-team", "skills", "루나_디렉터", "tools", "client_secret.json"'),
]

LOOP_RE = re.compile(
    r'(_root\s*=\s*(?:_here|os\.path\.abspath[^\n]+))\n'
    r'(?:for _ in range\(\d+\):\s*\n'
    r'    if os\.path\.(?:isdir|isfile|exists)\(os\.path\.join\(_root[^)]+\):\s*\n'
    r'        break\s*\n'
    r'    _root\s*=\s*os\.path\.dirname\(_root\)\s*\n)',
    re.MULTILINE
)

for rel_path, depth in FILE_DEPTHS.items():
    fpath = os.path.join(AI_TEAM, rel_path)
    if not os.path.exists(fpath):
        print(f"SKIP (not found): {rel_path}")
        continue
    with open(fpath, "r", encoding="utf-8") as f:
        content = f.read()
    orig = content

    dots = ", ".join(['".."] ' for _ in range(depth)]).strip()
    dots = ", ".join(['".."]' for _ in range(depth)]).replace('".."]', '".."')

    replacement = (
        f'_ai_team_root = os.path.abspath(os.path.join(_here, '
        + ", ".join(['".."] ' for _ in range(depth)]).strip().rstrip(",")
        + "))\n"
        + "if _ai_team_root not in sys.path:\n"
        + "    sys.path.insert(0, _ai_team_root)\n"
        + "from _shared.env_loader import find_project_root\n"
        + "_root = find_project_root(_here)\n"
    )

    content = LOOP_RE.sub(replacement, content)

    # Remove leftover sys.path.insert(_root) lines
    content = re.sub(r'^sys\.path\.insert\(0,\s*_root\)\s*\n', '', content, flags=re.MULTILINE)
    content = re.sub(r'^sys\.path\.insert\(0,\s*os\.path\.join\(_root,\s*[\'"]ai-team[\'"]\)\)\s*\n', '', content, flags=re.MULTILINE)

    for old, new in REPLACEMENTS:
        content = content.replace(old, new)

    if content != orig:
        with open(fpath, "w", encoding="utf-8") as f:
            f.write(content)
        print(f"FIXED: {rel_path}")
    else:
        print(f"NO CHANGE: {rel_path}")

print("Done.")
