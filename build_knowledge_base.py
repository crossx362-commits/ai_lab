import os
import json
import re

ROOT_DIR = r"d:\ai_lab"
SHARED_DIR = os.path.join(ROOT_DIR, "ai-team", "_shared")
AGENT_DIR = os.path.join(ROOT_DIR, ".agent")

# 1. Create knowledge_base.py
kb_code = """import os
import json
import datetime
import glob

def get_kb_dir():
    _here = os.path.dirname(os.path.abspath(__file__))
    _root = _here
    for _ in range(6):
        if os.path.isdir(os.path.join(_root, ".agent")):
            break
        _root = os.path.dirname(_root)
    kb_path = os.path.join(_root, ".agent", "memory", "knowledge_base")
    os.makedirs(kb_path, exist_ok=True)
    return kb_path

def store_knowledge(agent_name: str, topic: str, content: str, tags: list = None):
    kb_path = get_kb_dir()
    now_str = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    safe_topic = "".join([c if c.isalnum() else "_" for c in topic])[:30]
    filename = f"{now_str}_{agent_name}_{safe_topic}.md"
    file_path = os.path.join(kb_path, filename)
    
    # Write MD file
    with open(file_path, "w", encoding="utf-8") as f:
        f.write(f"# {topic}\\n")
        f.write(f"**Agent**: {agent_name}\\n")
        f.write(f"**Date**: {now_str}\\n")
        if tags:
            f.write(f"**Tags**: {', '.join(tags)}\\n")
        f.write("\\n---\\n\\n")
        f.write(content)
        
    # Update Index JSON
    index_path = os.path.join(kb_path, "_index.json")
    index_data = []
    if os.path.exists(index_path):
        try:
            with open(index_path, "r", encoding="utf-8") as f:
                index_data = json.load(f)
        except Exception:
            pass
            
    index_data.append({
        "file": filename,
        "agent": agent_name,
        "topic": topic,
        "tags": tags or [],
        "timestamp": now_str
    })
    
    with open(index_path, "w", encoding="utf-8") as f:
        json.dump(index_data, f, ensure_ascii=False, indent=2)
        
    return file_path

def search_knowledge(query: str, limit: int = 5):
    kb_path = get_kb_dir()
    index_path = os.path.join(kb_path, "_index.json")
    if not os.path.exists(index_path):
        return []
        
    try:
        with open(index_path, "r", encoding="utf-8") as f:
            index_data = json.load(f)
    except Exception:
        return []
        
    results = []
    q = query.lower()
    # Search backwards for recency
    for item in reversed(index_data):
        if q in item["topic"].lower() or any(q in t.lower() for t in item.get("tags", [])):
            file_path = os.path.join(kb_path, item["file"])
            if os.path.exists(file_path):
                with open(file_path, "r", encoding="utf-8") as f:
                    snippet = f.read()[:500]
                results.append({
                    "topic": item["topic"],
                    "agent": item["agent"],
                    "snippet": snippet
                })
        if len(results) >= limit:
            break
            
    return results
"""
kb_path = os.path.join(SHARED_DIR, "knowledge_base.py")
with open(kb_path, "w", encoding="utf-8") as f:
    f.write(kb_code)
print("Created knowledge_base.py")

# 2. Append to 공통_스킬_지식.md
shared_md = os.path.join(SHARED_DIR, "공통_스킬_지식.md")
kb_docs = """
---

## 11. 통합 에이전트 지식 베이스 (Knowledge Base)

모든 에이전트는 리서치, 웹 검색, 트렌드 분석 등 새로운 정보나 인사이트를 학습했을 경우, 반드시 이를 휘발시키지 않고 중앙 지식 베이스에 영구 저장해야 한다. 이렇게 저장된 지식은 추후 다른 에이전트가 `search_knowledge`를 통해 호출하여 자신의 컨텍스트로 활용할 수 있다.

```python
from _shared.knowledge_base import store_knowledge, search_knowledge

# 지식 저장 (리서치 종료 시 필수 호출)
store_knowledge(
    agent_name="현빈", 
    topic="2026 하반기 B2B SaaS 트렌드", 
    content="상세 분석 리포트 내용...", 
    tags=["SaaS", "B2B", "Trend"]
)

# 지식 검색 (기획 전 과거 자료 참조 시 호출)
past_insights = search_knowledge("SaaS 트렌드")
```
"""
with open(shared_md, "a", encoding="utf-8") as f:
    f.write(kb_docs)
