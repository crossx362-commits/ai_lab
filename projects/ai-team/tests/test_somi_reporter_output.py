import contextlib
import importlib.util
import io
import pathlib
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "skills" / "소미_분석가" / "tools" / "somi_kis_reporter.py"


def load_reporter():
    spec = importlib.util.spec_from_file_location("somi_kis_reporter_output_test", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class SomiReporterOutputTests(unittest.TestCase):
    def test_diagnostic_log_does_not_write_to_stdout(self):
        reporter = load_reporter()
        stdout = io.StringIO()
        stderr = io.StringIO()

        with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
            reporter.log("KIS investor_today raw response: example")

        self.assertEqual(stdout.getvalue(), "")
        self.assertIn("KIS investor_today raw response", stderr.getvalue())


if __name__ == "__main__":
    unittest.main()
