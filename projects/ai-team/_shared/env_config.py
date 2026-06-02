"""
환경변수 메타정보 정의
모든 환경변수의 필수/선택 여부, 담당 에이전트, 검증 규칙을 정의합니다.
"""

ENV_CONFIG = {
    # Vercel (Kevin DevOps Agent)
    "VERCEL_OIDC_TOKEN": {
        "required": False,
        "description": "Vercel CLI 인증 토큰 (자동 생성)",
        "agents": ["케빈_인프라"],
        "setup_url": "https://vercel.com/docs/cli",
    },
    "VERCEL_TOKEN": {
        "required": True,
        "description": "Vercel API 토큰",
        "agents": ["케빈_인프라"],
        "setup_url": "https://vercel.com/account/tokens",
    },
    "VERCEL_TEAM_ID": {
        "required": True,
        "description": "Vercel 팀 ID",
        "agents": ["케빈_인프라"],
        "setup_url": "https://vercel.com/docs/teams-and-accounts",
    },
    "BLOB_READ_WRITE_TOKEN": {
        "required": True,
        "description": "Vercel Blob Storage 토큰",
        "agents": ["케빈_인프라"],
        "setup_url": "https://vercel.com/docs/storage/vercel-blob",
    },
    "CRON_SECRET": {
        "required": True,
        "description": "Cron Job 인증 시크릿",
        "agents": ["케빈_인프라"],
        "setup_url": None,
    },

    # Supabase (펫과나 DB)
    "SUPABASE_URL": {
        "required": True,
        "description": "Supabase 프로젝트 URL",
        "agents": ["케빈_인프라"],
        "setup_url": "https://supabase.com/dashboard/project/_/settings/api",
    },
    "SUPABASE_ANON_KEY": {
        "required": True,
        "description": "Supabase 공개 키 (RLS 활성화 필요)",
        "agents": ["케빈_인프라"],
        "setup_url": "https://supabase.com/dashboard/project/_/settings/api",
    },

    # Google APIs (AI 팀)
    "GEMINI_API_KEY": {
        "required": True,
        "description": "Google Gemini API 키",
        "agents": ["루나_디렉터", "아린_관리자", "가희_검수관", "영숙_비서", "현빈_전략가", "로율_변호사"],
        "setup_url": "https://aistudio.google.com/app/apikey",
        "validation": lambda x: x and (x.startswith("AI") or x.startswith("AQ")),
    },
    "YOUTUBE_API_KEY": {
        "required": True,
        "description": "YouTube Data API v3 키",
        "agents": ["루나_디렉터"],
        "setup_url": "https://console.cloud.google.com/apis/credentials",
        "validation": lambda x: x and x.startswith("AI"),
    },

    # Instagram/Facebook (AI 팀)
    "INSTAGRAM_APP_ID": {
        "required": True,
        "description": "Facebook App ID",
        "agents": ["아린_관리자", "경수_수사관", "코다리_개발자"],
        "setup_url": "https://developers.facebook.com/apps/",
        "validation": lambda x: x and x.isdigit(),
    },
    "INSTAGRAM_APP_SECRET": {
        "required": True,
        "description": "Facebook App Secret",
        "agents": ["아린_관리자", "코다리_개발자"],
        "setup_url": "https://developers.facebook.com/apps/",
    },
    "INSTAGRAM_ACCESS_TOKEN": {
        "required": True,
        "description": "Instagram 장기 액세스 토큰",
        "agents": ["아린_관리자", "경수_수사관", "코다리_개발자"],
        "setup_url": "docs/SETUP_INSTAGRAM.md",
        "validation": lambda x: x and len(x) > 50,
    },
    "INSTAGRAM_ACCOUNT_ID": {
        "required": True,
        "description": "Instagram Business 계정 ID",
        "agents": ["아린_관리자", "경수_수사관"],
        "setup_url": "docs/SETUP_INSTAGRAM.md",
        "validation": lambda x: x and x.isdigit(),
    },

    # Telegram (알림)
    "TELEGRAM_BOT_TOKEN": {
        "required": True,
        "description": "Telegram Bot API 토큰",
        "agents": ["영숙_비서", "코다리_개발자", "모든_에이전트(알림)"],
        "setup_url": "https://core.telegram.org/bots#6-botfather",
        "validation": lambda x: x and ":" in x,
    },
    "TELEGRAM_CHAT_ID": {
        "required": True,
        "description": "Telegram 채팅방 ID",
        "agents": ["영숙_비서", "코다리_개발자", "모든_에이전트(알림)"],
        "setup_url": "https://core.telegram.org/bots#6-botfather",
    },

    # Notion (영숙 비서)
    "NOTION_API_KEY": {
        "required": False,
        "description": "Notion Integration API 키",
        "agents": ["영숙_비서"],
        "setup_url": "https://www.notion.so/my-integrations",
        "validation": lambda x: not x or x.startswith("ntn_") or x.startswith("secret_"),
    },
    "NOTION_DATABASE_ID": {
        "required": False,
        "description": "Notion 데이터베이스 ID",
        "agents": ["영숙_비서"],
        "setup_url": "https://www.notion.so/my-integrations",
    },

    # 선택적 환경변수
    "SUPPRESS_TELEGRAM": {
        "required": False,
        "description": "Telegram 전송 억제 (테스트용)",
        "agents": ["_shared (공용)"],
        "setup_url": None,
        "validation": lambda x: not x or x in ["true", "false", "1", "0"],
    },
}


