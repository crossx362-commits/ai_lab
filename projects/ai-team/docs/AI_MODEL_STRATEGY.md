# AI 모델 전략 가이드

> 이 문서의 상세 버전은 낡은 아키텍처(`ollama_client.py`/`gemini_client.py`, 2026-06 초 로스터
> 기준)를 기술하고 있어 2026-07-22 정리 시 전면 교체했다. 현재 모델 전략의 권위 있는 설명은
> **`CLAUDE.md`의 "🧠 AI Model Strategy (Unified LLM Client)" 섹션**이다 — 그쪽이 갱신되면
> 이 문서도 같이 갱신할 것.

## 핵심 요약

- 통합 클라이언트: `projects/ai-team/_shared/llm.py`
- 우선순위: **Ollama(로컬, 무료) → 구독 Claude Code(`claude -p`) → 구독 Codex(`codex exec`) → Gemini → GPT API → Claude API**
- 코딩 작업은 `task="coding"`(DeepSeek/Codestral 계열), 블로그/캡션은 `task="blog"`(Qwen 계열, DeepSeek 제외)
- 기본 진입점:
  ```python
  from _shared.llm import text
  response = text("프롬프트", lm_first=True, task="coding")
  ```
- json_mode 호출은 `max_tokens` 1500 이상 유지(700 이하면 응답이 잘려 파싱 실패)
- 클라우드 API 모델은 비용 최소화를 위해 `claude-haiku-4-5` 고정(오너 지시) — 고품질이 꼭 필요한
  작업만 `.env`의 `ANTHROPIC_MODEL`로 개별 상향

상세 배경(사고 이력·가드레일 근거)은 `CLAUDE.md`의 하네스 가드레일 섹션 참고.
