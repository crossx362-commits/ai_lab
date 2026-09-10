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


## 형식
화면과 dispatch가 이 형식을 그대로 읽는다. 항목은 한 줄에 하나(`- `로 시작).
- 명령: `[YYYY-MM-DD HH:MM] (프로젝트 · git경로) 본문` — 맨 위가 현재 명령
- 의견: 파일 `loop/opinions/<AI>.md`(1줄 제목 + 본문). 판정은 「의견·초안」에 `[시각] 채택|반려: AI — 제목`
- 결정대기: 오너 예/아니오만. 답은 줄 끝 `→ 예 [시각]` / `→ 아니오 [시각]`
- 실행: `[ ]`/`[x]` 체크박스 + `[시각] 담당: 제목`


## 명령
- [2026-09-10 13:51] (loop · loop) 지휘 보드(loop/command-board, loop/dispatch-board.sh, loop/BOARD.md)를 검토해 남은 결함과 개선점을 의견으로 내라

## 의견·초안
-


## 결정대기
- 자산 5개 다운로드 승인 (모루·대장간·마구간·목공소 단품 4 + Kenney 동굴 키트) — 전부 CC0, 승인 시 막힌 화면 5개 풀림
- autodev·herdr 작업 폴더 16개 삭제 — 프로세스는 이미 껐고 쓸 것 하나만 루프에 넘김, 나머지 중복
- Quaternius MegaKit(절구통용) — itch.io에서 직접 받아 넣기. 아니오면 절구는 다른 통으로 대체


## 결정
- [2026-09-10 13:44] 예: 울론 검수 브랜치(qa-claude, 177커밋) master 병합 — 오너 지시 「울온 병합」, 커밋 91b5df43


## 실행
- [x] [2026-09-10 13:44] Claude: qa-claude → master 병합 (DEV_INBOX 충돌 양쪽 보존)
- [x] [2026-09-10 13:40] Claude: grok CLI 설치 (@xai-official/grok 1.0.25, `grok -p`/`--prompt-file`)
- [x] [2026-09-10 13:56] 오너: `grok` 로그인 완료 — `grok --prompt-file` 헤드리스 호출 성공 확인


## 질문
-
