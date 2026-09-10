의견 식별자에 명령·본문 버전 연결
loop/command-board/server/board-api.ts는 의견 ID·판정을 작성자와 제목만으로 연결하므로, 명령 ID와 본문 해시까지 포함하도록 개선할 것.
loop/dispatch-board.sh에서 결과에 해당 명령 ID를 기록하고, loop/BOARD.md 판정 형식에도 같은 식별자를 보존할 것.
위험: 같은 제목으로 본문이 바뀌면 이전 채택이 새 의견에 붙거나, 오래된 화면의 클릭이 다른 본문을 승인할 수 있음.
