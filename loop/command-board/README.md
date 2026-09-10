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

보드 자체가 다섯 살 눈높이다. 칸 이름은 시킬 일·로봇 생각·도장 기다림·찍은 도장·로봇 할 일·막힌 것(화면 라벨만 — `loop/BOARD.md`의 절 이름은 그대로, 로봇들이 읽는다). 라벨·그림·버튼 말은 전부 `src/lib/words.ts` 한 곳. 첫 방문엔 3컷 환영 안내가 한 번 뜨고(localStorage `command-board:welcome-seen`), 그림 설명서 `public/eli5.html`은 헤더 「설명서」로 언제든. 「우리 프로젝트들」은 모든 프로젝트의 git 진행과 오너 상태 버튼(🟢진행·🟡보류·🏁완료·📦접음).
