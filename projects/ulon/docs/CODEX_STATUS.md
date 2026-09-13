# 울온 Codex 개발 현황

## 오너 막힘 해결: 15675b18 (2026-09-12)

헤드리스 UI 경로 확보. 공유 tools/offscreen_imgui는 root 작성분만 있으므로 전용 트리에 가져와 실제 HUD/지도 어댑터를 구현할 수 있다. offscreen-verified3 최종 exit0: 실제 Screen1440×900/1024×768, Repaint72/121, 글자708픽셀, 글자 제거 대조717픽셀 차이, 전체IMGUI제거 Repaint0/글자0. 두 PNG 직접 확인. 진단 UI만 검증이며 실제 게임/OS입력/2인 PASS 아님. 과거 하단의 「지원 렌더 경로 미확보」「ScreenCapture만 허용」재개 조건은 이 새 근거로 해소됐다. 지도/HUD를 새 어댑터 구현 대상으로 복구. 원본 통합 대기는 전용 Bootstrap 가로등 한정 적용 도구·재생성 선택 구현을 막지 않는다. 월드485ebce0 근거 복구 후 #72 코드 변경 확인. 원본 세션/프로세스 제어 없음. 하네스 기존 데몬down/백업정체/아트분류 경고 exit1 유지.


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

## #58 HUD 독립 구현

22f2e28b: HUD 종이/목재/금속 프레임·밤색 본문·청록 선택 칸, 가방/장비 줄바꿈 및 HP/MP 잔량선 구현. 전용 Unity 컴파일 exit 0·스킨/대비/자원 검사 10 PASS. 기존 컴파일 경고·라이선스/Curl 진단 잔존. 실제 화면·DnD·게임·2인 검증 미실행; 완료 아님.

재개: OnGUI/Repaint 및 ScreenCapture가 작동하는 승인된 헤드리스 경로 확보 후 두 해상도·긴 한글·선택/비활성·장착/해제·은행/주머니 DnD 검증. 동일 환경 IMGUI 프로브 반복 금지. 원본 통합은 별도 차선.
증거: /Users/junholee/ai_lab/output/ulon-codex/loop58-hud/unity.log 및 전용 docs/CODEX_HUD_58.md. 원본/타 차선 수정·병합·프로세스 제어 없음. 부모32817/현재 자식32822·32823 확인. 작업 시작 전 전용 clean. 19:41 INBOX 담당 보호 지시 준수; 공유 INBOX 타 차선 항목 수정 없음.

## #59 3072m 확장 기반 독립 구현

485ebce0: WorldExpansionTerrain Editor 생성기, 512m×6×6·타일1025²·셀0.5m·TerrainCollider·LOD 이웃. 실제 Bootstrap는 600m 유지, 확장 외곽은 기존 함수의 해저이며 육지/콘텐츠 확대 완료 아님.
전용 Unity 6000.3.14f1 batchmode/nographics 최종 exit0, 60경계·기존3481+타일중심36 충돌·부모배율거부·불연속 NC PASS. 실제 화면/게임/2인 미검증 및 기존 경고 잔존. 증거 /Users/junholee/ai_lab/output/ulon-codex/loop59-world/final.log, 전용 docs/CODEX_WORLD_EXPANSION_59.md.
다음: 기존 섬 보존 띠 밖 확장 육지와 연결 경로/§6.1 콘텐츠 후보 구현, 지도·서버 경계·스폰·관심 영역 대조. 다른 카드 IMGUI 조건은 이 카드 구현을 막지 않는다. 3072m 원작 절반 환산은 미확인. 부모32817/자식34294·34295 확인, 원본 Unity85035 제어 없음. 공유 보드 본인 카드만 갱신, Grok 번호 필드 유지.

#59 보드 API 확인은 connection refused. 공유 board.json 재조회/타 카드 보존은 확인했으며 웹 화면 반영은 미확인. 서버 재시작 없음.

## #60 지도 검사 및 재개 조건

74fcb904: 현재 전용 코드 지도 로직 PASS·Unity batchmode/nographics exit0. 라이선스/Curl 진단 잔존. 코드/에셋 변경 없는 검증 전용 바퀴. 실제 UI·입력·게임·2인 검증 아님. 카드 막힘으로 보정; OnGUI/Repaint와 ScreenCapture 및 요청 해상도가 지원 헤드리스 경로에서 입증된 뒤 두 해상도/M입력/2인 시체 회수·만료 검증 재개. 동일 IMGUI 프로브 반복 금지, 다른 카드 구현은 독립 진행. 증거: 전용 docs/CODEX_MAP_60.md, output/ulon-codex/loop60-map/unity.log. 타 카드/필드 보존 확인.

## #61 HUD 빈 드롭 칸 배치

6fd9bef8: 가방/은행 4~5열로 넘치던 빈 칸을 공통 3열 줄바꿈으로 수정. 전용 Unity 컴파일 exit0·스킨 검사10 PASS, 242개 산술 모델 확인은 실제 GUILayout 검증과 별개. 기존 경고·라이선스/Curl 진단 잔존. 실제 화면/DnD/게임/2인 미실행, 동일 IMGUI 프로브 반복 없음. 검증 중 유지.
재개: 승인 헤드리스 OnGUI/Repaint/ScreenCapture 확보 후 두 해상도, 0/2/3/6개·긴 한글·빈 칸 DnD·하단 스크롤 확인. 독립 구현은 이 조건에 차단되지 않음. 증거: 전용 docs/CODEX_HUD_61.md 및 output/ulon-codex/loop61-hud/. 부모32817·현재38392/38393 외 중복 없음, 원본 Unity85035 제어 없음. 타 카드·최상위·Grok 번호 필드 보존 확인.

## #62 지도 재개 조건 확정

부모32817·현재42004/42017 확인, 전용 clean. 지정 지도 카드의 실제 코드/기획/기존 재현 JSON 대조. #60 이후 지원 헤드리스 IMGUI 경로 입증 자료는 확보하지 못함. 동일 프로브·로직 재검사 없이 담당 카드 막힘으로 전환. 코드/에셋 변경·신규 실행·커밋 없음이며 개발 진척으로 세지 않는다. 타 카드/최상위/Grok 번호 필드 보존 확인. 증거: /Users/junholee/ai_lab/output/ulon-codex/loop62-map/review.json
재개: 창을 열지 않는 승인된 실행 경로에서 OnGUI/Repaint>0, ScreenCapture PNG 및 요청 해상도를 먼저 입증한 뒤 전용 게임 1440×900/두 번째 해상도·M 입력·지역 범례·2인 시체 수신/회수/만료 검증. 엔진/렌더 경로 변화 증거 없이 기존 프로브와 로직 검사를 반복하지 않는다. 독립 구현 요구가 새로 생기면 별도 resume_token으로 재개 가능.

## #63 가로등 실제 적용 재개 조건

전용 clean, 부모32817·현재44122/44123 확인. Bootstrap 후보 직접 참조0, 공유/전용 장면·생성기·검사9개 차이 유지. 기존 Variant/저장 복사본 검사 반복 없이 담당 카드 막힘 전환. 다른 차선 통합 금지 때문에 원본 복사·병합 안 함. 새 코드/에셋/커밋/Unity/렌더/Play/2인 검증 없음; no_progress와 구별하지 말 것.
재개: 별도 통합 차선에서 현재 공유 Kind 폴더·지붕·wall-corner를 보존한 기준 장면/생성기를 마련하고 가로등 적용 소유권을 인계한 뒤 resume_token 변경. 이후 Variant 배치 영속성·재생성·VFX·전용 batchmode Metal 게임 화면 검증. 독립 Variant 개선 요구가 생기면 별도 재개 가능.
증거: /Users/junholee/ai_lab/output/ulon-codex/loop63-lantern/review.json . 타 카드·최상위·Grok 번호 필드 보존 확인.

## #64 HUD 은행 선택 안내

f5ca2ae6: 은행 선택 이름/수량과 가방/은행 줄바꿈 안내 구현. 전용 Unity 컴파일 exit0·기존 스킨10 PASS. 경고/라이선스/Curl 잔존. 실제 화면·스크롤·선택/찾기·DnD·게임·2인 미검증으로 검증 중 유지. 동일 IMGUI 프로브 반복 없음. 재개: 승인 헤드리스 OnGUI/Repaint/ScreenCapture/요청 해상도 입증 후 두 해상도·긴 이름·은행 선택/해제/찾기 안내·하단 도달 검증. 원본 통합 별도. 부모32817·현재46364/46372 외 중복 없음. 원본 파일/프로세스 제어 없음. 증거 output/ulon-codex/loop64-hud/unity.log, board-verification.json 및 전용 docs/CODEX_HUD_64.md. 타 카드·최상위·Grok 번호 보존 확인.

## #65 HUD 장비 전체 이름 안내

0711b395: 장비 칸 아래 전체 장착 이름/비장착 안내 추가. 전용 Unity 컴파일 exit0·기존 스킨10 PASS. 이번 로그 warning CS/error CS 없음, 라이선스/Curl 진단 잔존. 실제 화면·장착/해제·DnD·게임·2인 미검증으로 검증 중 유지. 재개: 승인 헤드리스 OnGUI/Repaint/ScreenCapture/요청 해상도 입증 후 두 해상도·긴 한글/태그 모양 이름·장착/해제 안내·추가 높이와 하단 도달 검증. 동일 IMGUI 프로브 반복 없음. 원본 통합 별도. 부모32817/현재49247·49254, 원본 SliceHud.cs 변경 보존. 증거 output/ulon-codex/loop65-hud/unity.log, board-verification.json 및 전용 docs/CODEX_HUD_65.md. 타 카드·최상위·Grok 번호 필드 보존 확인.

## #66 HUD 드래그 이름표

a60dabc6: 고정 120×24 이름표를 176폭·CalcHeight 높이·종이/목재/밤색·줄바꿈·richText=false로 개선. 서버 이동/드롭 판정 유지. 전용 Unity 컴파일 exit0·기존 스킨10 PASS; 기존 Editor warning CS·라이선스/Curl 진단 잔존. 새 이름표 배치/입력 검사 아님. 실제 화면·DnD·게임·2인 미검증, 검증 중 유지.
재개: 기존 승인 헤드리스 OnGUI/Repaint>0·ScreenCapture PNG·요청 해상도 입증 후 두 해상도·긴 한글/태그 모양 이름·드래그 이름표 높이·스크롤 패널 가장자리 잘림·드롭 성공/취소와 기존 장착/은행 조작 확인. 이름표가 스크롤 내부에 그려지는 구조의 가장자리 잘림은 미해결. 동일 IMGUI 프로브 반복 없음. 원본 통합 별도. 부모32817/자식53042·53043, 전용 시작 clean·Library 비링크 확인. 원본 프로세스/파일 제어·통합·push 없음. 증거 output/ulon-codex/loop66-hud/unity.log, board-verification.json 및 전용 docs/CODEX_HUD_66.md. 타 카드·최상위·Grok 번호 필드 보존 확인.

## #67 HUD 이름표 패널 클립 분리

9094fe68: DrawDragGhost를 OnGUI 마지막 화면 좌표로 이동·가방에서만 표시·화면 경계 위치 제한. 현재 PanelBag에는 스크롤이 없으며 #66 설명은 패널 Area 클립으로 정정. 드롭 판정 좌표/서버 명령 유지. 전용 Unity 컴파일 exit0·기존 스킨10 PASS, 이번 error CS/warning CS 없음·라이선스/Curl 잔존. 실제 화면/DnD/게임/2인 미검증으로 검증 중. 재개: 승인 헤드리스 OnGUI/Repaint>0·PNG·요청 해상도 입증 후 두 해상도·긴 이름·네 모서리·패널 밖 드래그·드롭 성공/취소 및 기존 장비/은행/하단 도달 검증. 동일 프로브 반복 없음, 원본 통합 별도. 부모32817/자식58854·58863. 증거 output/ulon-codex/loop67-hud/unity.log, board-verification.json, 전용 docs/CODEX_HUD_67.md. 타 카드·최상위·Grok 번호 보존 확인.

## #68 HUD 드래그 포커스 상실 취소

