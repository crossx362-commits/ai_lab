지휘 보드 검토 — 수집 중복·중단 불가
`loop/dispatch-board.sh`의 `cycle()`은 STOP과 `_last-signature`만 보고 `_dispatch.status`를 안 본다 — 상시 루프가 떠 있는 상태에서 보드가 `/api/command`로 `DISPATCH_ONCE=1 DISPATCH_FORCE=1`을 또 띄우면 두 수집이 같은 `loop/opinions/<AI>.md`·`.raw`에 겹쳐 쓴다. `cycle()` 첫머리에 `_dispatch.status`가 running이면 건너뛰기(또는 `loop/opinions/.lock` flock)를 넣어 한 번에 하나만 돌게 할 것.
멈춤 수단이 파일 `touch loop/STOP` 하나뿐이라, 수집이 고착되면 `command-board`의 `/api/command`가 409로 새 명령을 7~12분 막고 오너가 손으로 파일을 만들어야 한다 — 보드 금지 절(오너에게 일 시키지 말 것)과 정면으로 어긋난다. `server/board-api.ts`에 `POST /api/dispatch/stop`(STOP 생성 → child kill → STOP 제거 → `_dispatch.status`를 idle로)과 화면 버튼을 붙일 것.
위험: 중단 API가 `loop/STOP`을 지우는 걸 실패하면 이후 수집이 조용히 전부 멈춘다 — 정리는 `finally`에서 하고 보드 상단에 STOP 존재 여부를 표시할 것.
