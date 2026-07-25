"""백호 investigate_assigned_tasks() 재시도 상한 회귀 테스트 — 2026-07-11 2차 감사.

발견 경위: 배정 조사 과제가 실패하면(인프라 사유가 아니어도) attempts를 전혀
추적하지 않고 무조건 '다음 주기 재시도'만 해서, 구조적으로 실패하는 과제가
300초(데몬 주기)마다 영원히 재조사됐다 — 상한·보류 전환·에스컬레이션이 전무.
테오(_backlog_task_failed)와 동일한 계열의 결함이라 공용 헬퍼(_shared/backlog.py)로
통일해 고쳤다.
"""
import importlib.util
import io
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = AI_TEAM_ROOT / "skills" / "백호_백엔드" / "tools" / "petnna_backend_guard.py"


def load():
    spec = importlib.util.spec_from_file_location("baekho_under_test", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class InvestigateAssignedTasksFailureTests(unittest.TestCase):
    def setUp(self):
        self.baekho = load()
        self.tmp = Path(tempfile.mkdtemp()) / "backlog.json"

    def _write(self, items):
        self.tmp.write_text(json.dumps({"items": items}, ensure_ascii=False), encoding="utf-8")
        return mock.patch.object(self.baekho, "BACKLOG", self.tmp)

    def test_infra_failure_does_not_count_against_attempts(self):
        with self._write([{"id": "a", "title": "조사 과제", "owner": "백호", "status": "대기"}]), \
             mock.patch.object(self.baekho, "run_claude",
                              return_value=(False, "claude CLI 미발견 (PATH·표준 경로 모두 없음)")):
            self.baekho.investigate_assigned_tasks(do_send=False)
        item = json.loads(self.tmp.read_text(encoding="utf-8"))["items"][0]
        self.assertEqual(item["status"], "대기")
        self.assertNotIn("attempts", item, "인프라 실패는 시도 횟수에 반영되면 안 된다")

    def test_real_failure_accumulates_attempts_and_eventually_holds(self):
        """상한 없이 무한 재시도되던 결함 회귀 — 3회 진짜 실패 후 '보류'로 전환돼야 한다."""
        with self._write([{"id": "a", "title": "조사 과제", "owner": "백호", "status": "대기"}]), \
             mock.patch.object(self.baekho, "run_claude", return_value=(True, "")):
            # ok=True인데 out이 빈 문자열 → `not (ok and out)` 참 → 실패 경로
            for _ in range(self.baekho.TASK_MAX_ATTEMPTS):
                self.baekho.investigate_assigned_tasks(do_send=False)
        item = json.loads(self.tmp.read_text(encoding="utf-8"))["items"][0]
        self.assertEqual(item["attempts"], self.baekho.TASK_MAX_ATTEMPTS)
        self.assertEqual(item["status"], "보류", "상한 도달 시 무한 재조사 대신 보류로 전환해야 한다")

    def test_success_marks_done_and_stores_finding(self):
        # 2026-07-13 배칭 적용 이후 run_claude는 항목별 결론이 아니라 전체 과제를 담은
        # JSON 배열([{"id", "conclusion"}])을 한 번에 반환한다 — 모의 응답도 그 계약을 따라야 한다.
        batched = json.dumps([{"id": "a", "conclusion": "결론: 문제 없음"}], ensure_ascii=False)
        with self._write([{"id": "a", "title": "조사 과제", "owner": "백호", "status": "대기"}]), \
             mock.patch.object(self.baekho, "run_claude", return_value=(True, batched)):
            n = self.baekho.investigate_assigned_tasks(do_send=False)
        self.assertEqual(n, 1)
        item = json.loads(self.tmp.read_text(encoding="utf-8"))["items"][0]
        self.assertEqual(item["status"], "완료")
        self.assertIn("문제 없음", item["finding"])

    def test_only_own_waiting_tasks_are_picked(self):
        with self._write([
            {"id": "a", "title": "백호 과제", "owner": "백호", "status": "대기"},
            {"id": "b", "title": "다른 과제", "owner": "수리", "status": "대기"},
            {"id": "c", "title": "완료된 과제", "owner": "백호", "status": "완료"},
        ]), mock.patch.object(self.baekho, "run_claude", return_value=(True, "결론")) as rc:
            n = self.baekho.investigate_assigned_tasks(do_send=False)
        self.assertEqual(n, 1)
        rc.assert_called_once()

    def test_concurrent_write_during_processing_is_not_clobbered(self):
        """자동 파이프라인 감사 도구가 발견(2026-07-12): 예전엔 시작 시점 스냅샷을
        run_claude(최대 600s) 동안 들고 있다가 맨 마지막에 통째로 덮어써, 그 사이
        다른 에이전트가 backlog.json에 적재한 내용을 유실할 수 있었다. 이제는 과제
        처리 직후 파일을 다시 읽어 해당 항목만 갱신하므로, run_claude 호출 도중(실제로는
        그 반환 직후) 다른 프로세스가 추가한 항목이 살아남아야 한다."""
        with self._write([{"id": "a", "title": "조사 과제", "owner": "백호", "status": "대기"}]):
            def fake_run_claude(*args, **kwargs):
                # run_claude가 오래 걸리는 동안 다른 에이전트(예: 미오)가 새 항목을
                # 적재했다고 시뮬레이션 — investigate_assigned_tasks가 이 반환 직후
                # 파일을 다시 읽으므로 이 변경을 봐야 한다.
                data = json.loads(self.tmp.read_text(encoding="utf-8"))
                data["items"].append({"id": "concurrent", "title": "동시 적재된 항목",
                                      "owner": "미오", "status": "대기", "type": "디자인"})
                self.tmp.write_text(json.dumps(data, ensure_ascii=False), encoding="utf-8")
                # 2026-07-13 배칭 적용 이후 계약: JSON 배열([{"id", "conclusion"}]).
                return (True, json.dumps([{"id": "a", "conclusion": "결론: 문제 없음"}], ensure_ascii=False))

            with mock.patch.object(self.baekho, "run_claude", side_effect=fake_run_claude):
                self.baekho.investigate_assigned_tasks(do_send=False)

        items = {i["id"]: i for i in json.loads(self.tmp.read_text(encoding="utf-8"))["items"]}
        self.assertEqual(items["a"]["status"], "완료")
        self.assertIn("concurrent", items, "처리 도중 다른 에이전트가 적재한 항목이 유실되면 안 된다")


if __name__ == "__main__":
    unittest.main()


class LiveProbeBucketTests(unittest.TestCase):
    """버킷 부재 판정 오탐 회귀 — 2026-07-25.

    `GET storage/v1/bucket`(버킷 목록)은 anon 키에 권한이 없어 **오류가 아니라
    빈 배열 []을 HTTP 200으로** 돌려준다. 이걸 "버킷 0개"로 읽던 초판 프로브가
    멀쩡히 존재하는 petnna-media를 P1 '없음'으로 발행해, 오너를 Supabase 콘솔로
    보낼 뻔했다(실제 오탐). 이제 버킷별 object/list를 POST해 404/"not found"일
    때만 부재로 판정하고, 권한 문제(401/403)나 네트워크 오류는 침묵한다.
    """

    def setUp(self):
        self.baekho = load()

    def _probe_with(self, opener):
        """SUPABASE 자격증명과 urlopen을 갈아끼우고 _live_probe()의 버킷 소견만 뽑는다."""
        env = {"SUPABASE_URL": "https://example.supabase.co", "SUPABASE_ANON_KEY": "anon-key"}
        with mock.patch.dict("os.environ", env, clear=False), \
             mock.patch.object(self.baekho, "scan_frontend", return_value={"tables": set()}), \
             mock.patch("urllib.request.urlopen", side_effect=opener):
            found = self.baekho._live_probe()
        return [f for f in found if "버킷" in f["title"]]

    def test_permission_denied_listing_is_not_reported_as_missing(self):
        """존재하는 버킷: object/list가 200 → 소견 없음(빈 목록에 속지 않는다)."""
        class _Resp:
            def read(self): return b"[]"
            def __enter__(self): return self
            def __exit__(self, *a): return False
        self.assertEqual(self._probe_with(lambda *a, **kw: _Resp()), [])

    def test_missing_bucket_is_reported(self):
        """진짜 없는 버킷: object/list가 404 → P1 발행."""
        import urllib.error

        def opener(req, *a, **kw):
            raise urllib.error.HTTPError(
                req.full_url, 404, "Not Found", {}, io.BytesIO(b'{"message":"Bucket not found"}'))

        found = self._probe_with(opener)
        self.assertTrue(found, "실제로 없는 버킷인데 P1이 발행되지 않음")
        self.assertTrue(all(f["priority"] == "P1" for f in found))

    def test_auth_error_is_silent(self):
        """401/403은 권한 문제이지 부재 근거가 아니다 → 침묵(오탐 금지)."""
        import urllib.error

        def opener(req, *a, **kw):
            raise urllib.error.HTTPError(
                req.full_url, 401, "Unauthorized", {}, io.BytesIO(b'{"message":"unauthorized"}'))

        self.assertEqual(self._probe_with(opener), [])
