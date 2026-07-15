"""백로그 라우팅 회귀 테스트 — 2026-07-10 '아무도 안 집는 과제' 사고를 테스트로 굳힌다.

회의(council)가 액션아이템을 백호·미오·나무에게 배정했는데, 그 셋은 백로그를
적재만 하고 읽지 않는다. 결과: 대기 상태로 1.5일간 아무도 집지 않은 채 방치.
수리는 owner 불일치로 건너뛰고, 신선도 감사는 산출물만 보므로 경보도 없었다.
"""
import importlib.util
import json
import os
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
NAMU_PATH = AI_TEAM_ROOT / "skills" / "나무_기획" / "tools" / "petnna_product_manager.py"


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

    def test_owner_type_mismatch_goes_to_human_track(self):
        """자동 파이프라인 감사 도구가 발견(2026-07-11): 테오 _backlog_task()는
        type=='테스트'만, 미오 _assigned_tasks()는 type=='디자인'만 본다 — owner는
        소비자가 있어도 type이 안 맞으면 '대기'로 적재해도 아무도 못 집는 좀비가 된다."""
        self.assertTrue(self.council.needs_human("이상한 배정", "테오", "", "기획"),
                        "테오에게 type=기획 배정은 테오가 못 집으므로 사람 트랙이어야 한다")
        self.assertTrue(self.council.needs_human("이상한 배정", "미오", "", "백엔드"),
                        "미오에게 type=백엔드 배정은 미오가 못 집으므로 사람 트랙이어야 한다")

    def test_owner_type_match_stays_auto(self):
        self.assertFalse(self.council.needs_human("정상 배정", "테오", "", "테스트"))
        self.assertFalse(self.council.needs_human("정상 배정", "미오", "", "디자인"))

    def test_missing_type_does_not_block_backward_compat(self):
        """item_type을 안 넘기는 기존 호출부·테스트는 그대로 동작해야 한다(하위호환)."""
        self.assertFalse(self.council.needs_human("타입 모름", "테오"))


class OwnerTypeMismatchTests(unittest.TestCase):
    """자동 파이프라인 감사 도구가 발견(2026-07-11): owner+type 조합이 실제 소비 함수의
    필터와 안 맞으면 '대기'로 적재돼도 아무도 못 집는 좀비가 된다."""

    def setUp(self):
        from _shared.backlog import owner_type_mismatch
        self.mismatch = owner_type_mismatch

    def test_teo_only_consumes_test_type(self):
        self.assertTrue(self.mismatch("테오", "기획"))
        self.assertFalse(self.mismatch("테오", "테스트"))

    def test_mio_only_consumes_design_type(self):
        self.assertTrue(self.mismatch("미오", "백엔드"))
        self.assertFalse(self.mismatch("미오", "디자인"))

    def test_owners_without_type_restriction_never_mismatch(self):
        for owner in ("백호", "수리", ""):
            self.assertFalse(self.mismatch(owner, "아무거나"))

    def test_empty_type_never_mismatch(self):
        self.assertFalse(self.mismatch("테오", ""))


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

    def test_add_backlog_items_id_includes_seconds_not_just_date(self):
        """자동 파이프라인 감사 도구가 발견(2026-07-12): id가 날짜까지만 있어서(시각 없이)
        같은 날 review()가 두 번 불리면(배정과제 폴링+정기리뷰가 겹치는 경우, 2026-07-11
        폴링 도입으로 실현 가능해짐) 서로 다른 두 항목이 같은 id를 가질 수 있었다 —
        id로 조회하는 수리 dev_state의 attempts 오집계 위험. council과 같은 정밀도로 통일."""
        with self._write([]):
            self.mio.add_backlog_items(
                [{"title": "제안 A", "detail": "d", "priority": "P2"}], source="미오", itype="디자인")
            self.mio.add_backlog_items(
                [{"title": "제안 B", "detail": "d", "priority": "P2"}], source="미오", itype="디자인")
            items = json.loads(self.tmp.read_text(encoding="utf-8"))["items"]
        ids = [i["id"] for i in items]
        self.assertEqual(len(ids), len(set(ids)), f"두 번의 적재가 id를 공유하면 안 된다: {ids}")
        for i in ids:
            # source_YYYYMMDDHHMMSS_added 형태 — 초 단위까지 포함해야 한다
            self.assertRegex(i, r"^미오_\d{14}_\d+$", f"id에 초 단위 시각이 빠져 있다: {i}")

    def test_real_review_failure_counts_against_assigned_task_attempts(self):
        """자동 파이프라인 감사 도구가 발견(2026-07-11): 미오는 테오·백호와 달리 배정
        과제 실패를 attempts에 전혀 반영하지 않아 상한·보류 전환이 없었다 — 대칭 맞춤."""
        with self._write([
            {"id": "a", "title": "배정 과제", "owner": "미오", "status": "대기", "type": "디자인", "attempts": 2},
        ]), \
             mock.patch.object(self.mio, "take_screenshots", return_value=[]), \
             mock.patch.object(self.mio, "run_claude", return_value=(True, "이해가 안 갑니다")), \
             mock.patch.object(self.mio, "llm_text", return_value="이것도 JSON이 아님"):
            self.mio.review(do_send=False)
        item = json.loads(self.tmp.read_text(encoding="utf-8"))["items"][0]
        self.assertEqual(item["attempts"], 3)
        self.assertEqual(item["status"], "보류", "3회 실패 후 보류로 전환돼야 한다")

    def test_infra_failure_does_not_count_against_assigned_task_attempts(self):
        with self._write([
            {"id": "a", "title": "배정 과제", "owner": "미오", "status": "대기", "type": "디자인"},
        ]), \
             mock.patch.object(self.mio, "take_screenshots", return_value=[]), \
             mock.patch.object(self.mio, "run_claude",
                              return_value=(False, "claude CLI 미발견 (PATH·표준 경로 모두 없음)")):
            self.mio.review(do_send=False)
        item = json.loads(self.tmp.read_text(encoding="utf-8"))["items"][0]
        self.assertNotIn("attempts", item, "인프라 실패는 시도 횟수에 반영되면 안 된다")


