import json
import shlex
import unittest
from pathlib import Path

AI_TEAM = Path(__file__).resolve().parents[1]
SCHEDULES = AI_TEAM / "skills" / "영숙_비서" / "tools" / "schedules.json"


class ScheduleCommandArgvSafetyTests(unittest.TestCase):
    """schedule_sync._program_args()는 macOS launchd로 변환할 때 command 문자열을
    quote 없이 공백으로만 split한다(schedule_manager.py의 Windows 폴백은 shlex를 쓰지만
    이건 아니다). 그래서 command에 여러 단어짜리 값(예: --topic "여러 단어 안건")을 직접
    넣으면 launchd ProgramArguments가 단어 단위로 쪼개져 실제 인자와 달라진다.

    2026-08-04: petnna_daily_ideation.py를 설계하며 이 함정을 미리 피해 안건 텍스트를
    코드에 고정하고 인자 없이 실행하도록 만들었다 — 그 판단이 맞았는지, 그리고 앞으로
    누가 실수로 다단어 인자를 command에 넣지 않는지 이 테스트로 굳힌다.
    """

    def test_no_schedule_command_relies_on_shell_quoting(self):
        data = json.loads(SCHEDULES.read_text(encoding="utf-8"))
        for job in data["schedules"]:
            cmd = job["command"]
            naive_split = cmd.split()
            shell_aware_split = shlex.split(cmd)
            self.assertEqual(
                naive_split, shell_aware_split,
                f"{job['id']}: command이 quote에 의존한다 — schedule_sync._program_args()는 "
                f"공백을 quote 없이 split해 launchd ProgramArguments가 깨진다: {cmd!r}"
            )


if __name__ == "__main__":
    unittest.main()