1a718c65: 기존 MouseUp 해제를 공용 CancelItemDrag로 모으고 OnApplicationFocus(false)/OnDisable에서 취소. 전용 Unity 수정 전 포커스 상실 상태 유지 FAIL(exit1), 수정 후 상태 전이4·스킨10 PASS/컴파일 exit0. 기존 Editor warning CS·라이선스/Curl 진단 잔존. 실제 OS 입력/OnGUI/화면/DnD/게임/2인 검증 아님, 검증 중 유지. 기존 재개 조건에 포커스 이탈/복귀 후 이름표·오래된 드롭 부재 확인 추가. 동일 IMGUI 프로브 반복 없음. 원본 통합 별도. 부모32817/자식63325·63333. 증거 output/ulon-codex/loop68-hud/{red.log,unity.log,board-verification.json}, 전용 docs/CODEX_HUD_68.md. 타 카드·최상위·Grok 번호 보존 확인.


## #69 HUD 가방 이탈 드래그 취소

cb224883: Container.LateUpdate에서 가방 밖 드래그 취소. ShowPanel+LateUpdate 메시지 기반 수정 전 None 이탈 FAIL(exit1), 수정 후 상태 전이17·스킨10 PASS 및 전용 Unity 컴파일 exit0. 기존 Editor warning CS·라이선스/Curl 잔존. 실제 프레임/입력/OnGUI/화면/DnD/게임/2인 미검증으로 검증 중. 기존 헤드리스 재개 조건 유지, 가방 닫기·각 탭 전환/복귀 이후 이름표와 오래된 드롭 부재 확인 추가. 동일 IMGUI 프로브 반복 없음. 공유 변경 중 SliceHud.cs/Actions/Panels/NetAvatar는 수정·복사하지 않음. 원본 통합 별도. 부모32817/자식67493·67494. 증거 output/ulon-codex/loop69-hud/{red.log,unity.log,board-verification.json} 및 전용 docs/CODEX_HUD_69.md. 타 카드·최상위·Grok 번호 보존 확인.


## #70 HUD 앱 일시정지 드래그 취소

fe7c7fd5: OnApplicationPause(true)에서 공용 취소. 수정 전 pause 메시지 상태 FAIL(exit1), 수정 후 상태19·스킨10 PASS 및 전용 Unity 컴파일 exit0. 기존 Editor 경고·라이선스/Curl 잔존. 초기 MouseUp 직접 주입은 Event.current null로 무효(red.log), 해당 수정 미적용. 실제 OS/프레임/입력/화면/DnD/게임/2인 미검증, 기존 재개 조건 유지 및 앱 일시정지/복귀 후 오래된 이름표·드롭 부재 확인 추가. 동일 IMGUI 프로브 반복 없음. 원본 통합 별도. 부모32817/자식71283·71290, 전용 clean·Library 비링크. 증거 output/ulon-codex/loop70-hud/{red.log,pause-red.log,unity.log,board-verification.json}, 전용 docs/CODEX_HUD_70.md. 타 카드·최상위·Grok 번호 보존.


## #71 HUD 검증 전용 및 막힘 전환

037fe3f2: 현재 상태19·스킨10 PASS, 전용 Unity batchmode/nographics exit0. error CS/warning CS 없음, 라이선스/Curl 잔존. 코드/에셋 변경 없는 검증 전용이며 개발 진척 아님. 실제 화면·입력·DnD·게임·2인 미검증. 동일 IMGUI 프로브 반복 없이 지정 HUD 카드 막힘으로 전환. 재개: 기존 화면/입력 경로 입증 또는 독립 구현 요구/확인 가능한 결함 발생 후 resume_token 변경; 동일 상태 검사만 반복 금지. 원본 통합 별도. 증거 output/ulon-codex/loop71-hud/unity.log 및 전용 docs/CODEX_HUD_71.md. 부모32817/자식74883·74884 확인, 원본 프로세스 제어 없음. 타 카드·최상위·Grok 번호·구현 commit 보존.


## #72 확장 육지/연결 길 독립 구현

44f0813e(문서06ff8ea8): 기존 600m 섬 및 바깥20m 해저 띠 보존, 동·북동 초원/숲/산지 지형 후보와 1100m·폭8m 통행 띠, 기존4레이어 구현. 전용 Unity 수정 전 수면여유 FAIL(exit1)→높이 구현 후 PASS(exit0)→표면/Metal 최종 exit0. 60경계·기존충돌3481·타일중심36·통행1656·보존함수1681·외곽/높이/NC PASS. 8m 표본 추가육지 약491520m². 기존 CS 경고 및 라이선스 Access token 진단 잔존.
Metal 1440×900 Edit-mode 생성 지형3장 직접 검토: 확장 내부 길 연결 확인. 단순 타원 해안/직각 길·평탄한 지역/소품 부재, 검증 수면 외곽 경계가 보이므로 최종 아트 미합격. 실제 Bootstrap600m·기존 콘텐츠 유지, 중앙 해상 연결·지도/서버 이동/스폰/관심영역·게임/2인 미완료, 검증 중. Blender/OnGUI/플레이 실행 아님.
재개: 기존 지형 보존 해상 연결/접근로, 자연스러운 해안·지역 표식부터 독립 진행. 이후 전용 저장/재생성/실제 이동·2인 검증. 원본 병합·offscreen-repair·다른 IMGUI 재개 조건으로 이 카드 차단 금지. 원작 절반→3072m 환산 미확인 유지.
증거 output/ulon-codex/loop72-world/{red.log,green.log,metal.log,layout-top.png,layout-quarter.png,route-corner.png,board-verification.json}, 전용 docs/CODEX_WORLD_EXPANSION_72.md. 부모32817/현재8041·8042 확인, 전용 시작clean·Library 비링크. 원본/다른 차선 파일·프로세스 및 격리 offscreen-repair 제어 없음. 타 카드/최상위/Grok 번호 필드 보존 확인.
보드 API HTTP200 확인; 구현 커밋 포함 여부: True


## #73 실제 지도 OnGUI 어댑터

0e716f79: 전용 Editor에서 실제 SliceHud.DrawWorldMap 호출·M 상태 전이 분리. 입력 red exit1 뒤 합성6 PASS, 지도 로직9 PASS. Metal batchmode final exit0: 실제1440×900/1024×768, 제목잉크346/343·범례945, 흰점/본인 시체 픽셀·경계 검사. 닫기95940/만료931픽셀 차이·IMGUI제거 Repaint0. PNG4장 직접 검토. 기존 Editor CS 경고/라이선스 진단 잔존. 전체 HUD/Play/OS 입력/게임/2인/원본 통합은 미검증; 검증 중.

재개: CodexMapOffscreenCheck와 CodexMapOffscreenBridge에 전체 HUD/Play 문맥을 연결한다. 초기 렌더 경로 프로브 반복 불필요. 현재 합성 fixture는 서버 수신을 입증하지 않음. ULON_MAP_EVIDENCE는 새 빈 디렉터리, batchmode Metal, -quit/-nographics 없이 실행. camera.targetTexture/s_RenderingView/GL.LoadPixelMatrix/Editor 프레임 폰트 준비 유지. 증거 output/ulon-codex/loop73-map/{final.log,logic.log,review.json,board-verification.json}, 전용 docs/CODEX_MAP_73.md. 부모32817/자식18504·18505 외 중복 없음. 원본 파일/프로세스·격리 offscreen-repair 미조작. 한 바퀴 종료.


## #74 전용 Bootstrap 가로등 한정 적용

e12cd812 + 4e27ec7f: 실제 전용 Bootstrap 29개 기존 LanternLit → Storybook Variant. 루트 Transform/부모/순서/이름/활성·VFX 참조/개별 모듈 override 보존. 구형 메시로 구운 불꽃 localPosition은 새 창 anchor로 이관(화면에서 기존 위치 이탈 확인→alignment red→green). 실제 Place는 Variant 선택; EnsureEnvPropVfx는 사용자 이름 판정 전 Variant 보호. 리뷰의 Torch 이름 우회 red→green, 독립 재검토 주요 결함 없음.
Unity final.log exit0, 적용/재로드/재적용0·바이트 불변/fixture/생성기 PASS. 비대상13961객체 불변; YAML4969블록 중 대상54만 변경. VFX 이름4종 실제 생성 호출 PASS/exit0. 테스트가 재저장한 TorchMounted는 검사 직전 바이트로 복원, 에셋 잔여 diff0. 최초 실패/진단 산출물 보존.
play-final.log exit0: 실제 저장 전용 Bootstrap Metal batch Play 자연 입자3표본/1440x900. 8/18/36m PNG 직접 검토, 불꽃 창 내부 확인. ON/OFF18m17/18/19px,8m68/59/79,36m7/6/8; pan 전부>0. 원거리 체감은 미합격. 정적 컴파일 및 Unity 컴파일 오류0; 기존 CS 경고/Access token 진단 잔존. 원본 배포·접속/OS조작·2인/전체 배치 아트/성능 미검증, 검증 중.
재개: CODEX_LANTERN_74.md의 RunAlignment/RunFixtures/RunVfxNames 및 CODEX_LANTERN_APPLIED=1 Play 경로 사용. 최초 Run은 미적용 Bootstrap에만 사용; 검사를 위해 과거 장면 자동 복원하지 않는다. 다른 지역/실내 배치별 시각/VFX/성능 검수를 다음 독립 범위로 제한. 원본 통합은 별도 소유권/diff 검토이며 전용 개발 차단 사유 아님.
증거 output/ulon-codex/loop74-lantern/{final.log,scene-diff.json,vfx-names-final.log,play-final.log,play-final/,board-verification.json}. 부모32817/자식28549·28550 확인, 시작/최종 트리clean·Library 비링크. 원본/다른 차선/격리 offscreen-repair 프로세스·파일 제어 없음. 보드 담당 카드만 갱신, API HTTP200/커밋 및 타 필드 보존 확인. 한 바퀴 종료.


## 물가 단차 근본 수리 — root 7df3288a

별도 codex/ulon-water-depth: CarveLake/CarveRiver 경계30cm 단차를 안팎1m에서 연속 연결. 1m 밖 기존수식 유지. 전용 Depth로 수심복원 오차 평균1.92249→0.114108mm. 실제 게임 기본LOD5/1025 재생성·Metal 3샷12장 exit0, 강 근경 큰 계단 개선 직접 검토. 연속성 red0.30m exit1→green0.0000298m exit0; 카메라 버퍼/57,721GPU표본 테스트 exit0. 최종 리뷰의 진단 지형변경 옵션 제거, reviewed.log exit0 재확인.
전체 봉합/물가띠는 수정 전후 모두 exit1: 개구부11.8>8m, 강 NC2.3>=2m. 기준 유지. 몸통3528→3516㎡, 바다안닿음/강연결/개구부1 유지; 모래폭 유지 또는 증가. 바다 근경 경계/원거리 아트/Play/2인 미완료. root 작업트리 구운 Terrain은 검사 후 원상복구했으며 커밋은 소스/검사만 포함.
증거 output/ulon-codex/water-depth-regression3/unity.log, water-shore-{baseline,fixed}-regression.log, water-shore-final.log, water-shore-reviewed.log 및 PNG폴더. CODEX_PROMPT에 root 소유 코드만의 포팅 인계 명시. 다음 물가 작업은 기존 Codex 지형변경 보존 포팅·재생성과 바다 잔여 경계/기존 전체 회귀 실패의 원인 수리. 원본 자동 병합/푸시 없음.


## #75 HUD 실제 OnGUI·스크롤/입력 경계

92b4bda8: 전용 actual SliceHud.OnGUI Editor fixture 연결. 미니맵 아래 가방 세로 스크롤, 3열/이름 줄바꿈·퀵바/탭 겹침 수리. GUILayout 투영은 부모 clip render flip/input identity 분리. Metal release.log 두 해상도 실제 Screen/한글/배치·하단 도달·우하단5005픽셀·IMGUI 제거 PASS/exit0. 초기 뒤집힌 캡처는 시각 불합격, 제목 잉크0→75 음성 대조로 검출. 숨긴 셀 실제 fixture 이동 hidden-red exit1→화면 viewport 경계 적용 후 외부 불변/동일 DropDragged 내부 이동 PASS. 합성 MouseUp PASS, regression-final 상태19/스킨10·지도 회귀 exit0. 기존 Editor 경고/라이선스·디버거 진단 잔존. 실제 Play/OS 슬롯입력·서버 DnD·장착/2인·원본 배포 미검증.

