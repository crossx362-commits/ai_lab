import os
import json
import urllib.request
import urllib.error

def create_notion_page(title: str, markdown_content: str) -> str:
    from _shared.env import load_env
    load_env()
    
    api_key = os.getenv("NOTION_API_KEY", "")
    db_id = os.getenv("NOTION_DATABASE_ID", "")
    
    if not api_key or not db_id:
        return "❌ NOTION_API_KEY 또는 NOTION_DATABASE_ID가 환경변수에 없습니다."
        
    url = "https://api.notion.com/v1/pages"
    
    # Simple block conversion: Split by newlines and create text blocks
    # For a real implementation, a full markdown parser would be used,
    # but here we just convert simple paragraphs to text blocks.
    blocks = []
    lines = markdown_content.split("\n")
    for line in lines:
        text = line.strip()
        if not text:
            continue
        blocks.append({
            "object": "block",
            "type": "paragraph",
            "paragraph": {
                "rich_text": [{"type": "text", "text": {"content": text}}]
            }
        })
        
    # Limit blocks to 100 per request (Notion API limit)
    blocks = blocks[:100]

    payload = {
        "parent": {"database_id": db_id},
        "properties": {
            "title": {
                "title": [{"text": {"content": title}}]
            }
        },
        "children": blocks
    }
    
    headers = {
        "Authorization": f"Bearer {api_key}",
        "Content-Type": "application/json",
        "Notion-Version": "2022-06-28"
    }
    
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(url, data=data, headers=headers)
    
    try:
        with urllib.request.urlopen(req, timeout=15) as r:
            res = json.loads(r.read())
            page_url = res.get("url", "")
            return f"✅ 노션 리포트 작성 완료! 링크: {page_url}"
    except urllib.error.HTTPError as e:
        err = e.read().decode()
        return f"❌ 노션 API 에러: {err}"
    except Exception as e:
        return f"❌ 노션 작성 실패: {e}"