class NamuBacklogWriteTests(unittest.TestCase):
    """나무 add_backlog_items()의 id 충돌 방지 — 자동 파이프라인 감사 도구가 발견
    (2026-07-12): 미오에만 적용된 수정이 나무엔 누락된 비대칭이었다."""

    def setUp(self):
        self.namu = load("namu_under_test", NAMU_PATH)
        self.tmp = Path(tempfile.mkdtemp()) / "backlog.json"

    def _write(self, items):
        self.tmp.write_text(json.dumps({"items": items}, ensure_ascii=False), encoding="utf-8")
        return mock.patch.object(self.namu, "BACKLOG", self.tmp)

    def test_two_calls_never_produce_colliding_ids(self):
        with self._write([]):
            self.namu.add_backlog_items(
                [{"title": "제안 A", "detail": "d", "priority": "P2"}], source="나무", itype="기획")
            self.namu.add_backlog_items(
                [{"title": "제안 B", "detail": "d", "priority": "P2"}], source="나무", itype="기획")
            items = json.loads(self.tmp.read_text(encoding="utf-8"))["items"]
        ids = [i["id"] for i in items]
        self.assertEqual(len(ids), len(set(ids)), f"두 번의 적재가 id를 공유하면 안 된다: {ids}")


class BacklogReadFailureSafetyTests(unittest.TestCase):
    """backlog.json 읽기가 실패하면(다른 프로세스의 non-atomic write 도중 읽었을 가능성)
    빈 dict로 대체해 통째로 덮어쓰면 안 된다 — 자동 파이프라인 감사 도구가 발견
    (2026-07-12): 미오·나무·회의 셋 다 이 함정이 있었다. 미오·나무는 add_backlog_items()가
    write를 건너뛰고 return 0, 회의는 액션아이템 적재만 건너뛰고 회의록·텔레그램은 계속."""

    def _corrupt(self, tmp):
        tmp.write_text("{not valid json", encoding="utf-8")  # 실제 backlog.json 존재 + 파싱 실패 재현

    def test_mio_skips_write_on_corrupt_backlog(self):
        mio = load("mio_corrupt_under_test", MIO_PATH)
        tmp = Path(tempfile.mkdtemp()) / "backlog.json"
        self._corrupt(tmp)
        with mock.patch.object(mio, "BACKLOG", tmp):
            added = mio.add_backlog_items(
                [{"title": "새 제안", "detail": "d", "priority": "P2"}], source="미오", itype="디자인")
        self.assertEqual(added, 0)
        self.assertEqual(tmp.read_text(encoding="utf-8"), "{not valid json",
                         "파싱 실패 시 기존(손상됐더라도) 파일을 덮어쓰면 안 된다")

    def test_namu_skips_write_on_corrupt_backlog(self):
        namu = load("namu_corrupt_under_test", NAMU_PATH)
        tmp = Path(tempfile.mkdtemp()) / "backlog.json"
        self._corrupt(tmp)
        with mock.patch.object(namu, "BACKLOG", tmp):
            added = namu.add_backlog_items(
                [{"title": "새 제안", "detail": "d", "priority": "P2"}], source="나무", itype="기획")
        self.assertEqual(added, 0)
        self.assertEqual(tmp.read_text(encoding="utf-8"), "{not valid json")

    def test_missing_file_still_starts_fresh(self):
        """파일이 아예 없는 건(첫 실행) 손상과 다르다 — 빈 백로그로 정상 시작해야 한다."""
        mio = load("mio_missing_under_test", MIO_PATH)
        tmp = Path(tempfile.mkdtemp()) / "backlog.json"  # 생성 안 함 — 없는 파일
        with mock.patch.object(mio, "BACKLOG", tmp):
            added = mio.add_backlog_items(
                [{"title": "첫 제안", "detail": "d", "priority": "P2"}], source="미오", itype="디자인")
        self.assertEqual(added, 1)
        self.assertTrue(tmp.exists())


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

    def test_failed_task_accumulates_attempts_and_stays_waiting_under_cap(self):
        """자동 파이프라인 감사 도구가 발견(2026-07-11): 상한 없이 무한 재시도되던 결함
        회귀 — 상한 미만이면 계속 재시도할 수 있게 '대기' 상태를 유지해야 한다."""
        with self._write([{"id": "a", "owner": "테오", "status": "대기", "type": "테스트"}]):
            self.teo._backlog_task_failed("a")
            item = json.loads(self.tmp.read_text(encoding="utf-8"))["items"][0]
            self.assertEqual(item["attempts"], 1)
            self.assertEqual(item["status"], "대기", "상한 미만이면 계속 재시도 가능해야 한다")

    def test_task_held_after_max_attempts(self):
        with self._write([{"id": "a", "owner": "테오", "status": "대기", "type": "테스트",
                          "attempts": self.teo.TASK_MAX_ATTEMPTS - 1}]):
            self.teo._backlog_task_failed("a")
            item = json.loads(self.tmp.read_text(encoding="utf-8"))["items"][0]
            self.assertEqual(item["attempts"], self.teo.TASK_MAX_ATTEMPTS)
            self.assertEqual(item["status"], "보류",
                             "상한 도달 시 무한 재시도 대신 보류로 전환해야 한다")


