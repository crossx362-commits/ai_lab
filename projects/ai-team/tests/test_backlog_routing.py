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
MIO_PATH = AI_TEAM_ROOT / "skills" / "미오_디자인" / "tools" / "petnna_design_review.py"


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
        for owner in ("", "수리", "테오", "미오", "백호"):
            self.assertFalse(self.council.needs_human("카드 여백 개선", owner),
                             f"{owner!r}는 백로그를 소비하므로 자동 트랙이어야 한다")

    def test_non_consumers_go_to_human_track(self):
        for owner in ("나무", "사람"):
            self.assertTrue(self.council.needs_human("시안 기준 명시", owner),
                            f"{owner!r}는 백로그를 읽지 않으므로 사람 트랙이어야 한다")

    def test_every_auto_owner_actually_has_consume_code(self):
        """AUTO_OWNERS에 이름만 올리는 실수를 막는다 — 소비 함수가 실재해야 한다.

        (2026-07-10: `grep -c backlog`로 소비 여부를 눈대중해 백호를 오분류했다.
        백호는 상수명이 대문자 BACKLOG라 잡히지 않았을 뿐 완전한 소비자였다.)
        """
        consumers = {
            "수리": (AI_TEAM_ROOT / "skills/수리_개발자/tools/petnna_dev_engine.py", "select_backlog"),
            "테오": (AI_TEAM_ROOT / "skills/테오_테스트/tools/petnna_test_engineer.py", "_backlog_task"),
            "미오": (AI_TEAM_ROOT / "skills/미오_디자인/tools/petnna_design_review.py", "_assigned_tasks"),
            "백호": (AI_TEAM_ROOT / "skills/백호_백엔드/tools/petnna_backend_guard.py",
                    "investigate_assigned_tasks"),
        }
        for owner in self.council.AUTO_OWNERS:
            if owner == "":
                continue
            self.assertIn(owner, consumers, f"{owner}가 AUTO_OWNERS인데 소비 함수가 등록돼 있지 않다")
            path, func = consumers[owner]
            src = path.read_text(encoding="utf-8")
            self.assertIn(f"def {func}", src, f"{owner}의 소비 함수 {func}가 사라졌다")
            self.assertIn("BACKLOG", src, f"{owner}가 백로그를 읽지 않는다")

    def test_approval_tag_overrides_consumer_owner(self):
        self.assertTrue(self.council.needs_human("[승인필요] 카드 여백 개선", "수리"))

    def test_db_auth_task_never_enters_auto_loop(self):
        """수리는 supabase·migration diff를 병합할 수 없다 — 3회 실패 낭비 방지."""
        self.assertTrue(self.council.needs_human(
            "QR 미아방지 공개 프로필", "수리", "public_pet_profiles 신규 테이블 + RLS 정책 추가"))


class DbAuthGate(unittest.TestCase):
    """적재 시점 DB/인증 판별 — 범위는 회의가 명시한 것으로 좁힌다(오탐 금지)."""

    def setUp(self):
        from _shared.backlog import touches_db_auth
        self.touches = touches_db_auth

    def test_flags_db_and_auth_work(self):
        for text in ("supabase.js 쿼리 추가", "migrations/ 스키마 변경", "RLS 정책 손질",
                     "신규 테이블 medical_records", "api_key 회전"):
            self.assertTrue(self.touches(text), f"{text!r}는 수리가 병합 못 한다")

    def test_does_not_flag_plain_ui_work(self):
        for text in ("로그인 화면 여백 개선", "카드 타이포 계층 강화",
                     "소셜 버튼 아이콘 정렬", "인증 완료 토스트 색상"):
            self.assertFalse(self.touches(text), f"{text!r}는 순수 UI인데 보류로 새어나갔다")

    def test_detail_is_searched_too(self):
        self.assertTrue(self.touches("건강수첩", "supabase에 medical_records 테이블 추가"))


class MioBacklogConsumption(unittest.TestCase):
    """미오는 배정된 디자인 과제를 리뷰 지침으로 집고, 산출물을 남긴 뒤에만 닫는다."""

    def setUp(self):
        self.mio = load("mio_under_test", MIO_PATH)
        self.tmp = Path(tempfile.mkdtemp()) / "backlog.json"

    def _write(self, items):
        self.tmp.write_text(json.dumps({"items": items}, ensure_ascii=False), encoding="utf-8")
        return mock.patch.object(self.mio, "BACKLOG", self.tmp)

    def test_picks_only_own_waiting_design_tasks(self):
        with self._write([
            {"id": "a", "owner": "미오", "status": "대기", "type": "디자인"},
            {"id": "b", "owner": "미오", "status": "보류", "type": "디자인"},
            {"id": "c", "owner": "테오", "status": "대기", "type": "디자인"},
            {"id": "d", "owner": "미오", "status": "대기", "type": "기획"},
        ]):
            self.assertEqual([t["id"] for t in self.mio._assigned_tasks()], ["a"])

    def test_close_marks_only_given_ids(self):
        with self._write([
            {"id": "a", "owner": "미오", "status": "대기", "type": "디자인"},
            {"id": "b", "owner": "미오", "status": "대기", "type": "디자인"},
        ]):
            self.mio._close_tasks(["a"])
            got = {i["id"]: i["status"] for i in json.loads(self.tmp.read_text(encoding="utf-8"))["items"]}
            self.assertEqual(got, {"a": "완료", "b": "대기"})


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
