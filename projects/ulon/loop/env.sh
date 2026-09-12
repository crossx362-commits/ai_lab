# 울온 자율 개발 루프 설정. 비밀값은 loop/env.local.sh (git 제외).

# 모델 (난이도별) — Codex CLI slug
MODEL_LOW=gpt-5.3-codex-spark   # 하: 가볍고 빠른 모델
MODEL_MID=gpt-5.6-sol           # 중: 기본 모델
MODEL_HIGH=gpt-6-astra          # 상: 가장 강한 모델

# 루프
MAX_TURNS=80                    # 한 바퀴 최대 턴 수 (PROMPT에 명시, 하드 제한은 타임아웃)
SLEEP_BETWEEN=45                # 바퀴 사이 대기(초)
MAX_LOOPS=0                     # 최대 바퀴 수 (0이면 무제한)
LOOP_TIMEOUT_MIN=90             # 바퀴 타임아웃(분)
MAX_CONSEC_FAIL=3               # 연속 실패 허용 횟수
LOG_KEEP_DAYS=14                # 로그 보관 일수

# 프로젝트
DESIGN_DOC="docs/GAME_DESIGN.md docs/DESIGN.md docs/source/울온_철학_저폴리3D_온라인_샌드박스_RPG_통합기획서_v1.1_UO보완_검토완료.docx"
BUILD_CMD="./tools/rebuild_client.sh"
RUN_SERVER_CMD="./server/start_postgres.sh && ./server/start_persist.sh && ./builds/client/UlonClient.app/Contents/MacOS/Ulon -batchmode -nographics -ulon-server -logFile ./builds/check/server.log"
RUN_CLIENT_CMD="./builds/client/UlonClient.app/Contents/MacOS/Ulon -ulon-client -ulon-host 127.0.0.1"
TEST_CMD='python3 tools/test_alpha_readiness.py && SELFCHECK_LOG=$PWD/unity/Logs/selfcheck_dev.log ./tools/slice_selfcheck.sh'

# 이미지 생성 (GPT)
IMAGE_PROVIDER=openai
IMAGE_MODEL=gpt-image-1

# 3D 모델링 (GPT + Blender)
MODEL_PROVIDER=openai
MODEL_LLM=gpt-5.6-sol
BLENDER_BIN=/Applications/Blender.app/Contents/MacOS/Blender

# 보드
BOARD_PORT=8787

# 도구 경로 (launchd는 터미널 PATH를 물려받지 않음)
UNITY_BIN=/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/MacOS/Unity
CODEX_BIN=/opt/homebrew/bin/codex
PYTHON_BIN=/opt/homebrew/bin/python3
