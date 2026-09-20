# 모델 제작 단축 방안 조사

2026-09-20. 조사 결과이며 실제 생성 속도/품질 비교는 아직 수행하지 않았다.

## 우선 적용할 제작 흐름

1. 정본에 카메라를 먼저 맞춘다. 지금처럼 서로 다른 각도로 비교하면서 형상 수치를 수정하면 재작업이 생긴다.
2. 얼굴·몸통 실루엣부터 맞추고 장식은 뒤로 미룬다. 기존 메시를 Blender lattice로 변형해 전체 비율을 수정할 수 있다. Lattice는 2D 원화를 자동으로 3D로 맞추는 기능은 아니다.
3. 한 종 수정 시 해당 종의 5장만 렌더한다. 현재 ModelAcceptanceCapture.Run은 매번 13종×5=65장을 렌더한다. 프레임 수는 65→5로 줄일 수 있으나 전체 작업시간 13배 단축을 의미하지 않는다. 개별 검사 중 전체 Mac 빌드/로스터 재생성을 반복하지 않고 묶음 반영 시 수행한다.
4. 기종별 .blend와 내보내기 결과를 독립 처리하고 공유 tankfall_roster.blend/manifest는 마지막 한 번 통합한다. 현재 rebuild_character.py는 매번 공유 roster를 열고 저장하여 동시 작업이 충돌한다. 분리하면 병렬 제작이 가능하지만 동일 소스 파일 동시 편집도 분리해야 한다.

## 다른 제작 경로

- 가장 큰 잠재 단축 후보: 정본 이미지→AI 3D 초벌→Blender에서 잘못된 얼굴/무기 보정·부품 분리→기존 TankShape 기준에 시각 리그만 연결. Rodin과 Tripo 공식 문서에 이미지/다중 이미지 3D 생성이 있다. 이 프로젝트 원화 재현, 부품 분리 품질 및 총 소요시간은 미검증. 생성시간과 게임 투입 완료시간을 혼동하지 않는다.
- 외부 유료 API 신규 실행은 하지 않았다. 계정/크레딧 이용 가능 여부도 확인되지 않았다. 13종 대량 생성 전에 실패가 명확한 Laser 1종을 비교해야 한다. 앞/옆/뒤와 조준 분리까지 보고 기존 모델보다 나은 경우에만 확대한다.
- 로컬 TRELLIS.2 MLX 후보도 확인했다. 이 Mac은 Apple M5 / 16GB이며 해당 포트 README는 M4 Max / 128GB에서 검증, 낮은 메모리는 미검증이라고 명시한다. 15GB 모델 다운로드와 설치부터 시작하는 것은 현재 가장 빠른 경로라고 판단할 근거가 없다.

## 결론

지금 확실히 줄일 수 있는 것은 전체 반복 빌드/렌더와 공유 파일 직렬화다. 모델링 자체의 큰 단축은 AI 초벌 한 종 비교가 성공해야 입증된다. 60~100시간을 특정 짧은 시간으로 다시 약속할 근거는 아직 없다. 품질 기준을 낮추거나 그림을 평면으로 붙여 완료로 처리하지 않는다.

## 확인한 1차 출처

- Blender Lattice: https://docs.blender.org/manual/en/5.2/animation/lattice.html
- Hyper3D features: https://docs.hyper3d.ai/en/get-started/features
- Tripo generation: https://docs.tripo3d.ai/
- TRELLIS.2 MLX 작성자 저장소: https://github.com/gtrg55/trellis2-mlx
- Microsoft TRELLIS.2: https://github.com/microsoft/TRELLIS.2
