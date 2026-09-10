# 지휘 보드

공유 원본. GPT · 제미니 · Claude · Grok · Grok Build 전부 이 파일을 읽고 일한다.
화면: `loop/command-board/` (`npm run dev` → http://127.0.0.1:5177). 화면이 이 파일에 쓰고 같은 호흡으로 커밋·푸시한다.
의견 수집: `loop/dispatch-board.sh` — 구독 CLI(claude·codex·gemini·grok)를 불러 `loop/opinions/<AI>.md`에 남긴다.

## 금지
**오너에게 일을 시키지 말 것.** 받는 것은 명령·채택·반려 클릭뿐이다.
확인·설치·실행·붙여넣기·다운로드를 오너에게 넘기지 말 것.
막히면 조사한다. 질문 칸은 예/아니오 결정만.
예외는 셋뿐(오너 지시 2026-09-08): ①파일 다운로드·외부 게시 ②되돌릴 수 없는 삭제 ③돈 드는 것 — 그리고 자격증명 입력(로그인)은 AI가 대신 못 한다.

## 방송
오너가 명령을 내리면 전원 필독.

## 연결 규칙
1. 먼저 이 파일과 README·STATUS를 읽는다
2. 의견은 누구 `의견` 칸
3. 채택된 것만 실행. 실행은 Grok Build 또는 Claude
4. 메인은 자율 루프. 막히면 조사
5. 시크릿·배포·삭제·master force·harness 수정은 멈춤
6. Windows 펫나 데몬 금지
7. 「프로젝트」 절의 상태가 보류·완료·접음이면 그 프로젝트에 새 일을 시작하지 않는다(진행 중인 것은 마무리만). 상태는 오너만 바꾼다

## 형식
화면과 dispatch가 이 형식을 그대로 읽는다. 항목은 한 줄에 하나(`- `로 시작).
- 명령: `[YYYY-MM-DD HH:MM] (프로젝트 · git경로) 본문` — 맨 위가 현재 명령
- 의견: 파일 `loop/opinions/<AI>.md`(1줄 제목 + 본문). 판정은 「의견·초안」에 `[시각] 채택|반려: AI — 제목`
- 결정대기: 오너 예/아니오만. 답은 줄 끝 `→ 예 [시각]` / `→ 아니오 [시각]`
- 실행: `[ ]`/`[x]` 체크박스 + `[시각] 담당: 제목`
- 프로젝트: `<id>: 진행|보류|완료|접음 [시각] — 메모` (id는 보드 트리의 폴더 id: petnna·ai-team·ashes·ulon·homepage·bboggl·picker·chinaguard·geoguard·docs·loop·lab)

## 프로젝트
- ai-team: 접음 [2026-09-10 21:16]
- ashes: 접음 [2026-09-10 21:16]
- bboggl: 완료 [2026-09-10 21:16]
- chinaguard: 완료 [2026-09-10 21:16]
- homepage: 진행 [2026-09-10 21:16]
- petnna: 진행 [2026-09-10 21:16]
- picker: 완료 [2026-09-10 21:16]

## 명령
- [2026-09-10 21:06] (ulon · projects/ulon) 울온 자율개발루프 시작해
- [2026-09-10 13:51] (loop · loop) 지휘 보드(loop/command-board, loop/dispatch-board.sh, loop/BOARD.md)를 검토해 남은 결함과 개선점을 의견으로 내라

## 의견·초안
- [2026-09-10 20:58] 반려: Claude — 의견 커밋 이중 writer로 git 경합·유실 위험
- [2026-09-10 20:58] 반려: Grok — 새 명령이 채택 근거 원문을 git에서 지움
- [2026-09-10 21:00] 채택: GPT — 의견 식별자에 명령·본문 버전 연결
- [2026-09-10 21:00] 채택: Claude — 지휘 보드 검토 — 수집 중복·중단 불가
- [2026-09-10 21:05] 채택: Grok — BOARD.md 무잠금 덮어쓰기로 실행줄 유실
- [2026-09-10 21:07] 채택: Claude — 울온 루프 러너 부재 — loop.sh는 재와별 전용
- [2026-09-10 21:07] 채택: GPT — 울온 루프의 프로젝트 연결부터 분리
- [2026-09-10 21:10] 채택: Grok — 두 번째 루프 금지 — 이미 qa_shots 중

## 결정대기
- 자산 5개 다운로드 승인 (모루·대장간·마구간·목공소 단품 4 + Kenney 동굴 키트) — 전부 CC0, 승인 시 막힌 화면 5개 풀림 → 예 [2026-09-10 20:58]
- autodev·herdr 작업 폴더 16개 삭제 — 프로세스는 이미 껐고 쓸 것 하나만 루프에 넘김, 나머지 중복 → 예 [2026-09-10 20:58]
- Quaternius MegaKit(절구통용) — itch.io에서 직접 받아 넣기. 아니오면 절구는 다른 통으로 대체 → 예 [2026-09-10 20:58]

## 결정
- [2026-09-10 13:44] 예: 울론 검수 브랜치(qa-claude, 177커밋) master 병합 — 오너 지시 「울온 병합」, 커밋 91b5df43
- [2026-09-10 20:58] 예: Quaternius MegaKit(절구통용) — itch.io에서 직접 받아 넣기. 아니오면 절구는 다른 통으로 대체
- [2026-09-10 20:58] 예: 자산 5개 다운로드 승인 (모루·대장간·마구간·목공소 단품 4 + Kenney 동굴 키트) — 전부 CC0, 승인 시 막힌 화면 5개 풀림
- [2026-09-10 20:58] 예: autodev·herdr 작업 폴더 16개 삭제 — 프로세스는 이미 껐고 쓸 것 하나만 루프에 넘김, 나머지 중복
- [2026-09-10 21:00] 채택: 의견 식별자에 명령·본문 버전 연결 (GPT)
- [2026-09-10 21:00] 채택: 지휘 보드 검토 — 수집 중복·중단 불가 (Claude)
- [2026-09-10 21:05] 채택: BOARD.md 무잠금 덮어쓰기로 실행줄 유실 (Grok)
- [2026-09-10 21:07] 채택: 울온 루프 러너 부재 — loop.sh는 재와별 전용 (Claude)
- [2026-09-10 21:07] 채택: 울온 루프의 프로젝트 연결부터 분리 (GPT)
- [2026-09-10 21:10] 채택: 두 번째 루프 금지 — 이미 qa_shots 중 (Grok)

## 실행
- [x] [2026-09-10 13:44] Claude: qa-claude → master 병합 (DEV_INBOX 충돌 양쪽 보존)
- [x] [2026-09-10 13:40] Claude: grok CLI 설치 (@xai-official/grok 1.0.25, `grok -p`/`--prompt-file`)
- [x] [2026-09-10 13:56] 오너: `grok` 로그인 완료 — `grok --prompt-file` 헤드리스 호출 성공 확인
- [x] [2026-09-10 14:00] 오너: `claude auth login` 완료(claude.ai 구독) — Claude 의견 칸 정상화
- [x] [2026-09-10 14:15] Claude: 보드 전체 개선(오너 지시) — 빌드 골격·BOARD.md 연동·실 CLI 의견·수집 프로세스 분리·질문 예/아니오·의견 원문 커밋·CSRF 가드·Grok 제목 오염 정규화 (Claude·Grok 의견 4건 반영)
- [ ] [2026-09-10 21:00] Grok Build: 의견 식별자에 명령·본문 버전 연결 — loop/command-board/server/board-api.ts는 의견 ID·판정을 작성자와 제목만으로 연결하므로, 명령 ID와 본문 해시까지 포함하도록 개선할 것. / loop/dispatch-board.sh에서 결과에 해당 명령 ID를 기록하고, loop/BOARD.md 판정 형식에도 같은 식별자를 보존할 것. / 위험: 같은 제목으로 본문이 바뀌면 이전 채택이 새 의견에 붙거나, 오래된 화면의 클릭이 다른 본문을 승인할 수 있음.
- [ ] [2026-09-10 21:00] Claude: 지휘 보드 검토 — 수집 중복·중단 불가 — `loop/dispatch-board.sh`의 `cycle()`은 STOP과 `_last-signature`만 보고 `_dispatch.status`를 안 본다 — 상시 루프가 떠 있는 상태에서 보드가 `/api/command`로 `DISPATCH_ONCE=1 DISPATCH_FORCE=1`을 또 띄우면 두 수집이 같은 `loop/opinions/<AI>.md`·`.raw`에 겹쳐 쓴다. `cycle()` 첫머리에 `_dispatch.status`가 running이면 건너뛰기(또는 `loop/opinions/.lock` flock)를 넣어 한 번에 하나만 돌게 할 것. / 멈춤 수단이 파일 `touch loop/STOP` 하나뿐이라, 수집이 고착되면 `command-board`의 `/api/command`가
- [ ] [2026-09-10 21:05] Grok Build: BOARD.md 무잠금 덮어쓰기로 실행줄 유실 — loop/command-board/server/board-api.ts의 /api/command·/api/decide·/api/project-state는 loop/BOARD.md를 통째로 읽어 잠금 없이 writeFileSync하고, 실행 완료 API가 없어 담당도 같은 파일의 [ ]를 직접 [x]로 고친다. flock으로 읽기-쓰기-커밋을 직렬화하고 POST /api/run-done만 체크를 뒤집게 할 것. / 위험: 채택 커밋과 완료 체크가 겹치면 한쪽 줄이 HEAD에서 사라져 같은 일을 다시 연다.
- [ ] [2026-09-10 21:07] Claude: 울온 루프 러너 부재 — loop.sh는 재와별 전용 — `loop/loop.sh`는 상태 파일이 `docs/STATUS.md`·`docs/feedback/INBOX.md`·`projects/ashes-to-stars/CLAUDE.md`로 하드코딩돼 있고 `projects/ulon/tools/`엔 러너가 없다 — 울온 루프는 지금까지 클로드 세션이 `projects/ulon/docs/SESSION_HANDOFF.md`의 「■ 지금 여는 랩」을 읽고 도는 방식이었다(현재 랩: NC 굽기 4→2회 병합). / 착수하려면 loop.sh의 세 상태 경로를 `LOOP_PROJECT`(기본 ashes)로 인자화해 울온은 `projects/ulon/docs/{SESSION_HANDOFF,DEV_INBOX,GAME_DESIGN}.md`를 읽게 하고, 매 이터레이션 끝에 `too
- [ ] [2026-09-10 21:07] Grok Build: 울온 루프의 프로젝트 연결부터 분리 — 채택 후 실행 담당은 `loop/loop.sh`의 재와 별 고정 프롬프트·상태 경로를 울온용으로 분리하고 `projects/ulon/docs/SESSION_HANDOFF.md`에 연결할 것. / `projects/ulon/unity`에서 한 항목씩 개발·검증하고, 반복 로그와 검증 산출물로 실제 진행을 판정할 것. / 위험: 현재 루프를 그대로 기동하면 `docs/STATUS.md`의 재와 별 작업을 수행할 수 있음.
- [ ] [2026-09-10 21:10] Grok Build: 두 번째 루프 금지 — 이미 qa_shots 중 — `loop/loop.sh`를 울온용으로 켜지 말고, 이미 점유된 워크트리 `../ai_lab-loop`(브랜치 loop-claude)에서 `docs/SESSION_HANDOFF.md` 「지금 여는 랩」(NC 굽기 4→2)을 이어서 닫아라. / 그 트리는 지금 `projects/ulon/tools/slice_selfcheck.sh` 두 판 뒤 `projects/ulon/tools/qa_shots.sh`가 Unity 배치로 돌아가는 중이다. / 위험: 재와별용 `loop/loop.sh`나 공유 트리 `projects/ulon/unity`에 배치를 하나 더 붙이면 진행 중 샷이 락 충돌로 죽는다.

## 질문
-
