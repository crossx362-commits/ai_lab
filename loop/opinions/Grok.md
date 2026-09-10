BOARD.md 무잠금 덮어쓰기로 실행줄 유실
loop/command-board/server/board-api.ts의 /api/command·/api/decide·/api/project-state는 loop/BOARD.md를 통째로 읽어 잠금 없이 writeFileSync하고, 실행 완료 API가 없어 담당도 같은 파일의 [ ]를 직접 [x]로 고친다. flock으로 읽기-쓰기-커밋을 직렬화하고 POST /api/run-done만 체크를 뒤집게 할 것.
위험: 채택 커밋과 완료 체크가 겹치면 한쪽 줄이 HEAD에서 사라져 같은 일을 다시 연다.
