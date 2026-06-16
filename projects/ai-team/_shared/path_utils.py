"""
path_utils.py - 경로 설정 유틸리티 (중복 코드 중앙화)

모든 에이전트 스크립트에서 사용하는 공통 경로 설정 로직을 통합합니다.
"""
import os
import sys
from pathlib import Path


def get_ai_team_root(current_file: str = None) -> Path:
    """
    현재 파일 위치에서 ai-team 루트 디렉토리를 찾습니다.

    Args:
        current_file: __file__ 또는 파일 경로 (None이면 자동 감지)

    Returns:
        Path: ai-team 루트 디렉토리 경로

    Example:
        >>> from _shared.path_utils import get_ai_team_root
        >>> ai_team_root = get_ai_team_root(__file__)
    """
    if current_file is None:
        # 호출자의 __file__ 자동 감지
        import inspect
        frame = inspect.currentframe().f_back
        current_file = frame.f_globals.get('__file__', __file__)

    current_path = Path(current_file).resolve()

    # ai-team 폴더를 찾을 때까지 상위로 이동
    for parent in current_path.parents:
        if parent.name == 'ai-team':
            return parent
        # projects/ai-team 구조도 처리
        if parent.name == 'projects' and (parent / 'ai-team').exists():
            return parent / 'ai-team'

    # 찾지 못하면 현재 경로에서 상대적으로 계산
    # tools → 스킬폴더 → skills → ai-team
    return current_path.parent.parent.parent.parent


def get_project_root(current_file: str = None) -> Path:
    """
    프로젝트 최상위 루트 디렉토리를 찾습니다 (ai_lab).

    Args:
        current_file: __file__ 또는 파일 경로

    Returns:
        Path: 프로젝트 루트 디렉토리

    Example:
        >>> root = get_project_root(__file__)
        >>> env_path = root / '.env'
    """
    ai_team = get_ai_team_root(current_file)
    # ai-team → projects → ai_lab
    return ai_team.parent.parent


def setup_import_paths(current_file: str = None):
    """
    _shared 모듈 및 프로젝트 경로를 sys.path에 추가합니다.

    모든 에이전트 스크립트의 시작 부분에서 호출하세요:

    Example:
        >>> from _shared.path_utils import setup_import_paths
        >>> setup_import_paths(__file__)
        >>> from _shared.env_loader import load_env
        >>> from _shared.telegram_notifier import send_telegram_message

    Args:
        current_file: __file__ 경로
    """
    ai_team_root = get_ai_team_root(current_file)
    project_root = get_project_root(current_file)

    # sys.path에 추가 (중복 방지)
    paths_to_add = [
        str(ai_team_root),  # _shared 모듈 접근용
        str(project_root),  # 프로젝트 루트 접근용
    ]

    for path in paths_to_add:
        if path not in sys.path:
            sys.path.insert(0, path)


def get_agent_output_dir(agent_name: str, create: bool = True) -> Path:
    """
    에이전트별 출력 디렉토리를 반환합니다.

    Args:
        agent_name: 에이전트 이름 (예: '코다리', 'kodari')
        create: 디렉토리가 없으면 생성 (기본값: True)

    Returns:
        Path: output/{agent_name} 경로

    Example:
        >>> output_dir = get_agent_output_dir('코다리')
        >>> result_path = output_dir / 'result.json'
    """
    project_root = get_project_root()
    output_dir = project_root / 'output' / agent_name.lower()

    if create:
        output_dir.mkdir(parents=True, exist_ok=True)

    return output_dir


# 하위 호환성을 위한 별칭
find_project_root = get_project_root