# 에이전트별 필수 환경변수 매핑
AGENT_REQUIRED_VARS = {
    "루나_디렉터": ["GEMINI_API_KEY", "YOUTUBE_API_KEY"],
    "아린_관리자": ["GEMINI_API_KEY", "INSTAGRAM_APP_ID", "INSTAGRAM_APP_SECRET",
                     "INSTAGRAM_ACCESS_TOKEN", "INSTAGRAM_ACCOUNT_ID"],
    "가희_검수관": ["GEMINI_API_KEY"],
    "영숙_비서": ["GEMINI_API_KEY", "TELEGRAM_BOT_TOKEN", "TELEGRAM_CHAT_ID"],
    "현빈_전략가": ["GEMINI_API_KEY"],
    "코다리_개발자": ["TELEGRAM_BOT_TOKEN", "TELEGRAM_CHAT_ID",
                       "INSTAGRAM_APP_ID", "INSTAGRAM_APP_SECRET", "INSTAGRAM_ACCESS_TOKEN"],
    "경수_수사관": ["GEMINI_API_KEY", "INSTAGRAM_ACCESS_TOKEN", "INSTAGRAM_ACCOUNT_ID"],
    "티모_디자이너": ["GEMINI_API_KEY"],
    "케빈_인프라": ["VERCEL_TOKEN", "VERCEL_TEAM_ID", "BLOB_READ_WRITE_TOKEN",
                    "CRON_SECRET", "SUPABASE_URL", "SUPABASE_ANON_KEY"],
    "로율_변호사": ["GEMINI_API_KEY"],
    "예원_CEO": ["GEMINI_API_KEY", "TELEGRAM_BOT_TOKEN"],
}


def get_required_vars() -> list:
    """필수 환경변수 목록 반환"""
    return [k for k, v in ENV_CONFIG.items() if v["required"]]


def get_optional_vars() -> list:
    """선택적 환경변수 목록 반환"""
    return [k for k, v in ENV_CONFIG.items() if not v["required"]]


def get_vars_for_agent(agent_name: str) -> list:
    """특정 에이전트에 필요한 환경변수 목록 반환"""
    return AGENT_REQUIRED_VARS.get(agent_name, [])


def validate_var(var_name: str, value: str) -> bool:
    """환경변수 값 검증"""
    if var_name not in ENV_CONFIG:
        return True  # 정의되지 않은 변수는 검증 스킵

    config = ENV_CONFIG[var_name]

    # 선택적 변수이고 값이 없으면 통과
    if not config["required"] and not value:
        return True

    # 필수 변수인데 값이 없으면 실패
    if config["required"] and not value:
        return False

    # 커스텀 검증 함수 실행
    if "validation" in config and config["validation"]:
        try:
            return config["validation"](value)
        except:
            return False

    return True


def get_setup_url(var_name: str) -> str:
    """환경변수 설정 방법 URL 반환"""
    if var_name not in ENV_CONFIG:
        return None
    return ENV_CONFIG[var_name].get("setup_url")
