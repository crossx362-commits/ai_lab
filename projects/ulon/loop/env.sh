# 울온 자율 개발 루프 설정. 비밀값은 loop/env.local.sh (git 제외).

# 모델 (난이도별) — Grok만. GPT/Codex 쓰지 않는다.
MODEL_LOW=grok-4.5              # 하
MODEL_MID=grok-4.6              # 중
MODEL_HIGH=grok-4.6             # 상
EFFORT_LOW=low
EFFORT_MID=medium
EFFORT_HIGH=high

# 루프 (환경변수가 있으면 그걸 쓴다 — 수동 두 바퀴: MAX_LOOPS=2 ./loop/loop.sh)
MAX_TURNS="${MAX_TURNS:-80}"                    # 한 바퀴 최대 턴 수 (PROMPT에 명시, 하드 제한은 타임아웃)
SLEEP_BETWEEN="${SLEEP_BETWEEN:-45}"            # 바퀴 사이 대기(초)
MAX_LOOPS="${MAX_LOOPS:-0}"                     # 최대 바퀴 수 (0이면 무제한)
LOOP_TIMEOUT_MIN="${LOOP_TIMEOUT_MIN:-90}"      # 바퀴 타임아웃(분)
MAX_CONSEC_FAIL="${MAX_CONSEC_FAIL:-3}"         # 연속 실패 허용 횟수
LOG_KEEP_DAYS="${LOG_KEEP_DAYS:-14}"            # 로그 보관 일수

# 프로젝트
DESIGN_DOC="docs/GAME_DESIGN.md docs/DESIGN.md docs/source/울온_철학_저폴리3D_온라인_샌드박스_RPG_통합기획서_v1.1_UO보완_검토완료.docx"
BUILD_CMD="./tools/rebuild_client.sh"
RUN_SERVER_CMD="./server/start_postgres.sh && ./server/start_persist.sh && ./builds/client/UlonClient.app/Contents/MacOS/Ulon -batchmode -nographics -ulon-server -logFile ./builds/check/server.log"
RUN_CLIENT_CMD="./builds/client/UlonClient.app/Contents/MacOS/Ulon -ulon-client -ulon-host 127.0.0.1"
TEST_CMD='python3 tools/test_alpha_readiness.py && SELFCHECK_LOG=$PWD/unity/Logs/selfcheck_dev.log ./tools/slice_selfcheck.sh'

# 이미지 생성 (Grok image_gen / image_edit)
IMAGE_PROVIDER=xai
IMAGE_MODEL=grok-imagine

# 3D 모델링 (Grok bpy + Blender)
MODEL_PROVIDER=xai
MODEL_LLM=grok-4.6
BLENDER_BIN=/Applications/Blender.app/Contents/MacOS/Blender

# 보드
BOARD_PORT=8787

# 도구 경로 (launchd는 터미널 PATH를 물려받지 않음)
UNITY_BIN=/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity
GROK_BIN=/Users/junholee/.grok/bin/grok
PYTHON_BIN=/opt/homebrew/bin/python3
