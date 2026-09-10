새 명령이 채택 근거 원문을 git에서 지움
loop/command-board/server/board-api.ts의 archiveOpinions가 의견 md를 .gitignore된 loop/opinions/archive/로 옮긴 뒤 commitBoard가 그 삭제를 커밋하고, 채택 분기는 제목만 loop/BOARD.md 실행 줄에 넣는다.
채택 때 card.body(경로·방법·위험)를 실행 줄에 붙인 뒤에만 보관하거나, archive/를 git 추적하라.
위험: 다음 명령이 떨어지는 순간 직전 채택의 수정 경로가 HEAD에서 사라져 실행 담당이 다른 파일을 고친다.
