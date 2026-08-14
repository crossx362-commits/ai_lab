#!/usr/bin/env python3
"""
게임 "재와 별" 회귀 검증 자동화 — W1·W2·W3 통합 실행

W1 성능(500체 60fps + 렌더링) ·W2 조작감(무적 흡수 3회 이상)·W3 밸런스(생존시간 단조증가)
세 검증을 한 번에 순서대로 빌드·실행·판정하고 통합 리포트를 생성한다.

사용:
  python game_regression.py                         # 빌드 + 실행 (3종 전부)
  python game_regression.py --only w1               # W1만
  python game_regression.py --skip-build            # 기존 빌드로 실행만
  python game_regression.py --send                  # 실패 시 텔레그램
"""
import argparse
import csv
import glob
import io
import os
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
sys.path.insert(0, _here)

from game_platform import (                                  # noqa: E402
    build_target, ensure_no_editor_lock, find_unity, player_managed_dll, player_path,
    terminate_player,
)

try:
    from _shared.env import load_env
    load_env(_root)
except Exception as e:
    print(f"[마루] env 로드 생략: {e}")

GAME = os.path.join(_root, "projects", "ashes-to-stars")
UNITY_PROJECT = os.path.join(GAME, "unity")
LOG_DIR = os.path.join(GAME, "results")
REPORT_DIR = os.path.join(_root, "output", "qa", "ashes-to-stars")

# 빌드 디렉토리와 실행 파일
# "stem"은 확장자 없는 산출물 이름 — 실제 경로는 player_path()가 플랫폼별로 만든다
# (맥은 W1.app/Contents/MacOS/W1, Windows는 W1.exe)
BUILDS = {
    "w1": {
        "dir": os.path.join(GAME, "build_w1_perf"),
        "stem": "W1",
        "runner": "W1Runner.Build",
        "csv": "w1_result.csv",
        "timeout": 300,
    },
    "w2": {
        "dir": os.path.join(GAME, "build_w2_control"),
        "stem": "W2",
        "runner": "W2Runner.Build",
        "csv": "w2_result.csv",
        "timeout": 120,
        "args": ["--bot"],
    },
    "w3": {
        "dir": os.path.join(GAME, "build_w3_party"),
        "stem": "W3",
        "runner": "W3Runner.Build",
        "csv": "w3_result.csv",
        "timeout": 200,
    },
}

def newest_source_mtime():
    """Assets 아래 .cs 중 가장 최근 수정 시각."""
    newest = 0.0
    for base, _dirs, files in os.walk(os.path.join(UNITY_PROJECT, "Assets")):
        for f in files:
            if f.endswith(".cs"):
                newest = max(newest, os.path.getmtime(os.path.join(base, f)))
    return newest


def check_build_freshness(build_key, build_before_time=None):
    """
    빌드 갱신 확인 — 산출물의 Assembly-CSharp.dll이 **소스보다 새로운지** 본다.
    (2026-08-13 사고: 낡은 빌드로 측정해 불가능한 수치를 읽었다)

    ⚠️ "빌드 시작 시각보다 새로운가"로 보면 안 된다 — 유니티 증분 빌드는
       코드가 안 바뀌면 DLL을 다시 쓰지 않으므로, 정상 빌드가 매번 '낡음'으로 뒤집힌다(실측).
       우리가 막으려는 사고는 "코드를 고쳤는데 옛 빌드로 측정하는 것"이니
       기준은 빌드 시각이 아니라 **소스 시각**이다.

    반환: (ok, message)
    """
    spec = BUILDS[build_key]
    dll_path = player_managed_dll(spec["dir"], spec["stem"])

    if not os.path.exists(dll_path):
        return False, f"DLL 없음: {dll_path}"

    dll_mtime = os.path.getmtime(dll_path)
    src_mtime = newest_source_mtime()
    if dll_mtime < src_mtime:
        return False, (f"DLL이 소스보다 낡았다 (소스: {src_mtime:.0f}, DLL: {dll_mtime:.0f}) "
                       "— 빌드가 코드 변경을 반영하지 못했다")

    return True, "DLL이 소스보다 새롭다"


