#!/usr/bin/env python3
"""
환경 변수 암호화/복호화 스크립트
.env 파일을 암호화하여 Git에 안전하게 저장
"""
import os
import sys
import base64
from cryptography.fernet import Fernet

ENV_FILE = ".env"
ENCRYPTED_FILE = ".env.encrypted"
KEY_FILE = ".env.key"

def generate_key():
    """새 암호화 키 생성"""
    key = Fernet.generate_key()
    with open(KEY_FILE, "wb") as f:
        f.write(key)
    print(f"✅ 암호화 키 생성: {KEY_FILE}")
    print(f"⚠️  이 키는 안전한 곳에 보관하세요 (Git에 올리지 마세요!)")
    return key

def load_key():
    """기존 암호화 키 로드"""
    if not os.path.exists(KEY_FILE):
        return generate_key()

    with open(KEY_FILE, "rb") as f:
        key = f.read()
    return key

def encrypt_env():
    """'.env' 파일을 암호화하여 '.env.encrypted'로 저장"""
    if not os.path.exists(ENV_FILE):
        print(f"❌ {ENV_FILE} 파일이 없습니다.")
        return False

    # 키 로드 또는 생성
    key = load_key()
    cipher = Fernet(key)

    # .env 파일 읽기
    with open(ENV_FILE, "rb") as f:
        env_data = f.read()

    # 암호화
    encrypted_data = cipher.encrypt(env_data)

    # 암호화된 파일 저장
    with open(ENCRYPTED_FILE, "wb") as f:
        f.write(encrypted_data)

    print(f"✅ 환경 변수 암호화 완료: {ENCRYPTED_FILE}")
    print(f"   원본 크기: {len(env_data)} bytes")
    print(f"   암호화 크기: {len(encrypted_data)} bytes")
    return True

def decrypt_env():
    """'.env.encrypted' 파일을 복호화하여 '.env'로 저장"""
    if not os.path.exists(ENCRYPTED_FILE):
        print(f"❌ {ENCRYPTED_FILE} 파일이 없습니다.")
        return False

    if not os.path.exists(KEY_FILE):
        print(f"❌ {KEY_FILE} 파일이 없습니다.")
        print("   암호화 키가 필요합니다!")
        return False

    # 키 로드
    key = load_key()
    cipher = Fernet(key)

    # 암호화된 파일 읽기
    with open(ENCRYPTED_FILE, "rb") as f:
        encrypted_data = f.read()

    try:
        # 복호화
        decrypted_data = cipher.decrypt(encrypted_data)

        # .env 파일 저장
        with open(ENV_FILE, "wb") as f:
            f.write(decrypted_data)

        print(f"✅ 환경 변수 복호화 완료: {ENV_FILE}")
        print(f"   {len(decrypted_data)} bytes 복원됨")
        return True

    except Exception as e:
        print(f"❌ 복호화 실패: {e}")
        print("   암호화 키가 올바르지 않을 수 있습니다.")
        return False

def main():
    if len(sys.argv) < 2:
        print("사용법:")
        print("  암호화: python encrypt_env.py encrypt")
        print("  복호화: python encrypt_env.py decrypt")
        return 1

    command = sys.argv[1].lower()

    if command == "encrypt":
        success = encrypt_env()
    elif command == "decrypt":
        success = decrypt_env()
    else:
        print(f"❌ 알 수 없는 명령어: {command}")
        print("   'encrypt' 또는 'decrypt'를 사용하세요.")
        return 1

    return 0 if success else 1

if __name__ == "__main__":
    sys.exit(main())
