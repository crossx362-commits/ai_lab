import os
import re

bot_file = r"d:\ai_lab\.agent\skills\예원\tools\telegram_bot.py"
with open(bot_file, "r", encoding="utf-8") as f:
    content = f.read()

# 1. Update Yewon dispatch prompt
old_prompt = """팀원 역할:
- 아린: 인스타그램 이미지 생성·포스팅
- 루나: 유튜브 뮤직비디오 제작·업로드
- 코다리: 코딩·개발·웹 구축
- 현빈: 비즈니스 리서치·전략 분석
- 영숙: 유튜브 추천·개인 비서 업무
- 예원: 일정 관리, 현황 보고, 일반 대화"""

new_prompt = """팀원 역할:
- 아린: 인스타그램 이미지 생성·포스팅
- 루나: 유튜브 뮤직비디오 제작·업로드
- 코다리: 코딩·개발·웹 구축
- 현빈: 비즈니스 리서치·전략 분석
- 케빈: Vercel 배포 관리 및 서버 클린업
- 로율: 상속세/증여세 시뮬레이션 및 세무 상담
- 영숙: 유튜브 추천·개인 비서 업무
- 예원: 일정 관리, 현황 보고, 일반 대화"""

content = content.replace(old_prompt, new_prompt)

# 2. Add import for new tools at the end of the helper section
import_block = """
# ─── New Agents ──────────────────────────────────────────────────────────────
def _run_hyunbin():
    try:
        import sys
        sys.path.insert(0, os.path.join(PROJECT_ROOT, ".agent", "skills", "현빈_전략가", "tools"))
        import business_research
        return business_research.run_research()
    except Exception as e:
        return f"현빈 리서치 실패: {e}"

def _run_kevin():
    try:
        import sys
        sys.path.insert(0, os.path.join(PROJECT_ROOT, ".agent", "skills", "케빈_인프라", "tools"))
        import vercel_manager
        return vercel_manager.run_vercel_cleanup()
    except Exception as e:
        return f"케빈 클린업 실패: {e}"

def _run_loyul(amount=1000000000):
    try:
        import sys
        sys.path.insert(0, os.path.join(PROJECT_ROOT, ".agent", "skills", "로율_변호사", "tools"))
        import tax_simulator
        return tax_simulator.run_simulation(amount)
    except Exception as e:
        return f"로율 시뮬레이션 실패: {e}"
"""
if "_run_hyunbin" not in content:
    content = content.replace("# ─── Telegram API 헬퍼", import_block + "\n# ─── Telegram API 헬퍼")

# 3. Add to HELP_TEXT
old_help = """/evaluate  — 전체 RL 성과 평가"""
new_help = """/evaluate  — 전체 RL 성과 평가
/research  — 현빈 비즈니스 리서치
/vercel    — 케빈 Vercel 서버 클린업
/tax [금액] — 로율 세무 시뮬레이션"""
content = content.replace(old_help, new_help)

# 4. Modify route() to handle commands
# Looking for cmd_evaluate
route_addition = """
    elif text.startswith("/research"):
        return _run_hyunbin()
    elif text.startswith("/vercel"):
        return _run_kevin()
    elif text.startswith("/tax"):
        parts = text.split()
        amount = parts[1] if len(parts) > 1 else "1000000000"
        return _run_loyul(amount)
"""
# We will inject this via regex into the `route` function
# Let's find: `elif text == "/evaluate":\n        return cmd_evaluate()`
route_search = 'elif text == "/evaluate":\n        return cmd_evaluate()'
content = content.replace(route_search, route_search + route_addition)

# 5. Modify route() to handle Yewon dispatch logic for new agents
dispatch_search = 'elif agent in ("코다리_개발자", "현빈_전략가"):'
dispatch_replace = """elif agent == "현빈" or agent == "현빈_전략가":
            res = _run_hyunbin()
            return f"👑 예원: {yw_reply}\\n\\n{res}" if yw_reply else res
        elif agent == "케빈":
            res = _run_kevin()
            return f"👑 예원: {yw_reply}\\n\\n{res}" if yw_reply else res
        elif agent == "로율":
            res = _run_loyul()
            return f"👑 예원: {yw_reply}\\n\\n{res}" if yw_reply else res
        elif agent == "코다리_개발자":"""
content = content.replace(dispatch_search, dispatch_replace)

with open(bot_file, "w", encoding="utf-8") as f:
    f.write(content)

print("Bot routing updated successfully.")