def build(unity, build_key):
    """
    유니티 배치 빌드 실행.

    반환: (ok, errs, build_time)
    """
    spec = BUILDS[build_key]
    os.makedirs(LOG_DIR, exist_ok=True)
    log = os.path.join(LOG_DIR, f"agent_build_{build_key}.log")
    if os.path.exists(log):
        os.remove(log)

    print(f"[마루] {build_key.upper()} 빌드 시작…")
    t0 = time.time()
    build_before_time = t0  # DLL 검증용

    r = subprocess.run(
        [unity, "-batchmode", "-quit", "-projectPath", UNITY_PROJECT,
         "-buildTarget", build_target(),
         "-executeMethod", spec["runner"], "-logFile", log],
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

    # 유니티가 **애초에 안 돌았는데 rc=0** 인 경우가 있다.
    # 실측(2026-08-14): Rosetta 2 미설치 상태의 맥 에디터는
    #   "Rosetta 2 isn't installed …" 한 줄만 stdout에 뱉고 rc=0으로 즉시 종료한다.
    #   로그 파일조차 안 생기는데 이 함수는 "빌드 성공 (0s)"을 찍었다.
    # 「rc=0 ≠ 실행됨」 — 판정 근거는 **로그의 존재**다.
    # ⚠️ 소요 시간으로 판정하지 마라: 증분 빌드는 3~4초 만에 정상 완료한다
    #    (처음엔 dur<5를 미실행 신호로 넣었다가 멀쩡한 빌드를 실패로 뒤집었다 — 실측).
    if ok and not text.strip():
        head = (r.stdout or r.stderr or "").strip().splitlines()
        why = head[0] if head else "로그 파일이 생성되지 않았다"
        errs = [f"유니티가 실행되지 않았다(rc=0, {dur:.0f}s): {why}"]
        ok = False

    if ok:
        # DLL 신선도 검증
        fresh_ok, fresh_msg = check_build_freshness(build_key, build_before_time)
        print(f"[마루] {build_key.upper()} 빌드 성공 ({dur:.0f}s) — {fresh_msg}")
        if not fresh_ok:
            print(f"[마루] ⚠️ 경고: {fresh_msg}")
            ok = False
    else:
        print(f"[마루] {build_key.upper()} 빌드 실패 (rc={r.returncode})")
        for e in errs[:10]:
            print("   " + e.strip())

    return ok, errs, dur


def run_player(build_key, timeout=None):
    """
    플레이어 실행 및 CSV 수집.

    반환: (csv_path, success)
    """
    spec = BUILDS[build_key]
    if timeout is None:
        timeout = spec["timeout"]

    exe = player_path(spec["dir"], spec["stem"])
    if not os.path.exists(exe):
        print(f"[마루] 빌드 산출물 없음: {exe}")
        return None, False

    out_csv = os.path.join(LOG_DIR, spec["csv"])
    plog = os.path.join(LOG_DIR, f"agent_player_{build_key}.log")
    for f in [out_csv, plog] + glob.glob(os.path.join(LOG_DIR, f"{build_key}_*.png")):
        if os.path.exists(f):
            os.remove(f)

    print(f"[마루] {build_key.upper()} 플레이어 실행…")
    args = [exe, "-screen-width", "1280", "-screen-height", "720",
            "-screen-fullscreen", "0", "-logFile", plog]

    # W2·W3는 CSV 출력 경로 지정 필요
    if build_key == "w1":
        args.extend(["--out", out_csv])
    else:
        args.extend(["--out", out_csv])

    # 추가 인자 (W2는 --bot)
    if "args" in spec:
        args.extend(spec["args"])

    try:
        subprocess.run(args, timeout=timeout, capture_output=True)
        if os.path.exists(out_csv):
            print(f"[마루] {build_key.upper()} 완료: {out_csv}")
            return out_csv, True
        else:
            print(f"[마루] {build_key.upper()} CSV 생성 실패")
            return None, False
    except subprocess.TimeoutExpired:
        print(f"[마루] {build_key.upper()} 타임아웃 ({timeout}s) — 강제 종료")
        terminate_player(exe)
        return None, False


def judge_w1(csv_path):
    """W1 판정: 500체 60fps 이상 + 렌더 검증"""
    if not csv_path or not os.path.exists(csv_path):
        return False, "CSV 없음", []

    with open(csv_path, encoding="utf-8") as f:
        rows = list(csv.DictReader(f))

    if not rows:
        return False, "CSV 비어 있음", []

    TARGET_FPS = 60.0
    TARGET_MOBS = 500

    target = next((r for r in rows if int(r["mobs"]) == TARGET_MOBS), None)
    if target is None:
        return False, f"{TARGET_MOBS}체 구간 결과 없음", rows

    fps = float(target["avg_fps"])
    perf_ok = fps >= TARGET_FPS

    # 렌더 검증 (game_build_verify.py의 로직 재사용 가능)
    # 여기서는 간단하게: "PASS" verdict가 있으면 렌더링이 정상이라고 가정
    render_ok = target.get("verdict", "FAIL").upper() == "PASS"

    if not render_ok:
        return False, f"평균 {fps:.0f}fps지만 렌더링 검증 실패", rows

    note = f"평균 {fps:.0f}fps @ {TARGET_MOBS}체 / 렌더 검증 통과"
    return perf_ok, note, rows


def judge_w2(csv_path):
    """W2 판정: absorbs >= 3"""
    if not csv_path or not os.path.exists(csv_path):
        return False, "CSV 없음", []

    with open(csv_path, encoding="utf-8") as f:
        rows = list(csv.DictReader(f))

    if not rows:
        return False, "CSV 비어 있음", []

    row = rows[0]
    absorbs = int(row.get("absorbs", 0))

    MIN_ABSORBS = 3
    ok = absorbs >= MIN_ABSORBS

    note = f"무적 흡수 {absorbs}회 (기준 {MIN_ABSORBS}회 이상)"
    return ok, note, rows


def judge_w3(csv_path):
    """W3 판정: 생존 시간이 Aggressive < Balanced < Defensive < Survival 순서인가"""
    if not csv_path or not os.path.exists(csv_path):
        return False, "CSV 없음", []

    with open(csv_path, encoding="utf-8") as f:
        rows = list(csv.DictReader(f))

    if not rows:
        return False, "CSV 비어 있음", []

    # 스타일별로 생존 시간 추출
    styles = {}
    for row in rows:
        style = row.get("style", "Unknown").strip()
        survived = float(row.get("survived_s", 0))
        styles[style] = survived

    expected_order = ["Aggressive", "Balanced", "Defensive", "Survival"]
    times = [styles.get(s, 0) for s in expected_order]

    # 단조 증가 확인
    ok = all(times[i] < times[i+1] for i in range(len(times)-1))

    note = f"생존시간: {' < '.join([f'{s}({times[i]:.1f}s)' for i, s in enumerate(expected_order)])}"
    if not ok:
        note += " — 기준 미충족 (단조 증가 필요)"

    return ok, note, rows


def write_report(results, errs=None):
    """
    통합 리포트 작성.

    results: {"w1": (ok, note, rows), "w2": (ok, note, rows), "w3": (ok, note, rows)}
    """
    os.makedirs(REPORT_DIR, exist_ok=True)
    stamp = datetime.now().strftime("%Y%m%d_%H%M")
    path = os.path.join(REPORT_DIR, f"regression_{stamp}.md")

    lines = [
        f"# 게임 회귀 검증 — {datetime.now():%Y-%m-%d %H:%M}",
        "",
        "| 항목 | 판정 | 상세 |",
        "|---|---|---|",
    ]

    overall_ok = True
    for key in ["w1", "w2", "w3"]:
        if key not in results:
            lines.append(f"| {key.upper()} | SKIP | 실행 안 함 |")
            continue

        ok, note, rows = results[key]
        status = "✓ PASS" if ok else "✗ FAIL"
        if not ok:
            overall_ok = False
        lines.append(f"| {key.upper()} | {status} | {note} |")

    lines += [
        "",
        "## 세부 결과",
    ]

    # W1: 구간별 성능
    if "w1" in results and results["w1"][2]:
        lines.append("### W1 성능 (부하 단계별 FPS)")
        lines.append("| 잡몹 | 소환 | 투사체 | 평균fps | 최저fps | verdict |")
        lines.append("|---|---|---|---|---|---|")
        for r in results["w1"][2]:
            lines.append(f"| {r.get('mobs')} | {r.get('summons')} | {r.get('projectiles')} | {r.get('avg_fps')} | {r.get('min_fps')} | {r.get('verdict')} |")

    # W2: 조작감 계측
    if "w2" in results and results["w2"][2]:
        lines.append("### W2 조작감 (무적 흡수)")
        lines.append("| 모드 | 대시 | 흡수 | 피격 | Verdict |")
        lines.append("|---|---|---|---|---|")
        for r in results["w2"][2]:
            lines.append(f"| {r.get('mode')} | {r.get('dashes')} | {r.get('absorbs')} | {r.get('hits')} | {r.get('verdict')} |")

    # W3: 스타일별 생존
    if "w3" in results and results["w3"][2]:
        lines.append("### W3 밸런스 (스타일별 생존 시간)")
        lines.append("| 스타일 | 생존시간 | 처치 | 도발 | Verdict |")
        lines.append("|---|---|---|---|---|")
        for r in results["w3"][2]:
            lines.append(f"| {r.get('style')} | {r.get('survived_s')}s | {r.get('kills')} | {r.get('taunts')} | {r.get('verdict')} |")

    lines += [
        "",
        f"**최종 판정: {'PASS' if overall_ok else 'FAIL'}**",
        "",
        "기준: 기획서 §21 프로토타입 검증 V1~V3",
    ]

    if errs:
        lines += ["", "## 빌드 오류"] + errs[:20]

    with io.open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))

    print(f"[마루] 리포트: {path}")
    return path, overall_ok


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", choices=["w1", "w2", "w3"], help="하나만 실행")
    ap.add_argument("--skip-build", action="store_true")
    ap.add_argument("--send", action="store_true", help="실패 시 텔레그램")
    a = ap.parse_args()

    targets = [a.only] if a.only else ["w1", "w2", "w3"]
    results = {}
    all_errs = []

    # 빌드 단계
    if not a.skip_build:
        unity, note = find_unity(UNITY_PROJECT)
        if not unity:
            print(f"[마루] 유니티를 찾을 수 없다 — {note}")
            return 2
        print(f"[마루] 유니티: {note} ({unity}) / 타겟 {build_target()}")

        lock_ok, lock_msg = ensure_no_editor_lock(UNITY_PROJECT)
        if not lock_ok:
            print(f"[마루] 빌드 중단 — {lock_msg}")
            return 3

        for build_key in targets:
            ok, errs, dur = build(unity, build_key)
            if not ok:
                results[build_key] = (False, "빌드 실패", [])
                all_errs.extend(errs)
                continue

            # 빌드 성공 후 플레이어 실행
            csv_path, run_ok = run_player(build_key)

            if not run_ok:
                results[build_key] = (False, "플레이어 실행 실패", [])
                continue

            # CSV 판정
            if build_key == "w1":
                ok, note, rows = judge_w1(csv_path)
            elif build_key == "w2":
                ok, note, rows = judge_w2(csv_path)
            else:  # w3
                ok, note, rows = judge_w3(csv_path)

            results[build_key] = (ok, note, rows)
            print(f"[마루] {build_key.upper()} 판정: {'PASS' if ok else 'FAIL'} — {note}")
    else:
        # --skip-build: 기존 빌드로 실행만
        for build_key in targets:
            spec = BUILDS[build_key]
            csv_path = os.path.join(LOG_DIR, spec["csv"])

            if not os.path.exists(csv_path):
                print(f"[마루] {build_key.upper()} CSV 없음: {csv_path}")
                results[build_key] = (False, "CSV 없음", [])
                continue

            if build_key == "w1":
                ok, note, rows = judge_w1(csv_path)
            elif build_key == "w2":
                ok, note, rows = judge_w2(csv_path)
            else:  # w3
                ok, note, rows = judge_w3(csv_path)

            results[build_key] = (ok, note, rows)
            print(f"[마루] {build_key.upper()} 판정: {'PASS' if ok else 'FAIL'} — {note}")

    # 리포트 작성
    report_path, overall_ok = write_report(results, all_errs)

    # 알림 (실패했을 때만)
    if a.send and not overall_ok:
        try:
            from _shared.telegram import send
            summary = " / ".join([f"{k}={'PASS' if results.get(k, (False,))[0] else 'FAIL'}" for k in targets])
            send(f"🎮 게임 회귀 검증 실패 — {summary}\n{report_path}")
        except Exception as e:
            print(f"[마루] 텔레그램 전송 실패: {e}")

    return 0 if overall_ok else 1


if __name__ == "__main__":
    sys.exit(main())
