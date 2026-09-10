두 번째 루프 금지 — 이미 qa_shots 중
`loop/loop.sh`를 울온용으로 켜지 말고, 이미 점유된 워크트리 `../ai_lab-loop`(브랜치 loop-claude)에서 `docs/SESSION_HANDOFF.md` 「지금 여는 랩」(NC 굽기 4→2)을 이어서 닫아라.
그 트리는 지금 `projects/ulon/tools/slice_selfcheck.sh` 두 판 뒤 `projects/ulon/tools/qa_shots.sh`가 Unity 배치로 돌아가는 중이다.
위험: 재와별용 `loop/loop.sh`나 공유 트리 `projects/ulon/unity`에 배치를 하나 더 붙이면 진행 중 샷이 락 충돌로 죽는다.
