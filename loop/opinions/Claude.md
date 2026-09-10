의견 커밋 이중 writer로 git 경합·유실 위험
loop/dispatch-board.sh의 `commit_opinions`와 loop/command-board/server/board-api.ts의 `commitBoard`가 둘 다 loop/opinions를 add·commit·push한다 — 같은 저장소 두 writer라, detached로 도는 dispatch의 수집-종료 커밋과 오너가 누른 `/api/decide` 커밋이 겹치면 git index.lock 경합이 난다. `/api/command`만 `dispatchState().running`이면 409로 막지만 `/api/decide`엔 그 가드가 없다.
게다가 서버가 dispatch를 `DISPATCH_ONCE=1`로 띄우므로 `commit_opinions` 푸시가 실패해도 재시도할 다음 사이클이 없어, 다음 오너 조작 전까지 의견이 로컬에만 남는다(다른 기계에서 근거 유실).
제안: 커밋 경로를 서버 `commitBoard` 한 곳으로 모으고 dispatch는 파일만 남기게 분리(공용 함수 재사용), 최소한 `/api/decide`에도 수집 중 거절 가드 추가.
위험: 수집-커밋 순서를 잘못 바꾸면 수집 완료 전에 커밋돼 빈 의견이 확정될 수 있으니 「수집 종료 후 커밋」 순서 보존이 필수다.
