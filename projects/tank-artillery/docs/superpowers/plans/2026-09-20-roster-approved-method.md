# 승인된 Laser 방식의 전 기종 적용

오너가 Laser 형태를 합격 처리하고 나머지 탱크에도 같은 방식을 적용하라고 승인했다. Laser 승인본은 보존한다. 기존 orthographic-v1 원화, 원본 모델, 작업 파일을 유지하며 12종의 지오메트리와 UV 데칼을 수정한다. 정면 등록/겹침, 실제 측면·상면 볼륨, Unity 네이티브 자산/전투 화면을 함께 검토한다.

## 제작 결정

회전 분리, 눈, 후방 식별은 docs/art/MODEL_TRANSITION_CONTRACT.md의 13행 표를 따른다. Carrot 몸체와 잎은 Barrel에 함께, MultiMissile 목은 차체에 유지한다. Team 재질은 문양/몸체와 분리한다. TankShape/fireZ/히트박스/반지름 수정 금지. Laser 승인 SHA는 실행 전 저장한다. 기존 작업 디렉터리의 오너 자산을 대상으로 요청된 편집을 수행하며 Unity는 기존 headless 복제에서만 검증한다.

## 작업

- [x] A: Catapult/CrossBow/Cannon/Carrot — art/blender/refine_organic_a.py. 앞 윤곽, 눈, 목재/잎/해골/당근 마디. 동일 원화 기준으로 정면 비교를 생성하고 실제 메시를 수정한다.
- [x] B: Duke/MineLander/Missile/MultiMissile — art/blender/refine_mechanical_b.py. 독병/삽/미사일 받침/거북 목과 등 발사관, 궤도 비율. 개별 비교 이미지로 확인한다.
- [x] C: SuperTank/IonAttacker/Poseidon/SecWind — art/blender/refine_fantasy_c.py. 사자·왕관/구체와 위성/고래·삼지창/새 날개·터빈. 개별 비교 이미지로 확인한다.
- [x] 공통: art/blender/roster_refinement.py 및 roster_decals.py. 기존 원본을 유지한 후처리 API와 기종별 투명 문양, 정점 UV/네이티브 영구 머티리얼 연결. 참고 이미지로 눈·실루엣을 가리는 평면 빌보드 금지.
- [x] 검증: 12종 Blender 렌더와 고정 비교판, 13종 Unity UV/팀색/리그 검사. 원화 배열/전투 각도 두 비교판, 성능 측정 가능 범위 기록. 변경 전 모델/렌더/해시 보존.
- [x] 전달: headless native bake → 조준 검사/실제 캡처 → Mac build/runtime roster → 주 프로젝트 자산/앱 해시 대조. 검증하지 않은 미술 합격이나 성능 완료를 주장하지 않는다.

각 독립 모듈은 apply(kind, root, api)로 기존 build_tank 결과에 적용한다. api는 build_models globals이며 bpy와 mathutils 사용 가능. 공통 공유파일/roster/manifest/Unity 앱은 주 에이전트만 수정·빌드한다. 각 담당은 자기 모듈과 output/roster-approved-method/<group>/만 수정한다.

실행 결과와 미술/성능 한계: docs/art/ROSTER_APPROVED_METHOD.md. 12종에 오너 합격을 임의 부여하지 않음.
