"""백로그 라우팅 회귀 테스트 — 2026-07-10 '아무도 안 집는 과제' 사고를 테스트로 굳힌다.

회의(council)가 액션아이템을 백호·미오·나무에게 배정했는데, 그 셋은 백로그를
적재만 하고 읽지 않는다. 결과: 대기 상태로 1.5일간 아무도 집지 않은 채 방치.
수리는 owner 불일치로 건너뛰고, 신선도 감사는 산출물만 보므로 경보도 없었다.
"""
import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_TEAM_ROOT))

COUNCIL_PATH = AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools" / "petnna_council.py"
TEO_PATH = AI_TEAM_ROOT / "skills" / "테오_테스트" / "tools" / "petnna_test_engineer.py"


def load(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


class CouncilOwnerRouting(unittest.TestCase):
    """소비자 없는 owner에게 배정된 과제는 대기로 남으면 안 된다."""

    def setUp(self):
        self.council = load("council_under_test", COUNCIL_PATH)

    def test_consumers_get_auto_track(self):
        for owner in ("", "수리", "테오"):
            self.assertFalse(self.council.needs_human("E2E 테스트 작성", owner),
                             f"{owner!r}는 백로그를 소비하므로 자동 트랙이어야 한다")

    def test_non_consumers_go_to_human_track(self):
        for owner in ("백호", "미오", "나무", "사람"):
            self.assertTrue(self.council.needs_human("시안 기준 명시", owner),
                            f"{owner!r}는 백로그를 읽지 않으므로 사람 트랙이어야 한다")

    def test_approval_tag_overrides_consumer_owner(self):
        self.assertTrue(self.council.needs_human("[승인필요] 스키마 변경", "수리"))


class TeoBacklogConsumption(unittest.TestCase):
    """테오는 자기에게 배정된 대기 중 테스트 과제를 집고, 채택 후에만 닫는다."""

    def setUp(self):
        self.teo = load("teo_under_test", TEO_PATH)
        self.tmp = Path(tempfile.mkdtemp()) / "backlog.json"

    def _write(self, items):
        self.tmp.write_text(json.dumps({"items": items}, ensure_ascii=False), encoding="utf-8")
        return mock.patch.object(self.teo, "BACKLOG", self.tmp)

    def test_picks_own_waiting_test_task(self):
        with self._write([
            {"id": "a", "owner": "수리", "status": "대기", "type": "테스트", "priority": "P1"},
            {"id": "b", "owner": "테오", "status": "완료", "type": "테스트", "priority": "P1"},
            {"id": "c", "owner": "테오", "status": "대기", "type": "디자인", "priority": "P1"},
            {"id": "d", "owner": "테오", "status": "대기", "type": "테스트", "priority": "P2"},
        ]):
            self.assertEqual(self.teo._backlog_task()["id"], "d")

    def test_priority_orders_the_pick(self):
        with self._write([
            {"id": "low", "owner": "테오", "status": "대기", "type": "테스트", "priority": "P3"},
            {"id": "high", "owner": "테오", "status": "대기", "type": "테스트", "priority": "P1"},
        ]):
            self.assertEqual(self.teo._backlog_task()["id"], "high")

    def test_none_when_nothing_assigned(self):
        with self._write([{"id": "a", "owner": "수리", "status": "대기", "type": "테스트"}]):
            self.assertIsNone(self.teo._backlog_task())

    def test_missing_backlog_is_not_fatal(self):
        with mock.patch.object(self.teo, "BACKLOG", self.tmp.parent / "없는파일.json"):
            self.assertIsNone(self.teo._backlog_task())

    def test_done_closes_only_the_named_task(self):
        with self._write([
            {"id": "d", "owner": "테오", "status": "대기", "type": "테스트"},
            {"id": "e", "owner": "테오", "status": "대기", "type": "테스트"},
        ]):
            self.teo._backlog_done("d")
            items = {i["id"]: i["status"] for i in json.loads(self.tmp.read_text(encoding="utf-8"))["items"]}
            self.assertEqual(items, {"d": "완료", "e": "대기"})


if __name__ == "__main__":
    unittest.main()
