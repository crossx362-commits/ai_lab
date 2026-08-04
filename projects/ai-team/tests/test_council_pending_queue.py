"""동시 트리거된 두 번째 회의가 사라지지 않는가 (파이프라인감사_20260712_1 구현).

문제: convene()은 `ProcessLock("petnna_council")`을 썼고, 그 락은 충돌 시 sys.exit(0)한다.
회의 A(안건 X)가 6인 LLM 의견수집으로 수분간 락을 쥐고 있는 동안 회의 B(안건 Y)가
트리거되면 **B는 흔적도 없이 죽었다**. 트리거 쪽(봄이·백호·수리)은 Popen으로 띄우기만
하므로 그 소멸을 알 수 없고, 재소집 경로도 없었다 — 진짜 이슈가 조용히 방치된다.

수리: 락을 비치명적 advisory_lock으로 바꾸고, 못 잡으면 죽는 대신 대기열에 남긴다.
진행 중이던 회의가 끝나면서 **자기가 쥔 락 안에서** 대기열을 소진한다 —
새 데몬도, 크로스프로세스 조율도 필요 없다(2026-07-12에 그런 걸 만들었다가 폐기했다).

여기서 굳히는 것:
  1. 락을 못 잡으면 대기열에 남는다(죽지 않는다).
  2. 같은 안건은 큐에 중복으로 쌓이지 않는다.
  3. 회의가 끝나면 대기열을 FIFO로 소진한다.
  4. 인프라 실패로 못 연 안건은 **큐로 되돌아간다** — 큐에서만 빠지면 고치려던
     '흔적 없는 유실'이 그대로 재현된다.
  5. convene 어디에도 sys.exit을 부르는 ProcessLock이 남아 있지 않다.
"""
import importlib.util
import json
import sys
import unittest
from pathlib import Path
from unittest import mock

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_TEAM_ROOT))

_COUNCIL = AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools" / "petnna_council.py"


def _load(tmp: Path):
    spec = importlib.util.spec_from_file_location("council_q_t", _COUNCIL)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    mod.OUT_DIR = tmp
    mod.STATE = tmp / "state.json"
    mod.PENDING = tmp / "pending_topics.json"
    return mod


class PendingQueueTests(unittest.TestCase):
    def setUp(self):
        import tempfile
        self._tmp = tempfile.TemporaryDirectory()
        self.tmp = Path(self._tmp.name)
        self.mod = _load(self.tmp)
        self.held = []

    def tearDown(self):
        self._tmp.cleanup()

    def _queued(self):
        try:
            return json.loads(self.mod.PENDING.read_text(encoding="utf-8"))
        except Exception:
            return []

    def test_lock_conflict_queues_instead_of_dying(self):
        from contextlib import contextmanager

        @contextmanager
        def busy(name):
            yield False   # 다른 회의가 이미 진행 중

        with mock.patch.object(self.mod, "advisory_lock", busy), \
             mock.patch.object(self.mod, "_hold_meeting") as hold:
            self.mod.convene("안건 Y", "ctx", "P1")
        hold.assert_not_called()
        q = self._queued()
        self.assertEqual([i["topic"] for i in q], ["안건 Y"],
                         "락 충돌로 회의가 통째로 사라졌다 — 재소집 경로가 없다")

    def test_same_topic_not_queued_twice(self):
        self.mod._queue_topic("안건 Y", "", "P1")
        self.mod._queue_topic("안건 Y", "", "P1")
        self.assertEqual(len(self._queued()), 1, "같은 안건이 큐에 중복으로 쌓인다")

    def test_drains_queue_after_own_meeting(self):
        self.mod._queue_topic("대기1", "c1", "P2")
        self.mod._queue_topic("대기2", "c2", "P3")
        with mock.patch.object(self.mod, "_hold_meeting",
                               side_effect=lambda t, c, p: self.held.append(t) or True):
            self.mod.convene("본안건", "ctx", "P1")
        self.assertEqual(self.held, ["본안건", "대기1", "대기2"],
                         "대기 안건을 FIFO로 이어서 열지 않았다")
        self.assertEqual(self._queued(), [], "소진된 안건이 큐에 남았다")

    def test_infra_failure_returns_topic_to_queue(self):
        self.mod._queue_topic("대기1", "c1", "P2")
        # 본 회의는 성공, 대기 안건은 인프라 실패
        def hold(t, c, p):
            self.held.append(t)
            return t == "본안건"
        with mock.patch.object(self.mod, "_hold_meeting", side_effect=hold):
            self.mod.convene("본안건", "ctx", "P1")
        self.assertEqual([i["topic"] for i in self._queued()], ["대기1"],
                         "인프라 실패로 못 연 안건이 큐에서 사라졌다 — 아무도 다시 열지 않는다")

    def test_own_meeting_infra_failure_skips_drain(self):
        """클로드가 죽어 있으면 줄 선 안건까지 태우지 않는다."""
        self.mod._queue_topic("대기1", "c1", "P2")
        with mock.patch.object(self.mod, "_hold_meeting",
                               side_effect=lambda t, c, p: self.held.append(t) or False):
            self.mod.convene("본안건", "ctx", "P1")
        self.assertEqual(self.held, ["본안건"], "본 회의가 인프라로 실패했는데 대기열을 소진했다")
        self.assertEqual([i["topic"] for i in self._queued()], ["대기1"])

    def test_no_fatal_processlock_remains(self):
        """실행되는 코드에 ProcessLock이 남아 있으면 안 된다.

        (과거 사고를 설명하는 주석·독스트링의 'ProcessLock' 언급은 기록으로 남겨야 하므로
        문자열 검색이 아니라 AST에서 실제 이름 참조만 본다.)
        """
        import ast
        tree = ast.parse(_COUNCIL.read_text(encoding="utf-8"))
        names = {n.id for n in ast.walk(tree) if isinstance(n, ast.Name)}
        names |= {a.name for n in ast.walk(tree) if isinstance(n, ast.ImportFrom)
                  for a in n.names}
        self.assertNotIn("ProcessLock", names,
                         "충돌 시 sys.exit하는 ProcessLock이 코드에 남아 있다 — 회의가 다시 사라진다")


if __name__ == "__main__":
    unittest.main()
