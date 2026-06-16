#!/usr/bin/env python3
"""
모든 환경 변수 및 자격 증명 파일 암호화 스크립트
.env, client_secret.json 등을 안전하게 암호화하여 Git에 커밋 가능하게 만듭니다.
"""

import os
import sys
import base64
from cryptography.fernet import Fernet
from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.kdf.pbkdf2 import PBKDF2HMAC
from cryptography.hazmat.backends import default_backend

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
if hasattr(sys.stderr, 'reconfigure'):
    sys.stderr.reconfigure(encoding='utf-8', errors='replace')


def generate_key(password: str, salt: bytes) -> bytes:
    """비밀번호로부터 암호화 키 생성"""
    kdf = PBKDF2HMAC(
        algorithm=hashes.SHA256(),
        length=32,
        salt=salt,
        iterations=100000,
        backend=default_backend()
    )
    key = base64.urlsafe_b64encode(kdf.derive(password.encode()))
    return key


def encrypt_file(input_file: str, output_file: str, password: str):
    """파일 암호화"""
    # 랜덤 솔트 생성
    salt = os.urandom(16)

    # 키 생성
    key = generate_key(password, salt)

    # Fernet 암호화 객체 생성
    fernet = Fernet(key)

    # 원본 파일 읽기
    with open(input_file, 'rb') as f:
        data = f.read()

    # 암호화
    encrypted_data = fernet.encrypt(data)

    # 암호화된 파일 저장 (salt + encrypted_data)
    with open(output_file, 'wb') as f:
        f.write(salt + encrypted_data)

    return True


def main():
    # 고정 비밀번호 (프로덕션에서는 환경 변수나 안전한 저장소에서 가져와야 함)
    PASSWORD = "ai_lab_secure_env_2026"

    # 암호화할 파일 목록
    files_to_encrypt = [
        (".env", ".env.encrypted"),
        ("client_secret.json", "client_secret.json.encrypted"),
    ]

    print("=" * 60)
    print("  환경 변수 및 자격 증명 파일 암호화")
    print("=" * 60)
    print()

    encrypted_count = 0
    skipped_count = 0

    for source, target in files_to_encrypt:
        if not os.path.exists(source):
            print(f"⏭️  {source} - 파일 없음 (건너뜀)")
            skipped_count += 1
            continue

        try:
            encrypt_file(source, target, PASSWORD)
            size = os.path.getsize(target)
            print(f"✅ {source} → {target} ({size:,} bytes)")
            encrypted_count += 1
        except Exception as e:
            print(f"❌ {source} 암호화 실패: {e}")

    print()
    print("=" * 60)
    print(f"  완료: {encrypted_count}개 암호화, {skipped_count}개 건너뜀")
    print("=" * 60)
    print()

    if encrypted_count > 0:
        print("📋 다음 단계:")
        print()
        print("1. 암호화된 파일을 Git에 추가:")
        print("   git add .env.encrypted client_secret.json.encrypted")
        print()
        print("2. .gitignore 확인 (원본 파일이 제외되어 있는지):")
        print("   - .env")
        print("   - .env.key")
        print("   - client_secret.json")
        print("   - *.pickle")
        print()
        print("3. 커밋:")
        print("   git commit -m 'feat: add encrypted environment variables'")
        print()
        print("4. 복호화 방법:")
        print("   python decrypt_all_secrets.py")
        print()
        print("⚠️  주의사항:")
        print("   - 원본 파일(.env, client_secret.json)은 절대 Git에 커밋하지 마세요!")
        print("   - 비밀번호는 안전하게 보관하세요")
        print("   - 팀원에게는 decrypt_all_secrets.py와 비밀번호만 전달")


if __name__ == "__main__":
    main()