print("Updated 공통_스킬_지식.md")

# 3. Patch 현빈 deep_search_6h.py
ds_path = os.path.join(AGENT_DIR, "skills", "현빈_전략가", "tools", "deep_search_6h.py")
if os.path.exists(ds_path):
    with open(ds_path, "r", encoding="utf-8") as f:
        content = f.read()
    if "from _shared.knowledge_base import store_knowledge" not in content:
        content = content.replace("from _shared.env_loader import load_env", "from _shared.env_loader import load_env\\nfrom _shared.knowledge_base import store_knowledge")
        content = content.replace(
            "send_telegram_message(f\"🏁 <b>6시간 딥서치 종료</b>",
            "store_knowledge('현빈', f'DeepSearch: {custom_topic}', final_report, ['DeepSearch', 'Strategy'])\\n    send_telegram_message(f\"🏁 <b>6시간 딥서치 종료</b>"
        )
        with open(ds_path, "w", encoding="utf-8") as f:
            f.write(content)
        print("Patched deep_search_6h.py")

# 4. Patch 현빈 business_research.py
br_path = os.path.join(AGENT_DIR, "skills", "현빈_전략가", "tools", "business_research.py")
if os.path.exists(br_path):
    with open(br_path, "r", encoding="utf-8") as f:
        content = f.read()
    if "from _shared.knowledge_base import store_knowledge" not in content:
        content = content.replace("import sys\\nimport json", "import sys\\nimport json\\nfrom _shared.knowledge_base import store_knowledge")
        content = content.replace(
            "send_telegram_message(msg)",
            "store_knowledge('현빈', 'Business Research Hourly', report, ['Business', 'Hourly'])\\n    send_telegram_message(msg)"
        )
        with open(br_path, "w", encoding="utf-8") as f:
            f.write(content)
        print("Patched business_research.py")

# 5. Patch 아린 image_research.py
ar_path = os.path.join(AGENT_DIR, "skills", "아린_관리자", "tools", "image_research.py")
if os.path.exists(ar_path):
    with open(ar_path, "r", encoding="utf-8") as f:
        content = f.read()
    if "from _shared.knowledge_base import store_knowledge" not in content:
        content = content.replace("import json\\nimport datetime", "import json\\nimport datetime\\nfrom _shared.knowledge_base import store_knowledge")
        content = content.replace(
            "send_telegram_message(msg)",
            "store_knowledge('아린', 'Instagram Trend Research', report, ['Trend', 'Instagram'])\\n    send_telegram_message(msg)"
        )
        with open(ar_path, "w", encoding="utf-8") as f:
            f.write(content)
        print("Patched image_research.py")
        
# 6. Patch 루나 youtube_research.py
lr_path = os.path.join(AGENT_DIR, "skills", "루나_디렉터", "tools", "src", "youtube_research.py")
if os.path.exists(lr_path):
    with open(lr_path, "r", encoding="utf-8") as f:
        content = f.read()
    if "from _shared.knowledge_base import store_knowledge" not in content:
        content = content.replace("import os\\nimport sys\\nimport json", "import os\\nimport sys\\nimport json\\nfrom _shared.knowledge_base import store_knowledge")
        content = content.replace(
            "send_telegram_message(msg)",
            "store_knowledge('루나', 'YouTube Music/Video Trend', report, ['Trend', 'YouTube'])\\n    send_telegram_message(msg)"
        )
        with open(lr_path, "w", encoding="utf-8") as f:
            f.write(content)
        print("Patched youtube_research.py")

print("All patches completed successfully.")
