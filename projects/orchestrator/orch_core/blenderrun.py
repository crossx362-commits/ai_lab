"""Blender 배치 판정 축 (오너 지시 2026-09-11 「코덱스는 블렌더 사용해서 개발」).

Unity 축과 같은 원칙이다: AI가 "만들었다"고 말한 것은 만든 것이 아니다.
저장소의 검증 장치(orch_check.py, protected)가 **팩토리 빈 씬**에서 build.py를 돌려
실제 메시가 생겼는지·tests/test_*.py가 통과하는지를 재고 stdout에 마커를 찍는다.
마커가 없으면 UNKNOWN — 초록을 지어내지 않는다.

    blender -b --factory-startup --python <worktree>/orch_check.py -- --report <json>
"""

from __future__ import annotations

import json
import re
from pathlib import Path

from . import proc
from .config import Target
from .unityrun import UnityResult

OK_MARKER = "ORCH_BLENDER_OK"
FAIL_MARKER = "ORCH_BLENDER_FAIL"
_TRACE_LAST = re.compile(r"^(\w+Error|\w+Exception|SyntaxError|KeyError|AssertionError)\b.*$", re.M)


def check(t: Target, worktree: Path, *, log_prefix: Path, conn=None,
          task_id=None, attempt_id=None) -> UnityResult:
    script = worktree / t.blender_check
    if not script.is_file():
        # 검증 장치가 사라졌다 — 변조 게이트가 먼저 잡지만, 여기까지 오면 판정 불가다.
        return UnityResult("UNKNOWN", f"검증 장치 없음: {t.blender_check}", None)
    report = Path(f"{log_prefix}.blender.json")
    cmd = [str(t.blender_bin), "-b", "--factory-startup", "--python", str(script),
           "--", "--report", str(report)]
    r = proc.run(cmd, cwd=worktree, timeout=t.blender_timeout_sec, log_prefix=Path(f"{log_prefix}.blender"),
                 conn=conn, task_id=task_id, attempt_id=attempt_id, kind="blender")
    out = (r.stdout or "") + "\n" + (r.stderr or "")
    rep = None
    if report.is_file():
        try:
            rep = json.loads(report.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            rep = None

    if r.status == "SPAWN_FAILED":
        return UnityResult("UNKNOWN", f"Blender 실행 불가: {(r.stderr or '')[:200]}", None,
                           log_path=r.stdout_path, duration_s=r.duration_s)
    if r.status == "TIMEOUT":
        return UnityResult("UNKNOWN", f"Blender 타임아웃({t.blender_timeout_sec}s)", r.exit_code,
                           log_path=r.stdout_path, duration_s=r.duration_s)

    errors = list((rep or {}).get("errors", [])) + list((rep or {}).get("failures", []))
    if FAIL_MARKER in out:
        if not errors:
            errors = _TRACE_LAST.findall(out)[-3:] or ["(사유를 추출하지 못했다 — 로그를 봐라)"]
        n_fail = len((rep or {}).get("failures", []))
        reason = (f"Blender 테스트 {n_fail}건 실패" if n_fail
                  else f"Blender 검증 실패: {errors[0][:120]}")
        return UnityResult("FAILED", reason, r.exit_code, errors, r.stdout_path, rep, r.duration_s)
    if OK_MARKER in out and r.exit_code == 0:
        m = (rep or {}).get("meshes", "?"); pl = (rep or {}).get("polys", "?"); ts = (rep or {}).get("tests", 0)
        return UnityResult("PASS", f"Blender 검증 통과 — 메시 {m}개·면 {pl}개·테스트 {ts}건", r.exit_code,
                           [], r.stdout_path, rep, r.duration_s)
    # 마커가 없다: Blender가 스크립트에 닿기 전에 죽었거나 출력이 잘렸다. 초록 아님.
    tail = _TRACE_LAST.findall(out)[-3:]
    return UnityResult("UNKNOWN", f"Blender 판정 마커 없음(exit {r.exit_code})", r.exit_code,
                       tail, r.stdout_path, rep, r.duration_s)
