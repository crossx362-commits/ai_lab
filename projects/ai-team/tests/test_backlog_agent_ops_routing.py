"""에이전트 코드·운영 과제는 수리에게 배정되면 안 된다 (2026-08-06 자기참조 루프).

무슨 일이 있었나: 회의가 "diff_gate 크기 판정 개선"을 owner=수리로 적재했다. diff_gate는
projects/ai-team 코드라 수리 헌장(projects/petnna 한정) 밖이고, diff_gate 자신이 그 경로를
차단한다 — 그래서 **반드시** 3회 실패 후 보류로 갔다.

그러자 다음 회의가 "그 유령 과제를 종결하고 잔재 브랜치를 지워라"는 과제를 **또
owner=수리로** 적재했다. 그것도 상태 파일 정리라 수리가 못 한다. 이 세션이 실제로
수리 사이클을 돌려보니 클로드가 "이 백로그 항목은 petnna 기능이 아니라 잘못 분류됐다"고
답하고 빈손으로 끝났다 — 막힌 것을 치우라는 과제가 같은 이유로 막히는 자기참조 루프다.

여기서 굳히는 것:
  1. 에이전트 코드/운영 상태를 건드리는 과제는 structurally_blocked로 잡힌다
     (= 적재 시점에 사람 검토 트랙으로 간다).
  2. 순수 petnna 기능·디자인 과제는 절대 잡히지 않는다 — 오탐이 나면 자동 루프가 굶는다.
  3. 판정이 structurally_blocked 하나에 들어가 있다(needs_human·promote_approved_holds가
     같은 판정을 보게 — 사유를 두 곳에 나열하다 어긋난 사고가 이미 두 번 있었다).
"""
import sys
import unittest
from pathlib import Path

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(AI_TEAM_ROOT))

from _shared.backlog import (  # noqa: E402
    touches_agent_ops, structurally_blocked, needs_human,
)


class AgentOpsDetectionTests(unittest.TestCase):
    BLOCK = [
        ("diff_gate 크기 판정에 신규-단일-파일 예외 분류 추가", "diff_gate 개선안 설계"),
        ("유령 과제 회의_202608050716_1 완료 종결 + 잔재 브랜치 삭제", "dev_state 정리"),
        ("봄이 QA에 접이식 도달성 점검 추가", "petnna_qa_patrol interactive_checks 확장"),
        ("수리 게이트: 디자인 백로그는 P3 순증 단독 차단 금지", ""),
        ("백로그 상태 정규화", "backlog.json의 비어휘 상태 정리"),
        ("정시 잡 추가", "schedules.json에 등록 후 launchd 확인"),
        ("공용 모듈 정리", "_shared/ 아래 중복 함수 통합"),
    ]
    PASS = [
        ("건강 탭 「오늘의 기록」 중복 제목 통합", "templates/health.js 수정"),
        ("모바일 터치 타깃 확대", "css/style.css의 버튼 최소 44px"),
        ("데일리 케어 코인 & 미니 리워드 상점", "js/care-check.js에 코인 적립"),
        ("실종 모드(Lost Mode) + 이웃 제보", "lost-pets 화면 신설"),
        ("사료/간식 바코드 스캔", "카메라로 바코드 인식"),
        ("건강 탭 접이식 도달성 점검 카드", "사용자에게 보이는 카드"),
        ("산책 지도 위 툴팁 오버레이 가시성", "leaflet 오버레이 z-index"),
        ("비밀번호 표시 토글 버튼 추가", "로그인 폼 눈 아이콘"),
    ]

    def test_agent_ops_tasks_are_detected(self):
        for title, detail in self.BLOCK:
            self.assertTrue(touches_agent_ops(title, detail),
                            f"에이전트 운영 과제를 못 잡았다 — 수리가 3회 헛돈다: {title[:50]}")

    def test_petnna_feature_tasks_are_not_detected(self):
        for title, detail in self.PASS:
            self.assertFalse(touches_agent_ops(title, detail),
                             f"순수 petnna 과제를 막았다 — 자동 루프가 굶는다: {title[:50]}")

    def test_routed_through_structurally_blocked(self):
        """판정이 단일 소스에 들어가 있어야 needs_human·승격 로직이 같은 결론을 본다."""
        title, detail = self.BLOCK[0]
        self.assertTrue(structurally_blocked("수리", "기능", title, detail),
                        "structurally_blocked가 에이전트 운영 과제를 통과시킨다")
        self.assertTrue(needs_human(title, "수리", detail, "기능"),
                        "적재 시점에 사람 트랙으로 안 보낸다")

    def test_normal_task_still_routes_to_suri(self):
        title, detail = self.PASS[0]
        self.assertFalse(structurally_blocked("수리", "기능", title, detail),
                         "정상 과제까지 사람 트랙으로 보냈다")


if __name__ == "__main__":
    unittest.main()
