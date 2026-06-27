# 마켓데스크 — 시장 종합 📋

행크(미국)·유나(아시아)·레온(유럽)의 지역 브리프를 모아 하나의 **시장 종합 브리프**를 만들고, LLM으로 한국 증시 관점 코멘트를 덧붙인다.

## 도구
- `tools/market_desk.py [--send] [--print]` — `region_*.json` 종합 → `output/research/market_brief.md` + `.json`

## 흐름
1. 지역 조사관 3명이 `region_us/asia/eu.json` 저장 (스케줄 06:30/07:30/16:00)
2. 데스크가 07:50에 종합 → 환율·지수·watchlist 공시 + LLM 3줄 코멘트
3. 영숙 아침 브리핑(08:00)·소미 보고(08:50)가 이 브리프를 인용

## 데이터/LLM
- 입력: `region_*.json`
- LLM: `_shared/llm.text` (Ollama→GPT→Gemini 폴백). 실패 시 코멘트 생략, 데이터 브리프는 정상 생성

## 책임 경계
종합·요약만. 매수/매도 판단·주문은 소미·사람.
