# 자율 개발 루프 — 운영 안내

> 오너 명세(2026-08-23)에 따라 만든 무한 개발 루프. 매 바퀴 헤드리스 세션을 **새로** 열고
> `loop/PROMPT.md` 다섯 절대로 일하게 한다. 대화는 이어 붙이지 않는다 — 기억은 전부 파일·Git에 있다.

## 파일 목록

| 파일 | 역할 |
|---|---|
| `loop.sh` | 루프 본체. 무한 반복 · 새 세션 · 날짜별 로그 · STOP 감시 · N바퀴마다 회의 소집 |
| `env.sh` | 설정 분리 (실행기·모델 / 바퀴 최대 턴 수 / 바퀴 사이 대기 / 최대 바퀴 수 / 회의 주기) |
| `PROMPT.md` | 세션 지시서 5절 (①합격기준 ②읽을문서 ③규칙·근거 ④바퀴순서 ⑤커밋규칙+자가학습) |
| `council.sh` | 자가학습 정기 회의 — planner·builder·tester **병렬** 새 세션 + 의장 합본 → `docs/meetings/COUNCIL_*.md` |
| `board_keeper.sh` + `deploy_boardkeeper.sh` | **보드 지킴이 에이전트** — 30분마다 보드 응답·state API·테스트 스위트 검증 → 이상 시 opencode 세션으로 외과 수리·커밋, 건강해도 주기적으로 1씽 개선. 결과는 `loop/board_keeper.json`과 보드 「운영」 줄의 지킴이 칩에 표시 |
| `agent_runner.py` | 병렬 코디네이터 — worktree 격리 worker/reviewer (opencode·claude·codex·grok) |
| `board.py` / `board.html` | 개발 보드 (http://127.0.0.1:8766) · MCP·러너·회의·개선안 운영 줄 표시 |
| `deploy_launchd.sh` | 레포 loop/ → Application Support 배포 + launchd 재등록 |
| `com.ailab.autonomous_loop.plist` | launchd 등록본 (RunAtLoad · 비정상 종료만 재시작 · PATH 명시) |
| `STOP` | (필요 시 만든다) 존재하면 현재 바퀴만 마치고 정상 종료 |
| `agent` | 실행기 지정 파일 (비워두면 env.sh의 LOOP_AGENT) — 현재 `opencode` |
| `logs/../../logs/YYYY-MM-DD/lap-*.log` | 바퀴별 로그 (`ai_lab/logs/날짜/`) |

기억 3종: `docs/DESIGN.md`(무엇을 만드는가) · `docs/STATUS.md`(어디까지 했나) · `docs/feedback/INBOX.md`(오너 지시, 최우선)
자가학습: 매 바퀴가 개선안을 `docs/feedback/PROPOSALS.md`에 쌓고, 기본 4바퀴(`LOOP_COUNCIL_EVERY`)마다
역할 병렬 회의가 심의해 채택(✅)·보류(⏸)를 판정한다. 판정문은 `docs/meetings/`에 적립된다.

## 켜는 법

```bash
# 배포 + launchd 등록 + 즉시 시작 (로그인 시 자동 시작)
bash loop/deploy_launchd.sh

# 수동으로 앞바퀴만 확인할 때
bash loop/loop.sh "$(pwd)"
```

## 끄는 법

```bash
# 정상 종료 — 현재 바퀴만 마치고 멈춘다 (권장)
touch loop/STOP

# 등록까지 완전히 내린다 (다음 로그인 자동 시작 방지)
launchctl bootout gui/$(id -u)/com.ailab.autonomous_loop
rm loop/STOP   # 재시작할 때 STOP은 반드시 지운다
```

## 상태 보는 법

```bash
launchctl list | grep com.ailab.autonomous_loop   # 첫 칸 PID(=실행 중) / '-'(=멈춤), 둘째 칸 마지막 exit code
ps aux | grep -E "loop\.sh|grok --model" | grep -v grep
ls -t logs/$(date +%Y-%m-%d)/ | head              # 오늘 바퀴 로그
tail -f logs/$(date +%Y-%m-%d)/$(ls -t logs/$(date +%Y-%m-%d)/ | head -1)
```

## 인수 기록

- **2026-08-23**: 오너 지시로 외부 그록 루프를 정상 종료(STOP)하고 대화 세션이 루프를 직접 수행하는
  모드로 전환. launchd 등록은 내려두되 본 문서의 「켜는 법」으로 언제든 복구 가능.
- PROMPT.md ③ 변경(오너 2026-08-23): 그래픽 리소스는 외부 생성기 없이 **직접 생성이 기본**.
