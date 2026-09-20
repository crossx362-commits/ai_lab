# 탱크 모델 전환 결정 — 2026-09-20

정본: `/Users/junholee/ai_lab/docs/refs/tankfall_tanks_concept_2026-09-20.webp` 및 오너가 나란히 놓은 원화 시트. 아래는 제작 결정이며 구현 완료 판정이 아니다. 생성된 orthographic-v1은 보조 자료이며 정본을 대체하지 않는다.

## 제작 전 기종별 결정

| 기종 | 차체 고정 | 좌우 조준 회전 | 상하 조준 회전 | 눈 | 후방·상부 식별 |
|---|---|---|---|---|---|
| Catapult | 나무 몸통·바퀴·얼굴 | 투석 받침 | 팔·밧줄·바가지·돌 전체 | 몸통 눈동자가 표적 추적 | 원화의 돌·바가지·붉은 바퀴 유지 |
| CrossBow | 올빼미 몸통·나뭇잎·바퀴·얼굴 | 쇠뇌 받침 | 활대·줄·레일 전체 | 몸통 눈동자가 표적 추적 | 등 나뭇잎 겹침과 넓은 활대 |
| Cannon | 목재 차대·바퀴 | 구형 얼굴·눈썹·뚜껑·해골 문양 | 포신만 | 구형 얼굴에 붙여 표적 추적 | 뒤쪽에도 작은 해골 문양 추가 |
| Carrot | 궤도·하부 지지대 | 당근 전체 받침 | 당근 몸통·포신·잎·눈 전체, 중간을 꺾지 않음 | 몸통과 함께 회전하며 눈동자 추적 | 잎 부채와 당근 몸통 마디 |
| Duke | 개구리 몸통·독병·차대 | 작은 발사부 받침 | 발사부만 | 개구리 눈동자 표적 추적 | 투명 독병 안 녹색 액체·위험 표식 |
| MineLander | 두더지 차체·고글·삽·궤도 | 후방 박격포 받침 | 박격포 관 | 고글 안 렌즈의 제한된 추적 | 후방 박격포와 황색 등 장갑 |
| Missile | 궤도·발사대 기부 | 발사대 상부 | 붉은 미사일·눈·꼬리날개 전체 | 미사일과 함께 회전하며 추적 | 붉은 몸통·십자 꼬리날개 |
| MultiMissile | 거북 머리·목·다리·바퀴 | 등껍질 발사관 받침 | 등껍질과 9연장 관 전체 | 목은 고정, 눈동자 표적 추적 | 등껍질 격자와 뒤쪽 육각 무늬 |
| SuperTank | 차대·궤도 | 사자 얼굴·갈기·왕관·어깨 포드 | 중앙 포신 및 포드 내부 관 | 사자 눈동자 표적 추적 | 갈기 후면·왕관·등 왕관 문양 추가 |
| Laser | 호버 지지부 | 가오리 얼굴·날개·등지느러미 전체 | 발사 개구의 내부 부품만 | 눈 슬릿 안 발광 시선 추적 | 보라색 가오리 외곽·등지느러미 |
| IonAttacker | 하부 부유 코어 | 구형 몸통·금테·위성 눈 전체 | 발사 렌즈 내부만 | 중앙 눈과 위성 눈이 표적 추적 | 분홍 구체·금테·4개 위성 눈 |
| Poseidon | 고래 몸통·눈·꼬리·바퀴 | 등 삼지창 받침 | 삼지창 전체 | 몸통 눈동자가 표적 추적 | 꼬리·등지느러미·등 물방울 무늬 |
| SecWind | 궤도·하부 몸통 | 새 머리·날개·터빈 전체 | 터빈 내부 발사부만 | 새 얼굴에 붙여 표적 추적 | 깃털 부채와 양 날개 터빈 |

눈동자는 안구 표면 안에서만 움직인다. 장식/눈의 조준 움직임은 판정에 참여하지 않는다. 비대칭 실루엣은 원화에 맞추고 후방 추가 표식은 정면 원화를 가리지 않는다.

## 변경 금지와 검증 순서

1. `TankShape` 수치 및 기존 발사점 산식, 히트박스·반지름을 보존한다. 모델의 authored FirePoint는 시각 자료이며 탄도 기준으로 사용하지 않는다. 조준 제어용 계층과 모델 계층을 분리한다.
2. 첫 모델의 전용 팀색 슬롯에 실제 MaterialPropertyBlock을 적용해 청/적 두 렌더를 검증한다. 원화 몸통색을 팀색으로 덮지 않는다.
3. 모델 파일 존재 시 모델을 사용한다. 손상된 모델은 오류이며 조용한 코드 폴백 금지. 13종 모두 전환 후 코드 생성 제거. 과거 기준선은 독립 보관하여 측정한다.
4. 갤러리와 로그에 `모델 N종 · 코드 M종` 표시.
5. 원화 비교판은 4+4+4+1 배열, 동일 셀 크기와 촬영 크기. 전투 비교판은 실제 후방 40m 카메라와 조준 포즈로 13종 촬영. 두 판 모두 직접 눈으로 검토한다.
6. 동일 장면·해상도·카메라·워밍업 조건에서 8대+폭발의 전후 삼각형, 실제 드로콜, CPU/GPU 또는 사용 가능한 프레임 비용을 기록한다. 렌더러 수는 드로콜 실측값이 아니다.

