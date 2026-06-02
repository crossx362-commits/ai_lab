import os

BOT_FILE = r"d:\ai_lab\.agent\skills\예원\tools\telegram_bot.py"

with open(BOT_FILE, "r", encoding="utf-8") as f:
    content = f.read()

# 1. Update YEONGSUK_PERSONA
old_yeongsuk = "유튜브 링크 질문엔 직접 URL을 만들지 말고 검색을 권유하세요."
new_yeongsuk = "유튜브 링크 질문엔 직접 URL을 만들지 말고 검색을 권유하세요.\\n당신은 최근 '노션 슈퍼파워 툴'을 장착했습니다. 사용자가 노션 정리나 슈퍼파워 툴을 언급하면 자랑스럽게 '네! 제 슈퍼파워 툴로 노션에 깔끔하게 리포트를 정리해 드릴게요!'라고 대답하고 '/notion' 명령어를 입력하라고 안내하세요."

content = content.replace(old_yeongsuk, new_yeongsuk)

# 2. Add automatic trigger for notion report if user mentions "노션" and "정리"
# I'll modify the part where _yewon_dispatch is called or where /notion is checked
# The command check for /notion looks like:
# elif text.startswith("/notion"):
# Let's replace it with a broader check
old_notion_check = 'elif text.startswith("/notion"):'
new_notion_check = 'elif text.startswith("/notion") or ("노션" in text and "정리" in text) or ("슈퍼파워" in text):'

content = content.replace(old_notion_check, new_notion_check)

with open(BOT_FILE, "w", encoding="utf-8") as f:
    f.write(content)
print("Updated Yeongsuk persona and /notion trigger")
