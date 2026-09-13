# 울온 Codex 개발 현황

## 오너 지시로 무진척 반복 수리 (2026-09-12 19:38)

기존 하단의 통합/IMGUI 재개 조건은 해당 카드에만 적용한다. HUD 독립 구현·월드 확장 작업은 계속한다. 부모가 current_task를 배정하므로 이전 막힘 조회로 이를 대체하지 않는다. Codex 카드가 그록 제외 목적으로 막힘으로 바뀐 것은 개발 차단 사유가 아니다. 이번 HUD 대기 복구, 지도·가로등의 미검증은 막힘으로 명시. 부모는 no_progress 시 동일 카드 재호출을 유예하고, 실행 가능한 카드가 없으면 CLI 없이 waiting_work로 대기한다.


- 바퀴: 22
- 카드: `codex-storybook-lantern`
- 난이도: 하 — 저장 장면 프리팹 참조 종류별 집계와 원본/전용 차이 확인
- 상태: 검증 중
- 작업 공간: `/Users/junholee/ai_lab/.worktrees/ulon-codex-art`
- 브랜치: `codex/ulon-storybook-map`
- 기존 구현·검사 커밋: `8fc5a200` 유지

## #22 내부 재개 근거

증거: `/Users/junholee/ai_lab/output/ulon-codex/loop22-inventory/inventory.json`, `inventory.md`.

18:37 장면 오브젝트 종류별 정리 피드백을 읽고 기존 가로등 카드의 실제 적용 전 장면 참조 목록을 확인했다. 직접 저장 PrefabInstance는 전용1369·공유1383, GUID 미해석0. 식생611·울타리313·암석120·조명44 등 종류별 표와 전체 에셋 목록은 공유 보드 담당 카드에 기록. 중첩 내부·활성·실행 생성 수가 아니다.

원본 Bootstrap와 Village/Regions/Prefabs 빌더, HouseRoof 검사, RoofLeft/Right 신규 프리팹이 변경 중. 지붕 참조만 집계 차이14개이며 복사·병합·계층 이동 없음. 양 장면 SHA256은 읽기 전후 동일. Storybook Variant 직접 참조0; LanternLit29/TorchMounted14/원본 lantern.fbx1 유지. #21 임시 저장 Play 근거와 실제 Bootstrap 배치를 구별한다.

시작 clean, 부모45326과 현재 자식2869/2870 외 중복 codex exec 없음. 원본 Unity85035 및 작업 파일 제어 없음. 코드/에셋 변경이 없어 Unity 컴파일·Play·화면 검사는 이번에 수행하지 않았고 기존 결과를 새 PASS로 표기하지 않음.

## 재개 조건

#21 저장 복사본 검사는 통과했으므로 조건 변화 없이 반복하지 말 것. 실제 Bootstrap 적용과 계층 정리는 원본 주택/장면 소유권 충돌 해소 후 별도 검토. 18:20 집 모양 및 18:37 정리 피드백은 미처리 유지: 목록 제공만으로 계층 정리 완료가 아니다.
지도 #3 및 #12~15 IMGUI 동일 조건 반복 금지. 지원 헤드리스 경로 입증 후 재개. 새 카드·루프·예약·push 없음.

## #26 내부 재개 보정

보드가 읽기 도중 cards 0→29로 변경되었고 가로등 #17 기록으로 돌아와 #22 inventory 증거를 담당 카드에 다시 연결했다. 변경 주체·원인은 미확인. 다른 카드·최상위 필드 보존 및 API 반영 검증. 증거: `/Users/junholee/ai_lab/output/ulon-codex/loop26-board-reconcile/verification.json`. 원본 Bootstrap·빌더·지붕 관련 미커밋 변경이 지속되어 실제 적용 재개 조건 미충족. 18:20 피드백은 Grok #26 처리 표기, 18:37 계층 정리는 미처리. 게임 코드/에셋 변경·새 검증·커밋 없음. #22 근거를 현재 실행 PASS로 재해석하지 말 것.

## #27 내부 재개 보정