## 현재 확인된 결함 / 미검증

- 네이티브 원본의 authored FirePoint는 보존하고 전투 반환값은 TankAimRig의 기존 산식 기준으로 분리했다.
- 기존 PerfBaseline의 잘못된 모델 측정을 수정했다. 폭발 포함 실제 성능 측정은 별도로 남아 있다.
- 현재 MPB는 TankDrive/CharacterAnimator 경로를 추가 확인해야 하며 머티리얼 참조 검사만으로 렌더 검증을 대체할 수 없다.
- 미사일 색 값은 빨강이며 주석은 이미 수정되어 있다.
- 13종 형상 일치, 두 비교판 및 폭발 포함 성능 전후 검증은 미완료. 기존 에셋 검사 PASS는 미술 합격이 아니다.

## 첫 검증 기록

- `TankAimRig`로 조준/발사 기준과 시각 리그 분리. 기본 자세 13종 및 yaw -135/0/70 × pitch -10/35/80의 117개 대조에서 기존 프로시저럴 발사점과 일치(위치 오차 한계 0.1mm). `output/headless-unity/shape-contract-v2.log`. 이 실행의 전체 결과는 팀색 13건 실패이므로 전체 PASS로 표기하지 않는다.
- Catapult에 Blender 지오메트리로 후면/윗면 전용 Team 표식 추가. 네이티브 MPB 적용 후 청/적 실제 640px 렌더를 눈으로 대조했고 원화 목재/붉은 바퀴색 유지 확인. `output/headless-unity/output/team-contract/Catapult-blue.png`, `Catapult-red.png`; `team-render.log`의 TEAM_MPB_FAILURES 0. 표식 미술 품질 및 후방 40m 가독성은 아직 미합격.
- 기존 코드 성능 기준선이 명시적으로 ForceProcedural 대조군을 생성하도록 수정. 폭발 및 프레임 비용 측정 완료를 의미하지 않는다.
- `output/model-contract-compile.log` 컴파일 통과. 에셋 베이크 첫 실행은 프리팹 캐시 계층 검사에서 실패했으며 별도 프로세스로 재검증한다. 편집 중인 사용자 Unity는 건드리지 않았다.
- 다음 순서: 팀 표식 디자인/가독성 및 나머지 12종 → TankShape 산식 단일화와 독립 대조군 보관 → 전환 경로 제거/계수 → 회전·눈 구현 → 두 비교판 → 폭발 포함 전후 성능. 원화 일치까지 반복한다.

- 후속 원인 확인: 베이크 오류는 캐시가 아니라 새 조준 제어 노드 3개를 이전 검사기가 모델 노드로 셌기 때문이다. 모델 정점 검사는 그대로 유지하고 조준 계층 3개 및 렌더러 부재를 명시 검사하도록 수정. `team-native-verify-v2.log`에서 13종/26발사체 PASS. 검증된 네이티브 에셋을 주 프로젝트에 복사했다. 앱 재빌드는 아직 안 했다.

## 헤드리스 후속 — 2026-09-20 14:40 KST

- CrossBow 바퀴 외측 금속 허브 안에 Team 에나멜 면을 Blender 메시로 추가. 목재 바퀴/금속 테/청록 잎은 유지. 재생성 70,484 triangles.
- `bake-crossbow-team.log`: 네이티브 13종/발사체 26종 및 원본 39종 검사, HEADLESS_ART_BAKE_VERIFIED. 검증된 에셋을 주 프로젝트에 복사.
- `crossbow-team-render.log`: TEAM_MPB_FAILURES 0. `output/headless-unity/output/team-contract/CrossBow-blue.png`, `CrossBow-red.png`를 직접 확인. 허브만 청/적으로 달라진다. 후방 40m에서는 작을 가능성이 있으므로 가독성 합격으로 처리하지 않는다. 후방에 빈 몸통 면이 크게 보이고 잎 분포도 추가 개선 필요.
- BlenderModels에 실제 프리팹 수 기반 `모델 N종 · 코드 M종` 로그 추가. 프리팹이 없지만 원본 JSON이 있으면 오류, 두 모델 파일 모두 없을 때만 명시 경고 후 임시 코드 폴백. 모든 모델이 있는 현재 코드 생성 경로 삭제와 갤러리 라벨은 아직 남았다.
- `model-contract-heartbeat-compile.log` 통과. 이번 갱신은 앱 재빌드/두 비교판/성능 전후/13종 형상 일치 완료를 의미하지 않는다.

