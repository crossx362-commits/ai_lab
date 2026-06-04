"""
env_crypto.py — 환경변수 암호화/복호화 유틸리티

.env 파일을 암호화하여 .env.encrypted로 저장하고,
실행 시 복호화하여 사용합니다.
"""
import os
import base64
from cryptography.fernet import Fernet
from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.kdf.pbkdf2 import PBKDF2HMAC
from cryptography.hazmat.backends import default_backend


def _get_encryption_key() -> bytes:
    """암호화 키 생성 (머신 고유 정보 기반)."""
    # 머신 고유 정보를 사용하여 키 생성 (사용자명 + 호스트명)
    import platform
    import getpass

    username = getpass.getuser()
    hostname = platform.node()
    salt = b'ai_team_env_salt_v1'  # 고정 salt

    # 머신 정보로 암호화 키 생성
    password = f"{username}@{hostname}".encode('utf-8')

    kdf = PBKDF2HMAC(
        algorithm=hashes.SHA256(),
        length=32,
        salt=salt,
        iterations=100000,
        backend=default_backend()
    )
    key = base64.urlsafe_b64encode(kdf.derive(password))
    return key


def encrypt_env_file(env_path: str = ".env", output_path: str = ".env.encrypted") -> bool:
    """환경변수 파일 암호화."""
    try:
        # 원본 .env 파일 읽기
        if not os.path.exists(env_path):
            print(f"[ERROR] {env_path} 파일이 없습니다.")
            return False

        with open(env_path, "rb") as f:
            env_data = f.read()

        # 암호화
        key = _get_encryption_key()
        fernet = Fernet(key)
        encrypted_data = fernet.encrypt(env_data)

        # 암호화된 파일 저장
        with open(output_path, "wb") as f:
            f.write(encrypted_data)

        print(f"[OK] 암호화 완료: {output_path}")
        print(f"   원본 크기: {len(env_data):,} bytes")
        print(f"   암호화 크기: {len(encrypted_data):,} bytes")
        return True

    except Exception as e:
        print(f"[ERROR] 암호화 실패: {e}")
        return False


def decrypt_env_file(encrypted_path: str = ".env.encrypted", output_path: str = ".env.decrypted") -> bool:
    """암호화된 환경변수 파일 복호화."""
    try:
        # 암호화된 파일 읽기
        if not os.path.exists(encrypted_path):
            print(f"[ERROR] {encrypted_path} 파일이 없습니다.")
            return False

        with open(encrypted_path, "rb") as f:
            encrypted_data = f.read()

        # 복호화
        key = _get_encryption_key()
        fernet = Fernet(key)
        decrypted_data = fernet.decrypt(encrypted_data)

        # 복호화된 파일 저장
        with open(output_path, "wb") as f:
            f.write(decrypted_data)

        print(f"[OK] 복호화 완료: {output_path}")
        return True

    except Exception as e:
        print(f"[ERROR] 복호화 실패: {e}")
        return False


def load_encrypted_env(encrypted_path: str = ".env.encrypted") -> dict:
    """암호화된 환경변수 파일을 복호화하여 딕셔너리로 반환."""
    try:
        if not os.path.exists(encrypted_path):
            return {}

        with open(encrypted_path, "rb") as f:
            encrypted_data = f.read()

        # 복호화
        key = _get_encryption_key()
        fernet = Fernet(key)
        decrypted_data = fernet.decrypt(encrypted_data)

        # 환경변수 파싱
        env_vars = {}
        for line in decrypted_data.decode('utf-8').splitlines():
            line = line.strip()
            if line and not line.startswith('#') and '=' in line:
                key, value = line.split('=', 1)
                env_vars[key.strip()] = value.strip()

        return env_vars

    except Exception as e:
        print(f"  [Warning] 암호화된 환경변수 로드 실패: {e}")
        return {}


if __name__ == "__main__":
    import sys

    if len(sys.argv) < 2:
        print("사용법:")
        print("  암호화: python env_crypto.py encrypt [.env] [.env.encrypted]")
        print("  복호화: python env_crypto.py decrypt [.env.encrypted] [.env.decrypted]")
        sys.exit(1)

    command = sys.argv[1]

    if command == "encrypt":
        env_path = sys.argv[2] if len(sys.argv) > 2 else ".env"
        output_path = sys.argv[3] if len(sys.argv) > 3 else ".env.encrypted"
        encrypt_env_file(env_path, output_path)

    elif command == "decrypt":
        encrypted_path = sys.argv[2] if len(sys.argv) > 2 else ".env.encrypted"
        output_path = sys.argv[3] if len(sys.argv) > 3 else ".env.decrypted"
        decrypt_env_file(encrypted_path, output_path)

    else:
        print(f"알 수 없는 명령: {command}")
        sys.exit(1)