재개: 전용 Play/실제 아이템 fixture에서 스크롤 후 슬롯 클릭/드래그 시작→은행/주머니/장비 전달 검증. 렌더 프로브·원본 통합 대기 반복 금지. 현재 합성 좌표 이름표는 전체 HUD 뒤 실제 DrawDragGhost 별도 호출이며 게임 OnGUI 전달 전체 PASS 아님. 부모32817/현재46195·46196 확인; 원본/물가 차선 프로세스 제어 없음. 근거 output/ulon-codex/loop75-hud/{release.log,release/,hidden-red.log,regression-final.log,map-regression.log,board-verification.json}, 전용 docs/CODEX_HUD_75.md. 공유 담당 카드만 갱신 및 API 확인.


## #76 Bootstrap 배경 HUD 통합

a4cb4e2e(시각 문서 c216e906): RunIntegratedHud로 실제 전용 저장 Bootstrap Main Camera Edit 배경에 #75 actual SliceHud.OnGUI fixture 연결. 종이/목재 스크롤 트랙·청록/금속 손잡이 적용, 원본 스킨/입력 치수 보존. scroll-red exit1→green exit0/스킨13 PASS. Metal reviewed.log 두 해상도1440×900/1024×768·필수6영역/가방·장비/컨테이너16그리기 호출·겹침/경계 PASS exit0. missing-map 음성대조 FullMapRect 부재 exit1. IMGUI 제거 Repaint0/제목2710픽셀 차이/배경26629표본·12217색·변화0. 실제 PNG5장 직접 검토. map-regression exit0·정적340소스 PASS. 기존 Editor CS/라이선스 진단 잔존.

긴 슬롯 이름 일부 잘림은 남으며 선택 전체 안내로 확인. 드래그 이름표는 별도 실제 메서드 호출이며 탭 일부를 가린다. Play·실제 슬롯/OS/서버 입력·장착/2인·원본 배포 미검증, 검증 중. 재개는 이름 시작/줄임표 가독성 개선 또는 Play 어댑터+실제 아이템 스크롤/슬롯 전달이며 기존 프로브/통합 대기 반복 금지. 원본/타차선/물가/offscreen-repair 파일·프로세스 제어 없음. 시작 부모32817/현재63823·63824 외 중복 Codex 없음; 전용 저장 장면/에셋 diff 없음. 독립 리뷰 필수영역 부재 검사 지적 수리 후 추가 중요 결함 없음.
증거 output/ulon-codex/loop76-integrated/{reviewed.log,reviewed/,missing-map.log,scroll-red.log,scroll-green.log,map-regression.log,review.json,board-verification.json}, 전용 docs/CODEX_GFX_UI_76.md. 담당 카드만 갱신, 타 필드/Grok 번호 보존·API HTTP200/커밋 반영 확인. 한 바퀴 종료.


## #77 확장 해안 만/곶 및 접합부 수리

#77 ce826ed9(선행57d74b03): 확장 육지에 완만한 만/곶 외형 구현. 초기 접합부 수역을 실제 화면/실패 검사로 발견→기존 육지를 깎지 않는 외측 변형으로 수리. 전용 Unity Metal final.log exit0: 60경계·중앙충돌3481·길1656·해안충돌725 PASS, 추가육지8m 표본 약0.563km². 최종1440×900 PNG4장 검토, 접합부 구멍 없음. 중앙 연결·단조로운 표면/직각길/소품·지도/서버·실제게임/2인 미완료. 기존 CS/라이선스 진단 잔존; 검증 중.

재개: 중앙 섬 보존 해상 연결/육상 접근로 및 기존 에셋 지역 표식을 전용 트리에서 독립 구현. 해안 최신 기준 ce826ed9와 loop77-world/final.log·final PNG 사용. 이후 저장/재생성·지도/서버/스폰/관심영역 원장·실제 보행/2인 검사. 원본 병합/다른 IMGUI 대기로 차단하지 않는다.
문서478facf9: 전용 docs/CODEX_WORLD_COAST_77.md. 검사는 생성 지형 Edit-mode이며 Bootstrap 배포/Play/2인 아님. 초기 signed coast 수역은 inlet-red exit1 후 외측 변형으로 수리; 원인/회귀 문서 기록. 독립 리뷰 bracket/수면 교차 검사 보강. 시작 부모32817·현재73753/73754 확인, 전용 Library 비링크·시작clean. 원본/타차선/물가/offscreen-repair 파일·프로세스 제어 없음. 한 바퀴 종료.
증거 output/ulon-codex/loop77-world/{final.log,final/,review.json,board-verification.json}.


## #78 해상 잔교·양안 접점

uo-half-span: 0a186926 구현/2ea59bf1 시각수리. 중앙 높이 보존·동해안 x220~400 길이180m 목조 잔교, 상판8.8m/통행 띠8m/3144삼각형. 전용 Unity Metal visual-fixed.log exit0: 중앙충돌3481·경계60·길1656·해안725·상판1765·접점2247·난간36·교량/난간 제거NC·중복거부·중앙캡슐스윕 PASS. ACCESS_FAIL 최대179.46%>65%: 기존 서쪽 산비탈이며 전체 접근성 완료 아님. 코드가 새로운 급격한 높이차는 만들지 않음을 별도 검사. 말뚝 침범과 난간검사 거짓양성 수리, 동쪽 공면 줄무늬1.5cm 렌더 오프셋으로 수리 후 근경 재검토. 기존 CS/라이선스 진단 남음.

증거 output/ulon-codex/loop78-world/{build-red.log,green.log,clearance-green.log,reviewed.log,visual-fixed.log,review.json,visual-fixed/crossing-east.png,visual-fixed/crossing-west.png,visual-fixed/crossing-overview.png}. 전용 docs/CODEX_WORLD_CROSSING_78.md. 임시 생성 지형 Edit-mode 검사이며 Bootstrap/저장재로딩/Play/실제게임/OS입력/2인 PASS 아님. 전용 시작 clean·Library 비링크; 부모32817/현재83258 확인. 원본/타 차선/프로세스 제어·병합·push·예약·추가루프 없음.

재개: 기존 중앙 높이를 보존하는 산비탈 우회 접근로를 독립 구현하여 ACCESS_FAIL을 실제로 해소할 것. 조건 변화 없이179% 경사 검사를 반복하지 않는다. 단조로운 교량/지역 표식은 후속. 이후 저장/재생성·지도/서버/스폰/관심영역·실제보행/2인 검사. 원본 통합 대기는 독립 접근로 구현의 차단 사유가 아니다.


## #79 중앙 지형 보존 고가 목조 우회

uo-half-span: 983dceb4. 중앙 원점→(120,40)→(120,-90)→(300,-90)→잔교/초원, 구조400m·폭8.8m·7704삼각형. 초기 x260 하강 부족19.92m→x300 연장, 난간 상판 침범→외측 접속, 시작점15.72m 급단차→북쪽40m 진입 연장. 실패 로그 모두 보존. final.log 전용 Metal batchmode exit0: 접근7530표본 max0.3000≤0.65, 폭/난간338·코너4, 전체/직선난간/코너난간누락·문막기NC·중복거부 PASS. 기존 중앙3481·경계60·길1656·해안725/보존함수1681 PASS. 기존 직행산비탈1.7946은 DIRECT_SLOPE_FAIL 유지하고 새 APPROACH_ACCESS와 구분.

final/approach-overview·entry·gate.png 1440×900 3뷰 직접 검토. 공유법선의 계단형 음영→면별법선, landing/기존잔교 공면중복 제거 후 해소 확인. 생성10뷰 모두를 직접 검토했다는 뜻 아님. 높은 가는 교각/단조로운 외형·표식 부재로 최종아트 미합격. 독립 리뷰의 폭/난간·코너 검사 공백 수리, 최종 새 중요결함 없음. 정적344소스 PASS, Unity 기존 Editor CS/라이선스/디버거 진단 잔존.

재개: 983dceb4, output/ulon-codex/loop79-world/final.log·final PNG 기준. 다음 독립범위는 생성 Mesh/Terrain 자산 저장·재로딩/재생성 검증. procedural Mesh가 장면 저장만으로 영속된다고 가정 금지. 이후 기존 Bootstrap 콘텐츠/가로등을 보존하는 전용 적용·지도/서버/스폰/관심영역·실제보행/2인·아트 검수. 현재 임시 생성 Edit-mode 충돌/렌더이며 Bootstrap 배포/Play/OS입력/게임/서버/2인 PASS 아님. 기존 직행179% 또는 다른 카드 막힘 반복 금지; 원본 통합은 독립 구현을 막지 않음.

증거 output/ulon-codex/loop79-world/{red,green,extended,clearance,entry,final,static}.log·review.json·board-verification.json, 전용 docs/CODEX_WORLD_APPROACH_79.md. 시작 전용clean·부모32817/자식97664·97666 확인, 원본/타차선 프로세스·파일 제어 없음. 검사된 본인10파일만 커밋, push/병합/예약/새루프 없음. 담당 카드만 갱신, 최상위/타카드/Grok번호/resume_token 보존. 한 바퀴 종료.


## #80 확장 월드 자산 영속화·재생성

uo-half-span: 구현6875770c·실측문서ed15f5ee. 새 revision 독립 Terrain36/Mesh192 저장기, CreateAsset 뒤 알파맵 유실은 원래 가중치 재설정/dirty 저장으로 수리. SetNeighbors 저장누락은 직렬화36참조 OnEnable 연결로 수리. red(독립자산 없음)→diagnostic(알파36만 유실)→fixed(이웃누락)→final/fresh/regen exit0. UV/법선/재질 포함1048서명 재생성 바이트 동일, 새 프로세스 생성 없이 저장장면 로딩/144방향/기존 접근7530·폭난간338·코너4·누락NC PASS. 기존 직행179% 유지, 우회30% 별도. 정적347소스 PASS; 기존 Unity 경고/라이선스 진단·하네스 env/데몬/백업 WARN exit1 잔존.

재로딩 장면 Metal PNG approach-entry/gate/layout-quarter 3장 직접 검토, 연결/표면 보존 확인. 높은 가는 교각·단조로운 지역/목재·표식부재/검증수면 외곽은 최종아트 미합격. Bootstrap/Play/OS입력/서버/2인/원본 배포 미검증. 다음 독립범위는 기존 전용 Bootstrap 콘텐츠/가로등 inventory·diff 보존 한정 적용 도구. 원본 통합 대기 반복 금지. WorldExpansionPersistence.Save는 새 Terrain만 허용하며 기존 출력 거부.

증거 output/ulon-codex/loop80-world/{final,fresh,regen}.log·regeneration.json·review.json·render PNG 및 전용 docs/CODEX_WORLD_PERSISTENCE_80.md. 실패/최종/재생성 fixture 폴더와 .meta 전부 fixtures/Assets에 보존; 필요 시 전용 Assets로 해당 폴더+.meta 복원 후 Verify. Library 복사 금지. 검사 후 생성 fixture만 증거 폴더로 이동; 전용 코드트리 clean. 부모32817/현재13511 자식 확인, 원본/타차선 프로세스 제어·병합·push·예약·새루프 없음. 담당 카드만 갱신, 최상위/타카드/Grok 번호/resume_token 보존. 한 바퀴 종료.


## #81 Bootstrap 중앙 보존 적용 도구·실제 콘텐츠 대조

uo-half-span: 구현426baaaf·검증14e89e6b. 기존 Ground는 활성/자산 그대로, 확장36 중 중앙4개만 새 TerrainData/holes로 복제하는 WorldExpansionBootstrap.Apply 구현. 저장 복사본 적용 후 원래14,251객체 JSON·4,968 YAML블록·76루트 순서·직접프리팹1,369(Variant29/횃불14) 보존, 루트1개 추가. 전역SaveAssets 비대상dirty 저장(red)→clone만SaveAssetIfDirty, clone외곽holes손상 허용(red)→mask검사 수리. final/fresh 새 UnityMetal exit0: 중앙3600Terrain ray·2404경계 높이차0·clone 높이/alpha/layer동일·멱등/no-op바이트·외곽holesNC PASS. 입력 #80파일462개·원래 전용Bootstrap 바이트동일. 전체원자성 실패주입은 미실행.

