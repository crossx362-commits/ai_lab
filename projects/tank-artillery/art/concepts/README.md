# 포트리스 참고 캐릭터 재설계 — 2026-09-19

## 조사 → 원화 → 모델링

이미지 검색으로 포트리스2 캐릭터와 후속 공식 캐릭터를 조사한 후,
`tankfall-character-sheet.png`를 먼저 생성하고 확인했다. 그 원화의 실루엣,
얼굴 위치, 주요 무기와 배색을 `../blender/concept_roster.py`의 Blender 조형에 반영했다.
원화는 디자인 목표이고 실제 게임 렌더와 동일한 이미지는 아니다.

참고 출처:
- [Fortress W 공식 캐릭터 소개](https://fortress-w.creta.world/ko#characters): 큰 눈, 과장된 무기, 짧은 차체. 공식 사이트 캐롯 화면을 브라우저에서 직접 확인.
- [Fortress3 Blue 공식](https://fortress3blue.com/): 미사일 캐릭터 이미지 검색 참고.
- [데일리게임 포트리스 슈퍼탱크 소개](https://www.dailygame.co.kr/view.php?ud=200908261208260014641_26): 원작 슈퍼탱크 이미지 검색 참고.

공식 게임 이미지를 런타임 텍스처로 복사하지 않았다. 원화와 Blender 모델은 이 프로젝트용 새 제작물이다.

| 기종 | 원화에서 옮긴 주요 형태 |
|---|---|
| Catapult | 나무통 몸체, 붉은 바퀴, 큰 투석 바위 |
| CrossBow | 청록 딱정벌레 잎 등판, 넓은 쇠뇌 |
| Cannon | 남색 구형 대포 몸통, 황동 포구, 포신에 붙은 눈 |
| Carrot | 주황 둥근 차체, 녹색 잎, 커다란 크림색 포 |
| Duke | 개구리형 넓은 주둥이와 돌출 눈, 녹색 독가스 용기 |
| MineLander | 두더지 고글, 낮은 공병 차체와 큰 삽날 |
| Missile | 로켓 본체의 눈, 크림색 날개, 작은 남색 하부 |
| MultiMissile | 거북 등판과 머리, 9구 발사대 |
| SuperTank | 사자형 볼과 갈기, 금색 왕관, 중장갑 |
| Laser | 낮고 넓은 가오리형 비행체, 청록 외줄 눈 |
| IonAttacker | 분홍 구체, 큰 외눈, 금색 궤도 고리 |
| Poseidon | 푸른 고래 몸체, 흰 배, 꼬리지느러미 |
| SecWind | 새 부리, 녹색 깃털 날개, 쌍 터빈 |

## 실제 3D 리소스

- 편집 가능한 Blender 원본: `art/blender/tankfall_roster.blend`
- Blender 조형 코드: `art/blender/concept_roster.py`
- Unity FBX: `unity/Assets/_Project/Art/Tanks/FBX/`
- 런타임 탱크 프리팹 13종: `unity/Assets/_Project/Resources/TankModels/Tanks/`
- 발사체 프리팹 26종: `unity/Assets/_Project/Resources/TankModels/Shells/`
- 저장된 메시: `unity/Assets/_Project/Resources/TankModels/Meshes/`
- 재질: `unity/Assets/_Project/Art/Tanks/Materials/`
- Unity 비교 씬: `unity/Assets/_Project/Scenes/TankArtGallery.unity`

`build_models.py` → `render_concept_comparison.py` → Unity 메뉴
`Tankfall/Rebuild Native 3D Assets` 순으로 다시 생성한다.
게임은 저장된 프리팹과 메시를 로드한다. JSON은 에디터에서 리소스를 만드는 중간 형식이다.
원본과 프리팹의 부품별 정점, 회전축, 발사점, 바퀴 연결을 검사한다.
사자·레이저의 과도하게 긴 포신은 `TankShape` 길이를 줄여 실제 발사점도 함께 맞췄다.

## 원화 일치 기준

오너 요청: 원화와 90% 이상 닮은 수준이 완성 기준이다.
현재 작업을 90% 달성으로 판정하지 않았다. 자동 메시/실행 검사는 외형 유사도 검사가 아니다.
특히 원화의 얼굴 조형, 다층 장갑 구조, 마모와 재질 표현에 차이가 남아 있다.

비교 자료: `output/native-models/compare.html` (기종별 원화 / 실제 Blender 렌더),
`output/native-models/comparison/` (원본 모델에서 직접 렌더),
`output/native-models/gallery.png` (Unity 저장 프리팹 렌더).
AI 이미지로 모델 렌더를 보정하거나 원화를 변경해서 닮아 보이게 만들지 않는다.

검증 로그는 `output/native-models/`에 보관한다. 이전 `output/concept-remodel/` 결과는
이전 모델 버전의 기록이며, 현재 네이티브 프리팹 버전의 검증 근거로 사용하지 않는다.
