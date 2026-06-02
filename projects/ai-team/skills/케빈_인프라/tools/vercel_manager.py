import os
import sys
import json
import urllib.request

_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, ".agent")):
        break
    _root = os.path.dirname(_root)
sys.path.insert(0, _root)
sys.path.insert(0, os.path.join(_root, 'ai-team'))

from _shared.env_loader import load_env

def run_vercel_cleanup():
    load_env(_root)
    token = os.getenv("VERCEL_TOKEN")
    team_id = os.getenv("VERCEL_TEAM_ID")
    
    if not token:
        return "❌ VERCEL_TOKEN이 설정되지 않았습니다."
        
    team_query = f"?teamId={team_id}" if team_id else ""
    headers = {
        "Authorization": f"Bearer {token}",
        "Content-Type": "application/json"
    }
    
    output = ["🛠️ **[케빈의 Vercel 클린업 보고서]**"]
    
    # 1. 이전 배포본(오래된 프로젝트) 조회
    try:
        req = urllib.request.Request(f"https://api.vercel.com/v9/projects{team_query}", headers=headers)
        with urllib.request.urlopen(req, timeout=10) as r:
            res = json.loads(r.read())
            
        projects = res.get("projects", [])
        temp_projects = [p for p in projects if p.get("name", "").startswith("temp-project-")]
        
        if not temp_projects:
            output.append("✅ 삭제할 임시 프로젝트(temp-project-)가 없습니다.")
        else:
            deleted = 0
            for p in temp_projects:
                try:
                    del_req = urllib.request.Request(
                        f"https://api.vercel.com/v9/projects/{p['id']}{team_query}", 
                        headers=headers, 
                        method="DELETE"
                    )
                    with urllib.request.urlopen(del_req, timeout=10) as dr:
                        if dr.status in (200, 204):
                            deleted += 1
                except Exception as e:
                    output.append(f"⚠️ 프로젝트 {p['name']} 삭제 실패: {e}")
            output.append(f"🗑️ 총 {deleted}개의 임시 프로젝트를 삭제했습니다.")
            
    except Exception as e:
        output.append(f"❌ Vercel API 호출 실패: {e}")
        
    output.append("\n💡 (참고) Blob 스토리지 정리는 `/api/cleanup-projects` 크론 작업으로 위임되어 처리됩니다.")
    return "\n".join(output)

if __name__ == "__main__":
    print(run_vercel_cleanup())
