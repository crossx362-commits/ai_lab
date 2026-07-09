# AI Team Skills

이 폴더는 에이전트별 지시문(`SKILL.md`)과 실행 도구(`tools/`)를 담는 운영 중심 영역입니다.

## 현재 에이전트

| 에이전트 | 역할 | 주요 도구 |
| --- | --- | --- |
| `예원_CEO` | 오케스트레이션·하네스·워치독 | `tools/yewon_dispatcher.py`, `tools/harness_monitor.py` |
| `영숙_비서` | 텔레그램 게이트웨이·일정·정시 잡 | `tools/telegram_receiver.py`, `tools/schedule_manager.py` |
| `봄이_QA` | 펫나 상시 QA 순찰 | `tools/petnna_qa_patrol.py` |
| `수리_개발자` | 펫나 자동 개선 엔진 | `tools/petnna_dev_engine.py` |
| `테오_테스트` | 펫나 E2E 테스트 자동 작성·실행 | `tools/petnna_test_engineer.py` |
| `백호_백엔드` | Supabase 계약 감사 | `tools/petnna_backend_guard.py` |
| `미오_디자인` | 펫나 디자인 리뷰 | `tools/petnna_design_review.py` |
| `나무_기획` | 펫나 기획 PM | `tools/petnna_product_manager.py` |
| `공용스킬` | 공통 지식/가이드 | Markdown 지식 파일 |

> 과거 주식·코인 트레이딩 에이전트(소미·한별·행크·유나·레온·마켓데스크·지아)와 그 이전 세대
> 에이전트(데이브·레오·시그널·펄스·케빈·경수·코다리·티모·로율)는 전부 삭제됨 —
> git 이력에서 복구 가능. 배경은 `CLAUDE.md` 하네스 가드레일 섹션 참고.

## 운영 규칙

- 각 에이전트 폴더는 `SKILL.md`를 기준 문서로 둡니다.
- 실행 파일은 가능한 한 `tools/` 아래에 둡니다.
- 여러 에이전트가 쓰는 클라이언트, 히스토리, 환경변수 로더는 `_shared/`에 둡니다.
- 디스패처 연결은 `예원_CEO/tools/yewon_dispatcher.py`에서 관리합니다.

## 점검 명령

```bash
python projects/ai-team/harness/check_all.py
```
