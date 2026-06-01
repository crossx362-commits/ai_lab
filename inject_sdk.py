import os
import re

ROOT = r"d:\ai_lab\.agent\skills"

yeongsuk_file = os.path.join(ROOT, "영숙", "SKILL.md")
loyul_file = os.path.join(ROOT, "로율", "SKILL.md")
gahee_file = os.path.join(ROOT, "가희", "SKILL.md")

# Read Yeongsuk
with open(yeongsuk_file, "r", encoding="utf-8") as f:
    ys_lines = f.readlines()

start_idx = 0
end_idx = 0
for i, line in enumerate(ys_lines):
    if "## Google Antigravity SDK" in line:
        start_idx = i
    if "## Section 5. 텔레그램 비서 모드" in line:
        end_idx = i
        break

base_sdk_block = "".join(ys_lines[start_idx:end_idx]).strip()

# 1. Update Yeongsuk to make it blend into her Secretary persona
ys_custom_intro = "비서로서 사장님을 대신해 복잡한 멀티에이전트 시스템을 설계하고 조율합니다. "
ys_new_block = base_sdk_block.replace("Design, implement, and debug", ys_custom_intro + "Design, implement, and debug")

new_ys_lines = ys_lines[:start_idx] + [ys_new_block + "\\n\\n---\\n\\n"] + ys_lines[end_idx:]
with open(yeongsuk_file, "w", encoding="utf-8") as f:
    f.writelines(new_ys_lines)


# 2. Inject to Loyul
loyul_custom_intro = "법률/세무 복합 시뮬레이션 및 규제 검토 파이프라인 자동화를 위해 특화된 하위 에이전트들을 설계하고 조율합니다. "
loyul_new_block = base_sdk_block.replace("Design, implement, and debug", loyul_custom_intro + "Design, implement, and debug")

with open(loyul_file, "r", encoding="utf-8") as f:
    ly_content = f.read()

if "## Google Antigravity SDK" not in ly_content:
    ly_content += f"\\n\\n---\\n\\n{loyul_new_block}\\n"
    with open(loyul_file, "w", encoding="utf-8") as f:
        f.write(ly_content)


# 3. Inject to Gahee
gahee_custom_intro = "콘텐츠 정책 위반 및 품질 검수 프로세스를 다각화하기 위해 특화된 검수 서브 에이전트들을 설계하고 통제합니다. "
gahee_new_block = base_sdk_block.replace("Design, implement, and debug", gahee_custom_intro + "Design, implement, and debug")

with open(gahee_file, "r", encoding="utf-8") as f:
    gh_content = f.read()

if "## Google Antigravity SDK" not in gh_content:
    gh_content += f"\\n\\n---\\n\\n{gahee_new_block}\\n"
    with open(gahee_file, "w", encoding="utf-8") as f:
        f.write(gh_content)

print("Injected SDK skills successfully.")
