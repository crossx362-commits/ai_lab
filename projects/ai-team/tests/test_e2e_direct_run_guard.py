"""E2E 테스트 파일의 '직접 실행 가드' 회귀 (2026-08-11 사고).

사고
----
`projects/petnna/tests/e2e/test_*.py`는 `NAME`과 `run(page, base_url)`만 정의하는
계약이다(테오 엔진이 importlib으로 불러 `run()`을 호출한다). `__main__` 블록이
없으므로 `python3 tests/e2e/test_x.py`로 실행하면 **모듈만 정의되고 끝나서 항상
exit 0**이다. 한 세션이 그 방식으로 "40/40 통과"를 확인하고 넘어갔는데, 실제로는
단 한 줄의 단언도 실행되지 않았다. 네거티브 컨트롤(결함을 일부러 주입)을 돌려
"통과해버리는" 걸 보고서야 알았다.

수리
----
①`tests/e2e/run_e2e.py` CLI 신설 ②모든 테스트 파일에 직접 실행을 **시끄럽게**
막는 가드 추가 ③테오가 새로 만든 파일에도 `_ensure_direct_run_guard`가 강제로
붙인다(프롬프트에만 적으면 LLM이 잊는다 — 잊었을 때 결과가 '조용한 거짓 통과'라
누락이 오탐보다 훨씬 위험하다).

이 테스트는 ②가 전 파일에 유지되는지, ③이 코드로 강제되는지를 지킨다.
"""
import ast
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
E2E_DIR = ROOT / "projects/petnna/tests/e2e"
ENGINE = ROOT / "projects/ai-team/skills/테오_테스트/tools/petnna_test_engineer.py"


def _has_main_guard(src: str) -> bool:
    """`if __name__ == "__main__":` 블록이 있는지 AST로 확인(문자열 grep 아님)."""
    for node in ast.parse(src).body:
        if not isinstance(node, ast.If):
            continue
        t = node.test
        if (isinstance(t, ast.Compare)
                and isinstance(t.left, ast.Name) and t.left.id == "__name__"
                and any(isinstance(c, ast.Constant) and c.value == "__main__"
                        for c in t.comparators)):
            return True
    return False


class E2EDirectRunGuardTests(unittest.TestCase):
    def test_all_e2e_files_refuse_direct_execution(self):
        files = sorted(E2E_DIR.glob("test_*.py"))
        self.assertTrue(files, f"E2E 테스트가 하나도 없다: {E2E_DIR}")
        missing = [f.name for f in files
                   if not _has_main_guard(f.read_text(encoding="utf-8"))]
        self.assertEqual(
            [], missing,
            "직접 실행 시 아무것도 안 하고 exit 0 하는 E2E 파일이 있다 — "
            f"__main__ 가드를 붙여라: {missing}",
        )

    def test_guard_is_enforced_by_code_not_only_by_prompt(self):
        """테오가 생성 직후 가드를 강제로 붙이는 경로가 실재해야 한다."""
        src = ENGINE.read_text(encoding="utf-8")
        tree = ast.parse(src)
        names = {n.name for n in ast.walk(tree) if isinstance(n, ast.FunctionDef)}
        self.assertIn(
            "_ensure_direct_run_guard", names,
            "테오에 가드 강제 함수가 없다 — 프롬프트 지시만으로는 LLM이 잊는다",
        )
        called = any(isinstance(n, ast.Call) and isinstance(n.func, ast.Name)
                     and n.func.id == "_ensure_direct_run_guard"
                     for n in ast.walk(tree))
        self.assertTrue(
            called,
            "_ensure_direct_run_guard가 정의만 되고 호출되지 않는다 "
            "(생성 경로에서 실제로 불려야 의미가 있다)",
        )

    def test_runner_cli_exists(self):
        runner = E2E_DIR / "run_e2e.py"
        self.assertTrue(
            runner.exists(),
            "가드가 안내하는 실행 경로(run_e2e.py)가 없으면 가드는 막다른 골목이다",
        )
        self.assertTrue(_has_main_guard(runner.read_text(encoding="utf-8")),
                        "run_e2e.py 자신은 직접 실행 가능해야 한다")


if __name__ == "__main__":
    unittest.main()