기존 조명/수면/안개 Bootstrap 복사본 PNG8뷰 생성, 마을전후/진입/접속/외곽5뷰 직접검토. 중앙holes는 배경1,296,000픽셀, 실제채움NC 전픽셀변경. 일부문/길 가시성차이는 rootoff에서도 남아 전체시각동등성 미확정; 높은가는교각/단조로운아트/기존수면외곽범위 미합격. persistence-regression 새Terrain36/Mesh192 저장/재로딩/서명/144방향/기존접근NC exit0. 임시월드PASS를 Bootstrap PASS로 바꾸지 않음.

Bootstrap 실제 콘텐츠 검사: 첫0.763경사 오류는 Player 자기CharacterController를 읽음. NetAvatar 조회시도는 저장장면 LocalAvatar여서 무효. 현재 LocalAvatar 활성자기Collider만 일시제외/finally복원하며 나머지환경 포함. access-blocker.log exit1: (67.59,0,22.53) MineVein3/Visual이 통행을 막음. 광맥/장면 보존·기준완화 없음. 다음은 광맥을 보존하며 중앙출발→(120,40) 입구 접근선수리; 같은실패 조건변화없이 반복금지. 이후 외곽수면/시각안정성·영구revision 실제Bootstrap/재생성 연동·지도/서버/스폰/관심영역·Play/2인/최종아트. 원본통합대기나 과거179%반복으로 대체하지 말 것.

증거 output/ulon-codex/loop81-world/{final,fresh,render,persistence-regression,access-blocker}.log·preservation.json·hole-render.json·review.json·전용 docs/CODEX_WORLD_BOOTSTRAP_81.md. fixtures/Assets의 __CodexWorld80Final+__CodexWorld81Apply 폴더/.meta를 전용Assets에 복원하면 Verify/Render/Access 가능(Library복사금지). Run은 새폴더만. 실제Bootstrap 영구배포/Play/OS입력/서버/2인 미검증, 검증중. 정적351소스PASS; 기존Unity경고/라이선스·하네스env/데몬/백업WARN exit1. 종료전용clean. 공유원본Bootstrap 해시변화 관측했으나 쓰기/복사/병합/프로세스제어 없음. 부모32817·현재23189/23190 확인, 새루프/예약/push 없음.


## #82 광맥 보존 북측 접근선·실제 지면 정합

uo-half-span: ef83a6f7. WorldExpansionEntry (0,0)→(60,40)→(120,40), 기존 DirtRoad재질/폭2.4m/3180tri/Collider없음. Terrain.Build·저장193Mesh·Bootstrap 새적용의 Ground맞춤 revision/Entry.asset 연결. 기존Ground/광맥/NPC/가로등 보존. 원래Bootstrap 바이트동일, 원래4968블록/1369직접프리팹/76루트순서 및14251객체JSON 보존. 소스#80/#81파일 동일, 새생성464파일 적용전후동일.

fresh-final/재생성/재생성fresh/적용재생성 exit0. 부분접근 (12,8)부터13lane·32201표본 max0.3000·난간/문/삭제NC PASS. 강화한 OverlapCapsule은 원점(0,0)의 HealerNpc/Healer 겹침을 탐지: full-access exit1. 자기LocalAvatar Collider1만 제외, NPC제외/이동/기준완화 없음. 기존광맥직행NC 유지. crossing-baseline/fixed 모두 x221.5 lane-3 Ground rise0.0623245 vs 함수0.039712+0.02 동일FAIL. 전체원점/전체잔교/게임PASS 아님.

초기 함수면 길가림→실제TerrainCollider0.5×0.4m 샘플, 삼각형22260표본 간격0.030589~0.046470m PASS. final 1440×900 4뷰 직접확인: 기본LOD5 길가림은 잔존. diagnostic/lod-one(미저장pixelError1)에서 해소, ground-off 길연속 확인. 실제 설정5 finally복원, 시각PASS아님. 두 차례 독립읽기리뷰 신규중요결함 없음, LOD결함명시. 정적353소스PASS·Unity컴파일 성공/기존EditorCS·라이선스/디버거 및 하네스WARN exit1 유지.

재개: CODEX_WORLD_ENTRY_82.md 및 loop82-world/{fresh-final,full-access,lod-diagnostic,apply-regenerated}.log. 기존Ground/콘텐츠 보존·렌더비용을 고려한 LOD길가림 해결, 중앙원점 실제 ClickMotor/CharacterController 헤드리스Play 확인. NPC삭제/기준완화로 정적실패 숨기지 말 것. fixtures/Assets의 __CodexWorld80Final+__CodexWorld81Apply+__CodexWorld82Entry+__CodexWorld82Regeneration+__CodexWorld82Applied 및 .meta 복원; Library복사금지. Run/ApplyRegenerated는 새폴더, Verify/FullAccess/Render/LodDiagnostic 재현가능. 이후 외곽수면/영구Bootstrap/지도/서버/2인. 같은광맥/Healer실패만 반복하지 않는다. 기존원본통합대기는 독립작업을 막지 않음.

증거 output/ulon-codex/loop82-world/ 및 전용docs/CODEX_WORLD_ENTRY_82.md. 본인10파일만커밋, fixture5개 증거로이동. 원본/타차선 프로세스·파일 제어/병합/push/예약/새루프 없음. 부모32817·현재35287/35288 확인. 한바퀴종료, 상태검증중. 공유카드만갱신·타카드/최상위/Grok번호/resume_token 보존 확인.


## 작업 선택 순환 수리 0097c9bb
선두 월드 카드 독점 원인: 진척 시 유예하지 않아 보드 순서로 재선택. 이제 history.jsonl의 마지막 실행순으로 오래 기다린 실행가능 카드 우선, 상태/보드순은 동률만. 13회귀 PASS·리뷰 중요 결함 없음. #82 정상 종료/부모32817 소멸·잠금해제 후 새 부모57351(PGID 동일), #83 water-shore-jag/자식57372 실제 새 로그 확인. 예약 없음. root 임시 CODEX_STOP 제거 완료. 기존 전체 하네스 경고 exit1 보존.


## #83 물가 포팅·재생성 내부 재개

891ad798: 승인된7df3288a의10파일 및 전용 VillageTerrain 재생성, 문서 CODEX_WATER_PORT_83.md. 시작 clean·세소스 인계부모와동일 확인. 원본/타차선 파일·Library 복사/프로세스 제어 없음. 부모57351/현재57372·57379 외 중복Codex 없음.

loop83-water/capture.log 및 reloaded.log 각exit0·기본LOD5/1025·12장. GPU depth/unity.log exit0 평균1.92249→0.114108mm. static355소스PASS; 기존Editor경고잔존. regression.log exit1 기존출구11.8>8m/강NC2.3>=2m 유지·기준변경없음. 강근경개선 직접확인, 바다각진수심경계·밴딩잔존. reload-pixels.json: 호수/바다동일, 강먼배경한정최대10/255차이 원인미확인·물가동일. 실제Play/2인/원본배포PASS아님.

다음: 바다근경 수심/법선/LOD 독립대조 후 원인기반수리. 포팅·전용재생성 대기조건 해소; 이전동일전체회귀만반복금지. 확장fixture는이번재생성대상아님. 보드담당카드만결과갱신·Grok번호/다른필드보존. 한바퀴종료.


## #85 가로등 배치 전수 검수 내부 재개

064659b1: CodexLanternPlayChecks의 선택적 CODEX_LANTERN_ALL_PLACEMENTS=1. 전용 저장 Bootstrap 29개 자연 Play에서 같은 Tick ON/OFF58장·계층/스케일/입자/360tris 기록. unity.log exit0, 기존3표본PASS, 정적355소스PASS. 기존CS경고 유지. 증거 output/ulon-codex/loop85-lantern 및 전용docs/CODEX_LANTERN_85.md.

17개9~16픽셀,12개0픽셀(02/03/07/14~17/20/25~28). 실내6은 외부지형이 보이는 카메라 표본으로 실내진입PASS아님. 다음은 실내전환 검사 연결·07/20 가림대조·원점중복4개 생성근거. 무근거 불꽃확대/삭제 없음. 전체배치·성능·원본배포·접속조작/2인미검증. 본인2파일만커밋, Bootstrap/에셋불변. 부모57351/자식64665 확인·다른세션제어없음. 한바퀴종료.


## #86 HUD 슬롯 입력 수리 내부 재개

c7875c9c: 실제 슬롯 버튼 MouseDown 소비+GetLastRect1×1, 클릭 MouseUp 소비 후 armed 잔존 수리. 입력/Rect 선확보·slotMouseUp 전달. 전용docs/CODEX_HUD_86.md, output/ulon-codex/loop86-hud. baseline-red 원본시작FAIL/exit1, mouseup-red 전달제거종료FAIL/exit1. verified.log 합성 슬롯6단계·클릭1/4px비드래그/20px드래그/해제/수량불변 및 화면PASS/exit0. visual.log 별도Metal2해상도PASS/exit0·기본/선택PNG직접검토. regression.log 상태19/스킨13PASS/exit0·static355소스PASS. 기존CS/환경경고 유지. 읽기전용리뷰 중요신규회귀없음.

초기반전clip내부/scroll·scroll2 입력은 슬롯도달전Ignore라 게임실패RED아님. 최종입력은 parentclip pop후 단일SlotCell에서 주입. 다음은 native이벤트디스패치/전용Play어댑터로 스크롤type/좌표·실제슬롯선택부터 은행/주머니왕복·장착/해제 검증. 같은Repaint내부치환/scroll Ignore 조건반복금지. Play/OS/2인/원본배포 미검증·카드검증중. 시작clean·부모57351/현재68558, 본인4파일만커밋·공유본인카드만결과갱신·타세션제어없음·한바퀴종료.


## #87 통합 HUD 긴 슬롯 이름 내부 재개

gpt-gfx-ui: 구현fab7b3cf·문서0a7f1b3c. 82×36 슬롯 중앙클립을 실제 폰트 높이의 접두부+줄임표로 수리, Unicode글자경계/선택전체이름/입력 유지. loop87-integrated/red exit1→green/final Metal 두해상도exit0·이름12/합성슬롯6단계·지도16조작 그리기/배경26629표본변화0. PNG3장 직접검토. regression 상태19/스킨13 exit0·기존CS/환경진단잔존. 카드전체완료 아님.

재개: 전용docs/CODEX_HUD_87.md. #86 parentclip밖 단일SlotCell 입력을 native이벤트/전용Play 전체스크롤영역으로 확장, 실제아이템 선택·은행/주머니왕복·장착해제·서버전달. 장비칸긴이름 별도확인. 같은 scroll Ignore/프로브 반복금지·원본통합대기 금지. Play/OS/2인/원본배포 미검증. 부모57351/현재85420·85421, 본인3파일만커밋·원본/타세션제어없음. 한바퀴종료.


## #88 확장 접근선 Ground LOD 한정 적용 내부 재개

uo-half-span: 구현353c2638·문서8f9f1d73. 저장Bootstrap 확장루트의 Ground LOD5→1만 명시적용; 1멱등/그외값·손상참조거부. red 도구부재exit1·scope-red LOD2덮기exit1→apply-final 새저장/재로딩/18706객체보존exit0. YAML차이Ground 한줄, 기준961/복원959파일불변. 실제Bootstrap 자동배포/재생성연동 아님. 원래Terrain자산/충돌/콘텐츠불변.

cost-final Metal4시점×3LOD: 광산길픽셀35173→39183, 최종동기렌더읽기9.5142→9.6605ms(초기9.6806→10.5271ms 편차), FPS/성능동등성PASS아님·triangles/batches미수집(-1). saved-final 새프로세스exit0·1440×900PNG4장직접검토, 큰길가림개선/작은가장자리·입구표시이음·단조아트잔존. entry-regression 표면22260·13lane부분PASS/전체원점HealerNpc FAIL유지. 실제보행/OS/서버/2인 미실행. 정적357소스PASS·기존CS/환경진단잔존, 하네스env/백업/데몬WARN exit1. 읽기전용리뷰P2두건수정후추가중요결함없음.

