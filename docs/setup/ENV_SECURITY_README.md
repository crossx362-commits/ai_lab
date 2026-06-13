# 환경변수 암호화 시스템

## 개요

AI Team 프로젝트의 모든 환경변수(API 키, 토큰 등)는 **암호화되어 저장**됩니다.

## 파일 구조

```
d:/ai_lab/
├── .env                    # [보안] 원본 평문 파일 (Git 제외, 로컬 백업만)
├── .env.encrypted          # ✅ 암호화된 환경변수 (안전하게 커밋 가능)
├── .env.backup.YYYYMMDD_HHMMSS  # 자동 백업 파일
└── projects/ai-team/_shared/
    └── env_crypto.py       # 암호화/복호화 유틸리티
```

## 암호화 방식

- **알고리즘**: Fernet (AES-128-CBC + HMAC)
- **키 생성**: PBKDF2-HMAC-SHA256
- **키 소스**: 머신 고유 정보 (사용자명 + 호스트명)
- **반복 횟수**: 100,000 iterations

→ **머신별 자동 암호화/복호화** - 다른 컴퓨터에서는 복호화 불가

## 사용 방법

### 1. 환경변수 수정

```bash
# .env 파일 직접 수정
notepad .env

# 수정 후 재암호화
python projects/ai-team/_shared/env_crypto.py encrypt .env .env.encrypted
```

### 2. 암호화/복호화

```bash
# 암호화
python projects/ai-team/_shared/env_crypto.py encrypt .env .env.encrypted

# 복호화 (확인용)
python projects/ai-team/_shared/env_crypto.py decrypt .env.encrypted .env.decrypted
```

### 3. 자동 로드

모든 AI 에이전트는 자동으로 암호화된 환경변수를 로드합니다:

```python
from _shared.env_loader import load_env

load_env()  # .env.encrypted 우선 로드 → .env 폴백
```

## 주요 환경변수

현재 암호화되어 있는 환경변수:

- `GEMINI_API_KEY` - Google Gemini API (Imagen 4.0 포함)
- `SUPABASE_URL` - Supabase 데이터베이스 URL
- `SUPABASE_KEY` - Supabase 서비스 키
- `TELEGRAM_BOT_TOKEN` - 텔레그램 봇 토큰
- `INSTAGRAM_USERNAME` - 인스타그램 계정
- `INSTAGRAM_PASSWORD` - 인스타그램 비밀번호
- `HF_API_TOKEN` - HuggingFace API 토큰 (선택)
- 기타 19개 환경변수

## 보안 주의사항

✅ **안전함**:
- `.env.encrypted` - Git 커밋 가능
- 다른 컴퓨터에서 복호화 불가

❌ **절대 커밋 금지**:
- `.env` - 평문 원본
- `.env.backup.*` - 백업 파일
- `.env.decrypted` - 복호화된 파일

→ 이미 `.gitignore`에 추가됨

## 새 환경변수 추가

1. `.env` 파일에 추가:
```bash
NEW_API_KEY=your_api_key_here
```

2. 재암호화:
```bash
python projects/ai-team/_shared/env_crypto.py encrypt .env .env.encrypted
```

3. 확인:
```bash
python -c "from _shared.env_loader import load_env; import os; load_env(); print(os.getenv('NEW_API_KEY'))"
```

## 복구 방법

만약 `.env` 원본을 잃어버린 경우:

1. `.env.encrypted`가 있다면:
```bash
python projects/ai-team/_shared/env_crypto.py decrypt .env.encrypted .env
```

2. 백업 파일 확인:
```bash
ls -lt .env.backup.*
cp .env.backup.YYYYMMDD_HHMMSS .env
```

## 다른 컴퓨터에서 사용

`.env.encrypted`는 **머신별 암호화**되어 있어 다른 컴퓨터에서는 복호화할 수 없습니다.

새 컴퓨터 설정:
1. `.env` 원본 파일을 안전하게 전송 (USB, 암호화된 채널)
2. 새 컴퓨터에서 재암호화:
```bash
python projects/ai-team/_shared/env_crypto.py encrypt .env .env.encrypted
```

---

**마지막 암호화**: 2026-06-04 15:00
**암호화 파일 크기**: 7,436 bytes (19개 환경변수)
