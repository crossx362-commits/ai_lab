"""backlog.json 공용 락 회귀 테스트 — 2026-08-02.

배경: backlog.json에 쓰는 도구가 7곳인데 저마다 자기 이름의 advisory_lock만 써서
서로를 전혀 배제하지 못했다. 각 도구가 파일 전체를 읽어 고친 뒤 통째로 덮어쓰므로,
두 도구의 읽기·쓰기가 겹치면 나중에 쓴 쪽이 먼저 쓴 쪽 변경을 통째로 지운다.
파이프라인 감사가 2026-07-11에 지적했고 "여러 파일을 한 번에 바꿔야 한다"는 이유로
미뤄져 있던 것을 이날 처리했다.

여기서 굳히는 것:
  1. backlog_lock이 실제로 상호배제한다(다른 프로세스가 쥐고 있으면 기다린다).
  2. 타임아웃을 넘겨도 예외를 던지지 않고 진행한다 — 락 실패로 적재가 통째로
     사라지는 것보다 드문 경합을 감수하는 편이 낫다.
  3. 백로그를 쓰는 도구들이 **읽기부터** 락 안에 넣었다. 쓰기만 감싸면
     read-modify-write 경합이 그대로 남으므로, 함수 본문 시작에 with가 와야 한다.
"""
import multiprocessing
import sys
import time
import unittest
from pathlib import Path

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_TEAM_ROOT))

from _shared.backlog import backlog_lock  # noqa: E402


def _hold(seconds, started, done):
    """다른 프로세스에서 락을 잡고 잠시 붙들고 있는다."""
    sys.path.insert(0, str(AI_TEAM_ROOT))
    from _shared.backlog import backlog_lock as bl
    with bl():
        started.set()
        time.sleep(seconds)
    done.set()


class BacklogLockTests(unittest.TestCase):
    def test_acquires_when_free(self):
        with backlog_lock() as got:
            self.assertTrue(got, "아무도 안 쥐고 있는데 락을 못 잡았다")

    def test_blocks_until_released(self):
        """다른 프로세스가 쥐고 있으면 기다렸다가 잡는다(비차단 스킵이 아니다)."""
        ctx = multiprocessing.get_context("spawn")
        started, done = ctx.Event(), ctx.Event()
        p = ctx.Process(target=_hold, args=(1.2, started, done))
        p.start()
        try:
            self.assertTrue(started.wait(15), "보조 프로세스가 락을 못 잡음")
            t0 = time.time()
            with backlog_lock(timeout=20) as got:
                waited = time.time() - t0
                self.assertTrue(got, "대기 후에도 락을 못 잡았다")
            self.assertGreater(waited, 0.3,
                               f"즉시 통과했다 — 상호배제가 안 되고 있다(대기 {waited:.2f}s)")
        finally:
            p.join(20)

    def test_timeout_does_not_raise(self):
        """타임아웃은 예외가 아니라 '락 없이 진행'이다 — 적재가 사라지면 안 된다."""
        ctx = multiprocessing.get_context("spawn")
        started, done = ctx.Event(), ctx.Event()
        p = ctx.Process(target=_hold, args=(2.0, started, done))
        p.start()
        try:
            self.assertTrue(started.wait(15))
            with backlog_lock(timeout=0.4) as got:
                self.assertFalse(got, "타임아웃인데 획득했다고 보고했다")
        finally:
            p.join(20)


class WritersUseLockTests(unittest.TestCase):
    """백로그를 쓰는 도구가 읽기부터 락 안에 넣었는지 — 쓰기만 감싸면 의미가 없다."""

    CASES = [
        ("테오_테스트/tools/petnna_test_engineer.py", "_backlog_done"),
        ("나무_기획/tools/petnna_product_manager.py", "add_backlog_items"),
        ("미오_디자인/tools/petnna_design_review.py", "add_backlog_items"),
        ("미오_디자인/tools/petnna_design_review.py", "_close_tasks"),
        ("수리_개발자/tools/petnna_dev_engine.py", "_update_backlog"),
    ]

    # 함수 안에 수분짜리 LLM 호출이 있어 **구간만** 감싼 것들.
    # 통째로 감싸면 락을 붙들어 다른 도구를 전부 막으므로 첫 실행문 규칙을 적용하지 않는다.
    # 대신 "백로그 읽기와 쓰기가 같은 with 블록 안에 있는가"를 본다.
    SPAN_CASES = [
        ("예원_CEO/tools/petnna_council.py", "convene"),
        ("백호_백엔드/tools/petnna_backend_guard.py", "investigate_assigned_tasks"),
    ]

    def test_span_wrapped_read_and_write_together(self):
        """구간만 감싼 경우: 읽기와 쓰기가 **같은** backlog_lock 블록 안에 있어야 한다."""
        import ast
        for rel, fn in self.SPAN_CASES:
            path = AI_TEAM_ROOT / "skills" / rel
            node = next((n for n in ast.walk(ast.parse(path.read_text(encoding="utf-8")))
                         if isinstance(n, ast.FunctionDef) and n.name == fn), None)
            self.assertIsNotNone(node, f"{rel}:{fn} 함수를 찾지 못함")
            found = False
            for w in [n for n in ast.walk(node) if isinstance(n, ast.With)]:
                call = w.items[0].context_expr
                if getattr(getattr(call, "func", None), "id", None) != "backlog_lock":
                    continue
                body = ast.dump(ast.Module(body=w.body, type_ignores=[]))
                if "read_text" in body and "write_text" in body:
                    found = True
                    break
            self.assertTrue(
                found,
                f"{rel}:{fn} 에서 백로그 읽기와 쓰기가 같은 backlog_lock 블록 안에 있지 않다 — "
                f"쓰기만 감싸면 read-modify-write 경합이 그대로 남는다")

    def test_read_and_write_inside_lock(self):
        import ast
        for rel, fn in self.CASES:
            path = AI_TEAM_ROOT / "skills" / rel
            src = path.read_text(encoding="utf-8")
            node = next((n for n in ast.walk(ast.parse(src))
                         if isinstance(n, ast.FunctionDef) and n.name == fn), None)
            self.assertIsNotNone(node, f"{rel}:{fn} 함수를 찾지 못함")
            body = [s for s in node.body
                    if not (isinstance(s, ast.Expr) and isinstance(s.value, ast.Constant)
                            and isinstance(s.value.value, str))]
            self.assertTrue(body, f"{rel}:{fn} 본문이 비었다")
            first = body[0]
            self.assertIsInstance(
                first, ast.With,
                f"{rel}:{fn} 의 첫 실행문이 with가 아니다 — 읽기가 락 밖에 있으면 "
                f"read-modify-write 경합이 그대로 남는다")
            call = first.items[0].context_expr
            name = getattr(getattr(call, "func", None), "id", None)
            self.assertEqual(name, "backlog_lock",
                             f"{rel}:{fn} 이 backlog_lock이 아닌 {name}으로 감싸져 있다")


if __name__ == "__main__":
    unittest.main()
