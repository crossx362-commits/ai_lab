"""
instagram_token_refresher.py — Instagram 장기 액세스 토큰 자동 갱신
- 현재 토큰 만료까지 10일 이하 남으면 자동 갱신 후 .env 업데이트
- telegram_bot.py _kodari_health_loop에서 매일 1회 호출
- 수동 실행: python instagram_token_refresher.py
"""
import os
import sys
import json
import urllib.request
import urllib.parse

_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, ".agent")):
        break
    _root = os.path.dirname(_root)
from _shared.env_loader import load_env as _load_env
from _shared.telegram_notifier import send_telegram_message

_ENV_PATH = os.path.join(_root, ".env")
_APP_ID     = "1219822826776845"
_APP_SECRET = "2b4e0b63ca84558ee64da6e856251235"
_REFRESH_BEFORE_DAYS = 10  # 만료 N일 전부터 갱신 시도


def _inspect_token(token: str) -> dict:
    """Instagram Graph API로 토큰 유효성 확인 (Facebook debug_token 불사용)."""
    url = f"https://graph.instagram.com/v23.0/me?fields=id,username&access_token={urllib.parse.quote(token)}"
    try:
        with urllib.request.urlopen(url, timeout=10) as r:
            data = json.loads(r.read())
        if data.get("id"):
            return {"is_valid": True, "type": "USER", "user_id": data["id"],
                    "username": data.get("username", "")}
        return {"is_valid": False}
    except Exception as e:
        print(f"  [토큰 조회] 실패: {e}")
        return {"is_valid": False}


def _exchange_to_long_lived(short_token: str) -> str | None:
    """단기 User 토큰 → 장기 토큰(60일) 교환."""
    url = (
        "https://graph.instagram.com/access_token"
        f"?grant_type=ig_exchange_token"
        f"&client_id={_APP_ID}"
        f"&client_secret={_APP_SECRET}"
        f"&access_token={urllib.parse.quote(short_token)}"
    )
    try:
        with urllib.request.urlopen(url, timeout=10) as r:
            data = json.loads(r.read())
        return data.get("access_token")
    except Exception as e:
        print(f"  [장기 토큰 교환] 실패: {e}")
        return None


def _refresh_long_lived(current_token: str) -> str | None:
    """장기 토큰 갱신 (만료 전 언제든 가능)."""
    url = (
        "https://graph.instagram.com/refresh_access_token"
        f"?grant_type=ig_refresh_token"
        f"&access_token={urllib.parse.quote(current_token)}"
    )
    try:
        with urllib.request.urlopen(url, timeout=10) as r:
            data = json.loads(r.read())
        return data.get("access_token")
    except Exception as e:
        print(f"  [토큰 갱신] 실패: {e}")
        return None


def _update_env(new_token: str, account_id: str = ""):
    """`.env` 파일에서 Instagram 토큰·계정 ID 업데이트."""
    if not os.path.exists(_ENV_PATH):
        print(f"  [Warning] .env 파일 없음: {_ENV_PATH}")
        return
    with open(_ENV_PATH, "r", encoding="utf-8") as f:
        lines = f.readlines()

    updated = []
    token_written = False
    account_written = False

    for line in lines:
        if line.startswith("INSTAGRAM_ACCESS_TOKEN="):
            updated.append(f'INSTAGRAM_ACCESS_TOKEN="{new_token}"\n')
            token_written = True
        elif account_id and line.startswith("INSTAGRAM_ACCOUNT_ID="):
            updated.append(f'INSTAGRAM_ACCOUNT_ID="{account_id}"\n')
            account_written = True
        else:
            updated.append(line)

    if not token_written:
        updated.append(f'INSTAGRAM_ACCESS_TOKEN="{new_token}"\n')
    if account_id and not account_written:
        updated.append(f'INSTAGRAM_ACCOUNT_ID="{account_id}"\n')

    with open(_ENV_PATH, "w", encoding="utf-8") as f:
        f.writelines(updated)
    print("  [토큰 갱신] .env 업데이트 완료")


def run_check():
    """토큰 상태 확인 → 필요 시 자동 갱신. 외부에서 직접 호출 가능."""
    _load_env()
    token = os.getenv("INSTAGRAM_ACCESS_TOKEN", "")
    if not token:
        send_telegram_message(
            "⚠️ <b>[코다리]</b> INSTAGRAM_ACCESS_TOKEN 없음\n"
            "Meta 개발자 콘솔에서 User 토큰을 발급해 .env에 추가하세요."
        )
        return

    info = _inspect_token(token)
    is_valid = info.get("is_valid", False)

    if not is_valid:
        send_telegram_message(
            "🚨 <b>[코다리]</b> Instagram 토큰 무효 (만료 또는 권한 취소)\n"
            "새 User 토큰을 발급해 주세요."
        )
        return

    username = info.get("username", info.get("user_id", ""))
    print(f"  [코다리] Instagram 토큰 유효 (계정: {username})")

    # 만료일 미노출 — 주기적 갱신으로 유지
    new_token = _refresh_long_lived(token)
    if new_token and new_token != token:
        _update_env(new_token)
        send_telegram_message("✅ <b>[코다리]</b> Instagram 토큰 갱신 완료 (60일 유효)")
    else:
        print("  [코다리] Instagram 토큰 갱신 불필요 또는 실패 — 현재 토큰 유지")


def exchange_and_save(short_token: str, account_id: str = ""):
    """단기 토큰 → 장기 토큰 교환 후 .env 저장. 최초 1회 수동 호출용."""
    _load_env()
    long_token = _exchange_to_long_lived(short_token)
    if long_token:
        _update_env(long_token, account_id)
        send_telegram_message(
            "✅ <b>[코다리]</b> Instagram 장기 토큰 발급 완료\n"
            "60일간 유효합니다. 이후 만료 10일 전 자동 갱신됩니다."
        )
        print(f"  장기 토큰 저장 완료")
    else:
        print("  장기 토큰 교환 실패 — User 토큰인지 확인해주세요")


if __name__ == "__main__":
    import sys as _sys
    if len(_sys.argv) > 1:
        # 수동 교환: python instagram_token_refresher.py <단기토큰> [계정ID]
        st = _sys.argv[1]
        aid = _sys.argv[2] if len(_sys.argv) > 2 else ""
        exchange_and_save(st, aid)
    else:
        run_check()
