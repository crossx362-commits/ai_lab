"""봄이 QA 순찰 — 위젯 계측 로그 오탐 방지 회귀 테스트.

2026-07-16: 케어위젯 노출/클릭 계측(회의_202607162027_3)이 AppLogger.addErrorLog
파이프라인을 재사용하면서 'widget_view'/'widget_click' 타입 로그가 생겼는데,
petnna_qa_patrol.py의 AppLogger.getErrorLogs() 흡수 루프는 원래 이걸 전부 오류로
간주해 P2 QA 이슈로 만들었다. 실사용 계측일 뿐 오류가 아니므로 제외해야 한다 —
반대로 진짜 오류 타입(global_error 등)까지 걸러지면 회귀를 못 잡으니 함께 검증한다.
"""
import importlib.util
import unittest
from pathlib import Path

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = AI_TEAM_ROOT / "skills" / "봄이_QA" / "tools" / "petnna_qa_patrol.py"


def load_patrol():
    spec = importlib.util.spec_from_file_location("bomi_qa_patrol_under_test", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class WidgetInstrumentationNotAnIssueTests(unittest.TestCase):
    def setUp(self):
        self.bomi = load_patrol()

    def test_widget_view_and_click_are_excluded(self):
        for t in ("widget_view", "widget_click"):
            self.assertIn(t, self.bomi.APP_LOG_NON_ISSUE_TYPES,
                          f"{t!r}는 QA 이슈가 아닌 사용 계측이라 제외 목록에 있어야 한다")

    def test_real_errors_are_not_excluded(self):
        for t in ("global_error", "global_rejection", "error", "warn"):
            self.assertNotIn(t, self.bomi.APP_LOG_NON_ISSUE_TYPES,
                              f"{t!r}까지 제외되면 진짜 오류 회귀를 못 잡는다")


if __name__ == "__main__":
    unittest.main()
