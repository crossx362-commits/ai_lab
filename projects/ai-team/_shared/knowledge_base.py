import os
import json
import datetime
import glob

def get_kb_dir():
    _here = os.path.dirname(os.path.abspath(__file__))
    try:
        from env_loader import find_project_root
    except ImportError:
        from _shared.env_loader import find_project_root
    kb_path = os.path.join(find_project_root(_here), "reports", "knowledge_base")
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
        f.write(f"# {topic}\n")
        f.write(f"**Agent**: {agent_name}\n")
        f.write(f"**Date**: {now_str}\n")
        if tags:
            f.write(f"**Tags**: {', '.join(tags)}\n")
        f.write("\n---\n\n")
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
