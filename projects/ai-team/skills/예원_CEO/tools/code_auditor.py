"""
code_auditor.py — 예원(CEO): 데드코드(미사용 함수) 자동 검수

이번 백테스트 데드코드 정리(2026-07-07)를 자동화. AST로 함수 정의를 뽑아
저장소 전역 참조수를 세고, '고확신' 미사용 함수는 자동 삭제+검증+커밋, 애매한 건 보고만.

동적호출 오탐 방지(핵심 — _obv_rising 오삭제 방지):
  - 전역 word-boundary 참조수 ≤1(자기 정의뿐)일 때만 후보. 문자열 registry/getattr도
    참조수에 잡히므로(grep -w 는 "name" 도 매칭) 참조수 자체가 강한 신호.
  - 이름이 비(非).py 파일(json·plist·md 문서)에 등장하면 보호(설정/문서 참조 가능).
  - 데코레이터 함수·main·dunder·메서드는 자동삭제 제외(동적 디스패치 위험) → 보고만.
  - 삭제 후 py_compile 검증 실패 시 git checkout 자동 롤백.

실행:
  python code_auditor.py --check          # 보고만(삭제·커밋 없음)
  python code_auditor.py --apply          # 고확신 자동삭제+검증+커밋 + 나머지 보고
  python code_auditor.py --apply --send   # + 텔레그램 전송

주의: --apply 커밋은 워치독 데몬 재배포를 유발 → 스케줄은 주말(장 없음)에 둘 것.
"""
import os
import sys
import ast
import re
import subprocess
import datetime

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

_here = os.path.dirname(os.path.abspath(__file__))
_ai_team_root = os.path.abspath(os.path.join(_here, "..", "..", ".."))
if _ai_team_root not in sys.path:
    sys.path.insert(0, _ai_team_root)

from _shared.notify import send
from _shared.env import find_root

_root = find_root(_here)
CHECK_ONLY = "--check" in sys.argv or "--apply" not in sys.argv
SEND_TG = "--send" in sys.argv

# 스캔 대상 — 에이전트 로직 tools. _shared 는 '전 에이전트 의존'이라 자동삭제 제외(가드레일).
SCAN_DIRS = [os.path.join(_root, "projects", "ai-team", "skills")]
PROTECT_NAMES = {"main", "run", "run_audit", "handle", "setup"}  # 관례적 엔트리/훅


def _py_files() -> list[str]:
    out = []
    for base in SCAN_DIRS:
        for dp, _, fns in os.walk(base):
            if "__pycache__" in dp or os.sep + "tests" in dp:
                continue
            out += [os.path.join(dp, f) for f in fns if f.endswith(".py")]
    return out


def _top_funcs(path: str) -> list[dict]:
    """파일의 top-level 함수만(메서드 제외 — 동적 오버라이드 위험). 라인범위·데코레이터 포함."""
    try:
        tree = ast.parse(open(path, encoding="utf-8").read())
    except (SyntaxError, UnicodeDecodeError):
        return []
    funcs = []
    for node in tree.body:  # top-level 만
        if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)):
            funcs.append({
                "name": node.name,
                "line": node.lineno,
                "end": getattr(node, "end_lineno", node.lineno),
                "decorated": bool(node.decorator_list),
            })
    return funcs


def _refcount(name: str) -> int:
    """저장소(ai-team) 전역 .py word-boundary 참조수. 자기 정의 포함."""
    pattern = re.compile(rf"(?<!\w){re.escape(name)}(?!\w)")
    count = 0
    for dp, _, fns in os.walk(_ai_team_root):
        if "__pycache__" in dp:
            continue
        for fn in fns:
            if not fn.endswith(".py"):
                continue
            path = os.path.join(dp, fn)
            try:
                with open(path, encoding="utf-8", errors="replace") as f:
                    count += sum(1 for line in f if pattern.search(line))
            except OSError:
                continue
    return count


def _in_nonpy(name: str) -> bool:
    """비.py(json·plist·md·txt)에 이름 등장 → 설정/문서 참조 가능 → 보호."""
    pattern = re.compile(rf"(?<!\w){re.escape(name)}(?!\w)")
    allowed = {".json", ".md", ".plist", ".txt", ".command"}
    for dp, _, fns in os.walk(_root):
        if "__pycache__" in dp or ".git" in dp:
            continue
        for fn in fns:
            if os.path.splitext(fn)[1].lower() not in allowed:
                continue
            path = os.path.join(dp, fn)
            try:
                with open(path, encoding="utf-8", errors="replace") as f:
                    if any(pattern.search(line) for line in f):
                        return True
            except OSError:
                continue
    return False


