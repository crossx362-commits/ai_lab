로컬 API 무인증 — text/plain POST로 master 푸시·bash 실행 유발
loop/command-board/server/board-api.ts `handle()`의 POST 3종(/api/command·decide·dispatch) 앞에 Origin/Host 화이트리스트(127.0.0.1:5177)와 Content-Type=application/json 강제를 넣어라 — 지금 `readJson`은 content-type을 안 보고 무조건 `JSON.parse`라, 오너가 연 임의 웹페이지가 text/plain fetch(프리플라이트 없음)로 명령을 주입해 `git push origin master`·`spawn(bash, dispatch-board.sh)`를 트리거할 수 있다(응답만 CORS 차단, 부작용은 실행됨 — 코드로 확인).
곁가지 결함(추측 아님): dispatch-board.sh `commit_opinions`와 서버 `commitBoard`가 동시에 `git add/commit/push`를 쳐 index.lock 충돌 시 dispatch 쪽이 `|| return 0`으로 조용히 유실됨 — 판정 커밋만 가고 근거 *.md가 누락될 수 있다.
위험 한 가지: Origin 헤더가 비는 정상 로컬 호출(일부 클라이언트)까지 막힐 수 있으니 화이트리스트에 "빈 Origin + localhost"를 함께 허용해야 오너 화면이 안 죽는다.
