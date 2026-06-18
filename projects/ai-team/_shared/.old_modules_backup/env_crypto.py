"""Encrypt and decrypt the repository environment file."""
import base64
import getpass
import os
import platform

from cryptography.fernet import Fernet
from cryptography.hazmat.backends import default_backend
from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.kdf.pbkdf2 import PBKDF2HMAC


def _get_encryption_key() -> bytes:
    """Build the machine-local Fernet key used by this repository."""
    salt = b"ai_team_env_salt_v1"
    password = f"{getpass.getuser()}@{platform.node()}".encode("utf-8")
    kdf = PBKDF2HMAC(
        algorithm=hashes.SHA256(),
        length=32,
        salt=salt,
        iterations=100000,
        backend=default_backend(),
    )
    return base64.urlsafe_b64encode(kdf.derive(password))


def encrypt_env_file(env_path: str = ".env", output_path: str = ".env.encrypted") -> bool:
    """Encrypt a plaintext env file."""
    try:
        if not os.path.exists(env_path):
            print(f"[ERROR] missing env file: {env_path}")
            return False

        with open(env_path, "rb") as f:
            env_data = f.read()

        encrypted_data = Fernet(_get_encryption_key()).encrypt(env_data)

        with open(output_path, "wb") as f:
            f.write(encrypted_data)

        print(f"[OK] encrypted: {output_path}")
        print(f"   source bytes: {len(env_data):,}")
        print(f"   encrypted bytes: {len(encrypted_data):,}")
        return True
    except Exception as e:
        print(f"[ERROR] encrypt failed: {e}")
        return False


def decrypt_env_file(encrypted_path: str = ".env.encrypted", output_path: str = ".env.decrypted") -> bool:
    """Decrypt an encrypted env file to disk."""
    try:
        if not os.path.exists(encrypted_path):
            print(f"[ERROR] missing encrypted env file: {encrypted_path}")
            return False

        with open(encrypted_path, "rb") as f:
            encrypted_data = f.read()

        decrypted_data = Fernet(_get_encryption_key()).decrypt(encrypted_data)

        with open(output_path, "wb") as f:
            f.write(decrypted_data)

        print(f"[OK] decrypted: {output_path}")
        return True
    except Exception as e:
        print(f"[ERROR] decrypt failed: {e}")
        return False


def load_encrypted_env(encrypted_path: str = ".env.encrypted") -> dict:
    """Return env variables from an encrypted env file."""
    try:
        if not os.path.exists(encrypted_path):
            return {}

        with open(encrypted_path, "rb") as f:
            encrypted_data = f.read()

        decrypted_data = Fernet(_get_encryption_key()).decrypt(encrypted_data)

        env_vars = {}
        for line in decrypted_data.decode("utf-8").splitlines():
            line = line.strip()
            if line and not line.startswith("#") and "=" in line:
                key, value = line.split("=", 1)
                env_vars[key.strip()] = value.strip()
        return env_vars
    except Exception as e:
        print(f"  [Warning] encrypted env load failed: {e}")
        return {}


if __name__ == "__main__":
    import sys

    if len(sys.argv) < 2:
        print("Usage:")
        print("  encrypt: python env_crypto.py encrypt [.env] [.env.encrypted]")
        print("  decrypt: python env_crypto.py decrypt [.env.encrypted] [.env.decrypted]")
        raise SystemExit(1)

    command = sys.argv[1]
    if command == "encrypt":
        env_path = sys.argv[2] if len(sys.argv) > 2 else ".env"
        output_path = sys.argv[3] if len(sys.argv) > 3 else ".env.encrypted"
        ok = encrypt_env_file(env_path, output_path)
    elif command == "decrypt":
        encrypted_path = sys.argv[2] if len(sys.argv) > 2 else ".env.encrypted"
        output_path = sys.argv[3] if len(sys.argv) > 3 else ".env.decrypted"
        ok = decrypt_env_file(encrypted_path, output_path)
    else:
        print(f"Unknown command: {command}")
        ok = False

    raise SystemExit(0 if ok else 1)
