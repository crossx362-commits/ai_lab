#!/usr/bin/env python3
"""
Supabase 관리 도구
케빈이 Supabase 프로젝트를 모니터링하고 관리합니다.
"""

import os
import sys
import json
import urllib.request
import urllib.error
from pathlib import Path
from datetime import datetime

# UTF-8 인코딩 설정
if hasattr(sys.stdout, 'reconfigure'):
    try:
        sys.stdout.reconfigure(encoding='utf-8', errors='replace')
        sys.stderr.reconfigure(encoding='utf-8', errors='replace')
    except Exception:
        pass

# 경로 설정
SCRIPT_DIR = Path(__file__).parent
SUPABASE_DIR = SCRIPT_DIR / "supabase"
TEMP_DIR = SUPABASE_DIR / ".temp"
ROOT_DIR = Path(__file__).parent.parent.parent.parent.parent.parent  # tools→케빈→skills→ai-team→projects→ai_lab
ENV_PATH = ROOT_DIR / ".env"


def load_env():
    """환경변수 로드"""
    env_vars = {}
    if ENV_PATH.exists():
        with open(ENV_PATH, 'r', encoding='utf-8') as f:
            for line in f:
                line = line.strip()
                if line and not line.startswith("#") and "=" in line:
                    key, value = line.split("=", 1)
                    value = value.strip('"').strip("'")
                    env_vars[key.strip()] = value
                    os.environ[key.strip()] = value
    return env_vars


def get_project_info():
    """Supabase 프로젝트 정보 가져오기"""
    linked_project = TEMP_DIR / "linked-project.json"

    if not linked_project.exists():
        return None

    with open(linked_project, 'r') as f:
        return json.load(f)


def get_versions():
    """Supabase 버전 정보"""
    versions = {}

    version_files = {
        'postgres': 'postgres-version',
        'cli': 'cli-latest',
        'gotrue': 'gotrue-version',
        'rest': 'rest-version',
        'storage': 'storage-version'
    }

    for key, filename in version_files.items():
        filepath = TEMP_DIR / filename
        if filepath.exists():
            versions[key] = filepath.read_text().strip()

    return versions


def check_supabase_connection():
    """Supabase 연결 테스트"""
    env_vars = load_env()
    supabase_url = env_vars.get('SUPABASE_URL', '')
    supabase_key = env_vars.get('SUPABASE_ANON_KEY', '')

    if not supabase_url or not supabase_key:
        return {
            'status': 'error',
            'message': 'SUPABASE_URL 또는 SUPABASE_ANON_KEY가 설정되지 않음'
        }

    try:
        # Storage 상태 엔드포인트 — anon 키 없이 접근 가능, 프로젝트 활성 여부 확인
        health_url = f"{supabase_url}/storage/v1/status"
        req = urllib.request.Request(health_url)

        with urllib.request.urlopen(req, timeout=8) as response:
            if response.status == 200:
                return {
                    'status': 'ok',
                    'message': 'Supabase 연결 정상 (storage 200)',
                    'url': supabase_url
                }
    except urllib.error.HTTPError as e:
        return {
            'status': 'error',
            'message': f'HTTP Error {e.code}: {e.reason}'
        }
    except Exception as e:
        return {
            'status': 'error',
            'message': f'연결 실패: {str(e)}'
        }


def generate_status_report():
    """Supabase 상태 보고서 생성"""
    report = []
    report.append("🗄️ **Supabase 상태 보고**\n")
    report.append(f"생성 시간: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n")

    # 프로젝트 정보
    project_info = get_project_info()
    if project_info:
        report.append("**프로젝트 정보:**")
        report.append(f"  - 이름: {project_info.get('name', 'N/A')}")
        report.append(f"  - Ref: {project_info.get('ref', 'N/A')}")
        report.append(f"  - Organization: {project_info.get('organization_slug', 'N/A')}")
    else:
        report.append("⚠️ 프로젝트 정보를 찾을 수 없습니다.")

    # 환경변수 확인
    env_vars = load_env()
    report.append("\n**환경변수:**")
    if 'SUPABASE_URL' in env_vars:
        report.append(f"  ✅ SUPABASE_URL: {env_vars['SUPABASE_URL'][:40]}...")
    else:
        report.append("  ❌ SUPABASE_URL: 미설정")

    if 'SUPABASE_ANON_KEY' in env_vars:
        report.append(f"  ✅ SUPABASE_ANON_KEY: {len(env_vars['SUPABASE_ANON_KEY'])} chars")
    else:
        report.append("  ❌ SUPABASE_ANON_KEY: 미설정")

    # 버전 정보
    versions = get_versions()
    if versions:
        report.append("\n**버전 정보:**")
        for key, value in versions.items():
            report.append(f"  - {key.capitalize()}: {value}")

    # 연결 테스트
    connection = check_supabase_connection()
    report.append("\n**연결 상태:**")
    if connection['status'] == 'ok':
        report.append(f"  ✅ {connection['message']}")
    else:
        report.append(f"  ❌ {connection['message']}")

    return "\n".join(report)


