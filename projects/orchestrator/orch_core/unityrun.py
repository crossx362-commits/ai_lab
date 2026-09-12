"""Unity 배치 실행 + 컴파일 판정.

Unity가 최종 심판이다(§8). 다만 "심판이 판정을 내렸다"는 사실 자체를 먼저 확인한다 —
종료 코드 0이지만 마커가 없으면 PASS가 아니라 UNKNOWN이다.
"""

from __future__ import annotations

import json
import re
from dataclasses import dataclass, field
from pathlib import Path

from . import proc, safety
from .config import Target

OK_MARKER = "ORCH_COMPILE_OK"
FAIL_MARKER = "ORCH_COMPILE_FAIL"
EXECUTE_METHOD = "Orch.EditorTools.OrchCompileCheck.Run"

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


@dataclass
class TestResult:
    verdict: str  # PASS / FAILED / UNKNOWN
    platform: str
    reason: str
    total: int = 0
    passed: int = 0
    failed: int = 0
    skipped: int = 0
    failures: list[str] = field(default_factory=list)
    xml_path: Path | None = None
    duration_s: float = 0.0

    @property
    def summary(self) -> str:
        return f"{self.platform}: {self.verdict} (총 {self.total}, 통과 {self.passed}, 실패 {self.failed}, 건너뜀 {self.skipped})"


def _parse_results(xml_path: Path) -> tuple[int, int, int, int, list[str]]:
    """NUnit3 결과 XML을 읽는다. 읽지 못하면 예외를 올린다 — 0건을 통과로 둔갑시키지 않기 위해."""
    import xml.etree.ElementTree as ET

    root = ET.parse(str(xml_path)).getroot()
    total = int(root.get("total", 0))
    passed = int(root.get("passed", 0))
    failed = int(root.get("failed", 0))
    skipped = int(root.get("skipped", 0))
    failures = []
    for case in root.iter("test-case"):
        if case.get("result") in ("Failed", "Error"):
            msg = ""
            f = case.find("failure/message")
            if f is not None and f.text:
                msg = " ".join(f.text.split())[:300]
            failures.append(f"{case.get('fullname')} :: {msg}")
    return total, passed, failed, skipped, failures


def run_tests(
    target: Target,
    project_path: Path,
    platform: str,
    *,
    log_prefix: Path,
    conn=None,
    task_id=None,
    attempt_id=None,
    unity_slots: int = safety.DEFAULT_UNITY_SLOTS,
) -> TestResult:
    """EditMode / PlayMode 테스트를 배치로 돌리고 결과 XML로 판정한다.

    "종료코드 0 = 통과"로 두지 않는다. **결과 XML이 있고, 총 개수가 0이 아니고, 실패가 0**이어야
    PASS다. 테스트가 한 건도 안 돌았는데 초록불이 켜지는 것이 가장 위험한 형태의 거짓 통과다.
    """
    xml_path = Path(f"{log_prefix}.{platform.lower()}.xml")
    unity_log = Path(f"{log_prefix}.{platform.lower()}.unity.log")
    unity_log.parent.mkdir(parents=True, exist_ok=True)
    if xml_path.exists():
        xml_path.unlink()  # 지난 판의 결과를 이번 판으로 착각하지 않게

    cmd = [
        str(target.unity_editor),
        "-batchmode",
        "-nographics",
        "-runTests",
        "-projectPath", str(project_path),
        "-testPlatform", platform,
        "-testResults", str(xml_path),
        "-logFile", str(unity_log),
    ]

    with safety.unity_slot(slots=unity_slots, timeout=target.unity_timeout_sec):
        r = proc.run(
            cmd, cwd=project_path, timeout=target.unity_timeout_sec,
            log_prefix=Path(f"{log_prefix}.{platform.lower()}"),
            conn=conn, task_id=task_id, attempt_id=attempt_id, kind="unity-test",
            watch_file=unity_log, stall_sec=target.unity_stall_sec,
        )

    log_text = unity_log.read_text(encoding="utf-8", errors="replace") if unity_log.is_file() else ""
    combined = log_text + "\n" + (r.stdout or "") + "\n" + (r.stderr or "")

    if r.status == "SPAWN_FAILED":
        return TestResult("UNKNOWN", platform, f"Unity 실행 실패: {r.stderr[:200]}", duration_s=r.duration_s)
    if r.status == "STALLED":
        return TestResult("UNKNOWN", platform,
                          f"Unity가 {target.unity_stall_sec}s 동안 로그를 한 줄도 쓰지 않았다 — 멎은 것으로 보고 세웠다",
                          duration_s=r.duration_s)
    if r.status == "TIMEOUT":
        return TestResult("UNKNOWN", platform, f"테스트 타임아웃({target.unity_timeout_sec}s)", duration_s=r.duration_s)

    cs_errors = [ln.strip() for ln in _CS_ERROR.findall(combined)]
    if cs_errors:
        return TestResult("FAILED", platform, f"컴파일 오류로 테스트를 돌리지 못했다 ({len(cs_errors)}건)",
                          failures=cs_errors[:20], xml_path=xml_path, duration_s=r.duration_s)

    if not xml_path.is_file():
        return TestResult("UNKNOWN", platform, "결과 XML이 없다 — 테스트가 실제로 돌았는지 알 수 없다",
                          xml_path=None, duration_s=r.duration_s)
    try:
        total, passed, failed, skipped, failures = _parse_results(xml_path)
    except Exception as e:
        return TestResult("UNKNOWN", platform, f"결과 XML을 읽지 못했다: {e}",
                          xml_path=xml_path, duration_s=r.duration_s)

    if total == 0:
        return TestResult("UNKNOWN", platform, "테스트가 0건 실행됐다 — 통과로 보지 않는다",
                          total=0, xml_path=xml_path, duration_s=r.duration_s)
    if failed > 0:
        return TestResult("FAILED", platform, f"테스트 {failed}건 실패",
                          total=total, passed=passed, failed=failed, skipped=skipped,
                          failures=failures, xml_path=xml_path, duration_s=r.duration_s)
    return TestResult("PASS", platform, f"테스트 {passed}/{total} 통과",
                      total=total, passed=passed, failed=failed, skipped=skipped,
                      xml_path=xml_path, duration_s=r.duration_s)