class PromoteApprovedHoldsTests(unittest.TestCase):
    """오너 승인된 보류 항목 자동 승격 — 2026-07-11 '승격해도 여전히 좀비' 사고를 굳힌다.

    council이 '보류'로 라우팅하는 이유는 셋(승인필요·owner 불일치·DB/인증)인데 `gate`
    필드는 DB/인증일 때만 붙는다. owner 불일치(나무처럼 백로그를 안 읽는 owner)로
    보류된 항목은 gate가 없어 승인만 받으면 '대기'로 승격됐지만, owner가 여전히
    소비자 없는 값이라 아무도 안 집는 좀비 상태가 됐다 — 방치를 모양만 바꾼 것.
    """

    def setUp(self):
        from _shared.backlog import promote_approved_holds
        self.promote = promote_approved_holds
        self.tmp = Path(tempfile.mkdtemp()) / "backlog.json"

    def _write(self, items):
        self.tmp.write_text(json.dumps({"items": items}, ensure_ascii=False), encoding="utf-8")

    def test_owner_without_consumer_is_never_promoted(self):
        self._write([{"id": "a", "title": "나무과제", "detail": "순수 UI",
                     "status": "보류", "owner": "나무", "approved_by": "오너"}])
        promoted = self.promote(self.tmp)
        self.assertEqual(promoted, [])
        item = json.loads(self.tmp.read_text(encoding="utf-8"))["items"][0]
        self.assertEqual(item["status"], "보류", "소비자 없는 owner는 승격해도 좀비 상태 — 보류 유지가 맞다")

    def test_consumer_owner_is_promoted(self):
        for owner in ("수리", "테오", "미오", "백호", ""):
            with self.subTest(owner=owner):
                self._write([{"id": "x", "title": "과제", "detail": "순수 UI",
                             "status": "보류", "owner": owner, "approved_by": "오너"}])
                promoted = self.promote(self.tmp)
                self.assertEqual(promoted, ["x"], f"owner={owner!r}는 승격돼야 한다")

    def test_hard_gate_blocks_promotion_regardless_of_approval(self):
        self._write([{"id": "b", "title": "DB트리거", "detail": "SQL 콘솔 실행 필요",
                     "status": "보류", "owner": "사람", "gate": "DB/인증", "approved_by": "오너"}])
        self.assertEqual(self.promote(self.tmp), [])

    def test_db_auth_content_still_blocks_even_with_valid_owner(self):
        self._write([{"id": "c", "title": "웹푸시", "detail": "Supabase reminders 테이블 필요",
                     "status": "보류", "owner": "수리", "approved_by": "오너"}])
        self.assertEqual(self.promote(self.tmp), [])

    def test_owner_type_mismatch_never_promoted_even_with_approval(self):
        """자동 파이프라인 감사 도구가 2번째로 발견(2026-07-11): needs_human()에
        owner_type_mismatch 사유를 추가했을 때 이 함수를 안 고치면, owner는 소비자가
        있어도(테오) type이 안 맞는(디자인) 항목이 승인만 받으면 '대기'로 승격되지만
        여전히 테오 _backlog_task()가 못 집는 좀비가 된다 — owner-불일치 사고의 재발."""
        self._write([{"id": "d", "title": "이상 배정", "type": "디자인",
                     "status": "보류", "owner": "테오", "approved_by": "오너"}])
        promoted = self.promote(self.tmp)
        self.assertEqual(promoted, [])
        item = json.loads(self.tmp.read_text(encoding="utf-8"))["items"][0]
        self.assertEqual(item["status"], "보류",
                         "owner+type 불일치는 승인해도 아무도 못 집는 좀비 — 보류 유지가 맞다")

    def test_owner_type_match_is_promoted(self):
        self._write([{"id": "e", "title": "정상 배정", "type": "테스트",
                     "status": "보류", "owner": "테오", "approved_by": "오너"}])
        self.assertEqual(self.promote(self.tmp), ["e"])