## 헤드리스 후속 — 2026-09-20 15:15 KST

- `ModelAcceptanceCapture.cs` 추가: 모델 13종의 고정 원화 방향 / 현재 사격 거리22m / 요청 거리40m, 총39장 렌더. 전 기종 같은 배율·해상도이며 윤곽에 맞춰 모델별 크기를 바꾸지 않는다. `output/model-acceptance/index.html`에 정본과 4+4+4+1 배열 비교, 카메라 전환, 실제 `모델 13종 · 코드 0종` 라벨.
- BattleDemo 실제 EnterFire는 카메라 pitch18 / distance22이며 focus높이2.2, 기본 FOV60. 따라서 40m를 현재 실제 사격 설정이라고 쓰지 않는다. 검증 장면 렌더이지 실제 플레이 스크린샷이 아니므로 최종 전투 합격판을 대체하지 않는다. 원화와 개별 투영/크기 정밀 일치도 남았다.
- 40m 이미지를 직접 확인: CrossBow는 화면에 작게 보이고 기존 허브 팀색만으로는 충분하지 않다. 다음 팀색 설계는 후방 상부의 더 큰 전용 면이 필요하다.
- CrossBow 후면의 빈 구체를 덮는 잎 3장과 잎맥을 Blender 메시로 추가. 72,848 triangles. `bake-rear-leaves.log`: 13종 네이티브/26발사체 및 원본 검사, HEADLESS_ART_BAKE_VERIFIED. 원화 형상 일치와는 별개다.
- `output/model-acceptance/compile.log` 통과. 코드 생성 제거/눈/나머지 팀색/실전 합격판/폭발 전후 성능/앱 재빌드는 계속 남아 있다.

## 직접 작업 전환 — 오너 지시

- automation-2를 PAUSED로 변경하고 설정 파일에서도 확인. 이후 예약에 맡기지 않고 현재 작업에서 수행.
- Carrot 몸통·눈·잎을 Barrel 아래로 옮겨 포신과 함께 상하 조준. 초기 고각 렌더에서 궤도 침범 발견 후 옆면 축 위치로 실제 Blender 피벗을 옮기고 기본 자세를 보존했다.
- Carrot 눈동자/하이라이트를 PupilGaze로 분리, 시각 조준 목표를 향해 제한된 범위로 움직임. 다른 기종은 아직 이 리그로 전환하지 않았다.
- `TankAimRigVerify.cs`: 13종×9자세=117 발사점 대조, 원화 모델 FirePoint를100m 옮기는13회 교란에도 실제 발사점 불변, Carrot 전체 회전 계층/눈동자2개 범위 검증. `direct-carrot-review.log` TANK_AIM_RIG_PASS.
- 같은 로그에서65장 렌더 완료. 35/60도 이미지를 직접 보고 포신만 꺾이던 문제가 제거됐고 고각에서 몸통이 궤도를 파고들던 현상이 줄었음을 확인. 고각의 고정 지지 프레임 연결 형태는 추가 보완 필요. 원화100%일치 판정 아님.
- `bake-carrot-axle.log`: 네이티브/원본 검사 통과. 검증 에셋 주 프로젝트 반영. 앱 빌드와 런타임 검사 결과는 아래 기록.

- 직접 빌드: `build-direct-carrot.log` Mac Succeeded / 394MB / 에러0. 실제 빌드 `-batchmode -rosterselftest` exit0, 13종 생성 자체검사 통과(`runtime-direct-carrot.log`). 주 프로젝트 `unity/Build/Tankfall.app`에 복사. 형상100%합격이나 폭발 프레임 성능 통과를 의미하지 않는다.

## 레이저 — 오너 재지시: 기존 원화 3면도와 직접 비교

- 작업 기준은 `art/concepts/orthographic-v1/Laser.png`의 정면/측면/평면. 정본의 사선 컷만 보며 수정하던 흐름을 중단했다. 원화는 변경하지 않았다.
- `art/blender/compare_three_views.py Laser` 추가. 원화 크롭 / 실제 Blender 3면도 / 50% 겹침의 3×3 비교판, 원본과 렌더 SHA256 및 균일 배율을 함께 기록한다. 가로세로 왜곡 없음. 시점별 독립적인 전체 크기 정렬이며 유사도 점수는 아니다.
- 실제 비교 이미지: `output/orthographic-review/Laser/three-view-comparison.png`. 각 행이 원화/모델/겹침. 눈 빈틈, 정면 발사부 간격, 측면 배기구 부재, 평면 외곽 차이를 직접 확인.
- V자 이마/턱 연결, 측면 발사부 기울기, 세 방향에서 폭을 가지는 입체 지느러미, 후방 배기구를 수정. 눈 빈틈을 막던 면이 이마 밖으로 튀어나온 중간 실패도 렌더로 발견해 안쪽 연결면으로 수정했다.
- 현재 비교에서도 발사부 정면 폭/평면 배치, 옆 장갑 외곽, 지느러미 곡률, 상부 패널 형태의 차이가 남아 있다. 원화 일치 합격 아님.
- 3면도 최신 렌더: `laser-brow-cavity-render.log` 3방향 저장. 최신 Blender 모델: `art/blender/characters/Laser.blend`. 비교판의 재료 차이를 형상 일치로 간주하지 않는다.

