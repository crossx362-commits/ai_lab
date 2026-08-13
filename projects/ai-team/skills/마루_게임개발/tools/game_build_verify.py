#!/usr/bin/env python3
"""
마루(게임 개발) — 유니티 빌드·실행·검증 자동화

무엇을 하는가:
  1) 유니티를 배치모드로 띄워 스탠드얼론 빌드
  2) 빌드된 플레이어를 실제로 실행 (렌더링이 도는 상태에서 측정)
  3) 결과 CSV + 스크린샷을 수집하고 **정말로 그려졌는지까지** 검증
  4) 판정 결과를 리포트로 남기고, 실패 시에만 텔레그램

왜 만들었나 (2026-08-13 실측 사고):
  W1 성능 테스트가 "500체 700fps PASS"를 냈는데 화면은 텅 비어 있었다.
  텍스처 Read/Write 플래그가 꺼져 있어 아틀라스 생성이 조용히 실패했고,
  스프라이트를 하나도 안 그린 채 시뮬레이션 성능만 측정한 것이었다.
  → **"수치가 나왔다"와 "측정하려던 것을 측정했다"는 다르다.**
  그래서 이 도구는 FPS만 보지 않고 스크린샷의 픽셀을 직접 세어
  화면에 유닛이 실제로 존재하는지 확인한 뒤에야 PASS를 준다.

사용:
  python game_build_verify.py                 # 빌드 + 실행 + 검증
  python game_build_verify.py --skip-build    # 기존 빌드로 실행만
  python game_build_verify.py --send          # 결과를 텔레그램으로도
"""
import argparse
import csv
import glob
import io
import os
import platform
import subprocess
import sys
import time
from datetime import datetime

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    _root = os.path.dirname(_root)
    if os.path.isdir(os.path.join(_root, "projects")):
        break
sys.path.insert(0, os.path.join(_root, "projects", "ai-team"))

try:
    from _shared.env import load_env
    load_env(_root)
except Exception as e:                                    # 단독 실행도 가능하게
    print(f"[마루] env 로드 생략: {e}")

GAME = os.path.join(_root, "projects", "ashes-to-stars")
UNITY_PROJECT = os.path.join(GAME, "unity")
BUILD_DIR = os.path.join(GAME, "build_w1_perf")
LOG_DIR = os.path.join(GAME, "results")
REPORT_DIR = os.path.join(_root, "output", "qa", "ashes-to-stars")

UNITY_CANDIDATES = [
    r"C:\Program Files\Unity\Hub\Editor\6000.0.36f1\Editor\Unity.exe",
    r"C:\Program Files\Unity\Hub\Editor\6000.2.14f1\Editor\Unity.exe",
    "/Applications/Unity/Hub/Editor/6000.0.36f1/Unity.app/Contents/MacOS/Unity",
]

# 판정 기준 — 기획서 §21 프로토타입 검증 V1
TARGET_FPS = 60.0
TARGET_MOBS = 500


def find_unity():
    for p in UNITY_CANDIDATES:
        if os.path.exists(p):
            return p
    hub = r"C:\Program Files\Unity\Hub\Editor"
    if os.path.isdir(hub):
        vers = sorted(os.listdir(hub), reverse=True)
        for v in vers:
            exe = os.path.join(hub, v, "Editor", "Unity.exe")
            if os.path.exists(exe):
                return exe
    return None


def kill_stale_unity():
    """배치 빌드는 다른 인스턴스가 프로젝트를 열고 있으면 실패한다"""
    if platform.system() != "Windows":
        return
    subprocess.run(["taskkill", "/IM", "Unity.exe", "/F"],
                   capture_output=True, text=True)
    lock = os.path.join(UNITY_PROJECT, "Temp", "UnityLockfile")
    if os.path.exists(lock):
        try:
            os.remove(lock)
        except OSError:
            pass


