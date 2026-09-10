# API 전 기능 점검(임시 저장소)

실 `loop/BOARD.md`·원격을 건드리지 않고, 임시 저장소 + 가짜 원격 + 가짜 디스패치(CLI 한도 안 태움)로 API 전부를 실제 호출한다.

`setup.sh`는 실 BOARD.md를 복사한 뒤 **미답 결정대기 카드 한 줄을 픽스처로 심는다** — 그날 미답 카드가 0건이면 「결정대기 카드 있음」과 「커밋 수 증가(판정 하나가 빠진다)」가 코드가 아니라 **데이터 때문에** 실패하기 때문이다(하네스는 실데이터에 기대지 않는다).

```bash
bash test/setup.sh                                   # test/.tmp/tmprepo 생성(원격은 test/.tmp/remote.git)
BOARD_ROOT=$PWD/test/.tmp/tmprepo npx vite --port 5178 --strictPort --host 127.0.0.1 &
python test/test.py                                  # 52개 PASS/FAIL 표, 실패 있으면 exit 1
```

점검 항목: 읽기(/api/board·projects·status·dispatch/log), CSRF 가드(text/plain·다른 Origin·cross-site → 403), 명령(커밋·푸시·디스패치 시작, 수집 중 409, 빈 명령 400),
의견 4장(실패 사유 표시), 판정(채택 → 결정+실행 줄, 반려, 재판정 409, 없는 id 404, 명령 카드 400), 결정대기 예/아니오(줄 끝 주석·결정 절), 프로젝트 상태(덮어쓰기·400),
원격 동기화(로컬 HEAD == 원격 master·작업 트리 깨끗), 두 번째 명령 때 의견 보관(archive/).
