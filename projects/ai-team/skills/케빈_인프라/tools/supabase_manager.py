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


def main():
    """CLI 인터페이스"""
    if len(sys.argv) < 2:
        print("사용법:")
        print("  python supabase_manager.py status    - 상태 보고서")
        print("  python supabase_manager.py test      - 연결 테스트")
        print("  python supabase_manager.py schema    - 스키마 동기화 가이드")
        print("  python supabase_manager.py backup    - 설정 백업")
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

    else:
        print(f"❌ 알 수 없는 명령어: {command}")


if __name__ == "__main__":
    main()
