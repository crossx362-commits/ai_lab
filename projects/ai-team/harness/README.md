# AI Team Harness

**목적**: 레포지토리 구조 + 런타임 상태 검증

## 사용법

```bash
python projects/ai-team/harness/check_all.py
```

## 검증 항목

| 항목 | 설명 |
|------|------|
| env | 환경변수 로딩 (_shared.env) |
| ops_hygiene | 디스크 여유·상태백업 신선도 |
| runtime | 에이전트 실행 상태 (영숙 등 상시 데몬) |
| schedule | 영숙 스케줄러 유효성 |
| structure | 폴더 구조 완전성 |
| classification_layout | 루트/문서/스킬 레이아웃 규칙 준수 |

## 출력 예시

```
[OK] env: ✅ unified env loaded
[WARN] runtime: youngsuk=down
[OK] schedule: enabled 14/14
```

## 통합 모듈

하네스는 6개 통합 모듈을 사용:
- env.py, llm.py, telegram.py, notify.py, process.py, utils.py
