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
| runtime | 에이전트 실행 상태 (youngsuk, pulse, dave, leo) |
| schedule | 영숙 스케줄러 유효성 |
| trading | 트레이딩 데이터 최신성 |
| structure | 폴더 구조 완전성 |

## 출력 예시

```
[OK] env: ✅ unified env loaded
[WARN] runtime: youngsuk=down; pulse=1728
[OK] schedule: enabled 14/14
```

## 통합 모듈

하네스는 5개 통합 모듈을 사용:
- env.py, llm.py, notify.py, process.py, utils.py

자세한 내용: `MIGRATION_GUIDE.md`
