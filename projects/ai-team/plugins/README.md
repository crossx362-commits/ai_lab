# ai_lab 플러그인 마켓플레이스

Claude Code 플러그인 마켓플레이스. 주식·코인 관련 플러그인(소미 리서치, 예원 리서치 디스패처)은
2026-07-08 오너 지시로 도메인 전체 삭제와 함께 제거됨.

## 구성

| 플러그인 | 에이전트 | 핵심 |
|---------|---------|------|
| `youngsuk-briefing` | 영숙(비서) | 데일리 브리핑·리포트 작성 (텔레그램 요약 + 노션 풀) |

## 설치 (등록)

이 마켓플레이스를 Claude Code에 등록한다(인터랙티브 세션에서):

```
/plugin marketplace add d:/ai_lab/projects/ai-team/plugins
/plugin install youngsuk-briefing@ai-lab-korea-finance
```

> 마켓플레이스 이름은 `.claude-plugin/marketplace.json`의 `ai-lab-korea-finance`(레거시 이름 유지 — 이미 등록된 로컬 참조 보존).

설치 후 사용 예:
- `/데일리브리핑` → 영숙 일일 브리핑

## 주의
- 디렉터리/플러그인 `name`은 ASCII 슬러그(로더 호환), 사람이 보는 텍스트·트리거·내용은 전부 한국어.