재개: 전용docs/CODEX_WORLD_LOD_88.md 및 output/ulon-codex/loop88-world. fixtures/Assets의#82다섯폴더+#88두폴더와.meta복원(Library복사금지). CODEX_WORLD_LOD_ASSETS=Assets/__CodexWorld88LodFinal로 GroundLodCheck.Verify/RenderSaved. Run은새폴더. 다음중앙원점ClickMotor/CharacterController 헤드리스Play·입구표시이음/잔여가림좌표별대조·실제revision재생성LOD보존계약/Play비용. NPC삭제/기준완화/동일실패반복금지. 기존직행잔교baseline/fixed동일FAIL은이번재실행없음·미해결유지. 외곽수면/지도/서버/2인/원본배포미완료.

시작clean·부모57351/현재91400·91401 확인, 검사후fixture7개증거폴더보존·전용코드트리clean. 타차선/원본프로세스제어·파일복사/병합/push/예약/새루프없음. 공유담당카드만결과갱신·Grok번호/타필드보존. #88한바퀴종료·검증중.


## #89 수면 투사 그림자 수리 내부 재개

water-shore-jag: 구현90532fa8·검증문서5540b931. Bootstrap SeaWater MeshRenderer574423795 투사1→0 한필드만 변경/바이트대조PASS. EnsureWater 투사OFF 추가. 지형/재질/셰이더/LOD/충돌 보존. ray 대조에서 바다LOD1·지형투사OFF=기존픽셀동일, 수면투사OFF=전체메시투사OFF픽셀동일. 큰각진경계는 수면의 해저투사 그림자로 확인.

red 저장설정FAIL exit1→green 저장설정/연속성PASS exit0. final 새Metal42장 exit0·바다/강/호수PNG직접검토. sea-regression 5PASS, 잘못된전후NC exit1. 바다수정=임시대조/투사ON=옛이미지, 변경164663픽셀·깊이동일. 세시점설정복원픽셀동일. 초기호수/강전체깊이불변assertion FAIL3169/761픽셀(pixels.json) 보존; 전체깊이PASS아님. 강후보먼배경29픽셀max10 차이원인미확인. 기존CS/환경진단 및 전체봉합/모래띠두실패미해결 유지·동일검사반복없음.

재개: 전용docs/CODEX_WATER_SHADOW_89.md와tools/water_depth_probe/README.md. 생성기투사OFF는컴파일만확인·전체재생성저장실행은다음검증. 바다잔여밴딩/작은무늬·원거리직선해안/넓은얕은물빛 미합격. 호수/강전체깊이차이는물마스크·픽셀좌표로원인분리. Play/OS/2인/원본배포미검증, 카드검증중. 수면그림자문제를깊이버퍼/원본통합대기로되돌리지말것.

증거 output/ulon-codex/loop89-water/{ray,red,green,final}.log·final PNG42·sea-regression.json·sea-negative.json·pixels.json·preservation.json. 시작clean·부모57351/현재4363·4364. 본인7파일만커밋, 원본/타차선제어·복사·병합/push/예약/새루프없음. 한바퀴종료.


## #90 지도 Bootstrap Play 연결 내부 재개

7f25523f·문서634ced0c. 저장 Bootstrap 임시 Play+실제 SliceHud/NetHud OnGUI bridge 연결. 스킬 패널 미니맵 겹침 initial FAIL→스킬 높이 한정 수정. 초기 별도NetHud 반전은 같은 GUI parent clip 연결로 수리. 최종 Metal 두해상도8단계 exit0: 합성M닫기/가방·스킬공존/실제HandleDeath→Healer부활→회수/새시체/단축0.25초 만료/전체IMGUI제거. 타인소유표식·만료Update정지오브젝트잔존·계정인자누락 각NC exit1. 마지막대조Repaint0, 최종제목346/범례945·회수만료906픽셀차이. PNG4장직접검토; 1024가방·스킬하단은스크롤아래, 끝조작PASS아님.

기존Edit지도/입력6·로직9·HUD상태19/스킨13 exit0, 정적359 PASS. 실제컴파일기존EditorCS경고18회표기/환경진단잔존. 저장Bootstrap바이트불변·Library비링크. AutoStartNetwork OFF·PersistDriver 제거·계정인자guard. 계정저장/서비스차단과전용data/oplog기록구분. 읽기전용리뷰지적2건수정.

재개: 전용docs/CODEX_MAP_90.md, output/ulon-codex/loop90-map/{final.log,review.json,regression-exits.json,preservation.json}. CodexMapPlayCheck.Run -ulon-account codex-map-play-90, 새ULON_MAP_EVIDENCE, batchmode Metal/noquit. 다음실제M키/패널전환/스크롤끝입력 또는 2인시체수신·관심영역검증. 합성배치·명령/단축만료를OS/2인/실제보행PASS로세지않음. 원본배포미완료·검증중. 같은Edit프로브반복/원본통합대기금지.

시작clean·부모57351/현재15296·15297, 원본Unity85035제어없음. 본인5파일커밋·원본/다른차선수정·병합/push/예약/새루프없음. 공유본인카드만결과갱신·타카드/필드/Grok번호보존. #90한바퀴종료.


## #91 가로등 외부 가림 대조

8af74afc 구현·9185758d 실측 문서. 전용 Unity 컴파일/Metal 비접속 Play exit0·자연입자3표본·정적359소스 PASS, 기존 CS 경고 잔존. 07/20 두 배치의 기준과 지형 제거0픽셀, 다른 Renderer 제거14/13픽셀·완전격리14/14. 기준 반복 ON/OFF 모두 픽셀 동일, 렌더러1671·지형1·카메라 복원 PASS. 실제 스크린샷20장과 contact 직접 검토. 특정 단일 메시 가림은 아직 미확정. 기본 화면은 여전히0픽셀이므로 수리 완료 아님. 증거 /Users/junholee/ai_lab/output/ulon-codex/loop91-lantern/ 및 전용 docs/CODEX_LANTERN_91.md.

재개: 시선 교차 Renderer 개별 제거 대조 후 필요시 전용 배치/생성기 함께 수리. 실내6개 실제 실내 전환·원점4개 생성 근거·성능·원본 배포·접속조작/2인 미검증. 실내에 외부 카메라만 이동하는 검사 반복 금지. 시작 전용 clean, 부모57351·자식26782/26783 외 중복 Codex 없음. 원본 Unity85035 제어·복사/병합·push·새 루프 없음. 담당 카드만 갱신하고 다른 카드/최상위/Grok 번호 필드 보존·재조회 확인.


## #92 HUD native Play 스크롤·선택 내부 재개

e41f1b42. 반전viewport 시작점+원래크기 혼합으로 화면포인터(759,278)를 viewport(714,562,276,490) 밖으로 오판: 두모서리변환/minmax 수리. click-diag 원본armed FAIL exit1→verified Metal Play 두해상도/지도8 exit0. native 입력은1024에서 내부/외부/역/재스크롤4 + 실제회수슬롯 Down/Up2, scroll0↔40.37·bagPick0/armedFalse/수량·ID·부모불변. BankVault실제부재 보존, 은행PASS아님. no-dispatch NC exit1. EditHUD 이름12/슬롯6/숨긴드롭·내부양성 exit0, 상태19/스킨13 exit0·정적360 PASS. PNG3직접검토, 기존CS/환경경고잔존. Bootstrap바이트불변/Library비링크.

재개: 전용docs/CODEX_HUD_92.md, output/ulon-codex/loop92-hud/verified.log·native-input-{1,4}.txt. ULON_HUD_NATIVE_INPUT=1 + ULON_MAP_EVIDENCE=<새폴더>, CodexMapPlayCheck.Run -ulon-account codex-map-play-90, Metal batchmode/noquit. #86 Repaint치환Ignore 대기 해소. 다음 선택후 추가스크롤·끝버튼/실제BankStation·BankVault·주머니fixture 왕복·장착/드래그 검증. Play합성native와OS/접속2인/원본배포 구별. 마지막선택화면 끝버튼은추가스크롤아래·클릭미검증. 초기1440 빈가방 no-overflow실패·최종추가검사 BankVault NullReference는 테스트조건/코드오류로분리하고수리, 게임실패로세지않음.

시작clean·부모57351/현재30422·30423 확인. 본인6파일만커밋. 원본/타세션제어·파일복사/병합/push/예약/다음루프없음. 공유담당카드만최신재조회갱신·다른카드/최상위/Grok번호보존검사. #92한바퀴종료·검증중.


## #93 통합 HUD 장비칸 이름 내부 재개

19d9a1ba 구현·9f5b7bb9 문서. gpt-gfx-ui: PaperSlot의 82×40 전체 문자열을 기존 FitSlotLabel로 축약, 슬롯명/접두부/줄임표·아래 전체 이름·클릭 인자 유지. 선택적 ULON_HUD_EQUIPMENT_NAMES=1은 프로세스 내 별도 카탈로그/장착 fixture이며 서버 장착 PASS 아님. red exit1→green Metal 두해상도 exit0, 첫 Repaint 높이36.55/40·이름12·합성슬롯6·드롭대조, PNG2 직접검토, 배경26629표본불변. 상태/스킨회귀 exit0. 읽기전용 리뷰 확정회귀 없음; 자동 높이는 첫 Repaint 1회·전체 이름 자동검사는 원문 보존으로 제한. 장비칸 클릭은 코드보존만 확인, 실행 미검증. 기존CS/환경경고 잔존.

재개: 전용 docs/CODEX_HUD_93.md·output/ulon-codex/loop93-equipment/. 다음은 #92 native 경로의 선택 후 추가스크롤/끝버튼·BankStation/BankVault/주머니 fixture 왕복·장착/해제/서버 전달. 이 Edit 렌더를 Play/OS/2인/원본배포 PASS로 세지 않는다. 같은 Edit/Ignore 반복이나 원본통합 대기 없음.

부모57351/자식41399·41402 확인, 시작/종료전용clean·Bootstrap HEAD바이트일치·Library비링크. 본인3파일만커밋, 원본/타차선제어·복사/병합/push·예약·새루프 없음. 공유본인카드만갱신·다른카드/최상위/Grok번호보존재조회확인. #93한바퀴종료·검증중.


## #94 새 revision LOD 보존 회귀 검사

8edc5256 검사·be4ddc2d 문서. 게임/아트 변경 없는 검증 도구 추가. #88 LOD1 저장본의 확장만 메모리 교체하고 #82 재생성 World를 새 revision에 실제 적용. LOD5 고의주입 negative exit1, 정상적용/14251 비대상 객체 JSON·Ground 참조/LOD1 보존/멱등/저장 및 별도프로세스 재로딩 exit0. 새36타일 높이 생성이나 자동이관 구현 아님. Metal 저장본4뷰 exit0·광산/전체PNG2 직접검토; 가장자리 가림/입구이음 잔존. 기존fixture965파일/전용Bootstrap바이트불변·Library비링크. 정적361 PASS, 기존CS/환경진단·하네스exit1 유지. 실제Play/원점Healer/직행잔교/성능/2인/원본배포 미해결.

재개: docs/CODEX_WORLD_REVISION_94.md 및 output/ulon-codex/loop94-world/. fixtures/locations.json 폴더와.meta만 복원. 새CODEX_WORLD_REGEN_ASSETS로 WorldExpansionLodRegenerationCheck.Run, 기존폴더 Verify. 이번 보존계약 동일반복금지; 다음 중앙원점 ClickMotor/CharacterController Play 또는 입구표시 이음 위치대조/수리. NPC삭제/검사제외/기준완화금지. #88의 실제배포/Play비용/외곽수면·지도·서버 조건은 유지.

부모57351/자식45991·45992 확인, 시작clean·원본Unity85035제어없음. 본인검사2파일+문서만커밋. fixture는#94증거로이동, 원본/타차선수정·통합/push·예약·새루프없음. #94한바퀴종료·검증중.


