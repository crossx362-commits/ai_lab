"""
env_loader.py — 공통 환경변수 로드

모든 에이전트에서 각자 구현하던 _load_env() 를 하나로 병합.
.env 파일 경로를 자동으로 프로젝트 루트에서 탐색.
"""
import os


def _find_root(start: str) -> str:
    """현재 경로에서 위로 올라가며 .agent 디렉토리를 가진 루트 반환."""
    root = start
    for _ in range(8):
        if os.path.isdir(os.path.join(root, ".agent")):
            return root
        root = os.path.dirname(root)
    return start


def load_env(start_path: str | None = None) -> None:
    """프로젝트 루트 .env 를 읽어 환경변수로 등록 (이미 설정된 값은 덮어쓰지 않음).

    start_path: 탐색 시작 경로. None 이면 이 파일 위치 기준.
    """
    root = _find_root(start_path or os.path.dirname(os.path.abspath(__file__)))
    env_path = os.path.join(root, ".env")
    if not os.path.exists(env_path):
        return
    with open(env_path, "r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("#") or "=" not in line:
                continue
            k, v = line.split("=", 1)
            os.environ.setdefault(k.strip(), v.strip().strip('"').strip("'"))
