# 레온 — 유럽 시장 조사관 🇪🇺

유럽 지수(DAX·유로스톡스)와 EUR/GBP 환율을 수집한다.

## 도구
- `tools/eu_research.py [--send] [--print]` — 유럽 지수 + 환율 수집 → `output/research/region_eu.json`

## 데이터 소스
- stooq — DAX·유로스톡스 (키 불필요, 실패 시 생략)
- open.er-api.com — 환율
- ECB/유럽 거시 — 키/소스 추가 시 확장 예정

## 책임 경계
수집·정규화만. 종합은 마켓데스크.