## #95 물 수심 화면 범위 분리 내부 재개

a2922116 검사 도구/README. 게임·아트 변경 없는 검증 진척. WaterSeaDiagnostic에 깊이투사ON·검정/흰색마스크 3대조 추가, 복제재질 shader/속성 복원. Metal51장 unity.log exit0, 저장투사OFF/연속성 verify.log exit0. 기존 바다5검사 exit0·세시점 baseline복원 변화0. 전체수면불변은 exit1 유지: 바다0/강43/호수138 변화가 전부1px경계, 외부0/689/3031. 내부562758/274451/360277표본 변화0. 각 내부1px주입NC가 내부검사도 FAIL로 바꿈. 경계 제외 기준완화 없음. 원시깊이 불변/경계혼합 원인 확정 아님. 기존CS/환경경고 잔존.

재개: tools/water_depth_probe/README.md #95, output/ulon-codex/loop95-water/. 다음 동일대조 반복 대신 경계 원시깊이/마스크 커버리지 동시계측 또는 미해결 작은밴딩 원인 수리. 새 검사만 추가한 이번을 게임개발 진척으로 세지 않는다. 기존봉합2실패/작은밴딩/원거리미관/전체재생성/Play/2인/원본배포 미해결 유지. 강·호수 경계표시 PNG 직접검토, 기존 강단차 수리 유지·호수밝은띠/원거리직선 해안 잔존. 기획6.1 지역구성 변경없음, 기획에 없는 UO 물가수치 합격 주장없음.

부모57351/자식50955·50956 외 중복실행 없음. 시작clean·본인3파일만커밋. Bootstrap/물재질/게임물셰이더 HEAD바이트 동일·Library비링크. 원본Unity85035 및 다른차선 제어/복사/통합/push·예약/새루프 없음. 공유본인카드만 재조회후갱신·최상위/다른카드/Grok번호보존 확인. #95 한바퀴종료·검증중.


## #96 지도 스킬 native 스크롤 내부 재개

a99a56a7 검사·fcbda3f3 문서. 게임/아트 변경 없는 검증 진척. Metal Bootstrap Play 8단계/두해상도 combined exit0, 스킬1024 휠5회 끝88px·하단고정/외부휠무시/상단0/끝복귀. 가방휠4·회수슬롯Down/Up2 동시회귀 exit0, no-dispatch exit1, 정적361 PASS. PNG2 직접검토·마지막 훔치기 행 표시 확인. 기존 CS/환경진단 잔존.

재개: 전용 docs/CODEX_MAP_96.md 및 output/ulon-codex/loop96-map/. ULON_MAP_SKILL_INPUT=1 + 기존 Play 명령, 가방회귀 ULON_HUD_NATIVE_INPUT=1 병용. 다음 마지막 버튼 클릭/잠금변경 또는 M키 입력 경로. OS/M키/끝버튼클릭/2인수신·관심영역/원본배포 미검증, 같은 휠검사만 반복하지 말 것.

부모57351/자식55246·55252, 시작clean·Bootstrap HEAD바이트일치·Library비링크. 본인검사2파일+문서만커밋. 원본Unity85035/타차선제어·복사/통합/push/예약/새루프없음. 공유담당카드만갱신·다른카드/최상위/Grok번호보존재조회확인. #96한바퀴종료·검증중.


## #97 가로등 개별 가림 내부 재개

4cc740e1 검사·29e0f54e 문서. 게임/아트 변경 없는 검증 진척. final Metal Bootstrap 비접속 Play exit0·자연입자3표본, 정적361 PASS. 관문 기둥2/상인방 개별제거0·동시제거14, 마을 높은지붕 중심(5.67,15.41,-12.14) 단독제거13. 기본두곳0 미수리. 동일Tick17쌍/기준반복일치·Renderer1671/Terrain1/카메라 복원 PASS. contact PNG 직접검토·기존CS/환경경고 잔존.

재개: 전용 docs/CODEX_LANTERN_97.md 및 output/ulon-codex/loop97-lantern/final/. 이제 배치와 생성기 위치 동시수리/저장/재로딩/재생성/실외회귀. 구조물 삭제 금지·같은 제거검사 반복 금지. 실내6개/원점4개/성능/접속조작/2인/원본배포 미검증 유지.

시작clean·부모57351/자식60278·60281·Bootstrap HEAD동일·Library비링크. 본인검사1파일+문서만커밋, 원본Unity85035/타차선제어/복사/통합/push/예약/새루프 없음. 공유담당카드만최신재조회갱신·다른카드/최상위/Grok번호보존 확인. #97 한바퀴 종료·검증중.


## #98 HUD 하단 native 입력·장착 왕복 내부 재개

