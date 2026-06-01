import os

# 1. Update README.md
readme_path = r"d:\ai_lab\ai-team\README.md"
with open(readme_path, "r", encoding="utf-8") as f:
    readme_content = f.read()
readme_content = readme_content.replace(
    "**로율** (세무사) | 재무/세무 컨설턴트 | 상속세 및 증여세 시뮬레이션·세금 계산",
    "**로율** (변호사/세무사) | 법률·세무 스마트 어시스턴트 | 상속/가족분쟁 민법 분석 및 세무 시뮬레이션"
)
with open(readme_path, "w", encoding="utf-8") as f:
    f.write(readme_content)

# 2. Update telegram_bot.py
bot_path = r"d:\ai_lab\.agent\skills\예원\tools\telegram_bot.py"
with open(bot_path, "r", encoding="utf-8") as f:
    bot_content = f.read()

bot_content = bot_content.replace(
    "- 로율: 상속세/증여세 시뮬레이션 및 세무 상담",
    "- 로율: 상속/가족분쟁 민법 자문 및 세무 시뮬레이션 (변호사/세무사)"
)
bot_content = bot_content.replace(
    "/tax [금액] — 로율 세무 시뮬레이션",
    "/tax [금액] — 로율 법률·세무 시뮬레이션"
)
bot_content = bot_content.replace(
    "로율 세무 시뮬레이션",
    "로율 법률·세무 시뮬레이션"
)
with open(bot_path, "w", encoding="utf-8") as f:
    f.write(bot_content)

print("Updated Loyul's persona to include Lawyer in README and telegram_bot.py")
