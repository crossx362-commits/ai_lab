# 헤르메스(Hermes Agent) 통합 — ai_lab 커스터마이징

NousResearch [hermes-agent](https://github.com/NousResearch/hermes-agent)(MIT)를
펫나 개발팀 실행 엔진으로 통합하는 작업. **오너 지시(2026-07-09): 6개 펫나 에이전트를
헤르메스 네이티브(스킬+메모리+자가학습)로 재설계.**

## 설치 위치 (ai_lab 저장소에 벤더링 안 함 — 610MB)
- 코드: `~/hermes-agent`(git clone + editable 설치, Python 3.11 전용 venv `.venv`)
- 상태: `~/.hermes/`(config.yaml·.env·cron/jobs.json·sessions/state.db·skills/)
- ai_lab엔 **통합 레이어만** 추적: `projects/ai-team/hermes/skills/*`(정본) → `~/.hermes/skills/`에 심링크

## 설정 (Phase 1 완료·검증)
- 모델: 로컬 Ollama(`gemma4:12b`, `http://localhost:11434/v1`) — 외부 API 비용 0
- 텔레그램/디스코드 **OFF**: 영숙 봇과 같은 토큰 충돌 방지(`gateway.platforms: []`). 크론 산출물은 파일 전달.
- 실행 검증: `hermes -z "..." --provider ollama` 로컬 왕복 정상, `hermes doctor` 클린.

## 실행 방식
- 크론: `hermes cron create <schedule> "<프롬프트>" --skill <스킬> --workdir /Users/junholee/ai_lab`
  - `--workdir`가 CLAUDE.md·파일/셸 도구 접근을 주입 → 에이전트가 ai_lab 안에서 작업.
  - 헤르메스 크론은 **게이트웨이 프로세스가 살아있을 때만** 발동(60초 틱).
- `--no-agent --script`: LLM 없이 순수 스크립트 스케줄(결정론 작업용 대안).

## ⚠️ 미해결: 네이티브 에이전트용 모델 (Phase 2 블로커)
경험 검증(2026-07-09): **gemma4:12b는 네이티브 도구 루프를 못 몬다** — 백호 감사 스킬을
로컬 gemma로 원샷 실행하니 파일 대조 대신 자기혼란 응답. 현재 데몬들이 `claude -p`(구독)를
쓰는 이유가 이것. 네이티브 재설계는 **강한 모델 필요**. 선택지(비용 정책 내):
1. 로컬 Ollama — 검증상 부족(기각).
2. 구독 프록시(`hermes proxy` + ChatGPT Codex/GitHub Copilot OAuth) — 강함·거의무료, 단 오너 대화형 로그인 필요.
3. 유료 API(OpenRouter/Anthropic 키) — 최고 품질이나 비용 정책 위반.
→ 오너 결정 대기. 결정 전까지 6개 네이티브 재설계 보류.

## 진행 상태
- [x] Phase 1: 설치·로컬 Ollama 설정·텔레그램 안전·e2e 검증
- [x] 레퍼런스 스킬 1개(petnna-backend-audit) 작성·심링크 설치
- [ ] Phase 2: 6개 에이전트 네이티브 재설계 — **모델 결정 후 착수**
