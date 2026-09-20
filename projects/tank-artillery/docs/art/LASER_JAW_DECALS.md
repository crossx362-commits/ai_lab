# Laser 아래턱 장갑과 UV 데칼 — 2026-09-20

오너가 보낸 18:05:10 스크린샷의 V자 아래턱을 대상으로 수정했다. 정면 원화 우선 결정과 고정 카메라 등록을 유지한다. 전체 모델이 원화와 100% 일치한다는 판정은 아니다.

- 이전 아래턱은 아래쪽이 0.85m 뒤로 물러나는 면이었다. 중앙 능선에서 만나는 두 장갑 면과 위·아래 가장자리 면으로 변경했다. 앞 윤곽과 눈·추진구 배치는 유지했다.
- `art/blender/laser_decal_texture.py`의 원화 좌표 기반 벡터 패널선을 투명 PNG로 만든다. 눈·명암·윤곽을 그림으로 덮는 텍스처가 아니다. 아래턱, 머리, 어깨, 날개, 발사 장갑에 별도 표면 메시와 ReferenceFrontUV를 배치한다.
- Blender 이미지가 blend 파일 안에 패킹된다. JSON exporter → mesh pack → Unity Mesh.uv → 영구 머티리얼/텍스처 → prefab 경로를 연결했다. 기존 UV 없는 모델도 지원한다.
- Body/Team과 분리된 LaserDecal 역할을 사용한다. 전용 ArmorDecal 셰이더는 투명 혼합, 깊이 쓰기 해제, 양면 표시를 사용한다. 한쪽 면 컬링 때문에 Unity에서 선 일부가 사라졌던 문제는 켜기/끄기 실제 렌더로 발견하고 수정했다.
- TankShape, fireZ, 히트박스, 반지름, 실제 조준 제어는 수정하지 않았다.

## 이번 검증

`output/laser-jaw-decals/`에 증거를 보관한다.

- `unity-bake.log`: 13 탱크/26 발사체 네이티브 검사, 34,107 데칼 UV 정점, Unity 2048×2048 텍스처 연결, 빨강·파랑 MPB 팀색 검사 통과.
- `unity-capture-final.log`: 117 조준 자세 및 13 시각 포구 교란 격리 검사 통과. Laser 원화용 각도 3장·전투 거리 22m/40m 2장 캡처. 이것은 시각적 합격 판정과 다르다.
- `unity-front-decal-on.png` / `unity-front-decal-off.png`: 동일 정면 카메라, 실제 Unity 렌더. 데칼 토글 시 RGB 차이가 12를 넘는 픽셀 13,186개.
- `jaw-comparison.png`: 동일 영역 원화 / 수정 전 Blender / 수정 후 Blender. 비교를 위해 모델 경계에 맞추는 재정렬은 하지 않았다.
- `blender-workbench.png`: 실제 Blender 작업창, 정면 원화 겹침 유지.

## 비용 범위

Laser 삼각형 39,348 → 51,493, 부모·재질 메시 그룹 7 → 8. 이 수치는 에셋의 기하/렌더러 수이며 실제 드로콜·프레임 시간 측정이 아니다. 8대와 폭발 동시 프레임 성능 검증은 이번 변경에서 완료하지 않았다.