def build(unity):
    os.makedirs(LOG_DIR, exist_ok=True)
    log = os.path.join(LOG_DIR, "agent_build.log")
    if os.path.exists(log):
        os.remove(log)
    print("[마루] 유니티 빌드 시작…")
    t0 = time.time()
    r = subprocess.run(
        [unity, "-batchmode", "-quit", "-projectPath", UNITY_PROJECT,
         "-executeMethod", "W1Runner.Build", "-logFile", log],
        capture_output=True, text=True, encoding="utf-8", errors="replace",
        timeout=1800,
    )
    dur = time.time() - t0
    text = ""
    if os.path.exists(log):
        with io.open(log, encoding="utf-8", errors="replace") as f:
            text = f.read()
    errs = [l for l in text.splitlines() if "error CS" in l or "BuildFailed" in l]
    ok = r.returncode == 0 and not errs
    print(f"[마루] 빌드 {'성공' if ok else '실패'} ({dur:.0f}s, rc={r.returncode})")
    for e in errs[:10]:
        print("   " + e.strip())
    return ok, errs


def run_player(timeout=300):
    exe = os.path.join(BUILD_DIR, "W1.exe")
    if platform.system() != "Windows":
        print("[마루] Windows 전용 플레이어 — 실행 생략")
        return None
    if not os.path.exists(exe):
        print("[마루] 빌드 산출물 없음: " + exe)
        return None

    out_csv = os.path.join(LOG_DIR, "w1_result.csv")
    plog = os.path.join(LOG_DIR, "agent_player.log")
    for f in [out_csv, plog] + glob.glob(os.path.join(LOG_DIR, "w1_*mobs.png")):
        if os.path.exists(f):
            os.remove(f)

    print("[마루] 플레이어 실행 (렌더링 상태에서 측정)…")
    try:
        subprocess.run(
            [exe, "-screen-width", "1280", "-screen-height", "720",
             "-screen-fullscreen", "0", "-logFile", plog, "--out", out_csv],
            timeout=timeout, capture_output=True,
        )
    except subprocess.TimeoutExpired:
        print("[마루] 타임아웃 — 강제 종료")
        subprocess.run(["taskkill", "/IM", "W1.exe", "/F"], capture_output=True)
    return out_csv