class TaskFailureTrackingTests(unittest.TestCase):
    """배정 과제 재시도 상한 공용 헬퍼 — 2026-07-11 2차 감사(테오에 이어 백호도 같은
    문제)로 _shared/backlog.py에 단일화. 새 에이전트가 배정 과제를 소비할 때
    각자 재구현하지 말고 이 함수들을 재사용해야 또 어긋나지 않는다."""

    def setUp(self):
        from _shared.backlog import is_infra_failure, apply_task_failure, record_backlog_task_failure
        self.is_infra = is_infra_failure
        self.apply_failure = apply_task_failure
        self.record_failure = record_backlog_task_failure
        self.tmp = Path(tempfile.mkdtemp()) / "backlog.json"

    def test_infra_keywords_detected(self):
        for text in ("claude CLI 미발견 (PATH·표준 경로 모두 없음)", "claude -p 타임아웃(600s)",
                     "429 Too Many Requests", "rate limit exceeded"):
            self.assertTrue(self.is_infra(text), f"{text!r}는 인프라 실패로 인식돼야 한다")

    def test_non_infra_text_not_flagged(self):
        self.assertFalse(self.is_infra("이해가 안 갑니다, 다시 설명해주세요"))
        self.assertFalse(self.is_infra(""))
        self.assertFalse(self.is_infra(None))

    def test_apply_task_failure_is_in_place_no_io(self):
        item = {"id": "x", "owner": "백호", "status": "대기"}
        self.apply_failure(item, max_attempts=3)
        self.assertEqual(item["attempts"], 1)
        self.assertEqual(item["status"], "대기", "상한 미만이면 대기 유지")

    def test_apply_task_failure_holds_at_cap(self):
        item = {"id": "x", "owner": "백호", "status": "대기", "attempts": 2}
        self.apply_failure(item, max_attempts=3)
        self.assertEqual(item["attempts"], 3)
        self.assertEqual(item["status"], "보류")
        self.assertIn("백호", item["gate"])

    def test_record_backlog_task_failure_persists_to_file(self):
        self.tmp.write_text(json.dumps({"items": [
            {"id": "a", "owner": "테오", "status": "대기", "attempts": 2}]}), encoding="utf-8")
        self.record_failure(self.tmp, "a", max_attempts=3)
        item = json.loads(self.tmp.read_text(encoding="utf-8"))["items"][0]
        self.assertEqual(item["attempts"], 3)
        self.assertEqual(item["status"], "보류")

    def test_suri_uses_shared_infra_keyword_check_not_hardcoded_copy(self):
        """자동 파이프라인 감사 도구가 발견(2026-07-11): 수리가 인프라 실패 키워드 목록을
        _shared/backlog.is_infra_failure 대신 자기 파일에 하드코딩 중복해뒀었다 — 목록이
        갱신되면 수리만 조용히 안 따라가는 어긋남(AUTO_OWNERS·needs_human과 같은 계열)."""
        src = (AI_TEAM_ROOT / "skills" / "수리_개발자" / "tools" / "petnna_dev_engine.py").read_text(encoding="utf-8")
        self.assertIn("is_infra_failure", src, "수리는 공용 is_infra_failure를 import해서 써야 한다")
        self.assertNotIn('"미발견", "타임아웃"', src,
                         "인프라 키워드를 여기 하드코딩하면 _shared/backlog.py와 어긋날 수 있다")


