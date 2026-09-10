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

## 디자인 원칙 (2026-09-10 조사 반영)

근거: 웹 조사(대시보드·칸반·승인 UI·WCAG 다크 대비) + 로컬 `references/awesome-design-md`의 Vercel·Linear·Raycast·Stripe 합성.
- 중립 팔레트는 조용하게, 의미색(채택=초록·반려=빨강)만 강하게. 라이트 본문 4.5:1, 다크 7:1 이상. 색만으로 상태를 전하지 않는다(항상 글자 배지).
- 실행을 일으키는 채택·예는 **두 번 눌러 확정**(3초), 반려·아니오는 한 번. 근거 본문이 버튼보다 먼저 온다.
- 3px 포커스 링, 200ms 페이드만(스핀·바운스 없음), 응답 대기는 스켈레톤.
- 「갱신 n초 전」 상시 표시, 오류는 상단 배너+재시도(조용한 실패 금지).
- 밀도: 라벨 12px/600, 본문 13px, 제목 14px/600, 컨트롤 32–36px, 카드 패딩 10–12px, 반경 4/6/8/12, 그림자 없이 표면 단계. 버튼·칩 pill 금지.
- 좁은 폭: 열은 가로 스크롤(열 최소 248px), 의견은 2×2. 다크는 시스템 설정 또는 `html[data-theme="dark"]`.

## 기록

- 2026-09-10 Grok이 화면 초안(src만) 작성 → Claude가 빌드 골격·BOARD.md 연동·실 CLI 의견·디스패치 수리.
