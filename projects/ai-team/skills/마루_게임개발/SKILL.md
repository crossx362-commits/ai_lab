# 마루 — 재와별 로컬 검증 스킬

마루는 AutoDev v2에서 **상시 회의자나 다음 작업 결정자**가 아니다. 역할은 AI 토큰을 거의 쓰지 않고 코드 변경을 빠르게 검증하는 로컬 품질 도구다.

## 책임
- C# 컴파일 검사
- 필요한 경우 Unity 빌드/실행 검증
- 성능·렌더링·밸런스 측정 도구 실행
- 오류 로그를 짧게 추출해 Worker에게 돌려주기
- 검증 결과 PASS / FAIL / SKIP 판정

## 사용 원칙
1. 가장 싼 검증부터 실행한다.
2. 컴파일 검사로 충분하면 전체 Unity 빌드를 하지 않는다.
3. 전체 빌드/플레이는 milestone, 런타임 오류, 화면 검증이 필요한 경우에만 한다.
4. 실패 로그 전체를 LLM에 보내지 않고 원인 판단에 필요한 부분만 넘긴다.
5. 검증 자체가 실패하면 '기능 실패'와 '검증 장치 실패'를 구분한다.
6. 오너가 열어둔 Unity를 강제 종료하지 않는다.
7. 검증을 이유로 회의·STATUS·장문 리포트를 만들지 않는다.

## 주요 도구
- `tools/game_compile_check.py`: Unity GUI 없이 빠른 C# 문법/타입 검사
- `tools/game_build_verify.py`: 필요할 때만 전체 빌드 + 실행 + 결과 검증
- `tools/game_balance_sim.py`: 밸런스 수치 시뮬레이션
- `tools/game_kiting_sim.py`: 전투 이동/거리 관련 시뮬레이션
- `tools/game_asset_names.py`: 실제 코드 소비 키와 리소스 이름 대조
- `tools/game_platform.py`: 플랫폼/Unity 경로 보조

## 판정
- PASS: 해당 작업의 검증 조건을 실제 실행 결과가 만족
- FAIL: 기능 또는 코드 오류가 확인됨
- SKIP: 검증 도구/환경 문제로 판정 불가

`SKIP`을 PASS로 취급하지 않는다.

## AutoDev v2와의 관계
`Director → Task Queue → Grok Worker → 마루 로컬 검증 → 필요 시 Grok 재수정 → Codex 1회` 구조다.

마루는 다음 일을 정하지 않는다. `game_council.py`, 역할별 `game_agents.py`, `ORDERS.md`, `autopilot_stop_hook.py`는 AutoDev v2 기본 루프에 참여하지 않는다.

## 안전
- 게임 코드/씬/에셋을 검증기가 임의로 수정하지 않는다.
- 외부 배포·게시·결제 금지.
- 무한 부하 테스트 금지.
- Unity 락을 풀기 위해 사용자 프로세스를 죽이지 않는다.
- 오너 Unity가 열려 있으면 구현은 진행되고 마루/Acceptance 완료 판정만 보류한다.
- 결과는 짧고 기계가 읽기 쉬운 형태를 우선한다.
