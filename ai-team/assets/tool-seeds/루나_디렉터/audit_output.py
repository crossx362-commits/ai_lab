#!/usr/bin/env python3
"""
audit_output.py — 루나 아웃풋 폴더 전문 정리 도구
1. 60초 미만 즉시 삭제
2. 3시간 경과 임시 파일(조각) 삭제
3. 24시간 경과 정상 파일 아카이브
"""
import os, subprocess, time, shutil

# ─── 경로 설정 (프로젝트 루트 탐색) ──────────────────────────────────────────
_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, ".agent")):
        break
    _root = os.path.dirname(_root)

from _shared.env_loader import load_env
load_env()

# 파이프라인이 사용하는 실제 출력 폴더 (루트/output)
OUTPUT_DIR = os.path.join(_root, "output")
ARCHIVE_DIR = os.path.join(OUTPUT_DIR, "archives")

# ffprobe 경로 동기화 (pipeline.py와 동일 로직)
FFPROBE = next(
    (p for p in [
        r"C:\Users\cross\AppData\Local\Microsoft\WinGet\Packages"
        r"\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe"
        r"\ffmpeg-8.1.1-full_build\bin\ffprobe.exe"
    ] if os.path.exists(p)),
    "ffprobe",
)

def get_duration(file_path):
    """ffprobe를 사용하여 미디어 파일의 길이를 반환"""
    try:
        cmd = [
            FFPROBE, "-v", "error", "-show_entries", "format=duration",
            "-of", "default=noprint_wrappers=1:nokey=1", file_path
        ]
        output = subprocess.check_output(cmd, stderr=subprocess.STDOUT).decode().strip()
        return float(output)
    except Exception:
        return None

def run_organization() -> str:
    report = []
    report.append(f"🧹 [루나] 아웃풋 폴더 정리 시작 (Target: {OUTPUT_DIR})")

    if not os.path.exists(OUTPUT_DIR):
        return "❌ output 폴더를 찾을 수 없습니다."
    
    if not os.path.exists(ARCHIVE_DIR):
        os.makedirs(ARCHIVE_DIR, exist_ok=True)

    found_short_files = []
    deleted_files = []
    archived_count = 0
    now_ts = time.time()

    # 임시 파일 패턴 (작업 완료 후 삭제 대상)
    TEMP_PATTERNS = ["visual_", "video_", "full_track", "video_list.txt", "bgm_merged_"]
    
    for filename in os.listdir(OUTPUT_DIR):
        file_path = os.path.join(OUTPUT_DIR, filename)
        if os.path.isdir(file_path): continue
        
        file_age_hrs = (now_ts - os.path.getmtime(file_path)) / 3600

        # 1. 임시 파이프라인 파일 정리 (3시간 경과 시)
        if any(p in filename for p in TEMP_PATTERNS):
            if file_age_hrs > 3:
                os.remove(file_path)
                continue

        # 2. 미디어 파일 길이 감사
        if filename.lower().endswith((".mp4", ".mp3", ".wav")):
            duration = get_duration(file_path)
            
            if duration is not None and duration < 120:
                # [규칙] 60초 미만은 즉시 삭제 (쓰레기 파일)
                if duration < 60:
                    try:
                        os.remove(file_path)
                        deleted_files.append(filename)
                        continue
                    except Exception as e:
                        report.append(f"  ❌ 삭제 실패 ({filename}): {e}")
                # [규칙] 60~120초 사이는 리스트업 (규정 미달 알림)
                found_short_files.append((filename, duration))
            
            # 3. 오래된 파일 아카이브 (24시간 경과 시)
            elif file_age_hrs > 24:
                try:
                    shutil.move(file_path, os.path.join(ARCHIVE_DIR, filename))
                    archived_count += 1
                except Exception as e:
                    report.append(f"  ⚠️ 아카이브 실패 ({filename}): {e}")

    if deleted_files:
        report.append(f"🗑️ [정리] 규정 위반(60초 미만) 파일 {len(deleted_files)}개를 즉시 삭제했습니다.")
        for d in deleted_files[:3]: report.append(f"  - {d}")

    if archived_count > 0:
        report.append(f"📦 [정리] 24시간 경과한 정상 파일 {archived_count}개를 archives/ 폴더로 이동했습니다.")

    if found_short_files:
        report.append(f"\n⚠️ [주의] 사장님 지시(2분) 위반 파일 {len(found_short_files)}개 발견:")
        for name, dur in found_short_files:
            report.append(f"  - {name} ({dur:.1f}초)")
    else:
        report.append("\n✨ 모든 미디어 파일이 규칙을 준수하며 정리가 완료되었습니다.")

    return "\n".join(report)

if __name__ == "__main__":
    result = run_organization()
    print(result)