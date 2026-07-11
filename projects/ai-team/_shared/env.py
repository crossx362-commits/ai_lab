"""Unified environment loader - replaces env_loader, env_config, env_crypto."""
import base64
import getpass
import os
import platform
import sys
from pathlib import Path

# UTF-8 인코딩 강제
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

from cryptography.fernet import Fernet
from cryptography.hazmat.backends import default_backend
from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.kdf.pbkdf2 import PBKDF2HMAC


# ==================== ROOT DETECTION ====================

def find_root(start: str | None = None) -> Path:
    """Find project root (D:/ai_lab)."""
    root = Path(start or __file__).resolve()
    for _ in range(10):
        if (root / "ENV_MANIFEST.json").exists() or (root / ".env.encrypted").exists():
            return root
        if root.parent == root:
            break
        root = root.parent
    return Path(__file__).resolve().parent


# ==================== ENCRYPTION ====================

def _get_key() -> bytes:
    """Machine-local Fernet key."""
    salt = b"ai_team_env_salt_v1"
    password = f"{getpass.getuser()}@{platform.node()}".encode()
    kdf = PBKDF2HMAC(algorithm=hashes.SHA256(), length=32, salt=salt, iterations=100000, backend=default_backend())
    return base64.urlsafe_b64encode(kdf.derive(password))


def encrypt(env_path: str = ".env", out: str = ".env.encrypted") -> bool:
    """Encrypt .env → .env.encrypted."""
    try:
        data = Path(env_path).read_bytes()
        encrypted = Fernet(_get_key()).encrypt(data)
        Path(out).write_bytes(encrypted)
        print(f"✅ Encrypted {len(data):,} → {len(encrypted):,} bytes")
        return True
    except Exception as e:
        print(f"❌ Encryption failed: {e}")
        return False


def decrypt(enc_path: str = ".env.encrypted", out: str = ".env.decrypted") -> bool:
    """Decrypt .env.encrypted → .env.decrypted."""
    try:
        encrypted = Path(enc_path).read_bytes()
        data = Fernet(_get_key()).decrypt(encrypted)
        Path(out).write_bytes(data)
        print(f"✅ Decrypted {len(encrypted):,} → {len(data):,} bytes")
        return True
    except Exception as e:
        print(f"❌ Decryption failed: {e}")
        return False


def load_encrypted(enc_path: Path) -> dict[str, str]:
    """Return env dict from .env.encrypted."""
    try:
        encrypted = enc_path.read_bytes()
        data = Fernet(_get_key()).decrypt(encrypted).decode()
        env = {}
        for line in data.splitlines():
            line = line.strip()
            if line and not line.startswith("#") and "=" in line:
                k, v = line.split("=", 1)
                env[k.strip()] = v.strip().strip('"').strip("'")
        return env
    except Exception:
        return {}


# ==================== LOAD ENV ====================

def load_env(start: str | None = None) -> None:
    """Load .env.encrypted or .env into os.environ.

    스테일 상속 근절(2026-07-08 사고): 장수 부모(워치독·스케줄러)가 옛 .env 값을 os.environ에
    물고 있다가 자식 데몬에 유전 — .env에서 '지운' 키는 update()로는 영원히 안 지워져
    삭제한 설정(SOMI_CANDIDATES_BEAR)이 며칠씩 되살아났다. 해법: 로드한 키 목록을
    _AILAB_ENV_KEYS로 남기고, 다음 load_env(자식 포함)가 그 목록을 먼저 청소한 뒤
    현재 파일로 새로 채운다 — .env 파일이 유일한 진실이 된다."""
    root = find_root(start)

    # 직전 로드(부모에게 상속받은 것 포함)가 넣었던 키를 먼저 제거 — 삭제된 키의 유령 방지
    for k in os.environ.pop("_AILAB_ENV_KEYS", "").split(","):
        if k:
            os.environ.pop(k, None)

    def _apply(env: dict) -> None:
        os.environ.update(env)
        os.environ["_AILAB_ENV_KEYS"] = ",".join(env.keys())

    # Try encrypted first
    enc = root / ".env.encrypted"
    if enc.exists():
        env = load_encrypted(enc)
        if env:
            _apply(env)
            return
        # 복호화 실패(다른 기계 키로 암호화된 경우 등) — 조용히 넘어가면 이 기계가
        # 계속 옛 평문 .env로 강등 운영되는 걸 아무도 모른다(2026-07-11 발견).
        print(f"⚠️  {enc} 복호화 실패 — 이 기계({getpass.getuser()}@{platform.node()})의 키와 "
              "안 맞을 수 있음. 평문 .env로 강등.", file=sys.stderr)

    # Fallback to plaintext
    plain = root / ".env"
    if plain.exists():
        env = {}
        for line in plain.read_text(encoding="utf-8").splitlines():
            line = line.strip()
            if line and not line.startswith("#") and "=" in line:
                k, v = line.split("=", 1)
                env[k.strip()] = v.strip().strip('"').strip("'")
        _apply(env)


# ==================== VALIDATION ====================

REQUIRED_VARS = [
    "TELEGRAM_BOT_TOKEN",
    "TELEGRAM_CHAT_ID",
    "GEMINI_API_KEY",
]

TRADING_VARS = []  # 주식·코인 삭제(2026-07-08) — 매매 API 키 검증 대상 없음


def validate(required: list[str] = REQUIRED_VARS) -> None:
    """Validate required env vars."""
    missing = [v for v in required if not os.getenv(v)]
    if missing:
        print("❌ Missing env vars:", ", ".join(missing))
        print("   Run: python projects/ai-team/_shared/env.py decrypt")
        raise SystemExit(1)


def get(key: str, default: str = "", warn: bool = True) -> str:
    """Get env var with fallback."""
    val = os.getenv(key)
    if not val:
        if warn:
            print(f"⚠️  {key} not set, using default: {default}")
        return default
    return val


# ==================== CLI ====================

if __name__ == "__main__":
    import sys
    if len(sys.argv) < 2:
        print("Usage:")
        print("  python env.py encrypt [.env] [.env.encrypted]")
        print("  python env.py decrypt [.env.encrypted] [.env.decrypted]")
        raise SystemExit(1)

    cmd = sys.argv[1]
    if cmd == "encrypt":
        ok = encrypt(sys.argv[2] if len(sys.argv) > 2 else ".env",
                     sys.argv[3] if len(sys.argv) > 3 else ".env.encrypted")
    elif cmd == "decrypt":
        ok = decrypt(sys.argv[2] if len(sys.argv) > 2 else ".env.encrypted",
                     sys.argv[3] if len(sys.argv) > 3 else ".env.decrypted")
    else:
        print(f"Unknown command: {cmd}")
        ok = False
    raise SystemExit(0 if ok else 1)