class RecentDecisionsTests(unittest.TestCase):
    """디자인 진자 방지(2026-07-14) — 미오·예원이 최근 결정을 참고하는 공용 헬퍼."""

    def setUp(self):
        from _shared.backlog import recent_reviewed_items, format_recent_decisions
        self.recent = recent_reviewed_items
        self.fmt = format_recent_decisions
        # mkstemp는 열린 fd를 돌려준다 — 닫지 않으면 Windows에서 unlink이
        # WinError 32(사용 중)로 실패한다(맥에서는 통과해 눈에 안 띄던 버그).
        fd, name = tempfile.mkstemp(suffix=".json")
        os.close(fd)
        self.tmp = Path(name)
        self.addCleanup(self.tmp.unlink)

    def _write(self, items):
        self.tmp.write_text(json.dumps({"items": items}), encoding="utf-8")

    def test_filters_to_completed_and_held_only(self):
        self._write([
            {"id": "a", "status": "완료", "updated": "2026-07-14T10:00:00"},
            {"id": "b", "status": "보류", "updated": "2026-07-14T09:00:00"},
            {"id": "c", "status": "대기", "updated": "2026-07-14T11:00:00"},
            {"id": "d", "status": "PR대기", "updated": "2026-07-14T12:00:00"},
        ])
        ids = [it["id"] for it in self.recent(self.tmp)]
        self.assertEqual(set(ids), {"a", "b"})

    def test_sorted_newest_first(self):
        self._write([
            {"id": "old", "status": "완료", "updated": "2026-07-10T00:00:00"},
            {"id": "new", "status": "완료", "updated": "2026-07-14T00:00:00"},
        ])
        ids = [it["id"] for it in self.recent(self.tmp)]
        self.assertEqual(ids, ["new", "old"])

    def test_type_filter(self):
        self._write([
            {"id": "d1", "type": "디자인", "status": "완료", "updated": "2026-07-14T00:00:00"},
            {"id": "p1", "type": "기획", "status": "완료", "updated": "2026-07-14T00:00:00"},
        ])
        ids = [it["id"] for it in self.recent(self.tmp, item_type="디자인")]
        self.assertEqual(ids, ["d1"])

    def test_excludes_self(self):
        self._write([
            {"id": "self", "status": "완료", "updated": "2026-07-14T00:00:00"},
            {"id": "other", "status": "완료", "updated": "2026-07-13T00:00:00"},
        ])
        ids = [it["id"] for it in self.recent(self.tmp, exclude_id="self")]
        self.assertEqual(ids, ["other"])

    def test_missing_backlog_returns_empty_not_fatal(self):
        self.assertEqual(self.recent(self.tmp.parent / "does-not-exist.json"), [])

    def test_format_includes_reason_and_status(self):
        block = self.fmt([{"title": "회원가입 버튼 통합", "status": "완료",
                            "review_reason": "레이아웃 파손 없이 개선"}])
        self.assertIn("완료", block)
        self.assertIn("회원가입 버튼 통합", block)
        self.assertIn("레이아웃 파손 없이 개선", block)

    def test_format_empty_list_returns_empty_string(self):
        self.assertEqual(self.fmt([]), "")

    def test_yewon_reviewer_passes_type_and_excludes_self(self):
        """예원의 _ask_yewon 호출부가 item_type·item_id를 실제로 넘기는지 — 안 넘기면
        recent_reviewed_items가 전체 미필터 결과를 주거나 자기 자신과 비교하게 된다."""
        src = (AI_TEAM_ROOT / "skills" / "예원_CEO" / "tools" / "petnna_pr_reviewer.py").read_text(encoding="utf-8")
        self.assertIn("recent_reviewed_items", src)
        self.assertIn('item_type=it.get("type"', src)
        self.assertIn("item_id=fp", src)

    def test_mio_review_passes_recent_decisions_to_prompt(self):
        """미오가 리뷰 생성 시 최근 디자인 결정을 실제로 프롬프트에 넣는지."""
        src = (AI_TEAM_ROOT / "skills" / "미오_디자인" / "tools" / "petnna_design_review.py").read_text(encoding="utf-8")
        self.assertIn("recent_reviewed_items", src)
        self.assertIn('item_type="디자인"', src)


if __name__ == "__main__":
    unittest.main()
