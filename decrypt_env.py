#!/usr/bin/env python3
"""
환경변수 복호화 스크립트
.env.encrypted 파일을 복호화하여 .env로 복원합니다.
"""

import os
import sys
import base64
from cryptography.fernet import Fernet
from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.kdf.pbkdf2 import PBKDF2HMAC
from cryptography.hazmat.backends import default_backend

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8')
    sys.stderr.reconfigure(encoding='utf-8')


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


def decrypt_file(encrypted_file: str, output_file: str, key_file: str = None):
    """파일 복호화"""
    # 암호화된 파일 읽기
    with open(encrypted_file, 'rb') as f:
        data = f.read()

    # salt와 encrypted_data 분리
    salt = data[:16]
    encrypted_data = data[16:]

    # 키 파일이 있으면 사용, 없으면 비밀번호로 생성
    if key_file and os.path.exists(key_file):
        with open(key_file, 'rb') as f:
            key = f.read()
        print("🔑 키 파일 사용")
    else:
        password = "ai_lab_secure_env_2026"
        key = generate_key(password, salt)
        print("🔑 비밀번호로 키 생성")

    # Fernet 복호화 객체 생성
    fernet = Fernet(key)

    try:
        # 복호화
        decrypted_data = fernet.decrypt(encrypted_data)

        # 복호화된 파일 저장
        with open(output_file, 'wb') as f:
            f.write(decrypted_data)

        print(f"✅ 복호화 완료!")
        print(f"   입력: {encrypted_file}")
        print(f"   출력: {output_file}")

        # 파일 권한 설정 (Unix 계열)
        if os.name != 'nt':
            os.chmod(output_file, 0o600)
            print(f"   권한: 600 (소유자만 읽기/쓰기)")

    except Exception as e:
        print(f"❌ 복호화 실패: {str(e)}")
        print("   키가 올바른지 확인하세요.")
        exit(1)


if __name__ == "__main__":
    # 파일 경로 설정
    encrypted_file = ".env.encrypted"
    output_file = ".env"
    key_file = ".env.key"

    # 파일 존재 확인
    if not os.path.exists(encrypted_file):
        print(f"❌ 오류: {encrypted_file} 파일을 찾을 수 없습니다.")
        exit(1)

    # 복호화 실행
    decrypt_file(encrypted_file, output_file, key_file)

    print(f"\n✅ {output_file} 파일이 생성되었습니다.")
    print(f"⚠️  주의: 이 파일은 절대 Git에 커밋하지 마세요!")
