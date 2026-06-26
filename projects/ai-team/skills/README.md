# AI Team Skills

이 폴더는 에이전트별 지시문(`SKILL.md`)과 실행 도구(`tools/`)를 담는 운영 중심 영역입니다.

## 현재 에이전트

| 에이전트 | 역할 | 주요 도구 |
| --- | --- | --- |
| `예원_CEO` | 중앙 디스패처, 작업 분배 | `tools/yewon_dispatcher.py` |
| `영숙_비서` | 텔레그램/일정/보고 | `tools/telegram_receiver.py`, `tools/reports_manager.py` |
| `경수_수사관` | 댓글/보안/검수 | `tools/comment_forensics.py` |
| `코다리_개발자` | 개발/헬스체크 | `tools/agent_health_check.py`, `tools/ollama_health_check.py` |
| `티모_디자이너` | UI/UX 리뷰 | `tools/petnna_reviewer.py` |
| `케빈_인프라` | Vercel/Supabase 운영 | `tools/vercel_manager.py`, `tools/petnna_monitor.py` |
| `시그널_분석가` | 시장 시그널 분석 | `tools/market_signal.py` |
| `펄스_애널리스트` | 시장 펄스 분석 | `tools/market_pulse.py` |
| `로율_변호사` | 법률/세무 | `tools/tax_simulator.py` |
| `데이브_주식` | 보수적 주식/가상자산 분석 | `tools/upbit_analyzer.py`, `tools/upbit_auto_trader.py` |
| `레오_트레이더` | 공격적 단타 트레이딩 | `tools/leo_aggressive_trader.py`, `tools/leo_learning_system.py`, `README.md` |
| `소미_분석가` | 국내주식 수급·세력상황·큰 수익 가능성·매수판단 | `tools/short_covering_analyzer.py`, `tools/somi_kis_reporter.py` |
| `공용스킬` | 공통 지식/가이드 | Markdown 지식 파일 |

## 운영 규칙

- 각 에이전트 폴더는 `SKILL.md`를 기준 문서로 둡니다.
- 실행 파일은 가능한 한 `tools/` 아래에 둡니다.
- 여러 에이전트가 쓰는 클라이언트, 히스토리, 환경변수 로더는 `_shared/`에 둡니다.
- 디스패처 연결은 `예원_CEO/tools/yewon_dispatcher.py`에서 관리합니다.
- 자동매매 스크립트는 실제 주문 함수가 포함되어 있으므로 점검 목적이라도 임의 실행하지 않습니다.
- 레오/데이브 트레이딩 루프는 `pyupbit` 의존성이 필요합니다. 의존성이 없으면 상태 확인만 가능합니다.

## 점검 명령

```powershell
$env:PYTHONUTF8='1'
& 'C:\Users\User\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' projects\ai-team\scripts\agents\check_agent_env_connections.py
```
