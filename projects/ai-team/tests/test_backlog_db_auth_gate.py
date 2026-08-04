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


class DbAuthStrongWeakTests(unittest.TestCase):
    """강/약 신호 분리 회귀 — 2026-08-04.

    순수 키워드 매칭이던 시절, DB를 안 건드린다고 **명시한** 과제가 그 단어 자체에 걸려
    영구 보류로 갔다. 실제 사례 2건:
      · "localStorage 로컬 완결, Supabase 동기화 경로 미접촉" → 'Supabase'에 걸림
      · "기존 저장펫은 빈 배열 폴백 마이그레이션" → 로컬 자료구조 변경인데 '마이그레이션'에 걸림

    반대 방향(지나친 제외)이 더 위험하다 — 진짜 DB 작업이 자동 루프로 새면 수리가
    병합할 수 없는 브랜치를 만들고 시도만 소진한다. 그래서 강 신호(신규 테이블·스키마
    변경·RLS·시크릿)는 부정문이 있어도 그대로 막는다.
    """

    def test_explicit_no_contact_clears_weak_signal(self):
        self.assertFalse(touches_db_auth(
            "궁합 히스토리 (localStorage 추이)",
            "빈 배열 폴백 마이그레이션. localStorage 로컬 완결, Supabase 동기화 경로 미접촉."))
        self.assertFalse(touches_db_auth(
            "localStorage 마이그레이션", "기존 데이터 모양 변경, 서버 연동 없음"))

    def test_strong_signal_survives_negation(self):
        """부정문으로 강 신호를 무력화할 수 있으면 게이트가 통째로 뚫린다."""
        self.assertTrue(touches_db_auth("실종 모드", "posts 스키마 변경 필요. DB 미접촉."))
        self.assertTrue(touches_db_auth("예약", "신규 테이블과 RLS 필요. supabase 미접촉."))
        self.assertTrue(touches_db_auth("점검", "api_key 하드코딩 확인. DB 미접촉."))

    def test_weak_signal_without_negation_still_blocks(self):
        """부정 선언이 없으면 supabase 언급만으로도 사람 검토로 보낸다(기존 동작 유지)."""
        self.assertTrue(touches_db_auth("가족 공동 돌봄", "Supabase를 활용하여 실시간 공유"))
        self.assertTrue(touches_db_auth("supabase.js 쿼리 추가", "medical_records 조회 추가"))

    def test_no_signal_is_not_blocked(self):
        self.assertFalse(touches_db_auth("무해한 UI", "버튼 정렬만 조정"))
