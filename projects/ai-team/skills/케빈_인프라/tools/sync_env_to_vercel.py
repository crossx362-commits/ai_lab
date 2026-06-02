#!/usr/bin/env python3
"""
환경 변수를 Vercel 프로젝트에 동기화
"""
import os
import sys
import json
import urllib.request
import urllib.parse

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8')
    sys.stderr.reconfigure(encoding='utf-8')

# .env 파일 로드
def load_env():
    env_vars = {}
    # skills/케빈_인프라/tools/ → ai_lab/ (.env 최상위 위치)
    root_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", "..", ".."))
    env_path = os.path.join(root_dir, ".env")

    if not os.path.exists(env_path):
        print(f"❌ .env 파일이 없습니다: {env_path}")
        print("   먼저 복호화하세요: python decrypt_env.py")
        sys.exit(1)

    with open(env_path, "r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line and not line.startswith("#") and "=" in line:
                key, value = line.split("=", 1)
                value = value.strip('"').strip("'")
                env_vars[key] = value

    return env_vars

# Vercel에 환경 변수 추가
def add_env_to_vercel(project_id, token, team_id, key, value, env_type="production"):
    url = f"https://api.vercel.com/v10/projects/{project_id}/env"
    if team_id:
        url += f"?teamId={team_id}"

    data = {
        "key": key,
        "value": value,
        "type": "sensitive",  # Vercel v10 recommendation
        "target": [env_type]
    }

    req = urllib.request.Request(
        url,
        data=json.dumps(data).encode(),
        headers={
            "Authorization": f"Bearer {token}",
            "Content-Type": "application/json"
        },
        method="POST"
    )

    try:
        with urllib.request.urlopen(req, timeout=10) as r:
            result = json.loads(r.read())
            return True, result
    except urllib.error.HTTPError as e:
        error_body = e.read().decode()
        return False, error_body

# Vercel 기존 환경 변수 조회
def get_existing_envs(project_id, token, team_id):
    url = f"https://api.vercel.com/v9/projects/{project_id}"
    if team_id:
        url += f"?teamId={team_id}"

    req = urllib.request.Request(
        url,
        headers={
            "Authorization": f"Bearer {token}"
        }
    )

    try:
        with urllib.request.urlopen(req, timeout=10) as r:
            result = json.loads(r.read())
            envs = result.get("env", [])
            return {e["key"]: e["id"] for e in envs}
    except Exception as e:
        print(f"⚠️  기존 환경 변수 목록 조회 실패: {e}")
        return {}

# Vercel 환경 변수 수정 (PATCH)
def update_env_on_vercel(project_id, token, team_id, env_id, key, value, env_type="production"):
    url = f"https://api.vercel.com/v10/projects/{project_id}/env/{env_id}"
    if team_id:
        url += f"?teamId={team_id}"

    data = {
        "value": value,
        "target": [env_type]
    }

    req = urllib.request.Request(
        url,
        data=json.dumps(data).encode(),
        headers={
            "Authorization": f"Bearer {token}",
            "Content-Type": "application/json"
        },
        method="PATCH"
    )

    try:
        with urllib.request.urlopen(req, timeout=10) as r:
            result = json.loads(r.read())
            return True, result
    except urllib.error.HTTPError as e:
        error_body = e.read().decode()
        return False, error_body

def main():
    print("=" * 60)
    print("🔧 Vercel 환경 변수 동기화")
    print("=" * 60)

    # .env 로드
    env_vars = load_env()

    # Vercel 프로젝트 정보
    project_id = env_vars.get("VERCEL_PROJECT_ID", "prj_SMZLMnPbKjrlBUZkA0zcfFbivfBI")
    vercel_token = env_vars.get("VERCEL_TOKEN")
    team_id = env_vars.get("VERCEL_TEAM_ID", "team_pbxOKG7NUl0T3PETu4yIpmCv")

    if not vercel_token:
        print("❌ VERCEL_TOKEN이 없습니다.")
        sys.exit(1)

    # Vercel에 업로드할 환경 변수 목록 (VERCEL_OIDC_TOKEN 제외)
    upload_vars = [
        "VERCEL_TOKEN",
        "VERCEL_TEAM_ID",
        "BLOB_READ_WRITE_TOKEN",
        "CRON_SECRET",
        "SUPABASE_URL",
        "SUPABASE_ANON_KEY",
        "GEMINI_API_KEY",
        "YOUTUBE_API_KEY",
        "INSTAGRAM_APP_ID",
        "INSTAGRAM_APP_SECRET",
        "INSTAGRAM_ACCESS_TOKEN",
        "INSTAGRAM_ACCOUNT_ID",
        "TELEGRAM_BOT_TOKEN",
        "TELEGRAM_CHAT_ID"
    ]

    print(f"\n프로젝트 ID: {project_id}")
    print(f"팀 ID: {team_id}")
    print("기존 환경 변수 목록 조회 중...")
    existing_envs = get_existing_envs(project_id, vercel_token, team_id)
    print(f"조회 완료 (기존 변수 {len(existing_envs)}개 감지)")
    print(f"동기화할 변수: {len(upload_vars)}개\n")

    success_count = 0
    fail_count = 0

    for key in upload_vars:
        value = env_vars.get(key)

        if not value:
            print(f"⚠️  {key}: 값 없음 (건너뜀)")
            continue

        if key in existing_envs:
            print(f"🔄 {key} 업데이트...", end=" ")
            success, result = update_env_on_vercel(project_id, vercel_token, team_id, existing_envs[key], key, value)
        else:
            print(f"📤 {key} 추가...", end=" ")
            success, result = add_env_to_vercel(project_id, vercel_token, team_id, key, value)

        if success:
            print("✅")
            success_count += 1
        else:
            print(f"❌ {result}")
            fail_count += 1

    print("\n" + "=" * 60)
    print(f"✅ 성공: {success_count}개")
    print(f"❌ 실패: {fail_count}개")
    print("=" * 60)

    if fail_count > 0:
        print("\n⚠️  일부 변수가 실패했습니다.")
        print("   Vercel 대시보드에서 수동으로 추가하세요:")
        print("   https://vercel.com/crossx362-s-projects/petnna/settings/environment-variables")

    return 0 if fail_count == 0 else 1

if __name__ == "__main__":
    sys.exit(main())
