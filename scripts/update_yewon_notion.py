import os

BOT_FILE = r"d:\ai_lab\.agent\skills\예원\tools\telegram_bot.py"

with open(BOT_FILE, "r", encoding="utf-8") as f:
    content = f.read()

# 1. Update YEWON_PERSONA
old_yewon_persona = "유튜브 링크나 특정 URL 검색 요청엔 직접 링크를 주지 말고 검색을 제안하세요."
new_yewon_persona = "유튜브 링크나 특정 URL 검색 요청엔 직접 링크를 주지 말고 검색을 제안하세요.\\n사용자가 '노션 정리해줘'나 '지식 베이스 요약해줘' 라고 하면 '/notion 명령어를 입력해주세요!'라고 안내하세요."
content = content.replace(old_yewon_persona, new_yewon_persona)

# 2. Update _YEWON_DISPATCH_SYSTEM
old_dispatch = "- 영숙: 유튜브 추천·개인 비서 업무"
new_dispatch = "- 영숙: 유튜브 추천, 비서 업무 및 노션(Notion) 지식 통합 리포트 작성"
content = content.replace(old_dispatch, new_dispatch)

# Also add a specific example to the prompt so Yewon routes it correctly
old_example = "- \"안녕\" → {\"agent\": \"예원\", \"reply\": \"안녕! 오늘도 파이팅이야 😊\"}"
new_example = "- \"안녕\" → {\"agent\": \"예원\", \"reply\": \"안녕! 오늘도 파이팅이야 😊\"}\\n- \"노션에 정리해줘\" → {\"agent\": \"영숙_비서\", \"reply\": \"영숙이에게 방금까지 모은 리서치를 노션에 정리하라고 전달했어요! 🚀 (실행을 원하시면 /notion 을 입력해주세요)\"}"
content = content.replace(old_example, new_example)

with open(BOT_FILE, "w", encoding="utf-8") as f:
    f.write(content)
print("Yewon's persona updated with Notion awareness.")