def sync_schema():
    """스키마 파일 확인 및 동기화 가이드"""
    schema_file = ROOT_DIR / "projects" / "petnna" / "supabase_schema.sql"

    report = []
    report.append("📋 **스키마 동기화 상태**\n")

    if schema_file.exists():
        report.append(f"✅ 스키마 파일 존재: {schema_file.name}")

        # 파일 크기와 수정 시간
        stat = schema_file.stat()
        mod_time = datetime.fromtimestamp(stat.st_mtime)
        report.append(f"  - 크기: {stat.st_size} bytes")
        report.append(f"  - 최종 수정: {mod_time.strftime('%Y-%m-%d %H:%M:%S')}")

        report.append("\n**동기화 방법:**")
        report.append("```bash")
        report.append("# Supabase 로컬 DB에 적용")
        report.append("cd projects/petnna")
        report.append("supabase db reset")
        report.append("")
        report.append("# 원격 프로덕션에 적용 (주의!)")
        report.append("supabase db push")
        report.append("```")
    else:
        report.append("❌ 스키마 파일이 없습니다.")
        report.append(f"   예상 위치: {schema_file}")

    return "\n".join(report)


def backup_config():
    """Supabase 설정 백업"""
    backup_dir = ROOT_DIR / "reports" / "history"
    backup_file = backup_dir / f"supabase_config_{datetime.now().strftime('%Y%m%d_%H%M%S')}.json"

    config = {
        'timestamp': datetime.now().isoformat(),
        'project': get_project_info(),
        'versions': get_versions(),
        'connection_test': check_supabase_connection()
    }

    backup_dir.mkdir(parents=True, exist_ok=True)
    with open(backup_file, 'w', encoding='utf-8') as f:
        json.dump(config, f, indent=2, ensure_ascii=False)

    return f"✅ 설정 백업 완료: {backup_file.name}"


def set_site_url(site_url: str) -> dict:
    """Supabase Management API를 사용하여 프로젝트의 Site URL 및 redirect URL 목록을 업데이트합니다."""
    env_vars = load_env()
    token = env_vars.get("SUPABASE_ACCESS_TOKEN", "").strip()
    
    if not token:
        return {
            "status": "error",
            "message": "SUPABASE_ACCESS_TOKEN이 .env 파일에 설정되어 있지 않습니다.\n"
                       "🔧 해결 방법:\n"
                       "  1. https://supabase.com/dashboard/account/tokens 에서 개인 엑세스 토큰(PAT)을 생성합니다.\n"
                       "  2. 프로젝트 루트의 .env 파일에 SUPABASE_ACCESS_TOKEN=\"토큰값\" 을 추가합니다."
        }
        
    project_info = get_project_info()
    project_ref = project_info.get("ref") if project_info else env_vars.get("SUPABASE_PROJECT_REF")
    if not project_ref:
        ref_file = TEMP_DIR / "project-ref"
        if ref_file.exists():
            project_ref = ref_file.read_text().strip()
            
    if not project_ref:
        return {
            "status": "error",
            "message": "Supabase 프로젝트 참조 ID(ref)를 찾을 수 없습니다."
        }

    url = f"https://api.supabase.com/v1/projects/{project_ref}/config/auth"
    
    # 리다이렉션 허용 리스트 빌드 (localhost 및 설정할 사이트 URL 포함)
    additional_urls = [
        "http://localhost:3000/**",
        "http://localhost:5173/**",
        f"{site_url.rstrip('/')}/**"
    ]
    
    payload = {
        "site_url": site_url,
        "additional_redirect_urls": additional_urls
    }
    
    try:
        data = json.dumps(payload).encode("utf-8")
        req = urllib.request.Request(
            url,
            data=data,
            headers={
                "Authorization": f"Bearer {token}",
                "Content-Type": "application/json",
                "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
            },
            method="PATCH"
        )
        with urllib.request.urlopen(req, timeout=15) as response:
            result = json.loads(response.read().decode())
            return {
                "status": "ok",
                "message": f"Supabase Site URL이 성공적으로 설정되었습니다: {site_url}\n"
                           f"허용된 리다이렉트 URL: {', '.join(additional_urls)}",
                "data": result
            }
    except urllib.error.HTTPError as e:
        error_body = e.read().decode()
        try:
            err_json = json.loads(error_body)
            msg = err_json.get("message", error_body)
        except Exception:
            msg = error_body
        return {
            "status": "error",
            "message": f"API 오류 (HTTP {e.code}): {msg}"
        }
    except Exception as e:
        return {
            "status": "error",
            "message": f"요청 중 오류 발생: {str(e)}"
        }


def main():
    """CLI 인터페이스"""
    if len(sys.argv) < 2:
        print("사용법:")
        print("  python supabase_manager.py status             - 상태 보고서")
        print("  python supabase_manager.py test               - 연결 테스트")
        print("  python supabase_manager.py schema             - 스키마 동기화 가이드")
        print("  python supabase_manager.py backup             - 설정 백업")
        print("  python supabase_manager.py set-url [site_url]  - Supabase Site URL 설정 (OAuth용)")
        return

    command = sys.argv[1].lower()

    if command == "status":
        print(generate_status_report())

    elif command == "test":
        connection = check_supabase_connection()
        if connection['status'] == 'ok':
            print(f"✅ {connection['message']}")
            print(f"   URL: {connection.get('url', 'N/A')}")
        else:
            print(f"❌ {connection['message']}")

    elif command == "schema":
        print(sync_schema())

    elif command == "backup":
        print(backup_config())

    elif command == "set-url":
        env_vars = load_env()
        default_url = env_vars.get("PETNNA_URL", "https://petnna.vercel.app")
        site_url = sys.argv[2] if len(sys.argv) > 2 else default_url
        
        print(f"Supabase Site URL 설정 시작 (대상: {site_url})...")
        res = set_site_url(site_url)
        if res["status"] == "ok":
            print(f"✅ {res['message']}")
        else:
            print(f"❌ {res['message']}")

    else:
        print(f"❌ 알 수 없는 명령어: {command}")


if __name__ == "__main__":
    main()
