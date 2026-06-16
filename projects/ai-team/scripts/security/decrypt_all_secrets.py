#!/usr/bin/env python3
"""
모든 암호화된 환경 변수 및 자격 증명 파일 복호화 스크립트
.env.encrypted, client_secret.json.encrypted 등을 복호화합니다.
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


def decrypt_file(encrypted_file: str, output_file: str, password: str):
    """파일 복호화"""
    # 암호화된 파일 읽기
    with open(encrypted_file, 'rb') as f:
        data = f.read()

    # salt와 encrypted_data 분리
    salt = data[:16]
    encrypted_data = data[16:]

    # 키 생성
    key = generate_key(password, salt)

    # Fernet 복호화 객체 생성
    fernet = Fernet(key)

    # 복호화
    decrypted_data = fernet.decrypt(encrypted_data)

    # 복호화된 파일 저장
    with open(output_file, 'wb') as f:
        f.write(decrypted_data)

    # 파일 권한 설정 (Unix 계열)
    if os.name != 'nt':
        os.chmod(output_file, 0o600)

    return True


def main():
    # 고정 비밀번호 (암호화 시 사용한 것과 동일)
    PASSWORD = "ai_lab_secure_env_2026"

    # 복호화할 파일 목록
    files_to_decrypt = [
        (".env.encrypted", ".env"),
        ("client_secret.json.encrypted", "client_secret.json"),
    ]

    print("=" * 60)
    print("  환경 변수 및 자격 증명 파일 복호화")
    print("=" * 60)
    print()

    decrypted_count = 0
    skipped_count = 0
    failed_count = 0

    for source, target in files_to_decrypt:
        if not os.path.exists(source):
            print(f"⏭️  {source} - 파일 없음 (건너뜀)")
            skipped_count += 1
            continue

        try:
            decrypt_file(source, target, PASSWORD)
            size = os.path.getsize(target)
            print(f"✅ {source} → {target} ({size:,} bytes)")
            decrypted_count += 1
        except Exception as e:
            print(f"❌ {source} 복호화 실패: {e}")
            failed_count += 1

    print()
    print("=" * 60)
    print(f"  완료: {decrypted_count}개 복호화, {skipped_count}개 건너뜀, {failed_count}개 실패")
    print("=" * 60)
    print()

    if decrypted_count > 0:
        print("✅ 복호화된 파일:")
        for _, target in files_to_decrypt:
            if os.path.exists(target):
                print(f"   - {target}")
        print()
        print("⚠️  주의:")
        print("   - 복호화된 파일(.env, client_secret.json)은 절대 Git에 커밋하지 마세요!")
        print("   - .gitignore에 이미 추가되어 있는지 확인하세요")
        print()
        print("📋 다음 단계:")
        print("   - 애플리케이션 실행")
        print("   - 환경 변수 자동 로드됨")

    if failed_count > 0:
        print()
        print("⚠️  오류:")
        print("   - 비밀번호가 올바른지 확인하세요")
        print("   - 암호화 파일이 손상되지 않았는지 확인하세요")


if __name__ == "__main__":
    main()
