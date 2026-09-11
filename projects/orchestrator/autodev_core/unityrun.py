"""Unity 배치 실행 + 컴파일 판정.

Unity가 최종 심판이다(§8). 다만 "심판이 판정을 내렸다"는 사실 자체를 먼저 확인한다 —
종료 코드 0이지만 마커가 없으면 PASS가 아니라 UNKNOWN이다.
"""

from __future__ import annotations

import json
import re
from dataclasses import dataclass, field
from pathlib import Path

from . import proc
from .config import Target

OK_MARKER = "AUTODEV_COMPILE_OK"
FAIL_MARKER = "AUTODEV_COMPILE_FAIL"
EXECUTE_METHOD = "AutoDev.EditorTools.AutoDevCompileCheck.Run"

_CS_ERROR = re.compile(r"^.*?\berror CS\d+\b.*$", re.M)
_LOCKED = re.compile(r"Multiple Unity instances cannot open the same project", re.I)
# 주의: "Licensing::"는 정상 기동 로그에도 매 판 찍힌다 — 그걸 넣었다가 진짜 컴파일 실패를
# UNKNOWN으로 덮어버렸다(2026-09-11 네거티브 컨트롤에서 발견). 실패 문구만 좁게 잡는다.
_LICENSE = re.compile(
    r"(No valid Unity Editor license|Failed to activate|license is not valid|Unable to obtain a license|LICENSE SYSTEM \[.*?\] (?:Failed|Error))",
    re.I,
)


@dataclass
class UnityResult:
    verdict: str  # PASS / FAILED / UNKNOWN
    reason: str
    exit_code: int | None
    errors: list[str] = field(default_factory=list)
    log_path: Path | None = None
    report: dict | None = None
    duration_s: float = 0.0

    @property
    def error_summary(self) -> str:
        if not self.errors:
            return ""
        return "\n".join(self.errors[:40])


def compile_check(
    target: Target,
    project_path: Path,
    *,
    log_prefix: Path,
    conn=None,
    task_id=None,
    attempt_id=None,
) -> UnityResult:
    """배치 모드로 Unity를 열어 컴파일 여부를 판정한다."""
    unity_log = Path(f"{log_prefix}.unity.log")
    report_path = Path(f"{log_prefix}.report.json")
    unity_log.parent.mkdir(parents=True, exist_ok=True)

    cmd = [
        str(target.unity_editor),
        "-batchmode",
        "-nographics",
        "-quit",
        "-projectPath",
        str(project_path),
        "-executeMethod",
        EXECUTE_METHOD,
        "-autodevReport",
        str(report_path),
        "-logFile",
        str(unity_log),
    ]

    r = proc.run(
        cmd,
        cwd=project_path,
        timeout=target.unity_timeout_sec,
        log_prefix=Path(f"{log_prefix}.unity"),
        conn=conn,
        task_id=task_id,
        attempt_id=attempt_id,
        kind="unity",
    )

    log_text = unity_log.read_text(encoding="utf-8", errors="replace") if unity_log.is_file() else ""
    combined = log_text + "\n" + (r.stdout or "") + "\n" + (r.stderr or "")

    report = None
    if report_path.is_file():
        try:
            report = json.loads(report_path.read_text(encoding="utf-8"))
        except Exception:
            report = None

    errors = [ln.strip() for ln in _CS_ERROR.findall(combined)]
    # 같은 오류가 어셈블리마다 반복돼 로그를 채우므로 순서를 지키며 중복만 제거한다.
    seen, uniq = set(), []
    for e in errors:
        if e not in seen:
            seen.add(e)
            uniq.append(e)
    errors = uniq

    if r.status == "SPAWN_FAILED":
        return UnityResult("UNKNOWN", f"Unity 실행 실패: {r.stderr[:200]}", None, errors, unity_log, report, r.duration_s)
    if r.status == "TIMEOUT":
        return UnityResult("UNKNOWN", f"Unity 타임아웃({target.unity_timeout_sec}s) — 그룹 종료함", r.exit_code, errors, unity_log, report, r.duration_s)
    has_ok = OK_MARKER in combined
    has_fail = FAIL_MARKER in combined

    # 순서가 규칙이다: **증거(CS 오류)가 먼저**다. 인프라 추정(락/라이선스)을 앞에 두면
    # 진짜 실패가 UNKNOWN으로 덮인다 — 실제로 그렇게 한 번 틀렸다.
    if errors:
        return UnityResult("FAILED", f"C# 컴파일 오류 {len(errors)}건", r.exit_code, errors, unity_log, report, r.duration_s)
    if has_fail:
        return UnityResult("FAILED", "컴파일 체크가 FAIL 마커를 냈다", r.exit_code, errors, unity_log, report, r.duration_s)
    if has_ok and r.exit_code == 0:
        return UnityResult("PASS", "컴파일 OK 마커 + 종료코드 0 + CS 오류 0", r.exit_code, errors, unity_log, report, r.duration_s)
    # CS 오류가 없는데 비정상 종료면, 그때서야 인프라 원인을 따진다(인프라 실패 ≠ 코드 실패).
    if _LOCKED.search(combined):
        return UnityResult("UNKNOWN", "같은 프로젝트를 다른 Unity 인스턴스가 점유 중", r.exit_code, errors, unity_log, report, r.duration_s)
    if _LICENSE.search(combined):
        return UnityResult("UNKNOWN", "Unity 라이선스 문제로 보임 — 사람 확인 필요", r.exit_code, errors, unity_log, report, r.duration_s)
    if r.exit_code != 0:
        return UnityResult("FAILED", f"Unity 비정상 종료(코드 {r.exit_code}) — CS 오류는 못 찾음", r.exit_code, errors, unity_log, report, r.duration_s)
    # 종료코드 0인데 마커가 없다 = 판정이 실제로 돌았는지 모른다. PASS로 올리지 않는다.
    return UnityResult("UNKNOWN", "종료코드 0이지만 판정 마커가 없다 — 검증됐다고 볼 수 없음", r.exit_code, errors, unity_log, report, r.duration_s)
