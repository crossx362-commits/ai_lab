"""백로그 과제는 P3 순증만으로 차단되지 않는다 (회의_202607290811_3 구현).

배경: 수리의 재검수 게이트는 `총 건수 <= 이전`을 요구했다. 그런데 봄이의 레이아웃
낭비 휴리스틱(2026-07-26 도입)은 **전부 P3이고 오탐 여지가 있어 보고 전용**이다.
새 카드·섹션을 추가하는 디자인/기능 과제는 그 P3가 거의 항상 몇 건 늘어, 멀쩡한
구현이 "지표 악화"로 3회 재시도 끝에 보류로 밀려났다.

회의 결정: 백로그 항목은 신규 P0~P2가 없으면 통과. 신규 P3는 차단 사유가 아니다.
QA 이슈 수정(is_backlog=False)은 기존 엄격한 총건수 규칙을 그대로 쓴다 —
자동 병합 대상이라 안전 여유가 없다.

함께 굳히는 것(회의_202607290811_2): 무엇이 새로 생겼는지 **실체**(우선순위·제목)를
로그에 남긴다. 카운트만으론 사람도 게이트도 판단할 수 없다.
"""
import importlib.util
import sys
import unittest
from pathlib import Path

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_TEAM_ROOT))

_ENGINE = AI_TEAM_ROOT / "skills" / "수리_개발자" / "tools" / "petnna_dev_engine.py"


def _load():
    spec = importlib.util.spec_from_file_location("dev_engine_p3_t", _ENGINE)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


_MOD = _load()          # 모듈 로드는 한 번만 — 단언마다 다시 읽을 이유가 없다


def _gate(base, after, is_backlog):
    return _MOD.qa_not_worse(base, after, is_backlog)


def _f(fid, pri, title="t"):
    return {"id": fid, "priority": pri, "title": title}


class BacklogP3GateTests(unittest.TestCase):
    """엔진의 실제 판정 함수(qa_not_worse)를 직접 호출한다 — 로직 복사본이 아니다."""

    def test_backlog_passes_with_new_p3_only(self):
        base = [_f("a", "P2")]
        after = [_f("a", "P2"), _f("new1", "P3"), _f("new2", "P3")]
        self.assertTrue(_gate(base, after, True),
                        "P3만 늘었는데 백로그 과제를 차단했다 — 멀쩡한 구현이 보류로 밀린다")

    def test_backlog_blocked_by_new_p2(self):
        base = [_f("a", "P3")]
        after = [_f("a", "P3"), _f("new", "P2")]
        self.assertFalse(_gate(base, after, True),
                         "신규 P2를 통과시켰다 — 완화가 과했다")

    def test_backlog_blocked_by_new_p1(self):
        base = []
        after = [_f("regress", "P1")]
        self.assertFalse(_gate(base, after, True))

    def test_backlog_blocked_by_any_p0(self):
        """P0는 신규가 아니어도(기존 잔존이어도) 절대 통과 금지."""
        base = [_f("boom", "P0")]
        after = [_f("boom", "P0")]
        self.assertFalse(_gate(base, after, True))

    def test_backlog_tolerates_preexisting_p2(self):
        """기존에 있던 P2는 이 과제 탓이 아니다 — 신규만 본다."""
        base = [_f("old", "P2")]
        after = [_f("old", "P2"), _f("n", "P3")]
        self.assertTrue(_gate(base, after, True),
                        "이전부터 있던 P2 때문에 무관한 과제가 영원히 막힌다")

    def test_qa_fix_keeps_strict_total_rule(self):
        """QA 이슈 수정은 자동 병합 대상 — 총건수 규칙을 완화하지 않는다."""
        base = [_f("a", "P2")]
        after = [_f("a", "P2"), _f("new", "P3")]
        self.assertFalse(_gate(base, after, False),
                         "자동 병합 경로의 총건수 규칙까지 풀렸다")

    def test_qa_fix_passes_when_resolved(self):
        base = [_f("a", "P2"), _f("b", "P3")]
        after = [_f("b", "P3")]
        self.assertTrue(_gate(base, after, False))


class NewFindingSubstanceLoggedTests(unittest.TestCase):
    """회의_202607290811_2 — 악화를 카운트로만 보고하면 사람도 게이트도 판단 못 한다."""

    def test_engine_logs_priority_and_title(self):
        src = _ENGINE.read_text(encoding="utf-8")
        self.assertIn("신규 {x['priority']}", src,
                      "신규 발견의 우선순위·제목을 로그에 남기지 않는다")
        self.assertIn("new_findings", src)

    def test_gate_is_a_single_named_function(self):
        """판정이 improve_one 안에 인라인으로 흩어져 있으면 예원 리뷰어와 어긋난다."""
        mod = _MOD
        self.assertTrue(callable(getattr(mod, "qa_not_worse", None)),
                        "게이트 판정이 공용 함수로 노출돼 있지 않다")


if __name__ == "__main__":
    unittest.main()
