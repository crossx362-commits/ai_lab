# 울온 개발계획 — 2026-09-11 인수 대조

기준: 조사 시작 HEAD 921a830e, 작업 트리 /Users/junholee/ai_lab.
기획 원장: GAME_DESIGN.md §13·§14·§18. 기획의 완료 기준을 낮추지 않고 다음 실행 순서를 정한다.

## 현재 판정

**MVP 완료 미확인.** 외부 서버 2인 접속부터 재접속·사냥·성장·채집·제작·거래·은행까지 이어지는 §13.1 전체 실행 증거가 이번 조사에는 없다.

| 계획 단계 | 현재 코드 근거 | 이번 검증 / 남은 일 |
|---|---|---|
| 부트스트랩·Vertical Slice | Unity 프로젝트, OfflineWorld, Client, Editor 게이트 | C# 소스 303개 컴파일 통과. 씬·게임 플레이는 미검증 |
| 온라인·저장 | FishNet, NetAvatar, CharacterStore, persist.py, schema.sql | DB 정상 저장 및 실패 롤백 검증. 게임 재접속·외부 2인 검증 남음 |
| 채집·제작·경제 | OfflineWorld.Craft/Economy/Combat, OpLog | 구현 존재. 다중 플레이어 거래·로그 귀속·중단 복구 검증 남음 |
| 콘텐츠·UX | DrawQuickbar, ActionSfx, ActionVfx 및 빌더/게이트 | 과거 “없음” 판정 정정. 실제 UI 조작·SFX 청취·VFX 재생은 미검증 |
| 안정화·운영 | /ready, closed_alpha_smoke.sh, OpLog.Backup | 허위 준비 성공 수정. DB 백업·복구 E2E, 관심 영역·부하 시험 남음 |
| Closed Alpha | 로컬 준비 도구 존재 | 운영 PostgreSQL 5432 연결 거부 확인. 외부 배포·20~50명 부하·장시간 검증 미확인 |

## 이번 수정과 증거

1. **저장 원자성**: PostgreSQL autocommit으로 저장 중 오류 이전 쿼리가 확정되던 결함.
   캐릭터(스킬·인벤토리·은행·주문·시체 포함), 집, 마구간의 저장 요청을 명시적 commit/rollback으로 처리한다.
   마이그레이션의 기존 연결 동작은 유지한다.
2. **준비 상태**: /ready가 DB를 조회하고 실패 시 HTTP 503과 ok:false를 반환한다. /health는 프로세스 생존 신호다.
3. **준비 스크립트**: /ready 실패를 /health 성공으로 덮지 않고 JSON 객체의 ok:true를 확인한다. 비JSON을 성공으로 바꾸던 처리도 제거했다.

검증 도구:
- tools/test_persist_atomicity.py: SQLite + 실제 PostgreSQL 10개 테스트. 고립된 임의 스키마 사용.
  기존 코드에서 부분 저장 7개 실패를 재현했고 수정 후 통과. DB 장애 주입 HTTP 503도 검증.
- tools/test_alpha_readiness.py: 운영 서비스를 띄우지 않는 격리 환경에서 준비 실패/잘못된 JSON/ok:false/정상 응답 네 경우 확인.
- game_compile_check.py --project projects/ulon/unity: 303개 소스, 오류 0.
- 전체 하네스: 백업 지연·운영 프로세스 중단 경고로 **통과 아님**. 게임 성공의 근거로 사용하지 않는다.
- 실행 로그: 저장소 루트 output/ulon-handoff-20260911/ (git 제외).

PostgreSQL 테스트는 운영 DB를 사용하지 않고 /tmp의 전용 클러스터·Unix 소켓으로 실행했다.
재실행은 ULON_TEST_PG_DSN에 테스트 DB DSN을 지정하고 server/.venv/bin/python tools/test_persist_atomicity.py를 실행한다.
DSN 미지정이면 PostgreSQL 테스트는 명시적으로 SKIP되므로 SQLite 성공을 PostgreSQL 성공으로 보고하지 않는다.

## 이어서 진행할 순서

| 우선순위 | 작업 | 완료 증거 |
|---|---|---|
| P0 | CharacterStore의 JSON 폴백과 DB 복구 후 최신 저장 선택 검증 | DB 중단 중 저장 → 복구 → 재접속해 최신 인벤토리·스탯 유지 |
| P0 | 운영 PostgreSQL 및 저장 서비스 기동·준비 확인 | 실제 /ready 200, DB 조회, 서비스 재시작 후 동일 데이터 |
| P1 | §13.1의 두 플레이어 핵심 순환을 같은 버전에서 재검증 | 서버/a/b 로그, 거래 양쪽 결과, 은행·제작·재접속 확인 |
| P1 | 외부 전용 서버 검증 | 외부 서버 빌드와 별도 머신 2인 연결 결과. 로컬 성공으로 대체하지 않음 |
| P2 | DB·집·마구간 포함 백업/복원, 저장 재시도·중복 거래 방지 | 고장 주입, 복원 전후 데이터 비교. 현재 OpLog.Backup은 JSON/로그 복사라 DB 백업 증거가 아님 |
| P2 | 준비 상태 소비자 전체 및 오래된 alpha_status 파일 처리 점검 | 장애 뒤 과거 성공 상태를 현재 성공으로 오인하지 않음 |
| P3 | 관심 영역·LOD·20~50명 부하·메모리 기준선 | 실제 CPU/RAM/FPS/지연 측정. 기능 부재는 별도 전수 확인 필요 |
| P4 | 퀵바 단축키·Paperdoll·컨테이너 UX·SFX/VFX 사용성 | 플레이 화면과 입력/결과 검증 |

하우징·조련·길드/PvP 등 앞서 작성된 Phase 2 코드는 유지한다. 존재만으로 MVP 완료나 운영 안정성을 주장하지 않는다.
GPT/Ollama/Claude/Grok 역할과 구독 중단 복구는 개발 도구의 별도 검증 항목이며, 이번 게임 저장 수정으로 완료된 것이 아니다.

## 작업 보존과 한계

- 시작 시 공유 트리 변경은 이전 턴의 .codex/hooks.json 한 개였다. 그 변경은 이번 수정과 분리한다.
- ai_lab-loop는 다른 HEAD와 미추적 에셋을 갖고 있다. 해당 파일·브랜치를 정리하거나 덮어쓰지 않았다.
- 현재 울온 Unity 에디터가 열려 있다. 씬을 checkout하는 기존 selfcheck 스크립트는 실행하지 않았다.
- 전체 기획 모든 조항의 플레이 검증은 미완료다. DESIGN_COVERAGE의 수정하지 않은 과거 ①은 현재 실행 통과를 뜻하지 않는다.