def count_unit_pixels(png_path):
    """
    스크린샷에서 '유닛으로 보이는 픽셀' 수를 센다.

    주의: 축소하면 안 된다. 유닛 스프라이트는 화면에서 수 픽셀 크기라
    320x180으로 줄이면 그대로 사라져 **있는데 없다고 판정**한다
    (2026-08-13 이 함수의 1차 버전이 실제로 그 오탐을 냈다).
    바닥(녹/갈 계열 저채도)과 구분되는 색만 센다.
    """
    from PIL import Image
    im = Image.open(png_path).convert("RGB")
    w, h = im.size
    px = im.load()
    vivid = 0
    for y in range(0, h, 2):            # 2픽셀 간격 표본 — 정확도는 충분, 속도는 4배
        for x in range(0, w, 2):
            r, g, b = px[x, y]
            mx, mn = max(r, g, b), min(r, g, b)
            if mx - mn < 30 or mx < 60:
                continue                 # 무채색·어두운 픽셀 = 바닥
            if g >= r and g >= b:
                continue                 # 녹색 우세 = 풀바닥 노이즈
            vivid += 1
    return vivid, (w // 2) * (h // 2)


def verify_rendered(shot_dir):
    """
    렌더링이 실제로 일어났는지 **단계 간 비교**로 판정한다.
    절대 임계값은 스프라이트 크기·색에 따라 흔들리지만,
    "몹이 많은 단계가 적은 단계보다 유닛 픽셀이 많다"는 관계는 견고하다.
    """
    try:
        from PIL import Image  # noqa: F401
    except ImportError:
        return None, "Pillow 없음 — 렌더 검증 생략(pip install pillow)"

    lo = os.path.join(shot_dir, "w1_100mobs.png")
    hi = os.path.join(shot_dir, "w1_1000mobs.png")
    mid = os.path.join(shot_dir, f"w1_{TARGET_MOBS}mobs.png")
    if not (os.path.exists(lo) and os.path.exists(hi) and os.path.exists(mid)):
        return None, "스크린샷 부족 — 렌더 검증 생략"

    n_lo, total = count_unit_pixels(lo)
    n_mid, _ = count_unit_pixels(mid)
    n_hi, _ = count_unit_pixels(hi)

    msg = f"유닛 픽셀 100체={n_lo} / {TARGET_MOBS}체={n_mid} / 1000체={n_hi} (표본 {total})"
    if n_mid < 50:
        return False, "화면이 비어 있다 — " + msg
    if not (n_hi > n_mid > n_lo):
        return False, "몹 수에 따라 유닛 픽셀이 늘지 않는다(렌더 누락 의심) — " + msg
    return True, msg


def judge(csv_path):
    if not csv_path or not os.path.exists(csv_path):
        return False, "결과 CSV 없음", []
    with open(csv_path, encoding="utf-8") as f:
        rows = list(csv.DictReader(f))
    if not rows:
        return False, "CSV 비어 있음", []

    target = next((r for r in rows if int(r["mobs"]) == TARGET_MOBS), None)
    if target is None:
        return False, f"{TARGET_MOBS}체 구간 결과 없음", rows

    fps = float(target["avg_fps"])
    perf_ok = fps >= TARGET_FPS

    # 렌더 검증 — 이게 없으면 "빈 화면 700fps"를 통과시킨다
    render_ok, render_msg = verify_rendered(LOG_DIR)

    if render_ok is False:
        return False, f"평균 {fps:.0f}fps지만 **화면에 유닛이 없다** ({render_msg}) — 측정 무효", rows
    note = f"평균 {fps:.0f}fps @ {TARGET_MOBS}체 / 렌더 검증: {render_msg}"
    return perf_ok, note, rows


def write_report(ok, note, rows, errs):
    os.makedirs(REPORT_DIR, exist_ok=True)
    stamp = datetime.now().strftime("%Y%m%d_%H%M")
    path = os.path.join(REPORT_DIR, f"w1_{stamp}.md")
    lines = [
        f"# W1 성능 검증 — {datetime.now():%Y-%m-%d %H:%M}",
        "",
        f"**판정: {'PASS' if ok else 'FAIL'}** — {note}",
        "",
        "| 잡몹 | 소환수 | 투사체 | 평균fps | 최저fps | 프레임(ms) | 판정 |",
        "|---|---|---|---|---|---|---|",
    ]
    for r in rows:
        lines.append("| {mobs} | {summons} | {projectiles} | {avg_fps} | {min_fps} | {frame_ms_avg} | {verdict} |".format(**r))
    if errs:
        lines += ["", "## 빌드 오류", "```"] + errs[:20] + ["```"]
    lines += ["", "기준: 기획서 §21 V1 — 잡몹 500체 + 소환수 50 + 투사체에서 60fps 유지"]
    with io.open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    print(f"[마루] 리포트: {path}")
    return path


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--skip-build", action="store_true")
    ap.add_argument("--send", action="store_true", help="결과를 텔레그램으로도 전송")
    a = ap.parse_args()

    errs = []
    if not a.skip_build:
        unity = find_unity()
        if not unity:
            print("[마루] 유니티를 찾을 수 없다")
            return 2
        kill_stale_unity()
        ok, errs = build(unity)
        if not ok:
            write_report(False, "빌드 실패", [], errs)
            return 1

    csv_path = run_player()
    ok, note, rows = judge(csv_path)
    print(f"[마루] 판정: {'PASS' if ok else 'FAIL'} — {note}")
    path = write_report(ok, note, rows, errs)

    # 알림은 실패했을 때만 (오너 지시: 경보는 못 고친 것만)
    if a.send and not ok:
        try:
            from _shared.telegram import send
            send(f"🎮 W1 검증 실패 — {note}\n{path}")
        except Exception as e:
            print(f"[마루] 텔레그램 전송 실패: {e}")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
