# 승인된 Laser 방식의 나머지 12종 적용 기록

2026-09-20. Laser 형태는 오너 승인본을 보존했다. 나머지 12종은 실제 Blender 메시 수정, 원화 정면 부품 등록, 3면 렌더 겹침 검토, 표면 UV 데칼, Unity 네이티브 자산으로 적용했다. 미술적 완전 일치나 오너의 12종 합격을 뜻하지 않는다.

## 적용

- A: 나무 몸통/올빼미 잎/캐논 뚜껑과 눈썹/당근 잎과 궤도.
- B: 개구리 턱과 독병/두더지 고글과 삽/미사일 받침과 눈/거북 목과 등 발사관.
- C: 사자 갈기와 왕관/이온 눈과 경사진 고리/고래 몸체와 삼지창/새 날개와 터빈.
- 각 기종에 2048px 투명 문양 atlas. 실제 표면에 붙은 UV 메시이며 얼굴이나 실루엣을 원화 평면으로 덮지 않는다. Team 재질 분리, 눈동자 PupilGaze 분리.
- 원화 원본은 변경하지 않았다. SecWind의 잘리던 날개를 포함하도록 파생 crop metadata만 정정했다.
- FRONT 부품 등록은 수정 전 카메라/배율에서 실제 메시 X·높이를 보정한다. 깊이는 별도 입체 확인한다. 이는 이미지 유사도 점수가 아니다.

## 실행 검증

- Native bake 및 UV/팀색 검사: 13종, 데칼 렌더러 33개.
- 조준 리그: 117 자세, 시각 포구 교란 13종이 게임 발사점에 영향 없음.
- 원화각/전투각 실제 Unity 캡처: 13종 × 5각도 = 65장.
- Mac 빌드 Succeeded, 실행 앱 로스터 13종 검사 통과.
- TankShape/fireZ/히트박스/게임 반지름은 이번 수정 대상이 아니다. 검증용 clone에 주 프로젝트의 동시 수정 코드도 동기화한 뒤 다시 빌드했다.

## 성능 범위

1280×720, 탱크 8대 + 실제 폭발 입자 106개, 6 warm-up + 30 samples. Editor Camera.Render와 GPU readback 비용이다. 게임 전체 FPS/최악 프레임 검증이 아니다.

| 항목 | 수정 전 native | 수정 후 native |
|---|---:|---:|
| 삼각형 | 643,798.0000 | 676,197.0000 |
| 드로콜 | 283.0000 | 322.0000 |
| 중앙값 ms | 5.0244 | 4.8651 |
| p95 ms | 6.1284 | 5.1856 |

드로콜과 삼각형은 증가했다. 단회 시간 측정 감소를 성능 개선이라고 해석하지 않는다. legacy procedural 기준은 20,332 triangles/221 draw calls이며 raw 로그를 함께 보존한다.

## 확인 자료

- `output/roster-approved-method/index.html`: 원화/모델/겹침 slider, 정면·측면·상면.
- `art/blender/roster_reference_workbench.blend`: 편집 가능한 12종과 packed 원화/정면 overlay.
- `output/roster-approved-method/original-unity-13.png`: 기존 4+4+4+1 원화 배열과 Unity 모델.
- `output/roster-approved-method/unity-combat40-13.png`: 실제 40m 후방/FOV60 화면 크기. 개별 확대하지 않았다.
- `output/roster-approved-method/delivery-hash-verification.json`: 주 프로젝트/앱 복사 후 파일 해시.
- 동일 폴더의 unity-bake-landmarks.log / unity-aim-capture.log / unity-build-final.log / runtime-roster-final.log.

## 남은 한계

12종에 방법 적용과 실행 검증을 완료했지만 원화와 100% 동일하지 않다. 원화 특유의 곡면·갈기·잎 겹침과 데칼 크기/연결은 추가 미술 다듬기 대상이다. 40m에서는 얼굴/작은 문양이 매우 작게 보인다. 코드 생성 legacy 경로는 기존 검증/임시 fallback 용도로 아직 남아 있으며 이번에 삭제하지 않았다. 일반 로스터는 모델 13종·코드 0종으로 확인했다.