def compile_check(
    target: Target,
    project_path: Path,
    *,
    log_prefix: Path,
    conn=None,
    task_id=None,
    attempt_id=None,
    unity_slots: int = safety.DEFAULT_UNITY_SLOTS,
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
        "-orchReport",
        str(report_path),
        "-logFile",
        str(unity_log),
    ]

    # 16GB 기계에서 Unity를 동시에 여럿 띄우면 스왑으로 전부 느려진다 — 슬롯으로 제한한다.
    with safety.unity_slot(slots=unity_slots, timeout=target.unity_timeout_sec):
        r = proc.run(
            cmd,
            cwd=project_path,
            timeout=target.unity_timeout_sec,
            log_prefix=Path(f"{log_prefix}.unity"),
            conn=conn,
            task_id=task_id,
            attempt_id=attempt_id,
            kind="unity",
            watch_file=unity_log,
            stall_sec=target.unity_stall_sec,
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
    if r.status == "STALLED":
        return UnityResult("UNKNOWN",
                           f"Unity가 {target.unity_stall_sec}s 동안 로그를 한 줄도 쓰지 않았다 — 멎은 것으로 보고 세웠다"
                           f"(타임아웃 {target.unity_timeout_sec}s를 다 태우지 않는다)",
                           r.exit_code, errors, unity_log, report, r.duration_s)
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


# ---------------------------------------------------------------------------
# 추가 게이트: 프로젝트가 가진 자기 검사(예: 울온 SliceSelfCheck.Run)를 배치로 돌려 종료코드로 판정
# ---------------------------------------------------------------------------

def run_method_gate(target: Target, project_path: Path, gate: dict, *, log_prefix: Path, conn=None,
                    task_id=None, attempt_id=None, unity_slots: int = safety.DEFAULT_UNITY_SLOTS) -> TestResult:
    """`-executeMethod <gate.execute_method>` 를 돌리고 **종료코드**로 판정한다.

    프로젝트의 자기 검사는 NUnit이 아니라 Exit(1)로 실패를 말하는 경우가 많다(울온).
    0이면 PASS, 아니면 FAILED(로그의 실패 줄을 근거로), 돌았는지 모르면 UNKNOWN.
    """
    name = gate.get("name") or gate["execute_method"].rsplit(".", 1)[-1]
    tag = "gate-" + "".join(c if c.isalnum() else "_" for c in name.lower())
    unity_log = Path(f"{log_prefix}.{tag}.unity.log")
    unity_log.parent.mkdir(parents=True, exist_ok=True)
    timeout = int(gate.get("timeout_sec") or target.unity_timeout_sec)
    cmd = [str(target.unity_editor), "-batchmode", "-nographics", "-quit",
           "-projectPath", str(project_path), "-executeMethod", gate["execute_method"],
           "-logFile", str(unity_log)]
    with safety.unity_slot(slots=unity_slots, timeout=timeout):
        r = proc.run(cmd, cwd=project_path, timeout=timeout, log_prefix=Path(f"{log_prefix}.{tag}"),
                     conn=conn, task_id=task_id, attempt_id=attempt_id, kind="unity-gate",
                     watch_file=unity_log, stall_sec=target.unity_stall_sec)
    log_text = unity_log.read_text(encoding="utf-8", errors="replace") if unity_log.is_file() else ""
    combined = log_text + "\n" + (r.stdout or "") + "\n" + (r.stderr or "")
    if r.status == "SPAWN_FAILED":
        return TestResult("UNKNOWN", name, f"Unity 실행 실패: {(r.stderr or '')[:200]}", duration_s=r.duration_s)
    if r.status == "STALLED":
        return TestResult("UNKNOWN", name, f"Unity가 {target.unity_stall_sec}s 동안 로그를 안 썼다 — 멎음", duration_s=r.duration_s)
    if r.status == "TIMEOUT":
        return TestResult("UNKNOWN", name, f"게이트 타임아웃({timeout}s)", duration_s=r.duration_s)
    cs_errors = [ln.strip() for ln in _CS_ERROR.findall(combined)]
    if cs_errors:
        return TestResult("FAILED", name, f"컴파일 오류로 게이트를 돌리지 못했다 ({len(cs_errors)}건)",
                          failures=cs_errors[:20], duration_s=r.duration_s)
    if not log_text:
        return TestResult("UNKNOWN", name, "Unity 로그가 없다 — 게이트가 돌았는지 알 수 없다", duration_s=r.duration_s)
    fail_lines = [ln.strip() for ln in log_text.splitlines()
                  if re.search(gate.get("fail_regex") or r"(FAIL|실패|✗|Exception|Error)", ln)
                  and "Licensing" not in ln][-20:]
    if r.exit_code == 0:
        return TestResult("PASS", name, f"게이트 {name} 통과(exit 0)", total=1, passed=1, duration_s=r.duration_s)
    return TestResult("FAILED", name, f"게이트 {name} 실패(exit {r.exit_code})", total=1, failed=1,
                      failures=fail_lines or [f"exit {r.exit_code} — 로그 {unity_log.name}"], duration_s=r.duration_s)


def prepare_library(target: Target, project_path: Path) -> str:
    """worktree의 Unity 프로젝트에 target별 공유 Library를 심는다.

    큰 프로젝트(울온 750MB)는 판마다 새로 임포트하면 몇 분씩 든다. Library는 Unity가 다시 만드는
    캐시라 공유해도 되고(변경된 자산만 재임포트), 동시 실행은 슬롯이 막는다. 첫 캐시는 본 체크아웃의
    Library를 복사해 심는다(seed). 반환값은 화면에 찍을 한 줄.
    """
    from . import config as _cfg
    import pathlib, shutil, subprocess
    if not target.library_cache:
        return ""
    cache = _cfg.STATE_DIR / "library" / target.name
    lib = project_path / "Library"
    if lib.is_symlink() or lib.exists():
        return f"Library 이미 있음: {lib}"
    cache.parent.mkdir(parents=True, exist_ok=True)
    note = "공유 Library 캐시 연결"
    if not cache.exists():
        if target.library_seed and (target.library_seed).is_dir():
            subprocess.run(["rsync", "-a", "--exclude", "Bee/", "--exclude", "*.lock",
                            str(target.library_seed) + "/", str(cache) + "/"], check=False)
            note = f"공유 Library 캐시를 seed에서 복사({target.library_seed})"
        else:
            cache.mkdir(parents=True, exist_ok=True)
            note = "공유 Library 캐시 새로 만듦(첫 임포트는 오래 걸린다)"
    lib.symlink_to(cache, target_is_directory=True)
    from . import gitwt
    try:
        gitwt.exclude_paths(project_path, [lib])
    except Exception as e:  # noqa: BLE001
        note += f" (exclude 등록 실패: {e})"
    return note
