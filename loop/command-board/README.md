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

## 원칙

- 화면은 의견을 **지어내지 않는다**. `loop/opinions/`에 실제 파일이 있을 때만 카드가 뜬다.
- 실패는 숨기지 않는다 — 미로그인·한도 초과·만료는 카드 본문에 그대로 뜬다.
- 프롬프트는 argv가 아니라 stdin/`--prompt-file`로 (Windows 셔임이 argv 개행 뒤를 자르는 사고 재발 방지).
- 한도·인증은 사람이 푼다: `claude auth login`, `grok login`, Codex는 한도 해제 시각까지 대기.

## 기록

- 2026-09-10 Grok이 화면 초안(src만) 작성 → Claude가 빌드 골격·BOARD.md 연동·실 CLI 의견·디스패치 수리.
