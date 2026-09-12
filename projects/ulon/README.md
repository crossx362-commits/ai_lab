# Ulon (작업명)

Classic UO 철학을 참고한 **저폴리 3D 온라인 샌드박스 RPG**.
정식 게임 제목·세계관·고유 명칭은 독자 IP로 따로 확정한다. 폴더명 `ulon`은 작업용이다.

기획 원장: `docs/GAME_DESIGN.md` (v1.1, 2026-08-31)
짧은 확정안: `docs/DESIGN.md`

한 줄 정의: 직업을 고르지 않고 행동으로 스킬을 키우며, 사냥·채집·제작·거래가 서로 연결되는 소규모 지속형 월드.

## 어디에 무엇이 있나

| 경로 | 역할 | 커밋 |
|---|---|---|
| `unity/` | Unity 6 게임 원본. 에디터가 여는 폴더 | ✅ |
| `unity/Assets/Game/` | 게임용 아트·데이터·스크립트·씬 | ✅ |
| `unity/Assets/_ThirdParty/` | 무료 에셋 원본·라이선스. 직접 수정 금지 | ✅ |
| `docs/` | 기획·부트스트랩 문서 | ✅ |
| `docs/source/` | 원본 docx | ✅ |
| `art/` | 다운로드 zip 보관. Unity가 읽지 않음 | ✅ (zip은 용량 크면 gitignore) |

Unity 에디터는 **`projects/ulon/unity`** 만 연다. 재와별(`projects/ashes-to-stars/unity`)과 섞지 않는다.

## 지금 확정된 축

저폴리 3D + 고정 3/4 쿼터뷰 + 공용 Humanoid Rig + 무료 모듈형 에셋 + 서버 권한형 스킬 성장 + 플레이어 제작 경제.

첫 마일스톤: 두 플레이어가 서버에 접속해 몬스터를 때리고 검술이 오르고, 광석을 캐 검을 만들어 거래하고, 재접속 후에도 데이터가 남는다.

## 두지 마라

- `_ThirdParty` 원본을 `Game/`으로 복사해 직접 수정
- 아이템/스킬/몬스터 수치를 코드에 하드코딩
- 클라이언트에서 스킬·골드·드랍·거래 결과 결정
- UO의 고유 이름·세계관·재료명 복제
- 하우징·Open PvP·조련을 MVP에 넣기

## 자율 개발 루프

코덱스가 총괄하는 헤드리스 개발 루프. 기획서는 옮기지 않고 `loop/env.sh`의 `DESIGN_DOC`로 읽는다. 로그인 자동 실행은 **아직 켜지 않았다** (plist는 `Disabled`).

### 만든 파일

| 경로 | 역할 |
|---|---|
| `loop/loop.sh` | 루프 본체. 한 바퀴마다 새 `codex exec` |
| `loop/env.sh` | 모델·타임아웃·빌드/실행 명령 |
| `loop/env.local.sh` | 비밀값 (git 제외) |
| `loop/PROMPT.md` | 한 바퀴 지시서 |
| `loop/board_server.py` | 현황 보드 `http://127.0.0.1:8787` |
| `loop/board.html` | 보드 화면 |
| `loop/launchd/*.plist` | launchd 등록용 (복사본) |
| `docs/feedback/INBOX.md` | 사람 지시함 |
| `docs/board.json` | 칸반 데이터 |
| `docs/STATUS.md` | 현황 (0번째 바퀴에서 작성) |
| `docs/ASSETS.md` | 에셋 목록 (0번째 바퀴에서 작성) |
| `logs/` | 날짜별 로그, `loop_state.json` |

### 켜는 법

보드만 (루프와 별개):

```bash
cd /Users/junholee/ai_lab/projects/ulon
python3 loop/board_server.py
# http://127.0.0.1:8787
```

루프 수동 (두 바퀴 시험):

```bash
cd /Users/junholee/ai_lab/projects/ulon
MAX_LOOPS=2 SLEEP_BETWEEN=5 ./loop/loop.sh
```

로그인 자동 실행 (확인 끝난 뒤):

```bash
cp loop/launchd/com.ulon.autodev.loop.plist ~/Library/LaunchAgents/
cp loop/launchd/com.ulon.autodev.board.plist ~/Library/LaunchAgents/
# plist의 Disabled를 false로 바꾼 다음
launchctl bootstrap gui/$(id -u) ~/Library/LaunchAgents/com.ulon.autodev.board.plist
launchctl bootstrap gui/$(id -u) ~/Library/LaunchAgents/com.ulon.autodev.loop.plist
```

### 끄는 법

- 보드에서 **정지**: `loop/STOP` 생성. 현재 바퀴를 마친 뒤 멈춤.
- 정지 해제: 보드의 정지 해제, 또는 `rm loop/STOP`.
- 즉시 종료가 필요하면 해당 프로세스만 종료. `launchctl bootout gui/$(id -u)/com.ulon.autodev.loop`

### 상태 보는 법

- 보드: http://127.0.0.1:8787
- `logs/loop_state.json` — 바퀴 번호, pid, 연속 실패
- `docs/STATUS.md`, `docs/board.json`, `docs/feedback/INBOX.md`

### 문제 생겼을 때 볼 로그

- 오늘 로그: `logs/YYYY-MM-DD.log`
- 바퀴 로그: `logs/loop_0001.log`
- 연속 실패 정지: `logs/stopped_fail.txt`
- launchd: `logs/launchd_loop.stdout.log`, `logs/launchd_loop.stderr.log`, `logs/launchd_board.*.log`
