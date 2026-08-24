# 자율 개발 루프 — 운영 안내

> 오너 명세: 매 바퀴 헤드리스 세션을 **새로** 열고 `loop/PROMPT.md` 다섯 절대로 일하게 한다.
> 대화는 이어 붙이지 않는다 — 기억은 전부 파일·Git에 있다.

## 파일 목록

| 파일 | 역할 |
|---|---|
| `loop.sh` | 루프 본체. 무한 반복 · 새 세션 · 날짜별 로그 · STOP 감시 · N바퀴마다 회의 소집 |
| `env.sh` | 설정 분리 (실행기·모델 / 한 바퀴 최대 턴 수 / 바퀴 사이 대기 / 최대 바퀴 수 / 회의 주기) |
| `PROMPT.md` | 세션 지시서 5절 (①합격기준 ②읽을문서 ③규칙·근거 ④바퀴순서 ⑤커밋규칙+자가학습) |
| `council.sh` | 자가학습 정기 회의 — planner·builder·tester **병렬** 새 세션 + 의장 합본 → `docs/meetings/COUNCIL_*.md` |
| `board_keeper.sh` + `deploy_boardkeeper.sh` | 보드 지킴이 |
| `speed_lane.sh` + `TASKS.json` · `SPEED_PROMPT.md` | 속도 레인 (문서·보드·아트 스크립트·테스트. 게임플레이 C#·STATUS.md는 메인 루프 전용) |
| `agent_runner.py` | 병렬 코디네이터 |
| `board.py` / `board.html` | 개발 보드 (http://127.0.0.1:8766) |
| `deploy_launchd.sh` | 레포 loop/ → Application Support 배포 + launchd 등록. `--no-start` 면 지금은 안 켠다 |
| `com.ailab.autonomous_loop.plist` | launchd 등록본 (RunAtLoad · 비정상 종료만 재시작 · PATH 명시) |
| `STOP` | 있으면 현재 바퀴만 마치고 정상 종료 |
| `agent` | 실행기 지정 (비워두면 env.sh의 LOOP_AGENT). 현재 기본 `grok` |
| `logs/YYYY-MM-DD/lap-*.log` | 바퀴별 로그 (`ai_lab/logs/날짜/`) |

기억 3종: `docs/DESIGN.md`(무엇을 만드는가) · `docs/STATUS.md`(어디까지 했나) · `docs/feedback/INBOX.md`(오너 지시, 최우선)
자가학습: 매 바퀴가 개선안을 `docs/feedback/PROPOSALS.md`에 쌓고, 기본 4바퀴(`LOOP_COUNCIL_EVERY`)마다
역할 병렬 회의가 심의해 채택(✅)·보류(⏸)를 판정한다.

## 켜는 법

```bash
# 배포 + launchd 등록 + 즉시 시작 (로그인 시 자동 시작, 비정상 종료만 재시작)
rm -f loop/STOP
bash loop/deploy_launchd.sh

# 등록만 하고 지금은 안 켬 (plist 는 LaunchAgents 에 남김)
bash loop/deploy_launchd.sh --no-start

# 수동으로 N바퀴만 (launchd 없이)
LOOP_MAX_LOOPS=2 LOOP_COOLDOWN=5 bash loop/loop.sh "$(pwd)"
```

## 끄는 법

```bash
# 정상 종료 — 현재 바퀴만 마치고 멈춘다 (권장)
touch loop/STOP

# 등록까지 완전히 내린다 (다음 로그인 자동 시작 방지)
launchctl bootout gui/$(id -u)/com.ailab.autonomous_loop
rm loop/STOP   # 재시작할 때 STOP은 반드시 지운다

# 속도 레인만 끄기 / 켜기
touch loop/STOP_LANE
rm -f loop/STOP_LANE && bash loop/deploy_launchd.sh
```

## 상태 보는 법

```bash
launchctl list | grep com.ailab.autonomous_loop   # 첫 칸 PID(=실행 중) / '-'(=멈춤), 둘째 칸 마지막 exit code
ps aux | grep -E "loop\.sh|grok --model" | grep -v grep
ls -t logs/$(date +%Y-%m-%d)/ | head              # 오늘 바퀴 로그
tail -f logs/$(date +%Y-%m-%d)/$(ls -t logs/$(date +%Y-%m-%d)/ | head -1)
```

설정 원천은 `loop/env.sh` — `LOOP_AGENT`(grok) · `LOOP_GROK_MODEL` · `LOOP_MAX_TURNS` · `LOOP_COOLDOWN` · `LOOP_MAX_LOOPS`(0=무한).

## 인수 기록

- **2026-08-24**: 오너 원 명세를 받아 개선 반영. 그림=힉스필드/그록 이매진(반입 `aigen.py`), 실행기 grok, ④/⑤ 커밋 모순은 「화면 보기 전 커밋」으로 고정, `deploy_launchd.sh --no-start` 추가.
- **2026-08-23**: 그래픽 직접 생성이 기본이었던 한시 규칙 → 위 원 명세로 되돌림.