- `bake-laser-cavity-final.log` HEADLESS_ART_BAKE_VERIFIED. 검증된 네이티브 에셋을 주 프로젝트에 복사. 이 최신 3면도 수정의 앱 재빌드는 아직 하지 않았다.

### Laser live Blender refinement — 2026-09-20

Owner request: display the original three-view sheet alongside editable geometry in Blender. Created `art/blender/laser_reference_workbench.blend`: packed untouched Laser.png on the left, editable model in quad views on the right. Preserved the previous live default scene.

Replaced box shoulders with arched, tapered armor and inner cheek plates; rebuilt thick fins as ridged plates; widened forward blades, added recessed slots and shoulder seams; added hover suspension and closed the visor hull. Updated Laser.blend, roster, FBX, JSON and verified native Unity assets. Laser now 21,810 triangles; this is not a performance acceptance.

Evidence: `output/laser-refinement-v2/blender-workbench.png`, `output/orthographic-review/Laser/three-view-comparison.png`. Untouched reference crops remain the source; no match percentage asserted. Visually still mismatched: central cap proportions, fin curvature, plan-view blade spread, reference panel detail. Not visually accepted.

Headless native bake PASS; TankAimRigVerify PASS 117 poses and 13 muzzle perturbations; captured five Laser Unity views. Logs in `output/laser-refinement-v2/`. Native asset directories copied back to project. Standalone app not rebuilt in this pass. No TankShape, fireZ, collision or radius changes.

### Laser direct overlay correction — 2026-09-20

Owner rejected side-by-side-only work. Live Blender now has three packed original image empties drawn FRONT at alpha 0.45 over the actual editable meshes, visible only on matching orthographic axes. Registration is fixed in `art/blender/laser_overlay_workbench.py`, independent of mesh bounding boxes. Original crops are not warped. Workbench saved with overlays enabled.

Adjusted eye width and visor curvature; moved lateral fins forward/lower and changed their base spread; widened the center fin root and seated all fin roots into the cap; raised and widened lower thruster bodies, angled luminous lift disks; moved rear paired exhausts inward/forward/up. Geometry-only: no TankShape changes. Evidence: `output/laser-overlay-fit/after-overlay.png`; source and headless bake logs in the same folder. Native bake passed and project native assets updated. Standalone app not rebuilt.

Still NOT an exact visual match: side-profile central fin and shoulder/helmet contour remain visibly offset; plan-view blade spread remains off. No 100 percent acceptance claim.

### Laser position gate — reference view conflict pending

User requested no intermediate images until all positions align. Replaced approximate eyes with independently traced original left/right cyan contours. Fixed-front eye mask: bounding rectangles identical, centroid error 0.761 px, eye-only IoU 0.9537. This is not overall acceptance. Detailed provenance and conflicting FRONT/TOP placement landmarks are in `LASER_POSITION_ALIGNMENT.md`. Asked owner which view governs conflicting placement; dependent modeling changes pending answer. Headless native bake PASS; project native assets synchronized. Live workbench not refreshed with another intermediate model; standalone app not rebuilt.

### 레이저 정면 우선 위치 정렬 전달 — 2026-09-20

오너 승인에 따라 정면 원화의 배치로 통일했다. 원본 3면도는 변경하지 않았다. 눈 중심 오차 0.2 px 이내, 추진구 중심 0.5 px 이내, 세 날개 끝 1.1~2.7 px(수동 윤곽 기준)인 고정 정면 비교를 확인했다. 상세 근거·범위는 `LASER_POSITION_ALIGNMENT.md`. Blender 작업 파일은 정면 겹침을 켠 최신 메시로 저장했다. Native 자산 검증, 117개 조준 포즈/13종 시각 포구 교란 검사, Mac 빌드와 13종 런타임 로드 검사를 통과했다. 최신 네이티브 자산과 402 MB 앱을 프로젝트에 전달하고 파일 해시를 확인했다. 다른 기종의 미술 합격이나 레이저 재질·문양까지 100% 일치를 선언하지 않는다.
