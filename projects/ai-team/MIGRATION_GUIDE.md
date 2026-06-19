# Migration Guide: _shared 통합 모듈

## 변경 사항

기존 24개 `_shared` 모듈 → **5개 통합 모듈**로 간소화

### 통합된 모듈

| 기존 모듈 | 새 모듈 | 변경 내용 |
|----------|---------|---------|
| `env_loader.py`<br>`env_config.py`<br>`env_crypto.py` | **`env.py`** | 통합 환경변수 로더 |
| `gemini_client.py`<br>`ollama_client.py`<br>`claude_client.py`<br>`huggingface_client.py`<br>`nanobanana_client.py` | **`llm.py`** | 통합 LLM 클라이언트 |
| `telegram_notifier.py`<br>`agent_status.py` | **`notify.py`** | 통합 알림 + 상태 |
| `process_lock.py`<br>`duplicate_guard.py` | **`process.py`** | 통합 프로세스 관리 |
| `path_utils.py`<br>`resource_utils.py`<br>`ffmpeg_utils.py`<br>`image_uploader.py` | **`utils.py`** | 통합 유틸리티 |

### 삭제 예정 모듈

- `calendar_client.py` → Youngsuk의 `calendar_manager.py`로 인라인
- `notion_client.py` → `notion_report_manager.py`로 인라인
- `content_validator.py` → Yewon의 `upload_manager.py`로 인라인
- `knowledge_base.py` → `agent_council.py`로 병합
- `history_recorder.py` → 미사용, 삭제

## 마이그레이션 방법

### 1. 환경변수 (env)

**기존:**
```python
from _shared.env_loader import load_env
from _shared.env_config import validate_var
from _shared.env_crypto import encrypt_env_file

load_env()
```

**신규:**
```python
from _shared.env import load_env, validate, encrypt

load_env()
validate(["TELEGRAM_BOT_TOKEN", "GEMINI_API_KEY"])
```

### 2. LLM 클라이언트

**기존:**
```python
from _shared.gemini_client import text as gemini_text
from _shared.ollama_client import text as ollama_text

result = gemini_text("프롬프트", lm_first=True)
```

**신규:**
```python
from _shared.llm import text

# Ollama → GPT → Gemini 폴백 체인
result = text("프롬프트", lm_first=True, task="coding")
```

### 3. 텔레그램 알림

**기존:**
```python
from _shared.telegram_notifier import send_telegram_message
from _shared.agent_status import get_agent_status

send_telegram_message("✅ 작업 완료")
status = get_agent_status()
```

**신규:**
```python
from _shared.notify import send, agent_status, status_report

send("✅ 작업 완료")
status = agent_status()
print(status_report())
```

### 4. 프로세스 락

**기존:**
```python
from _shared.process_lock import acquire_lock, release_lock

if not acquire_lock("dave"):
    sys.exit(0)
try:
    # 작업
finally:
    release_lock("dave")
```

**신규:**
```python
from _shared.process import ProcessLock

with ProcessLock("dave"):
    # 작업
```

### 5. 유틸리티

**기존:**
```python
from _shared.path_utils import find_project_root
from _shared.ffmpeg_utils import convert_video
from _shared.image_uploader import upload_image

root = find_project_root()
```

**신규:**
```python
from _shared.utils import find_root, convert_video, upload_image

root = find_root()
```

## 표준 임포트 패턴 (모든 에이전트)

```python
#!/usr/bin/env python3
"""에이전트 설명"""
import os, sys

# Windows 인코딩 수정
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

# _shared 경로 추가
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

# 통합 모듈 임포트
from _shared.env import load_env
from _shared.llm import text
from _shared.notify import send
from _shared.process import ProcessLock
from _shared.utils import find_root

# 환경변수 로드
load_env()
```

## 마이그레이션 체크리스트

### Phase 1: Core Scripts (우선순위 높음)
- [x] `check_holdings.py` ✅
- [ ] `daily_balance_check.py`
- [ ] `start_trading_team.py`
- [ ] `telegram_receiver.py` (영숙)
- [ ] `upbit_auto_trader.py` (데이브)
- [ ] `leo_aggressive_trader.py` (레오)
- [ ] `crypto_market_intelligence.py` (펄스)

### Phase 2: Support Scripts
- [ ] `start_daily_automation.py`
- [ ] `yewon_dispatcher.py`
- [ ] `upload_manager.py`
- [ ] `petnna_monitor.py`

### Phase 3: Cleanup
- [ ] Remove old `_shared` modules
- [ ] Update `CLAUDE.md`
- [ ] Run `harness/check_all.py`
- [ ] Test all 4 daemons

## 예상 효과

- **토큰 절감**: 70% (24개 모듈 → 5개)
- **임포트 라인**: 5~8줄 → 3~4줄
- **파일 크기**: ~3.7KB (_shared 총합) → ~1.5KB
- **로딩 속도**: 24개 모듈 로드 → 5개 모듈 로드

## 주의사항

1. **하나씩 마이그레이션**: 전체 동시 변경 금지
2. **테스트 필수**: 각 파일 변경 후 실행 확인
3. **롤백 가능**: Git 커밋 단위로 변경
4. **런타임 확인**: 데몬 봇들이 정상 작동하는지 확인

## 문제 발생 시

```bash
# 하네스로 문제 파악
python projects/ai-team/harness/check_all.py

# 기존 모듈로 롤백
git checkout projects/ai-team/_shared/env_loader.py
```

## 완료 기준

- [ ] 모든 데몬 봇 정상 실행 (youngsuk, dave, leo, pulse)
- [ ] `harness/check_all.py` 전체 OK
- [ ] `CLAUDE.md` 업데이트 완료
- [ ] 구 모듈 파일 삭제 완료