18:47 막힘 처리 요청 확인. 공유 HEAD e49e97b9, 지붕 ec8319f2 커밋으로 이전 미커밋 충돌 조건은 해소됨. 실제 원본/전용 Bootstrap 차이 +2138/-1131, Village +2, Regions +45/-20, Prefabs +3/-1. 증거: /Users/junholee/ai_lab/output/ulon-codex/loop27-ownership/ownership.json. 다른 에이전트 변경 병합 금지 범위 때문에 원본 복사·통합하지 않음. 통합 차선에서 지붕 보존과 가로등 적용 패치를 별도 검토해야 함. 담당 카드 verification_commit을 실제 최신 저장검증 8fc5a200으로 보정. API 반영 확인. 게임 코드/에셋 변경·Unity 검사·신규 커밋 없음. INBOX 두 미처리 항목 유지; 지도 IMGUI 동일 조건 반복 없음.

## #31 내부 재개 보정

원본 Village/Regions 미커밋 변경 및 신규 VisualSliceBuilder.Kind.cs·SliceSelfCheck.KindFolders.cs 확인. #27 이후 종류별 정리 관련 파일이 작업 중이므로 가로등 실제 적용과 겹치는 장면/빌더 수정 보류. 증거: /Users/junholee/ai_lab/output/ulon-codex/loop31-ownership/ownership.json. 담당 카드만 최신 읽기 후 갱신하고 API 일치 확인. 전용 트리 clean; 부모45326·현재 자식13071/13072 외 중복 세션 없음. 지도 IMGUI 동일 조건 재실행 없음. 신규 코드·에셋·Unity 실행·커밋 없음. 원본 정리 종료 및 별도 통합 차선에서 재개; 미처리 피드백 완료로 바꾸지 않음.

## #35 내부 재개 보정

종류별 정리 34c25fcd 커밋으로 #31 미커밋 충돌 해소. 공유 HEAD fed677fc. Bootstrap·Village·Regions·Kind·KindFolders 대상은 현재 clean이나 전용 트리와 다르고 신규 Kind 파일은 전용에 없음. 다른 에이전트 변경 병합 금지 때문에 복사·통합 없이 기존 가로등 카드만 보정. 증거: /Users/junholee/ai_lab/output/ulon-codex/loop35-ownership/ownership.json. 별도 통합 차선에서 종류 폴더·지붕 보존 후 가로등 적용 재개. INBOX 18:37은 Grok #27 처리 표기, 18:47은 미처리 유지. 부모45326·현재 자식15832/15833 외 중복 Codex 세션 없음. 코드/에셋 변경·Unity 검사·새 커밋 없음. 지도 IMGUI 동일 조건 재실행 없음.

## #40 내부 재개 보정

원본 HEAD fed677fc 유지이나 Village/Regions/Facilities 및 Build/Gates가 새 미커밋 상태이고 HouseCorner 검사가 신규 생성됨. #35 이후 소유권 충돌 재발로 가로등 실제 장면 적용 보류. 증거: /Users/junholee/ai_lab/output/ulon-codex/loop40-ownership/ownership.json. 전용 clean, 부모45326·자식19139/19140 외 중복 세션 없음. 지도 IMGUI 동일 조건 반복·새 카드·코드/에셋 변경·Unity 실행·커밋 없음. 기존 검증 결과 유지. 원본 작업 종료와 별도 통합 차선에서 재개. INBOX 18:47 미처리 유지.

## #46 내부 재개 보정

주택 모서리 f8ecfeb1 및 결과 c2ee97fa 커밋으로 #40 미커밋 충돌 해소. Bootstrap·빌더·검사 9개 대상 clean, 전용과 차이 유지. 증거: /Users/junholee/ai_lab/output/ulon-codex/loop46-ownership/ownership.json. 다른 차선 변경 통합 금지에 따라 원본 복사·병합하지 않음. 별도 통합 차선에서 Kind 폴더·지붕·wall-corner 보존 후 가로등 적용 재개. INBOX 18:47은 Grok #28 처리 확인, 미처리 없음. 부모45326·현재 자식22996/22997 외 중복 Codex 세션 없음. 전용 clean. 코드·에셋 변경, Unity/화면/게임/2인 검사, 신규 커밋 없음. 지도 IMGUI 동일 조건 재실행 없음.
