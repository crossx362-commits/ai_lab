# AI Team Scripts

이 폴더는 에이전트 스킬 폴더에 속하지 않는 운영/점검용 공용 스크립트를 모아둡니다.

## 실행 전 주의

- 자동매매, 업로드, 배포 스크립트는 실제 외부 서비스나 계정 상태를 바꿀 수 있습니다.
- 먼저 `agents/check_agent_env_connections.py`처럼 외부 호출이 없는 점검 스크립트로 환경을 확인하세요.
- 실제 API 호출 점검은 `agents/test_agent_api_connections.py`를 사용합니다.

## 폴더

| 경로 | 용도 |
| --- | --- |
| `agents/` | 에이전트 환경변수, API 연결, 경로 정합성 점검 |
| `security/` | `.env`와 credential 암호화/복호화 |
| `youtube/` | YouTube OAuth, 공개 전환, 메타데이터 업데이트 |

## 루트 스크립트

| 파일 | 용도 |
| --- | --- |
| `start_daily_automation.py` | 일일 AI 팀 자동화 시작 |
| `start_trading_team.py` | 데이브/레오 트레이딩 팀 시작 |
| `monitor_processes.py` | 주요 백그라운드 프로세스 모니터링 |
| `cleanup_duplicate_processes.py` | 중복 프로세스 정리 |
| `petnna_social_upload.py` | Petnna 소셜 업로드 파이프라인 |
| `petnna_user_sim.py` | Petnna 사용자 시뮬레이션 |
| `agent_self_learning.py` | 에이전트 학습 로그 처리 |
| `daily_trading_learning.py` | 트레이딩 성과 학습 |
| `daily_balance_check.py` | 일일 잔고 점검 |
| `check_holdings.py` | 보유 자산 확인 |
| `check_meta_video_api.py` | Meta/영상 API 확인 |
| `scan_env_usage.py` | 환경변수 사용처 스캔 |
| `kodari_ollama.py` | Ollama 상태 점검/복구 |
| `cycle.js` | VS Code 확장 자동 사이클 보조 |

## 정리 규칙

- 에이전트 전용 도구는 `projects/ai-team/skills/{에이전트}/tools/`에 둡니다.
- 여러 에이전트가 공유하는 코드는 `projects/ai-team/_shared/`에 둡니다.
- 일회성 점검은 `scripts/agents/`에 두고, 운영 파이프라인은 루트 `scripts/`에 둡니다.
- 생성물, 로그, 캐시는 `reports/`, `.logs/`, `output/` 또는 `__pycache__/`로 분리하고 Git 추적 대상에 넣지 않습니다.
