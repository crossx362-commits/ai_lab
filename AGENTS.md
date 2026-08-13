# AGENTS.md

This file provides guidance to Codex (and other non-Claude AI tools) when working with code in this repository.

> **⚡ 모든 작업 전 [DIRECTIVES.md](DIRECTIVES.md) 필수 준수** — 오너 원칙·작업 절차·안전 규칙·검증 체크리스트의 통합 지침(A등급).
> 상세 사고 이력·가드레일 원장은 [CLAUDE.md](CLAUDE.md), 사람용 운영 매뉴얼·사고 런북·문서 색인은 [HANDBOOK.md](HANDBOOK.md).
> 구조 설명을 이 파일에 복사하지 않는다 — 사본은 반드시 낡는다.
> (2026-08-04 전면 갱신: 이전 판은 "3 agents"·Windows 운영·삭제된 도구·옛 `_shared` 모듈명을 안내하던 알려진 부채였다.)

---

## 핵심 사실 (2026-08-04 기준)

- **운영 기계는 맥** — `projects/ai-team/_shared/fleet_machine_policy.json`의 `primary_platform: "darwin"`(2026-07-11 오너 지시). Windows에서 펫나 데몬을 켜지 마라(이중 병합 참사).
- **에이전트 9명**: 예원(CEO·워치독)·영숙(비서·텔레그램)·봄이(QA)·수리(Dev)·테오(Test)·백호(Backend)·미오(Design)·나무(PM)·마루(게임개발). 로스터·역할·도구·자동 개발 루프는 [CLAUDE.md](CLAUDE.md) 「Agent Roster」가 원천, 각 봇 헌장은 `projects/ai-team/skills/<봇>/SKILL.md`.
- **공유 모듈**: `projects/ai-team/_shared/` — `env.py`·`llm.py`·`notify.py`·`process.py`·`utils.py`(핵심 5) + `cc.py`(헤드리스 클로드)·`backlog.py`·`registry.py` 등. 임포트 패턴은 CLAUDE.md 「Shared Module System」.
- **LLM 체인**: Ollama(로컬) → 구독 클로드(`claude -p`, Max) → 구독 GPT(`codex exec`, Plus) → Gemini → API 크레딧 백업. 유료 API 신규 사용 금지, 클라우드 모델은 haiku 고정.
- **시크릿**: 전부 루트 `.env` 하나. `.env.encrypted`는 기계별 파생 키라 **git에 올리지 마라**(로컬 전용). 프로젝트별 `.env` 금지, 하드코딩 금지, 사용 전 `load_env()` 필수.
- **데몬 제어**: `python projects/ai-team/skills/영숙_비서/tools/agent_controller.py <에이전트> <start|stop|restart|status>` / launchd 서비스는 `launchctl list | grep com.ailab`. 정시 잡은 `skills/영숙_비서/tools/schedules.json`에 등록 후 `launchctl list | grep com.ailab.sched.<id>`로 실제 등록 확인(파일에 적었다고 도는 게 아니다).
- **점검·테스트**: `python3 projects/ai-team/harness/check_all.py`(exit 0 필수), 회귀 테스트는 `projects/ai-team/tests/test_*.py`.

## 금지 — 어기면 사고 (전체 목록은 DIRECTIVES §3)

1. `_shared/` 수정 후 전체 회귀 테스트 생략 금지
2. 평문 `.env` 커밋 금지 / API 키 하드코딩 금지
3. master force-push 금지 / 자동 git 작업 실패 시 즉시 원상복구(`merge --abort`)
4. 데몬에서 뮤텍스 락 제거 금지 (좀비 프로세스)
5. `load_env()` 생략 금지 — schedules.json으로 뜨는 스크립트는 독립 프로세스라 스스로 불러야 한다
6. 동시 세션 저장소 — `git add`와 `git commit`은 한 호흡으로 (스테이징 방치 금지)

## 파일 배치 정책 (하네스 `check_all.py`가 강제)

| 유형 | 경로 |
|------|------|
| 에이전트 전용 툴 | `projects/ai-team/skills/<agent>/tools/` |
| 공용 헬퍼 | `projects/ai-team/_shared/` |
| 일회성 진단 스크립트 | `projects/ai-team/scripts/agents/` |
| 정식 시스템 스크립트 | `projects/ai-team/scripts/` |
| 연구·분석 리포트 | `reports/` |
| 런타임 로그·미디어 | `output/` (git 제외 — 단 `output/cache/agent_registry.json`은 의도적 추적, 해제 금지) |

- 루트에 새 스크립트 생성 금지 / `__pycache__` 커밋 금지
- `projects/ai-team/harness/`는 **읽기 전용 검증 엔진** — 하네스 자체를 수정하지 마라
- 폴더 정리·마이그레이션·파일 이동 전후 반드시 `python projects/ai-team/harness/check_all.py`
- 라이브 런타임 경로(예: `schedules.json`)는 Producer+Consumer가 함께 이동되기 전까지 이동 금지

## 코딩 컨벤션 (요약)

- 한국어 폴더명 정상(에이전트명) — UTF-8. `sys.stdout.reconfigure(encoding="utf-8")` 필수
- 기존 패턴 유지, 성급한 추상화 금지, surgical change만 — 인접 코드 "개선" 금지
- JS 수정 시 `index.html`의 `?v=` 캐시버전 +1 (안 올리면 재방문 사용자에게 반영 안 됨)
- 테스트는 맥에서 검증 — Windows 전용 기능 추가 금지