def _scan() -> tuple[list[dict], list[dict]]:
    """(고확신 자동삭제 후보, 보고전용 후보) 반환."""
    high, report = [], []
    for path in _py_files():
        for fn in _top_funcs(path):
            nm = fn["name"]
            if nm.startswith("__") or nm in PROTECT_NAMES:
                continue
            rc = _refcount(nm)
            if rc > 1:            # 어딘가에서 참조됨(코드 호출 또는 문자열)
                continue
            rel = os.path.relpath(path, _root)
            item = {"file": rel, "name": nm, "line": fn["line"], "end": fn["end"], "refs": rc}
            # 고확신: 미참조 + 데코레이터 없음 + 비.py 문서/설정에도 없음
            if not fn["decorated"] and not _in_nonpy(nm):
                item["confidence"] = "high"
                high.append(item)
            else:
                item["confidence"] = "medium"
                item["why"] = "데코레이터" if fn["decorated"] else "문서/설정에 이름 존재"
                report.append(item)
    return high, report


def _delete(items: list[dict]) -> set[str]:
    """파일별로 함수 라인범위 제거. 변경 파일 경로 집합 반환."""
    by_file: dict[str, list[dict]] = {}
    for it in items:
        by_file.setdefault(it["file"], []).append(it)
    changed = set()
    for rel, its in by_file.items():
        path = os.path.join(_root, rel)
        lines = open(path, encoding="utf-8").read().splitlines(keepends=True)
        for it in sorted(its, key=lambda x: x["line"], reverse=True):  # 뒤에서부터
            del lines[it["line"] - 1:it["end"]]
        # 3줄+ 연속 공백 → 2줄로 축약
        src = "".join(lines)
        while "\n\n\n\n" in src:
            src = src.replace("\n\n\n\n", "\n\n\n")
        open(path, "w", encoding="utf-8").write(src)
        changed.add(rel)
    return changed


_NOWIN = {"creationflags": subprocess.CREATE_NO_WINDOW} if sys.platform == "win32" else {}


def _verify(changed: set[str]) -> bool:
    for rel in changed:
        r = subprocess.run([sys.executable, "-m", "py_compile", os.path.join(_root, rel)],
                           capture_output=True, text=True, encoding="utf-8", errors="replace",
                           **_NOWIN)
        if r.returncode != 0:
            print(f"❌ py_compile 실패: {rel}\n{r.stderr}")
            return False
    return True


def _rollback(changed: set[str]) -> None:
    subprocess.run(["git", "checkout", "--"] + list(changed), cwd=_root, capture_output=True, **_NOWIN)


def _commit(changed: set[str], n: int) -> bool:
    subprocess.run(["git", "add"] + list(changed), cwd=_root, capture_output=True, **_NOWIN)
    msg = f"chore(예원): 데드코드 자동정리 — 미사용 함수 {n}개 제거 (코드검수기)"
    r = subprocess.run(["git", "commit", "-m", msg], cwd=_root, capture_output=True,
                       text=True, encoding="utf-8", errors="replace", **_NOWIN)
    return r.returncode == 0


def _report(high: list[dict], report: list[dict], deleted: bool, committed: bool) -> str:
    ts = datetime.datetime.now().strftime("%Y-%m-%d %H:%M")
    L = [f"🔍 코드 검수 (데드코드) — {ts}"]
    if not high and not report:
        L.append("미사용 함수 없음 — 깨끗 ✅")
        return "\n".join(L)
    if high:
        head = "🗑️ 자동삭제" + ("+커밋 완료" if committed else " 시도") if deleted else "🗑️ 고확신 후보(삭제 대기)"
        L.append(f"\n{head} ({len(high)})")
        for it in high[:15]:
            L.append(f"  - {it['file']}::{it['name']} (L{it['line']}, 참조 {it['refs']})")
    if report:
        L.append(f"\n📋 검토 필요 — 자동삭제 보류 ({len(report)})")
        for it in report[:15]:
            L.append(f"  - {it['file']}::{it['name']} (L{it['line']}) — {it.get('why','')}")
    if deleted and not committed:
        L.append("\n⚠️ 검증 실패로 롤백됨 — 수동 확인 필요")
    return "\n".join(L)


def _market_hours() -> bool:
    """KR 장중(평일 09:00~15:40). 자동삭제 커밋은 워치독 거래데몬 재배포를 유발하므로
    이 시간엔 트리거(크론 overdue 캐치업 포함)와 무관하게 삭제를 막고 보고만 한다."""
    now = datetime.datetime.now()
    if now.weekday() >= 5:
        return False
    return (9, 0) <= (now.hour, now.minute) < (15, 40)


def run() -> str:
    high, report = _scan()
    deleted = committed = False
    blocked = not CHECK_ONLY and _market_hours()
    if blocked and high:
        report = [dict(it, why="장중 보호 — 삭제 보류(마감 후/주말 재실행)") for it in high] + report
        high = []
    if not CHECK_ONLY and not blocked and high:
        changed = _delete(high)
        if _verify(changed):
            committed = _commit(changed, len(high))
            deleted = True
        else:
            _rollback(changed)          # 검증 실패 → 원복, 보고로 강등
            report = high + report
            high = []
            deleted = True
    text = _report(high, report, deleted, committed)
    print(text)
    if SEND_TG:
        send(text)
    return text


if __name__ == "__main__":
    run()
