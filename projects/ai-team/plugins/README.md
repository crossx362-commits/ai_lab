# ai_lab 플러그인 마켓플레이스

Claude Code 플러그인 마켓플레이스.

## 구성

| 플러그인 | 에이전트 | 핵심 |
|---------|---------|------|
| `youngsuk-briefing` | 영숙(비서) | 데일리 브리핑·리포트 작성 (텔레그램 요약 + 노션 풀) |

## 설치 (등록)

이 마켓플레이스를 Claude Code에 등록한다(인터랙티브 세션에서):

```
/plugin marketplace add d:/ai_lab/projects/ai-team/plugins
/plugin install youngsuk-briefing@ai-lab
```

설치 후 사용 예:
- `/데일리브리핑` → 영숙 일일 브리핑

## 주의
- 디렉터리/플러그인 `name`은 ASCII 슬러그(로더 호환), 사람이 보는 텍스트·트리거·내용은 전부 한국어.
