#!/usr/bin/env python3
"""
환경변수 암호화 스크립트
.env 파일을 안전하게 암호화하여 .env.encrypted에 저장합니다.
"""

import os
import base64
from cryptography.fernet import Fernet
from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.kdf.pbkdf2 import PBKDF2HMAC
from cryptography.hazmat.backends import default_backend


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


def encrypt_file(input_file: str, output_file: str, key_file: str):
    """파일 암호화"""
    # 랜덤 솔트 생성
    salt = os.urandom(16)

    # 키 생성 (여기서는 고정된 시드 사용, 프로덕션에서는 안전한 키 관리 필요)
    password = "ai_lab_secure_env_2026"
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

    # 키 저장 (base64 인코딩)
    with open(key_file, 'wb') as f:
        f.write(key)

    print(f"✅ 암호화 완료!")
    print(f"   입력: {input_file}")
    print(f"   출력: {output_file}")
    print(f"   키 파일: {key_file}")
    print(f"\n⚠️  주의: {key_file}는 안전한 곳에 보관하세요!")


if __name__ == "__main__":
    # 파일 경로 설정
    env_file = ".env"
    encrypted_file = ".env.encrypted"
    key_file = ".env.key"

    # 파일 존재 확인
    if not os.path.exists(env_file):
        print(f"❌ 오류: {env_file} 파일을 찾을 수 없습니다.")
        exit(1)

    # 암호화 실행
    encrypt_file(env_file, encrypted_file, key_file)

    print(f"\n📋 다음 단계:")
    print(f"1. {encrypted_file}를 Git에 커밋")
    print(f"2. {key_file}는 .gitignore에 추가 (이미 추가됨)")
    print(f"3. 원본 {env_file}는 로컬에만 보관")
    print(f"4. decrypt_env.py로 복호화 가능")
