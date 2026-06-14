import os
import sys
import subprocess
import time

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
if hasattr(sys.stderr, 'reconfigure'):
    sys.stderr.reconfigure(encoding='utf-8', errors='replace')

_here = os.path.dirname(os.path.abspath(__file__))

# 병렬로 검수 및 수정할 비디오 ID 목록
video_ids = [
    "D5Rjbhb79DA",
    "NYBy6iRIY7w",
    "bufy0rEKtSY",
    "pdXiV5uI86o",
    "UUYgzrdevHw",
    "-2BsHU-Q8N8",
    "kFIKW60k-fg",
    "_pX1iTxo-Nw",
    "tyal-WuOXMY",
    "oEdn5Xm1GcI",
    "tkLUCnr8Glo",
    "QBYoBISz-l0"
]

inspector_path = os.path.join(_here, "projects", "ai-team", "skills", "가희_검수관", "tools", "content_inspector.py")

# 동시 실행 제한
MAX_WORKERS = 2
active_processes = []
pending_queue = list(video_ids)
finished_processes = []

print(f"=== {len(video_ids)}개 비디오 병렬 검수 및 자동 수정 기동 시작 (최대 동시 실행: {MAX_WORKERS}개) ===")

while pending_queue or active_processes:
    # 1. 종료된 프로세스가 있는지 검사 및 정리
    still_active = []
    for p_info in active_processes:
        p = p_info["proc"]
        status = p.poll()
        if status is not None:
            p_info["log_file"].close()
            finished_processes.append(p_info)
            print(f"✅ 비디오 [{p_info['id']}] 검수 및 수정 프로세스 완료! (Exit code: {status})")
        else:
            still_active.append(p_info)
    active_processes = still_active
    
    # 2. 실행 공간이 비어 있고 대기열이 남아 있다면 새로운 프로세스 시작
    while len(active_processes) < MAX_WORKERS and pending_queue:
        vid = pending_queue.pop(0)
        print(f"🚀 비디오 [{vid}] 검수 프로세스 시작... (대기열 남음: {len(pending_queue)}개)")
        cmd = [sys.executable, inspector_path, "--id", vid, "--new"]
        
        log_dir = os.path.join(_here, "reports", "parallel_logs")
        os.makedirs(log_dir, exist_ok=True)
        log_file_path = os.path.join(log_dir, f"{vid}_inspector.log")
        log_file = open(log_file_path, "w", encoding="utf-8")
        
        p = subprocess.Popen(
            cmd,
            stdout=log_file,
            stderr=log_file,
            text=True,
            cwd=_here
        )
        active_processes.append({"id": vid, "proc": p, "log_file": log_file, "path": log_file_path})
        time.sleep(2)  # 다음 프로세스 기동 간 딜레이
        
    print(f"⏳ 대기 중... 실행 중: {len(active_processes)}개 | 대기열: {len(pending_queue)}개")
    time.sleep(10)

print("\n=== 모든 병렬 검수 및 수정 완료! ===")
for p_info in finished_processes:
    try:
        with open(p_info["path"], "r", encoding="utf-8") as lf:
            lines = lf.readlines()
            summary_line = "No output"
            for line in reversed(lines):
                if any(m in line for m in ["검수 완료", "수정 완료", "검수 통과 완료", "실패", "성공"]):
                    summary_line = line.strip()
                    break
            print(f"🎯 비디오 [{p_info['id']}] 최종 로그 상태: {summary_line}")
    except Exception as e:
        print(f"🎯 비디오 [{p_info['id']}] 결과 확인 실패: {e}")
