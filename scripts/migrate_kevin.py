import os
import shutil

ROOT_DIR = r"d:\ai_lab"
KEVIN_SKILL = os.path.join(ROOT_DIR, ".agent", "skills", "케빈_인프라", "SKILL.md")
KEVIN_TOOLS = os.path.join(ROOT_DIR, ".agent", "skills", "케빈_인프라", "tools")
BOT_FILE = os.path.join(ROOT_DIR, ".agent", "skills", "예원_CEO", "tools", "telegram_bot.py")

# 1. Update SKILL.md
with open(KEVIN_SKILL, "r", encoding="utf-8") as f:
    skill_content = f.read()

if "Supabase" not in skill_content:
    skill_content = skill_content.replace(
        "Vercel 배포 파이프라인 최적화", 
        "Vercel 프론트엔드 및 Supabase 백엔드 인프라, 환경변수 제어 파이프라인 최적화"
    )
    skill_content = skill_content.replace(
        "클라우드 인프라(Vercel) 프로비저닝",
        "클라우드 인프라(Vercel, Supabase) 프로비저닝"
    )
    # Add a section for Supabase
    supabase_section = """
## Supabase 백엔드 인프라 관리 (Supabase Management)
* **스키마 마이그레이션 및 상태 동기화**: 프론트엔드와 연결되는 Supabase Database의 스키마 상태를 버전 관리하고 안정적으로 유지한다.
* **환경 변수 제어**: Vercel과 Supabase 간의 API Key, JWT Secret 등 기밀 정보 연동(sync_env_to_vercel)을 철저하게 관리하고 정기 보안 감사를 지원한다.
"""
    skill_content += "\n\n" + supabase_section
    with open(KEVIN_SKILL, "w", encoding="utf-8") as f:
        f.write(skill_content)
    print("Updated Kevin SKILL.md")

# 2. Move files
scripts_to_move = [
    "sync_env_to_vercel.py",
    "encrypt_env.py",
    "debug_env.py",
    "test_env_vars.py",
    "test_env_loader_direct.py",
    "parse_env_test.py"
]

for s in scripts_to_move:
    src = os.path.join(ROOT_DIR, s)
    dst = os.path.join(KEVIN_TOOLS, s)
    if os.path.exists(src):
        shutil.move(src, dst)
        print(f"Moved {s} to Kevin's tools")

# 3. Move Supabase folder
supabase_src = os.path.join(ROOT_DIR, "supabase")
supabase_dst = os.path.join(KEVIN_TOOLS, "supabase")
if os.path.exists(supabase_src):
    shutil.move(supabase_src, supabase_dst)
    print("Moved supabase folder to Kevin's tools")

# 4. Update telegram_bot.py
with open(BOT_FILE, "r", encoding="utf-8") as f:
    bot_content = f.read()

if "/vercel" in bot_content:
    # Change /vercel command in help text
    bot_content = bot_content.replace("/vercel    — 케빈 Vercel/서버 리포트", "/infra     — 케빈 Vercel/Supabase 서버 인프라 리포트")
    # Actually I should just do a generic replace for /vercel -> /infra for routing
    bot_content = bot_content.replace('elif text.startswith("/vercel"):', 'elif text.startswith("/infra"):')
    
    # Check if there is an explicit mention of vercel manager
    with open(BOT_FILE, "w", encoding="utf-8") as f:
        f.write(bot_content)
    print("Updated telegram_bot.py routing (/vercel -> /infra)")

