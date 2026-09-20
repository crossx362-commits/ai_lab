# 3면 원화 대조용 Blender 수정본 — 2026-09-20

검토 중인 12종의 실제 메시와 기종별 FRONT/SIDE/TOP 원화 평면을 보존한 스냅샷이다. Laser는 승인본을 유지하며 이 파일에 재생성하지 않는다.

`three_axes_workbench.blend`를 Blender에서 열고 기종별 씬을 선택한다. 원본 이미지가 패킹되어 있으며, 12개 씬에 총 36개 원화 평면이 있다. 기종별 `comparison.png`는 고정 등록 좌표의 원화·모델·겹침 비교다.

원화 일치 미승인·게임 미적용 상태다. 남은 오차와 뷰 간 상충, Unity 팀색·전투 시점·성능 검증의 미실행 상태는 `docs/art/THREEVIEW_CORRECTION_REVIEW.md`에 기록했다.

JSON의 output 경로는 생성 당시 작업 경로이며 이 스냅샷의 상대 경로가 아니다. proposed.blend 해시는 개별 생성 파일용으로, 통합 workbench 해시와 다르다. staged-audit는 12종의 원화 평면 3개, 제어점 불변, Team 표면 및 Laser 3개 파일 해시 보존 검사다. 시각적 합격 또는 Unity 실행 검증을 뜻하지 않는다.
