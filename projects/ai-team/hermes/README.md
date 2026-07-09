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

## 모델 결정: 구독 프록시(openai-codex) — 오너 로그인 1회 필요
오너 결정(2026-07-09): 네이티브 에이전트는 **구독 프록시**(ChatGPT Codex) 사용.
헤르메스 `openai-codex` 프로바이더가 `~/.codex/auth.json`을 재사용하도록 설계돼 있으나,
**진단 결과 헤드리스 재사용 불가**:
- 기존 codex 토큰이 만료 상태(codex CLI는 호출 시 자동 갱신해 작동하지만, 헤르메스가 읽는 원시 토큰은 만료).
- OAuth refresh 토큰은 **단일 사용** → 헤르메스와 기존 codex CLI가 같은 토큰을 두고 서로 무효화(충돌).
  헤르메스 자체도 "전용 로그인 권장"을 명시.
→ **오너가 1회 대화형 로그인 필요**(내가 헤드리스로 못 함). 터미널에서:
```
cd ~/hermes-agent && source .venv/bin/activate
python hermes auth add openai-codex     # 프롬프트에서 N(전용 새 세션 로그인) 선택 → 브라우저 OAuth
```
로그인 후 알려주면 config를 openai-codex로 전환하고 6개 네이티브 에이전트 구축 착수.
안전 확인: 위 진단이 기존 codex CLI(_shared/llm.py) 인증을 깨지 않음(검증 완료).


## 모델 벽 (2026-07-09 실측) — 오너 결정 필요
코덱스 제외(오너) 후, "강한 모델 + 무료 + 로그인없음 + 이 맥(16GB RAM)"이 동시에 되는 길 없음:
- 로컬 대형: **16GB RAM 한계로 ~12b급뿐** → gemma4:12b가 도구루프 실패(검증). 32b+ 미탑재.
- Gemini 무료: 분당 5요청 → 에이전트 루프 즉시 429 초과(실측). 게다가 기존 시스템도 쓰는 키.
- Anthropic API: 크레딧 소진(llm_probe 로그). Claude Max 구독은 헤르메스에 raw 모델로 못 꽂음(claude -p는 자체 에이전트).
남은 실제 선택지(하나의 제약을 완화해야 함):
1. 1회 구독 로그인 허용(Copilot `GITHUB_TOKEN` / Nous `hermes auth add nous`) — 강함·거의무료.
2. 소액 유료(OpenRouter 크레딧) — 최고 품질, 비용 정책 완화.
3. Windows PC에서 Hermes 실행(RAM/GPU 여유 시 로컬 대형 가능).
4. 현행 유지 — 6개 claude -p 데몬이 결정론 작업엔 이미 우수. 헤르메스는 챗 게이트웨이/자연어크론 등 특기 용도로만.
→ 오너 결정 대기. (설치·스킬·설정은 다 준비됨 — 모델만 정해지면 즉시 착수.)


## 최종 결론 (2026-07-09 실증 완료): 헤르메스 자체 루프 = 무료 불가
Claude Max OAuth를 헤르메스에 물려 백호 감사 실행 → **HTTP 400: "Third-party apps now draw
from your extra usage, not your plan limits."** Anthropic 정책 변경으로 **제3자 앱(헤르메스)이
Max 구독 토큰을 쓰면 유료 extra usage 과금**(현재 잔액 0 → 차단). `claude -p`(Claude Code
자체)만 Max 플랜 포함 무료.

→ **"헤르메스 자체 에이전트 루프 + 강한 모델 + 무료 + 이 맥"은 물리적으로 불가**(전 경로 실증):
로컬(16GB→12b 약함)·Gemini무료(5req/min)·AnthropicAPI(크레딧0)·ClaudeMax(제3자 유료)·코덱스(제외).

## 남은 진짜 선택지 (오너 결정)
- **A. 무료 하이브리드**: 헤르메스가 스케줄러/오케스트레이터, 실제 에이전트는 기존 `claude -p`
  스크립트(`--no-agent`)로 실행. Claude 사용·무료·무로그인·헤르메스가 실행엔진. 단 헤르메스
  자가학습/메모리 루프는 못 씀(그건 유료라). 사실상 launchd+α. **내가 지금 바로 구축 가능.**
- **B. 유료 수용**: extra usage 충전 or OpenRouter 소액 → 헤르메스 네이티브 자가학습 루프 풀가동.
- **C. 1회 구독 로그인**: 코덱스/Copilot/Nous 중 하나 대화형 로그인(코덱스는 오너 제외).
- **D. 현행 유지**: 6개 claude -p 데몬 그대로(결정론 작업엔 이미 최적). 헤르메스 보류.

## 진행 상태
- [x] Phase 1: 설치·로컬 Ollama 설정·텔레그램 안전·e2e 검증
- [x] 레퍼런스 스킬 1개(petnna-backend-audit) 작성·심링크 설치
- [x] 모델 결정: 구독 프록시(openai-codex)
- [ ] 오너 1회 로그인(`hermes auth add openai-codex`) — **대기**
- [ ] Phase 2: 6개 에이전트 네이티브 재설계 — 로그인 후 착수
