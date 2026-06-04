"""
env_loader.py — 공통 환경변수 로드

모든 에이전트에서 각자 구현하던 _load_env() 를 하나로 병합.
.env 파일 경로를 자동으로 프로젝트 루트에서 탐색.
"""
import os


def find_project_root(start: str | None = None) -> str:
    """프로젝트 루트 자동 탐색 (ENV_MANIFEST.json 또는 .env.encrypted 기준).
    ai-team/ 내부에도 reports/ 폴더가 있어 기존 방식은 오판함.
    """
    root = start or os.path.dirname(os.path.abspath(__file__))
    for _ in range(10):
        if (os.path.isfile(os.path.join(root, "ENV_MANIFEST.json")) or
                os.path.isfile(os.path.join(root, ".env.encrypted"))):
            return root
        parent = os.path.dirname(root)
        if parent == root:
            break
        root = parent
    return start or os.path.dirname(os.path.abspath(__file__))


def _find_root(start: str) -> str:
    """내부 호환용 — find_project_root() 를 사용하세요."""
    return find_project_root(start)


def load_env(start_path: str | None = None) -> None:
    """프로젝트 루트 .env 또는 .env.encrypted를 읽어 환경변수로 등록.

    우선순위:
    1. .env.encrypted (암호화된 환경변수)
    2. .env (평문 환경변수)

    start_path: 탐색 시작 경로. None 이면 이 파일 위치 기준.
    """
    root = _find_root(start_path or os.path.dirname(os.path.abspath(__file__)))

    # 1순위: 암호화된 환경변수 로드
    encrypted_path = os.path.join(root, ".env.encrypted")
    if os.path.exists(encrypted_path):
        try:
            from _shared.env_crypto import load_encrypted_env
            env_vars = load_encrypted_env(encrypted_path)
            for k, v in env_vars.items():
                os.environ[k] = v.strip('"').strip("'")
            return  # 암호화 파일 로드 성공 시 평문 .env 건너뜀
        except Exception as e:
            print(f"  [Warning] 암호화된 환경변수 로드 실패, 평문 .env로 폴백: {e}")

    # 2순위: 평문 .env 로드
    env_path = os.path.join(root, ".env")
    if not os.path.exists(env_path):
        return
    with open(env_path, "r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("#") or "=" not in line:
                continue
            k, v = line.split("=", 1)
            os.environ[k.strip()] = v.strip().strip('"').strip("'")


def validate_env(required_vars: list[str]) -> None:
    """필수 환경변수 검증. 누락 시 명확한 에러 메시지 출력 후 종료.

    Args:
        required_vars: 필수 환경변수 목록

    Raises:
        SystemExit: 필수 환경변수가 누락된 경우
    """
    missing_vars = []

    for var in required_vars:
        if not os.getenv(var):
            missing_vars.append(var)

    if missing_vars:
        print("=" * 70)
        print("환경변수 누락 오류")
        print("=" * 70)
        print(f"\n다음 필수 환경변수가 설정되지 않았습니다:\n")
        for var in missing_vars:
            print(f"  - {var}")
        print("\n설정 방법:")
        print("  1. 프로젝트 루트의 .env 파일을 확인하세요")
        print("  2. ENV_README.md 문서를 참고하여 API 키를 발급받으세요")
        print("  3. .env 파일에 누락된 환경변수를 추가하세요")
        print("\n자세한 내용: ENV_README.md")
        print("=" * 70)
        raise SystemExit(1)


def get_env_with_fallback(key: str, default: str = "", warning: bool = True) -> str:
    """환경변수 조회. 없으면 기본값 반환.

    Args:
        key: 환경변수 이름
        default: 기본값
        warning: 누락 시 경고 출력 여부

    Returns:
        환경변수 값 또는 기본값
    """
    value = os.getenv(key)

    if not value:
        if warning:
            print(f"경고: 환경변수 {key}가 설정되지 않았습니다. 기본값 사용: {default}")
        return default

    return value
