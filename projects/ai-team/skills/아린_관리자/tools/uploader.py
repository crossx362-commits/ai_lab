import os
import sys
import json
import urllib.request
import urllib.parse
import requests
import time

_here_uploader = os.path.dirname(os.path.abspath(__file__))
_ai_team_root = os.path.abspath(os.path.join(_here_uploader, "..", "..", ".."))
if _ai_team_root not in sys.path:
    sys.path.insert(0, _ai_team_root)

from _shared.env_loader import load_env, find_project_root
_root_uploader = find_project_root(_here_uploader)

_APP_ID     = "1219822826776845"
_APP_SECRET = "2b4e0b63ca84558ee64da6e856251235"
_REFRESH_BEFORE_DAYS = 10


def _get_env_path() -> str:
    return os.path.join(_root_uploader, ".env")  # ai_lab root


def _inspect_token(token: str) -> dict:
    """Instagram Graph API로 토큰 유효성 확인 (Facebook debug_token 불사용)."""
    url = f"https://graph.instagram.com/v23.0/me?fields=id,username&access_token={urllib.parse.quote(token)}"
    try:
        with urllib.request.urlopen(url, timeout=10) as r:
            data = json.loads(r.read())
        if data.get("id"):
            return {"is_valid": True, "user_id": data["id"], "username": data.get("username", "")}
        return {"is_valid": False}
    except Exception as e:
        print(f"  [토큰 조회] 실패: {e}")
        return {"is_valid": False}


def _refresh_long_lived(token: str) -> str | None:
    url = (
        "https://graph.instagram.com/refresh_access_token"
        f"?grant_type=ig_refresh_token"
        f"&access_token={urllib.parse.quote(token)}"
    )
    try:
        with urllib.request.urlopen(url, timeout=10) as r:
            return json.loads(r.read()).get("access_token")
    except Exception as e:
        print(f"  [토큰 갱신] 실패: {e}")
        return None


def _update_env_token(env_path: str, new_token: str):
    if not os.path.exists(env_path):
        return
    with open(env_path, "r", encoding="utf-8") as f:
        lines = f.readlines()
    updated = []
    written = False
    for line in lines:
        if line.startswith("INSTAGRAM_ACCESS_TOKEN="):
            updated.append(f'INSTAGRAM_ACCESS_TOKEN="{new_token}"\n')
            written = True
        else:
            updated.append(line)
    if not written:
        updated.append(f'INSTAGRAM_ACCESS_TOKEN="{new_token}"\n')
    with open(env_path, "w", encoding="utf-8") as f:
        f.writelines(updated)
    os.environ["INSTAGRAM_ACCESS_TOKEN"] = new_token
    print("  [아린] 토큰 갱신 완료 → .env 업데이트")


def ensure_token_fresh() -> str:
    """토큰 만료 확인 → 필요 시 자동 갱신. 최신 토큰 반환."""
    load_env()
    env_path = _get_env_path()
    token = os.getenv("INSTAGRAM_ACCESS_TOKEN", "")
    if not token:
        print("  [아린] INSTAGRAM_ACCESS_TOKEN 없음")
        return token

    info = _inspect_token(token)

    if not info.get("is_valid", False):
        # 토큰 무효 → 갱신 강제 시도 (최근 만료된 경우 복구 가능)
        print("  [아린] 토큰 무효 - 갱신 강제 시도 중...")
        new_token = _refresh_long_lived(token)
        if new_token:
            _update_env_token(env_path, new_token)
            print("  [아린] 토큰 재발급 성공 ✅")
            return new_token
        # 갱신 실패 → 텔레그램 긴급 알림
        try:
            import sys as _sys
            _root_path = env_path.replace("/.env", "")
            _sys.path.insert(0, _root_path)
            from _shared.telegram_notifier import send_telegram_message
            send_telegram_message(
                "🚨 <b>[아린]</b> Instagram 토큰 만료 - 즉시 재발급 필요\n\n"
                "📋 재발급 방법:\n"
                "1. developers.facebook.com/tools/explorer\n"
                "2. 앱 선택 → User 토큰 생성\n"
                "3. instagram_basic + instagram_content_publish 권한 추가\n"
                "4. Generate Access Token → .env의 INSTAGRAM_ACCESS_TOKEN 교체"
            )
        except Exception:
            pass
        print("  [아린] 토큰 재발급 실패 - 수동 발급 필요 (텔레그램 알림 발송)")
        return token

    print(f"  [아린] 토큰 유효 (계정: {info.get('username', info.get('user_id', ''))})")

    # 유효한 토큰 → 주기적 갱신으로 60일 유효 기간 유지
    new_token = _refresh_long_lived(token)
    if new_token and new_token != token:
        _update_env_token(env_path, new_token)
        print("  [아린] 토큰 갱신 완료")
        return new_token

    return token


class InstaUploader:
    def __init__(self, account_id, access_token):
        self.acc_id = account_id
        self.token = access_token
        self.version = "v23.0"
        self.base_url = f"https://graph.instagram.com/{self.version}/{self.acc_id}"

    def upload_image(self, image_url, caption="", alt_text=""):
        print(f"📸 1단계: 미디어 컨테이너 생성 요청 중... (버전: {self.version})")
        payload = {
            "image_url": image_url,
            "caption": caption,
            "access_token": self.token,
        }
        if alt_text:
            payload["alt_text"] = alt_text[:150]
        res = requests.post(f"{self.base_url}/media", data=payload).json()

        if "error" in res:
            print("❌ 오류 발생:")
            print(res["error"])
            return None

        creation_id = res.get("id")
        print(f"✅ 1단계 완료: 컨테이너 ID 획득 -> {creation_id}")

        print("⏳ 2단계: 서버 처리 중... 30초 대기합니다.")
        time.sleep(30)

        print("🚀 3단계: 인스타그램 피드 최종 발행 중...")
        publish_res = requests.post(f"{self.base_url}/media_publish", data={
            "creation_id": creation_id,
            "access_token": self.token
        }).json()

        if "error" in publish_res:
            print("❌ 발행 중 오류 발생:")
            print(publish_res["error"])
            return None

        post_id = publish_res.get("id")
        print(f"🎉 업로드 완료! 게시물 ID: {post_id}")
        return post_id


if __name__ == "__main__":
    load_env()
    account_id   = os.getenv("INSTAGRAM_ACCOUNT_ID")
    access_token = os.getenv("INSTAGRAM_ACCESS_TOKEN")
    if not account_id or not access_token:
        print("❌ .env에 INSTAGRAM_ACCOUNT_ID 또는 INSTAGRAM_ACCESS_TOKEN 없음")
        exit(1)
    uploader = InstaUploader(account_id, access_token)
    print("✨ InstaUploader 준비 완료!")
