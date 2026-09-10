울온 루프 러너 부재 — loop.sh는 재와별 전용
`loop/loop.sh`는 상태 파일이 `docs/STATUS.md`·`docs/feedback/INBOX.md`·`projects/ashes-to-stars/CLAUDE.md`로 하드코딩돼 있고 `projects/ulon/tools/`엔 러너가 없다 — 울온 루프는 지금까지 클로드 세션이 `projects/ulon/docs/SESSION_HANDOFF.md`의 「■ 지금 여는 랩」을 읽고 도는 방식이었다(현재 랩: NC 굽기 4→2회 병합).
착수하려면 loop.sh의 세 상태 경로를 `LOOP_PROJECT`(기본 ashes)로 인자화해 울온은 `projects/ulon/docs/{SESSION_HANDOFF,DEV_INBOX,GAME_DESIGN}.md`를 읽게 하고, 매 이터레이션 끝에 `tools/slice_selfcheck.sh`+`tools/qa_shots.sh` EXIT=0을 게이트로 걸어야 한다(추측 아님, 두 파일 실재 확인).
위험: 지금 loop.sh를 그대로 켜면 명령은 ulon인데 루프가 재와 별 STATUS.md를 갱신하며 다른 프로젝트를 개발한다 — 게다가 BOARD.md 「프로젝트」 절이 비어 있어 규칙 7의 ulon 상태 판정 근거도 없다.
