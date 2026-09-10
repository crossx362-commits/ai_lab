# 지휘 보드

오너는 **명령·채택·반려(예/아니오)** 만 한다. 오너에게 일을 시키지 않는다.

## 구조 (원본은 파일 하나)

```
loop/BOARD.md            ← 유일한 공유 원본 (git 추적). 전 AI가 읽는다
loop/dispatch-board.sh   ← 구독 CLI(claude·codex·gemini·grok)를 불러 의견을 받는다
loop/opinions/<AI>.md    ← 실제 CLI 출력 (git 제외, 새 명령 때 archive/로 이동)
loop/command-board/      ← 이 화면 (Vite + React). BOARD.md를 읽고 쓴다
```

흐름: 명령 입력 → `BOARD.md` 「명령」에 기록 + 커밋·푸시 → dispatch가 CLI 4종 병렬 호출 →
의견 카드(실제 응답만, 실패는 사유 표시) → 채택/반려 → 「의견·초안」「결정」「실행」에 기록 + 커밋·푸시.

## 실행

```bash
cd loop/command-board && npm install && npm run dev     # http://127.0.0.1:5177
```

앱 미리보기 이름: `command-board`. 상시 의견 수집 루프가 필요하면 `npm run dispatch`
(BOARD.md 「명령」이 바뀔 때만 한 바퀴, `touch loop/STOP`으로 멈춤).

## 맥에서 이어가기 (2026-09-10 인수인계)

```bash
git pull --rebase
cd loop/command-board && npm install && npm run dev      # http://127.0.0.1:5177
```

- 로봇 열쇠는 기계마다 따로다: 맥에서 `claude auth login`, `grok login`(npm i -g @xai-official/grok) 한 번씩. 헤더 열쇠 줄에 줄이 그어진 CLI가 아직 로그인 안 된 것.
- 이 화면은 Windows·맥 공용(bash는 /opt/homebrew/bin/bash → /bin/bash 순으로 찾음). `.claude/launch.json`은 git 밖이라 맥에선 그냥 `npm run dev`.
- **지금 상태**: 도장 기다림 5건(울론 자산 다운로드 승인 3건 + Grok·Claude 생각 2건), 프로젝트 상태는 전부 「아직 안 정함」, 실 명령은 「지휘 보드 검토」 1건. 원격 master와 동기 상태.
- 서버 코드를 고치면 `test/README.md`대로 임시 저장소 하네스(52항목)를 돌린다.

## API (dev 서버 내장, `server/board-api.ts`)

| 경로 | 하는 일 |
|---|---|
| `GET /api/board` | BOARD.md + opinions → 카드, CLI 설치·로그인 상태, 수집 진행 여부 |
| `POST /api/command` | 「명령」 맨 위에 기록 → 커밋·푸시 → 이전 의견 보관 → 수집 시작 |
| `POST /api/decide` | 의견 채택/반려, 결정대기·질문 예/아니오 → 기록 → 커밋·푸시 |
| `POST /api/dispatch` | 현재 명령으로 의견 수집 한 바퀴 (서버와 분리된 프로세스) |
| `GET /api/status?path=` | 로컬 git 최신 커밋 (프로젝트 상태 줄) |
| `GET /api/projects?paths=` | 모든 프로젝트의 마지막 커밋·이번 주 커밋 수 (프로젝트 현황 패널) |
| `POST /api/project-state` | 오너가 프로젝트 상태(진행·보류·완료·접음)를 정함 → BOARD.md 「프로젝트」 절 → 커밋·푸시 |

## 처음이면

보드 자체가 다섯 살 눈높이다(2026-09-10). 생김새는 그림 설명서와 같은 놀이터판 — 따뜻한 종이 바탕, 하늘·민트·산호·레몬·라벤더 다섯 색, 둥근 글씨(Do Hyeon), 스티커 카드(굵은 테두리+아래 그림자), 눌리는 단추. 토큰은 `src/styles.css` `@theme` 한 곳.

- **한 화면**: 위 메뉴는 세 줄(제목+연결 상태 · 입력창 · 프로젝트 칩+📍지금). 넓은 화면(xl)에서는 받은편지함과 로봇 생각이 나란히, 아래 칸은 시킬 일·찍은 도장·로봇 할 일·막힌 것(도장 기다림 칸은 받은편지함이 대신). 페이지 스크롤 없음.
- **키보드**(GitHub PR 대시보드·Linear 분류함 조사 반영): 받은편지함에서 `j`/`k` 이동, `a` 두 번 좋아, `r` 아니야, `?` 도움말. 좋아가 저장되면 색종이 1.2초(움직임 줄이기 설정이면 없음). 막힌 것 칸의 열린 카드는 ⚠️+산호 테두리.
- **기능 점검**: `test/README.md` — 임시 저장소에서 API 52항목 실호출.
- **도장 기다림**(맨 위 레몬 상자, `stamp-inbox.tsx`): 결정대기·막힌 것·판정 전 로봇 생각을 한 줄씩 모아 바로 도장. 헤더 배지 숫자도 이 합계.
- **로봇 생각**: 넷이 가로 한 줄, 「접기」로 로봇별 이모지 요약만 남길 수 있다(localStorage `command-board:lane-open`).
- **ai_lab이 제일 큰 집**: 칩 줄 맨 앞 🏠 ai_lab(전체), 나머지 프로젝트는 전부 그 안의 방. 입력창 자리표시는 `ai_lab › 방`.
- 칸 이름은 시킬 일·로봇 생각·도장 기다림·찍은 도장·로봇 할 일·막힌 것 — 화면 라벨만이고 `loop/BOARD.md`의 절 이름은 그대로(로봇들이 읽는다). 말은 전부 `src/lib/words.ts`.
- 첫 방문엔 3컷 환영 안내가 한 번(localStorage `command-board:welcome-seen`), 그림 설명서 `public/eli5.html`은 헤더 「설명서」. 「우리 프로젝트들」은 모든 프로젝트의 git 진행과 오너 상태 버튼(🟢진행·🟡보류·🏁완료·📦접음).
