"""touches_db_auth 게이트 — 코드 식별자 언급을 DB 접촉으로 오분류하지 않는다.

2026-07-27 사고: 3차 회의가 낸 프론트 수정 3건(피드 발행 재진입 가드, 체중 입력
검증, 오프라인 표기 정정)이 전부 자동 보류돼 수리가 하루 동안 못 집었다. 원인은
설명문이 `supabase.js:385`·`SupabaseService.isConnected`·`updatePetInSupabase`를
**인용**했다는 것뿐 — 셋 다 스키마·정책 무접촉 순수 프론트 수정이다.

맨몸 'supabase' 매칭은 "흔한 낱말을 넣으면 순수 과제까지 보류로 샌다"는 기존
가드레일(CLAUDE.md, 2026-07-10)을 그대로 재현한 것이다. 코드 식별자 형태를
제외하되, 진짜 DB 작업(테이블·스키마·RLS·마이그레이션)은 계속 잡아야 한다.
"""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from _shared.backlog import touches_db_auth  # noqa: E402


class DbAuthGateTests(unittest.TestCase):
    def test_code_identifiers_are_not_db_contact(self):
        """코드 식별자 인용은 보류 사유가 아니다 — 실제로 이 셋이 하루 묶였다."""
        for text in [
            "uploadPost(supabase.js:385)는 미연결이면 조용히 return한다",
            "SupabaseService.isConnected로 분기해 문구를 정직하게 바꾼다",
            "saveState + updatePetInSupabase로 DB 영속화되는 경로",
        ]:
            self.assertFalse(touches_db_auth(text), f"코드 식별자 언급이 보류로 샘: {text}")

    def test_real_db_work_is_still_caught(self):
        """진짜 DB 작업은 계속 잡아야 한다(가드를 느슨하게 만든 게 아니다).

        특히 'supabase.js 쿼리 추가'는 파일명이 들어가도 데이터 계층 작업이다 —
        제외 범위를 supabase.js 전체로 넓혔다가 기존 test_backlog_routing이
        이 반례로 잡아냈다. 구분 기준은 인용(`supabase.js:385`)이냐 행위냐다.
        """
        for text in [
            "supabase.js 쿼리 추가",
            "supabase 콘솔에서 신규 테이블 추가 필요",
            "supabase 스키마 변경",
            "RLS 정책을 적용해야 한다",
            "migrations/add_x.sql 마이그레이션 실행",
            "api_key 를 코드에 넣는다",
        ]:
            self.assertTrue(touches_db_auth(text), f"진짜 DB 작업을 놓침: {text}")

    def test_unrelated_task_is_untouched(self):
        self.assertFalse(touches_db_auth("카드 여백만 조정"))


if __name__ == "__main__":
    unittest.main()