2b6140bb 구현·b8a0230e 문서. 게임아트/규칙 변경 없는 검증 어댑터 수리. render flip/input identity는 하단착용 local좌표정상에도 Ignore(clip-diag exit1). native큐Y반전+inputTransform flip 수리 후 final Metal Bootstrap 오프라인Play 두해상도/지도8·가방14이벤트·스킬휠5 exit0. 선택후상단0→끝22.52002·착용wooden_club→하단해제·ID/수량/부모/선택/drag불변 PASS. 해제MouseUp누락NC exit1. native-equipped/corpse-looted/1440 PNG3직접검토. 정적361/상태스킨 exit0·기존CS 및 상태회귀ShouldRunBehaviour13회(#92동일) 잔존. 읽기전용4파일리뷰 중요결함없음. 초기선택후더내려간다 가정FAIL은 현재빈가방40.37/선택22.52 차이로 검사조건오류였음.

재개: 전용docs/CODEX_HUD_98.md·output/ulon-codex/loop98-hud/. ULON_HUD_NATIVE_INPUT=1 ULON_HUD_END_INPUT=1, 새ULON_MAP_EVIDENCE, 기존격리계정/Metal batchmode CodexMapPlayCheck.Run. 변환쌍보존·같은Ignore/끝버튼미검증반복금지. 다음 실제BankStation/BankVault·주머니fixture/native드래그 또는 장비칸자체클릭. 은행부재유지, OS/RPC/서버저장/2인/원본배포 미검증·카드검증중.

부모57351/현재64288·64294 확인·시작clean·Bootstrap HEAD바이트동일·Library비링크. 본인4코드+문서만커밋. 원본Unity85035/다른차선 제어·복사·통합/push/예약/새루프 없음. 공유본인카드만최신재조회갱신·다른카드/최상위/Grok필드보존 확인. #98 한바퀴 종료.


## #99 통합 HUD 장비칸 native 왕복 내부 재개

e77072b3 구현·a0bb9014 문서. 카드 gpt-gfx-ui. 스타일 Btn에 누락된 Editor Repaint 관찰만 보완하고 실제 빈오른손칸 장착/찬칸 해제 native 검사 추가. green.log는 이름과 달리 관찰누락 exit1, fixed Metal Bootstrap 오프라인Play exit0. 1024 장비칸 왕복·가방20 이벤트·스킬휠5·ID/템플릿/수량/부모/선택/drag불변 PASS. 찬칸MouseUp누락 exit1, 옵션off 기존경로 exit0·정적361 exit0. 두해상도 렌더이나 장비칸입력은1024만. PNG3직접검토·자체3파일diff리뷰, 독립리뷰 아님. 기존CS/환경경고 유지.

재개: 전용 docs/CODEX_HUD_99.md·output/ulon-codex/loop99-paper/. ULON_HUD_PAPER_INPUT=1 + END_INPUT=1 + NATIVE_INPUT=1. 다음 실제BankStation/BankVault·주머니fixture/native드래그. 관찰누락/Ignore 반복금지. 은행부재·다른장비/유령·1440장비칸입력·OS/RPC/서버저장/2인/원본배포 미검증. 카드검증중.

시작clean·부모57351/현재74457·74458 확인. Bootstrap HEAD바이트동일·Library비링크. 본인3코드+문서만커밋. 원본Unity85035/타차선제어·복사·통합/push/예약/새루프 없음. 공유 담당카드만 최신재조회 갱신·다른카드/최상위/Grok필드보존 확인. #99 한바퀴 종료.


## #100 중앙 원점 Play 보행 내부 재개

63ddd987 검사·문서. 게임/아트 변경 없는 검증 진척. 저장#94 Bootstrap 중앙(0,10.08,0)에서 실제 ClickMotor.Update/CharacterController.Move로 FullRoute6점→잔교400m끝 도달. nographics/Metal exit0·Metal경유점grounded6/6, 누적646.496m. 합성목적지만 전달·위치/NPC/충돌/속도불변. no-destination exit1(원점정지/잔여72.11102m/60초초과), 정적362 exit0. Metal6장중1/2/4/6직접검토: 입구길단절·밝은해안·직선연결로 잔존. 기존CS/환경진단 및 하네스환경·백업·데몬경고 exit1, 커밋후미추적분류경고해소.

재개: docs/CODEX_WORLD_WALK_100.md·output/ulon-codex/loop100-world/. fixtures/locations.json의 Assets+.meta만 복원, Library금지. -batchmode(quit없음) WorldExpansionPlayWalkCheck.Run -ulon-account codex-world-walk-100·CODEX_WORLD_OUTPUT. 다음 (120,40) 접근선/램프 입구표시 접점수리. 동일Play보행반복금지. 중앙단일경로PASS는 HealerNpc 전폭정적접근 및 #82직행잔교FAIL 해소아님. 확장육지내부/역방향/과적·전투/OS/서버보정/전체성능/2인/원본배포미검증. 카드검증중.

부모57351/자식80167·80170, 시작clean·fixture991파일불변·Bootstrap HEAD일치·Library비링크. 본인검사2파일+문서커밋. fixture실행후output보존. 원본Unity85035/타차선제어·복사/병합/push/예약/새루프없음. 공유담당카드만최신재조회갱신·다른카드/최상위/Grok번호보존확인. #100한바퀴종료.


## #101 물가 얕은 수심 내부 재개

9d9ab729 검사도구3파일. 게임/아트수정 없는 검증진척. loop101-water/final Unity Metal 격리실행 exit0: 얕은 카메라축0.5~25mm 9266표본 평균0.096710mm/최대0.217438mm; 기존57721표본 및 카메라콜백 PASS. negative exit1(3mm주입 평균2.90329mm), 정적362 exit0. After PNG 평면 그라데이션 직접검토. 게임 전체 Unity 컴파일/실제게임 화면/Play/2인/원본배포 새검증 아님.

재개: tools/water_depth_probe/README.md #101. 실제 게임 경계 float수심과 커버리지 동시대조 필요. DebugDepth Alpha1 확인으로 단순 알파설정 가설 제외; 원인확정 아님. #95 전체수면불변FAIL·봉합2실패·밴딩/원거리 유지. 동일평면 재실행 불필요. 카드 검증중.

부모57351/자식89049 확인·시작clean. 원본Unity85035/타차선/Library 제어·복사·통합/push/예약/새루프 없음. 독립 output Unity만 실행. 본인3파일 commit·공유카드 최신조회 후 타카드/최상위/Grok필드 보존. #101 한바퀴 종료.


## #102 지도 마지막 스킬 잠금 입력

5498283b(문서a2c952c0): 검증 도구 확장, 게임/아트 변경 없음. 전용 Metal Bootstrap Play 두해상도/8단계 exit0. 마지막 스킬 native 클릭3쌍 감소→고정→상승, 다른 잠금/숙련도 불변·끝88px. 가방 동시 회귀 통과. MouseUp 누락 exit1, 정적362 PASS. skills-map.png 직접검토; 중간 잠금은 로그 근거. 기존 CS 경고 잔존, OS/M키/서버저장/2인/관심영역/원본 배포 미검증. 카드 검증 중. 다음: M키 전달/지도 전환 또는 2인 시체 수신·관심영역. 동일 휠/마지막 버튼만 반복 금지. 재현은 전용 docs/CODEX_MAP_102.md, 증거 output/ulon-codex/loop102-map/. 원본 프로세스/타 차선 수정 없음.


## #103 관문 가로등 수리 내부 재개

615f2a79: D1 먼쪽 가로등만 측면2m 수리, Bootstrap Z한값과 실제 생성기 반영. fixed 적용/저장/재로드/멱등·비대상Transform4858/VFX 보존, generation 실제3관문6등불·비대상5좌표 유지 PASS. final Metal 비접속 Play exit0/자연입자3표본, 관문07 0→14픽셀·기존실외17 기여 유지. 상부 가시/등주하단 일부기둥가림. 정적363 PASS·기존CS경고 잔존. 코드/장면 개발진척 있음. 증거 output/ulon-codex/loop103-lantern/.

다음: 전용 docs/CODEX_LANTERN_103.md의 마을 +X4m 표본을 PlaceTownFill 모듈/건물밀어내기 후처리와 함께 구현·재생성. 마을0 미수리, 실내6/원점4/전체월드재생성/성능/배포/접속조작/2인 미검증. 초기 probe는 월드입자 미이동 무효; translated만 위치근거. Run 적용검사는 수리전장면 기대하므로 현장면에서 반복금지. 다른 Kind 부모/재배치 원본은 자동 적용대상 아님: 원본diff/계층 대조 필요. 원본프로세스/타차선/Library 제어·복사·통합/push/예약/추가루프 없음. 공유 담당카드만 갱신·타카드/최상위/Grok번호 보존 확인. #103 한바퀴 종료.


## #104 HUD 1440 장비칸 내부 재개

d6011398 검사·4e013120 문서. 게임/아트 변경 없는 검증진척. wide Metal Bootstrap 오프라인Play 지도8/가방native20/스킬휠5 exit0·1440 장비칸 착용/해제·선택끝0. no-paper-up exit1, small 1024회귀 exit0·끝22.52002, 정적363 PASS. PNG2 직접검토. 기존CS경고 유지. 은행/주머니·OS/RPC/서버저장/2인/원본배포 미검증.

재현: 전용 docs/CODEX_HUD_104.md·output/ulon-codex/loop104-hud/. ULON_HUD_WIDE_INPUT=1 + PAPER/END/NATIVE_INPUT=1, CodexMapPlayCheck.Run·기존격리계정·Metal batchmode. WIDE는 단계4만1440, 작은화면 넘침검사 유지. 다음 실제BankStation/BankVault·주머니fixture/native드래그. 동일 장비칸 검증만 반복금지. red의 큰화면넘침 가정오류는 수리됐으며 게임실패로 세지 않는다. 카드검증중.

부모57351/자식5415·5417 확인·시작clean. Bootstrap HEAD동일·Library비링크. 본인검사2파일+문서만커밋. 원본프로세스/타차선/Library 제어·복사·병합/push/예약/새루프 없음. #104 한바퀴 종료.


## #105 통합 HUD 패널 밖 드래그 취소 내부 재개

7bf635e5 검사·문서. 게임/아트 변경 없는 검증진척. final/wide Metal Bootstrap 오프라인Play exit0, 1024/1440 native Down/Drag/Up 패널 밖 취소·ID/템플릿/수량/부모/선택보존·drag해제. 기존장비칸/지도/스킬회귀. no-cancel-up exit1(잔존drag 검출), 정적363 PASS·기존CS경고 잔존. PNG2직접검토·자체diff리뷰.

재현: 전용 docs/CODEX_HUD_105.md·output/ulon-codex/loop105-hud/. DRAG_CANCEL=1 + NATIVE/END/PAPER_INPUT=1, WIDE_INPUT=1로1440. 다음 실제BankStation/BankVault·주머니fixture/native 성공이동. 같은취소반복금지. 은행부재·드래그도중픽셀·OS/RPC/서버저장/2인/원본배포 미검증. gpt-gfx-ui 검증중 유지.

부모57351/자식9832·9833, 시작clean·Bootstrap HEAD동일·Library비링크. 검사1파일+문서만커밋. 원본프로세스/타차선/Library 제어·복사·병합/push/예약/새루프없음. 공유본인카드만 갱신·타카드/최상위/Grok필드보존 재조회확인. #105 한바퀴종료.


## #106 입구 연결 수리 내부 재개

ca64714d 구현·a4668d13 문서. 카드 uo-half-span. 실제Ground10.95~11.16m/상판10m 차이로19m표시단절: 흙길20m연장·max실제표면+4cm. 새90도miter 법선뒤집힘3개는2m단면전환으로수리. final 저장/재생성/18705비대상보존·363표본PASS, regression-final 표면25620·13폭32201/난간관문NC PASS. Play Metal 중앙출발접점왕복5점/231.708588m exit0·PNG2/3/4직접검토, grounded4/5로전체접지합격아님. 정적364PASS·기존CS/라이선스경고·하네스환경/백업318h/데몬경고 exit1.

재개: docs/CODEX_WORLD_JOIN_106.md·output/ulon-codex/loop106-world/. fixtures/Assets10폴더+.meta만복원, Library금지. first-mesh는법선불합격이며사용금지. CODEX_WORLD_WALK_JOIN=1은수리접점왕복용·동일반복금지. 다음§6.1확장육지내부 초원→숲 연결로/콘텐츠 배치. 입구난간매몰·밝은해안·HealerNpc원점전폭/#82직행잔교FAIL유지. OS/서버/전체성능/2인/원본배포미검증·카드검증중.

시작clean·부모57351/현재14069·14077 확인. 입력991파일불변·Bootstrap HEAD일치·Library비링크. 본인7파일만커밋후문서추가커밋. 원본Unity85035/다른차선제어·복사·통합/push/예약/추가루프없음. 공유담당카드최신재조회갱신·타카드/최상위/Grok번호보존확인. #106한바퀴종료.


## #107 지도 M키 전달 경계 내부 재개

af210340 검사·문서. 게임/아트 변경 없는 검증 도구 개발. key/diagnostic/background exit1: IMGUI KeyDown1이나 legacy held/down/up0, 지도 열린 상태 유지. background=True에서도 동일. 3실행 뒤 같은 조건 반복중지·카드 막힘. regression 키옵션OFF Metal Bootstrap Play 두해상도/8단계 exit0, 정적364 PASS. PNG2 직접검토·기존CS/환경경고 유지. M키 release/재열기/no-down 대조는 미합격/미실행.

재개: 전용 docs/CODEX_MAP_107.md·output/ulon-codex/loop107-map/. 창 없이 legacy 입력 전달하는 새 근거 후 KEY_INPUT=1 정상/음성대조. 또는 소유권 대조한 2인 시체 수신·관심영역 검증 계획으로 resume_token 변경. native 큐·background만 동일반복금지. OS/2인/원본배포 미검증 유지. 실제 게임 M키 고장 또는 영구불가능 확정 아님.

시작clean·부모57351/현재22710·22711 확인. Bootstrap/ProjectSettings HEAD일치·Library비링크. 본인검사1파일+문서만커밋. 원본Unity85035/타차선 제어·복사·통합/push/예약/새루프 없음. 담당카드 최신조회 후 타카드/최상위/Grok필드 보존 확인. #107 한바퀴 종료.


## #108 마을 가로등 저장·생성 수리

9f542864(구현), 51fb7f02(인계). 마을 +X4m 저장 적용/재로드/멱등·비대상Transform4858/VFX 보존 PASS. 실제 PlaceTownFill 생성에서 불꽃 바운드 포함 접지로8개 Y20 부상 재현, Variant 한정 메시 접지로8개 지면Y10 PASS; ClearPropsFromBuildings2회 대상 위치 유지. Metal 저장Bootstrap 비접속 Play exit0: 마을0→15픽셀·기존실외18개 기여 유지. 대상 PNG 직접 검토로 등주/불꽃 지붕 밖 확인. 정적364 PASS, 기존 CS경고/라이선스 진단 잔존. 전체 재드레싱/실내6/원점4/성능/원본배포/접속조작/2인 미검증.
재개: 전용 docs/CODEX_LANTERN_108.md. 실내6 실제 전환 어댑터 또는 원점4 생성 소유권 대조 후 한정 수리. 마을/관문 같은 위치탐색·제거검사 반복 금지. 증거 output/ulon-codex/loop108-lantern/. 부모57351·현재28009/28012 외 중복 없음, 원본Unity85035/타차선 제어·통합·push·예약 없음. 공유 보드 담당 카드만 갱신/재조회 확인.


## #109 은행 드래그 수리 내부 재개

8a831bf1 게임수리·be48d261 문서. native 은행 드롭 cells0 재현(first/diag exit1): Layout 초기화를 Repaint로 옮겨 입력까지 셀 보존, 이동 뒤 개수/선택 범위 갱신. fixed 1024·wide1440 실제 HUD→OfflineWorld 은행 왕복 exit0; ID/템플릿/수량/부모 보존. no-bank-up exit1. wide 장비왕복/밖drag취소·지도8/스킬휠 회귀·정적364 PASS. PNG3 직접검토, 기존CS경고 잔존. 주머니/은행거리거부/OS/RPC/저장/2인/원본배포 미검증.
재개: 전용 docs/CODEX_HUD_109.md·output/ulon-codex/loop109-hud/. 실제 주머니fixture native 넣기/빼기 및 동일템플릿 복수인스턴스 보존. 단일은행 같은검사 반복금지. BANK_INPUT+NATIVE_INPUT, wide는 END/PAPER/WIDE/DRAG_CANCEL 추가. 임시 은행을 플레이어에 합성배치한 근거이며 마을Banker접근 검증 아님.
시작clean·부모57351·현재34309/34310 확인. Bootstrap HEAD일치/Library비링크. 원본Unity85035/타차선 제어·통합·push·예약·새루프 없음. 담당카드최신조회갱신·타카드/최상위/Grok번호보존확인. #109 한바퀴 종료.


## #110 통합 HUD 은행 거리 이탈 내부 재개

abe39def 검사·문서. 게임/아트 변경 없는 검증도구 개발. final1024/wide1440 Metal Bootstrap 오프라인Play exit0, 실제 native 드래그 도중 임시은행12.4m 이동→아이템ID/템플릿/수량/부모 보존·drag해제. keep-bank-near 거리0 유지에서 실제입금 검출 exit1. 정적364PASS·PNG2직접검토·기존CS경고 잔존. 코드소유권 자체diff리뷰.
재개: 전용 docs/CODEX_HUD_110.md·output/ulon-codex/loop110-hud/. BANK_RANGE+BANK+NATIVE_INPUT=1. 다음 주머니fixture native 왕복/복수인스턴스 또는 은행거부사유 가독성. 동일은행/거리반복금지. 마을Banker보행/주머니/OS/RPC/저장/2인/원본배포 미검증. gpt-gfx-ui 검증중 유지.
시작clean·부모57351/현재39352·39353·Bootstrap HEAD동일/Library비링크. 원본Unity85035/타차선 제어·통합/push/예약/새루프 없음. 담당카드 최신조회갱신·타카드/최상위/Grok필드 보존 확인. #110 한바퀴 종료.


## #111 확장 초원→숲 연결로 내부 재개

694be9a9 구현·f739f710 검사/문서. 기존 DirtRoad로748.2832m·폭2.4m·radius4·5988삼각형 연결로 추가, 전체 생성기에 호출 연결. 지형/충돌/원본Bootstrap 변경 없음. green 저장 전18706객체(루트Transform 제외)·길2회재생성·저장재로드8910표본 PASS. metal/final 곡선포함9213표본·약4cm간격·캡슐공간 PASS. hidden/곡선내부16삼각형 제거 음성대조 exit1. 실제 Metal 비접속Play10경유점/747.9657m 도달, grounded9/10(2번False). 시작400,0 합성배치라 중앙접근/OS/네트워크 검증 아님. PNG6직접검토: 연결됨, 밝은모래띠/원거리표시가림/식생없음/끝점직선절단 미관잔여. 독립review111 가드공백 보강·재리뷰 확인.

재개: 전용 docs/CODEX_WORLD_TRAIL_111.md 및 output/ulon-codex/loop111-world/. fixtures/Assets 입력10+최종1폴더와.meta 복원만, Library금지. 입력996파일/Bootstrap HEAD바이트불변. 다음 기획§6.1 숲입구 기존프리팹·벌목자원/서버스폰 소유권대조 후 콘텐츠 한묶음 구현. 같은 보행반복금지. 전체36타일재생성 호출 연결은 코드근거만, 실행미검증. #106난간매몰/grounded4of5·HealerNpc원점전폭/#82직행잔교FAIL 유지. 전체접지/원작3072m환산/성능/2인/원본배포 미검증, 카드검증중.

정적366PASS·실제Unity컴파일final exit0, 기존CS/라이선스진단 유지. harness-committed exit1 환경변수/백업319시간/데몬경고, 전체PASS아님. 시작clean·부모57351/자식40106·40107확인. 원본Unity85035/타차선 수정·제어·통합/push/새루프/예약 없음. #111한바퀴만 종료.


## #112 실내 가로등 검증 어댑터

4d43e4fa: 실제 QuarterViewCamera/ DungeonSightFade 메시지에 합성 follow를 연결. Metal 저장Bootstrap 비접속 Play exit0, 실내6개 판정/거리8.10703m·불꽃30~79픽셀, 6개 실루엣 contact 직접 확인. 정적366 PASS·기존 CS 경고. QA 코드 진척이며 게임 배치 수리/실제 입력·워프·2인 PASS 아님. 증거 output/ulon-codex/loop112-lantern/ 및 전용 docs/CODEX_LANTERN_112.md. 다음: 원점4 생성·루트 소유권 한정 수리 또는 실제 서버 전환/보행. 실내0픽셀/마을·관문 탐색 반복 금지. 담당 카드만 갱신·타 카드 보존 확인. 원본/타 차선/프로세스 제어 없음.


## #113 주머니 개체·대상·선택 수리

bc578265 + 54d62432. red.log에서 첫 곤봉→두 번째 주머니 native 드롭이 다른 곤봉→첫 주머니로 변함 재현(exit1); template만 전달하던 경로에 source/destination ID 전달·서버 필터 적용. selection-red.log exit1 출고 재삽입 선택 혼동은 종료 시 ID 재탐색 수리. final.log 1024/final-wide.log 1440 Metal Bootstrap Play exit0, native 주머니 왕복/전체 payload·선택 보존, 직접 잘못된 요청6 거부. 1440 장비/페이퍼돌·밖드래그취소·은행왕복/거리거부·지도8/스킬휠 회귀. no-pouch-up 음성대조 exit1. 정적366 PASS, 기존 Editor CS경고. PNG5 직접 검토·공유6파일/Bootstrap 보존. 증거 output/ulon-codex/loop113-hud/ 및 전용 docs/CODEX_HUD_113.md.

재개: native 주머니↓/↑ 버튼의 복수인스턴스·출고 선택 보존, ID 생략 기존 호출/스택 분할·합치기. DnD 동일 재검사만 반복하지 않음. RPC 인자변경은 같은 클라/서버 빌드 필요; 실제 RPC/지연동기화/저장/2인/OS/원본배포 미검증. 상태 검증 중, resume_token hud-pouch-instance-113. 별도 버튼 출고 선택 수명은 아직 검증/수리 대상. 다른 카드/원본 세션 제어 없음.


## #114 은행 이용 거리 안내

9a9e34df: gpt-gfx-ui 은행 위 이용가능/거리/유령 안내, 기존 BankInRange 재사용. red exit1 안내부재→final/wide exit0 Metal Bootstrap 오프라인Play1024/1440 은행거부·ID보존·장비/지도/스킬 회귀. 정적366 PASS, 기존 Editor CS경고 잔존. PNG2 직접검토: 한글2줄 표시, 단어 줄바꿈 개선 후보. 실제 OS/RPC/저장/2인/원본배포 미검증. 전용 docs/CODEX_HUD_114.md 및 output/ulon-codex/loop114-hud/. 다음 거부결과별 피드백 또는 #113 주머니 버튼 복수인스턴스. 동일 거리검사 반복금지. 원본/타차선 제어·통합 없음. 공유 담당카드만 갱신 및 다른 필드 보존 확인.


## #115 확장 숲 입구 식생 내부 재개

92568673 구현·a47164ca 문서. 기존 CC0 나무프리팹18·3024삼각형을 원래크기로 길 양쪽 배치·전체생성기 호출 연결. red 부재 exit1→green/fresh/Metal exit0: 두번생성 동일·중복거부·비대상18710객체 보존·저장재로드·513통행표본 PASS. hidden NC exit1. 실제 Metal 비접속Play69.86394m/3점도달·grounded1/3. 시작800,280 합성배치·OS/중앙접근/서버/2인 아님. PNG3직접검토: 나무표시·성긴밀도/밝은모래띠/길끝절단 미관잔여. 벌목노드 추가없음. 정적368PASS·기존Editor CS경고. 하네스exit1 환경변수/백업319시간/데몬경고 유지.

재개: 전용 docs/CODEX_WORLD_GROVE_115.md·output/ulon-codex/loop115-world/. fixtures 입력11+최종1폴더와.meta만 복원·Library금지. 입력1001파일바이트불변/Bootstrap HEAD동일·Library비링크. 다음 기존노드데이터/서버 이름해석·소유권 대조 후 벌목연결 또는 식생밀도/모래띠 한정개선. 동일3점보행반복금지. 전체36타일재생성/성능/3072m환산/원본배포 미검증, #106/#82 기존FAIL 유지.

시작clean·부모57351/현재45097·45098 확인. 원본Unity/타차선 제어·통합·push·새루프·예약 없음. 공유 담당카드만 갱신·다른카드/최상위/Grok번호 보존검사. #115 한바퀴 종료.


## #116 원점 가로등 내부 재개

2b4fabec: 원점4 저장ID/외부직렬화참조/실제Decor2/Play Renderer 대조 QA 구현. 게임 배치 변경 아님. final Unity Metal 비접속Play exit0·입자3표본·복원PASS. 직렬화참조0, 몸체y10~11.556/지형10, 전체4/개별1 화면기여90픽셀·서로차이0. 축소화면 등주 가독성 미합격. 초기정적369PASS·최종Unity컴파일/Play확인·기존Editor CS경고 유지. 원점4 동일RAW위치override는2c39f22f부터존재; 현재Decor2는원점누출0.

재개: 전용 docs/CODEX_LANTERN_116.md·output/ulon-codex/loop116-lantern/. ID623533903/1347627312/1800212613/2082635732 RAW→Env전환/이름소비자 대조 후 비대상보존·저장재로드·멱등 한정중복수리 또는 원점가림 대조. 저장참조0을 런타임참조0으로 확대금지. 묻힘단정/실내0픽셀/전체마을탐색 반복금지. 삭제/이동/실제보행/서버/OS/성능/배포/2인 미검증. 검증중·resume_token lantern-origin-provenance-116.

시작clean·부모57351/현재46225·46226, 원본Unity85035제어없음. Bootstrap/기존에셋diff없음·Library비링크. 담당카드만최신읽기/동시변경검사/원자교체후다른카드·최상위·Grok번호보존확인. 새루프/예약/병합/push없음. #116 한바퀴 종료.


## #117 HUD 주머니 버튼 출고 선택

#117 4350b71e: 주머니↑ 출고 후 선택이 다른 주머니로 바뀌는 문제 수리. native 버튼 red 선택실패 exit1→1024/1440 Metal Bootstrap Play exit0; 두 곤봉/두 주머니 payload·선택 보존, no-pouch-button-up exit1. 1440 장비/페이퍼돌·은행/거리·취소·지도8/스킬휠 회귀, 정적369 PASS, PNG3 검토·독립리뷰. 기존 CS경고. 스택/기존호출/지연RPC/저장/2인/OS/원본 미검증. 문서 b1061be9.
재개: CODEX_HUD_117.md: 다음 ID 생략 기존 호출·stackable 분할/합치기 및 합쳐진 개체 선택 해제 독립 검사. 동일 버튼/DnD 반복 금지. 지연 RPC 선택 수명·동일 클라/서버 실제 전달·저장/2인·OS/원본 배포 미검증 유지.
증거: /Users/junholee/ai_lab/output/ulon-codex/loop117-hud 및 전용 projects/ulon/docs/CODEX_HUD_117.md. 공유 관련4파일·전용 Bootstrap 바이트 불변, Library 비링크. 원본/다른 세션 제어·병합·push 없음. wide.log는 PAPER_INPUT 옵션 누락 exit1, wide-final.log에서 조건을 갖춰 통과.


## #118 은행 안내·유령 드롭 내부 재개

fcc223b8 구현·0acebda0 문서. 은행 상태 문구를 짧은 두 줄로 정리. red 문구기준 exit1→final1024/wide1440 Metal Bootstrap 비접속Play exit0: 유령 합성전환 드롭거부·payload/drag 보존 및 장비/지도/스킬 회귀. keep-bank-alive 음성대조 실제입금 검출 exit1. 정적369PASS·PNG3직접검토·기존CS경고. 실제사망/OS/RPC/저장/2인/원본배포 미검증.
재개: 전용 docs/CODEX_HUD_118.md 및 output/ulon-codex/loop118-hud/. 다음 과적/없는물건 서버결과와 기존알림 소비경로 대조 또는 스택병합 선택수명 검사. 같은 거리/유령드래그 반복금지. 안내는 응답 자체가 아님. 전용Bootstrap HEAD동일/Library비링크·시작clean·부모57351/현재48415·48416 확인. 원본/타차선 제어·병합·push·새루프·예약 없음. 담당카드만 갱신·다른카드/최상위/Grok번호 보존 확인. #118 한바퀴 종료.


## #119 숲 입구 바깥 식생 내부 재개

d45a470f 구현·40576a5d 검사/문서. 기존18그루 이름/행렬 보존·바깥18 추가, 원래크기·재질 유지. red exit1→green/fresh/final Metal exit0: 나무36/6048삼각형/접지/513통행표본·2회재생성·중복거부·비대상18710객체/저장재로드 PASS. collapse-outer NC 수36유지/깊이실패 exit1. PNG2 직접검토: 양쪽2띠 보이나 성긴반복/밝은모래띠/체커지형 잔여. 정적369PASS·기존Editor CS경고. 이번 Editor카메라 렌더이며 Play/OS/서버/벌목/2인 미검증, #115 grounded1of3 및 #106/#82 FAIL 유지. 하네스exit1 환경변수/백업320시간/데몬경고.

재개: 전용 docs/CODEX_WORLD_GROVE_119.md·output/ulon-codex/loop119-world/. fixtures 입력12+최종1폴더와.meta만 복원·Library금지. 다음 기존모래띠/숲바닥 경계 한정개선 또는 벌목데이터/서버소유권 확인 후 자원연결. 수량확대만 반복/동일3점보행 금지. 전체36타일재생성/원작3072m환산/성능/원본배포 미검증. 입력1004파일 바이트불변·Bootstrap HEAD동일·Library비링크. 시작clean·부모57351/현재49278·49279 확인. 원본Unity85035/타차선 제어·통합/push/새루프/예약 없음. #119 한바퀴 종료.


## #120 원점 가로등 개별 가림 내부 재개

0d431c95 구현·db454d3e 문서. Metal Bootstrap 비접속Play exit0·정적369PASS·기존CS경고. 동일Tick 17쌍, renderer1671/terrain1/카메라 복원PASS. 기본90px→Healer Visual 숨김276/후드94/후보전체585/격리339. PNG3검토: 분수/NPC가 등주를 가림, 가독성 미합격. 그림자 포함 픽셀 기여이며 몸체 면적 아님. 픽셀검사 빈기여 음성대조 exit1.
재개: 전용 docs/CODEX_LANTERN_120.md·output/ulon-codex/loop120-lantern/. 원점4 이름소비자/RAW→Env 대조 뒤 한정배치 수리 또는 통로보존 동시이동 표본. Healer 숨김을 제품수리로 적용 금지. 동일 가림/전체마을/실내 반복금지. 실제배치/보행/OS/서버/2인/원본배포 미검증. 시작clean·부모57351/현재50256·50257·Library비링크·Bootstrap diff없음. 원본/타차선 제어·통합/push/예약/새루프 없음. 담당카드만 갱신·다른카드/최상위/Grok번호 보존 확인. #120 한바퀴 종료.
