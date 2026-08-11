#!/usr/bin/env python3
"""펫나 E2E 스위트 실행 CLI.

왜 이 파일이 있나 (2026-08-11 사고)
-----------------------------------
`tests/e2e/test_*.py`는 `NAME`과 `run(page, base_url)`만 정의하고 `__main__`
블록이 없다 — 테오(`petnna_test_engineer.run_suite`)가 importlib으로 불러
`run()`을 호출하는 계약이기 때문이다. 그런데 이걸 모르고
`python3 tests/e2e/test_x.py`로 실행하면 **모듈만 정의되고 끝나서 항상 exit 0**이
나온다. 실제로 한 세션이 이 방식으로 "40/40 통과"를 확인하고 넘어갔는데,
그 사이 결함이 들어 있었고 네거티브 컨트롤을 돌려보고서야 "테스트가 아예
실행되지 않았다"는 걸 알았다.

즉 **없는 실행 경로가 조용한 거짓 통과를 만들었다.** 그래서 ①이 CLI를 두고
②각 테스트 파일에 직접 실행을 막는 `__main__` 가드를 넣었다.

사용법
------
    python3 projects/petnna/tests/e2e/run_e2e.py              # 전체
    python3 projects/petnna/tests/e2e/run_e2e.py login guest  # 이름 부분일치

실패가 하나라도 있으면 exit 1.
"""
import importlib.util
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]          # .../ai_lab
ENGINE = ROOT / "projects/ai-team/skills/테오_테스트/tools/petnna_test_engineer.py"


def _engine():
    spec = importlib.util.spec_from_file_location("petnna_test_engineer", ENGINE)
    mod = importlib.util.module_from_spec(spec)
    # 엔진이 `_shared.*`를 임포트하므로 projects/ai-team이 경로에 있어야 한다.
    sys.path.insert(0, str(ROOT / "projects/ai-team"))
    spec.loader.exec_module(mod)
    return mod


def main(argv: list[str]) -> int:
    if not ENGINE.exists():
        print(f"❌ 테스트 엔진을 찾을 수 없다: {ENGINE}")
        return 2
    eng = _engine()

    tests = eng.list_tests()
    if argv:
        tests = [t for t in tests if any(a in t.stem for a in argv)]
        if not tests:
            print(f"❌ 이름이 {argv} 에 맞는 테스트가 없다")
            return 2

    # run_suite는 단일 실행만 only= 를 받는다. 필터가 걸리면 하나씩 돌린다.
    results = {}
    if len(tests) == len(eng.list_tests()):
        results = eng.run_suite()
    else:
        for t in tests:
            results.update(eng.run_suite(only=t))

    bad = [k for k, v in results.items() if not v["ok"]]
    for name in sorted(results):
        v = results[name]
        print(f"{'✅' if v['ok'] else '❌'} {name}  ({v['sec']}s)")
    if bad:
        print()
        for name in bad:
            print(f"--- {name} ---\n{results[name]['error']}")
    print(f"\n{len(results) - len(bad)}/{len(results)} 통과")
    return 1 if bad else 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
