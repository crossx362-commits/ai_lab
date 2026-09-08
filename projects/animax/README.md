# AniMax — 3ds Max 2025 애니메이터 툴셋 (Biped 우선)

Maya용 [Animo](https://ehsanbayat.gumroad.com/l/Animo)(Ehsan Bayat)의 워크플로를 참고해
pymxs + PySide6로 **처음부터 재구현**했다. 원본 소스는 사용하지 않았다(원본 라이선스가 소스
수정·재배포를 금지하고, Maya API 는 Max 에 대응이 없다).

## 설치
1. 3ds Max 2025 실행 → Scripting > Run Script → `install/AniMax_install.ms`.
2. 오른쪽에 AniMax 도크가 열린다. Customize UI 카테고리 `AniMax` 에서 툴바/단축키 배정.
3. 코드 수정 후엔 매크로 `AniMax_Reload` 로 리로드.

패키지 경로: `projects/animax/animax/` (Max 내장 Python 3.11 에서만 import 된다).

## 핵심 설계
- **월드 포즈 통일**: 모든 연산은 `core.Pose`(월드 pos/rot/scale) 기준. Biped 는
  `biped.getTransform/setTransform`, 일반 노드는 `node.transform` 으로 같은 수식을 쓴다.
- **Track**: 노드 하나의 키 시간·샘플·기록 인터페이스. Biped/일반 공통.
- **Biped 규칙(탐침으로 확인)**: FK 파트는 위치 쓰기가 무효, 손·발은 IK 로 위치가 적용됨 →
  기본은 회전만 쓰고 COM 만 위치+회전, 손·발 IK 위치는 옵션(`Track.pos_ik`).
- **슬러프 이중표현 보정**: MaxScript `slerp` 는 q/−q 를 안 잡아 먼 길로 돈다 → `core.slerp`.
- **슬라이더 세션**: 드래그 시작 시 베이스라인 캡처, 매 이동마다 베이스라인 기준 재계산,
  놓으면 undo 1회 단위로 확정.

## 기능 대응표 (Animo → AniMax)
| Animo | AniMax | 상태 |
|---|---|---|
| Tween Machine / Blend to Neighbor / Blend to Default / Ease / Scale Keys(L/R/Avg) / Push-Pull / Connect to Neighbour / Time Offset(+Stagger) / Noise-Wave | `sliders.py` | ✅ |
| Blend to World | 모든 블렌드가 이미 월드 기준 | 해당 없음 |
| Selection Sets(색상·내보내기) / Select Opposite | `review.py`, `pose.py` | ✅ |
| Mirror Pose/Animation | Biped: 내장 copy/paste opposite, 일반: 이름규칙+평면대칭 | ✅ |
| Nudge / Share Keys / Crop / Delete Redundant / Smart Snap / Smooth / Keys Time / Fast Bake | `keys.py` | ✅ |
| Tangents Auto/Linear/Step | 일반: Bezier 탄젠트, Biped: TCB continuity(Step 불가) | ✅ (제한) |
| Reset T/R/S / Align / Xform copy·paste / Xform Relationship / Global Offset | `pose.py` | ✅ |
| Spacify World/New Pivot/Relative/World Orient/Camera/Group/Aim | `spacify.py` (임시 헬퍼 베이크 방식) | ✅ |
| Spacify Temp IK / FK Chain | Biped 는 자체 IK 키 사용 | ⏳ 미구현 |
| Arc Tracker / Camera-space | `arc.py` (SplineShape, 시간 콜백 갱신) | ✅ |
| Multi-View Playblaster / Quick Export-Import | `review.py` | ✅ |
| Anim/Pose Transfer between scenes | JSON + Biped `.bip` | ✅ |
| Twosify (비파괴 스텝) | | ⏳ 미구현 (Biped 는 Ease Curve 미지원) |
| Vectorify (리패스) | | ⏳ 미구현 |
| Fast Merge AnimLayers | Biped `collapseAtLayer` 래핑 예정 | ⏳ |

## 검증
```bash
"C:/Program Files/Autodesk/3ds Max 2025/3dsmaxbatch.exe" tests/selftest_batch.py
```
`tests/selftest_result.txt` 마지막 줄이 `SUMMARY pass=62 fail=0` 이어야 한다.
배치 모드는 undo 스택이 오브젝트 생성까지 한 덩어리로 묶여 undo 검증이 불가 →
Ctrl+Z 동작은 인터랙티브 Max 에서 확인할 것.
`tests/import_ui_batch.py` 는 UI 패널 생성과 슬라이더·미러·Spacify 버튼 경로를 헤드리스로 돈다.

## 파일
```
animax/core.py      Pose, Track, Biped 판별, undo_block, slerp
animax/sliders.py   슬라이더 연산 + SliderSession
animax/keys.py      키 유틸, Fast Bake, 탄젠트
animax/pose.py      미러, 반대편, 리셋, Align, Xform, Global Offset, 전송
animax/spacify.py   임시 컨트롤 세션
animax/arc.py       아크 트래커
animax/review.py    플레이블라스트, Quick Export/Import, 선택 세트
animax/ui.py        PySide6 도크
install/AniMax_install.ms
tests/selftest_batch.py
```
