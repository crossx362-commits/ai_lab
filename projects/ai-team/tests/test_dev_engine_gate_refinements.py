"""수리 diff_gate 정밀화 3종 (오너 승인 2026-08-05, 회의 3건이 공통 발견).

배경: 세 게이트가 코드는 정상(E2E 통과·QA 지표 악화 없음)인 브랜치를 반복 오탐 거부해
3회 실패→보류→긴급회의를 세 번 반복시켰다:
  1. FORBIDDEN_DIFF가 'token' 낱말 자체를 시크릿으로 오판(QR 공개프로필 tokenForPet 등)
     — minutes_20260805_0439.md
  2. FORBIDDEN_PATHS의 'supabase' 부분문자열이 js/supabase.js(프론트 클라이언트 래퍼)의
     순수 로직 편집까지 통째로 차단 — minutes_20260805_1239.md
  3. 크기 상한(파일≤6·줄≤200)이 응집된 단일 신규 파일(259줄)+소규모 배선(20줄)까지 차단
     — minutes_20260805_0716.md

세 회의 모두 "게이트 상한/패턴을 무단으로 느슨히 하지 말고, 진짜 위험(값 할당된 시크릿·
신규 테이블/RPC 접촉·산개된 대규모 변경)만 정밀하게 잡도록 좁혀라"로 만장일치했고,
오너가 세 정밀화 전부 승인했다(2026-08-05). 진짜 실제 git 저장소로 검증한다(모킹은
git diff/merge-base 같은 진짜 실패 지점을 못 잡는다 — 2026-07-12 교훈).
"""
import importlib.util
import subprocess
import tempfile
import unittest
from pathlib import Path

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
SCRIPT = AI_TEAM_ROOT / "skills" / "수리_개발자" / "tools" / "petnna_dev_engine.py"


def load():
    spec = importlib.util.spec_from_file_location("suri_gate_under_test", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def _git(args, cwd):
    return subprocess.run(["git", *args], cwd=cwd, capture_output=True, text=True)


class _RepoCase(unittest.TestCase):
    """공용 픽스처. master에 base 커밋만 만들고 끝난다 — 브랜치 분기는 각 테스트가
    _branch()로 명시 호출한다(추가 baseline 파일을 master에 더 얹어야 하는 케이스가
    있어, setUp에서 자동 분기하면 그 baseline이 work 브랜치에만 실려 merge-base
    기준으로 '전부 신규'로 오판된다)."""

    def setUp(self):
        self.suri = load()
        self.repo = Path(tempfile.mkdtemp())
        (self.repo / "projects/petnna/js").mkdir(parents=True)
        (self.repo / "projects/petnna/index.html").write_text(
            '<script defer src="js/app.js?v=1"></script>\n', encoding="utf-8")
        (self.repo / "projects/petnna/js/app.js").write_text("// base\n", encoding="utf-8")
        _git(["init", "-q", "-b", "master"], self.repo)
        _git(["config", "user.email", "t@t"], self.repo)
        _git(["config", "user.name", "t"], self.repo)
        _git(["add", "-A"], self.repo)
        _git(["commit", "-qm", "base"], self.repo)

    def _write(self, rel, content):
        p = self.repo / rel
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(content, encoding="utf-8")

    def _commit(self, msg="change"):
        _git(["add", "-A"], self.repo)
        _git(["commit", "-qm", msg], self.repo)

    def _branch(self):
        """diff_gate는 merge-base(master, HEAD)와 비교하므로 반드시 브랜치에서 작업한다."""
        _git(["checkout", "-q", "-b", "work"], self.repo)

    def _gate(self):
        return self.suri.diff_gate(self.repo)


class SecretDetectionPrecisionTests(_RepoCase):
    """FORBIDDEN_DIFF — 값 할당 리터럴만 잡고 낱말(식별자·주석)은 통과.

    index.html이 참조하지 않는 js/util.js를 쓴다 — js/app.js는 캐시버전(?v=)
    게이트 대상이라, 그 게이트와 뒤섞이면 이 테스트의 관심사가 흐려진다.
    """

    def test_bare_token_identifiers_pass(self):
        self._branch()
        self._write("projects/petnna/js/util.js",
                     "function tokenForPet(id) { return `/p/${id}`; }\n"
                     "const existingToken = localStorage.getItem('petna_token');\n")
        self._commit()
        ok, msg, _ = self._gate()
        self.assertTrue(ok, f"낱말 'token'이 든 식별자만으로 오탐 거부됨: {msg}")

    def test_hardcoded_api_key_literal_still_blocked(self):
        self._branch()
        self._write("projects/petnna/js/util.js",
                     'const apiKey = "AKIAIOSFODNN7EXAMPLE1234";\n')
        self._commit()
        ok, msg, _ = self._gate()
        self.assertFalse(ok, "하드코딩된 api key 리터럴을 통과시켰다")
        self.assertIn("시크릿", msg)

    def test_hardcoded_password_literal_still_blocked(self):
        self._branch()
        self._write("projects/petnna/js/util.js",
                     'const password = "hunter2Passw0rd!";\n')
        self._commit()
        ok, msg, _ = self._gate()
        self.assertFalse(ok, "하드코딩된 password 리터럴을 통과시켰다")

    def test_hardcoded_bearer_token_still_blocked(self):
        self._branch()
        self._write("projects/petnna/js/util.js",
                     'headers["Authorization"] = "Bearer eyJhbGciOiJIUzI1NiJ9abcdefgh";\n')
        self._commit()
        ok, msg, _ = self._gate()
        self.assertFalse(ok, "하드코딩된 Bearer 토큰 리터럴을 통과시켰다")

    def test_token_variable_from_expression_not_literal_passes(self):
        """값이 리터럴이 아니라 표현식(함수 호출 등)이면 진짜 시크릿 유출이 아니다."""
        self._branch()
        self._write("projects/petnna/js/util.js", "const token = getStoredToken();\n")
        self._commit()
        ok, msg, _ = self._gate()
        self.assertTrue(ok, f"리터럴이 아닌 표현식 대입까지 시크릿으로 오판: {msg}")


class SupabaseContractGateTests(_RepoCase):
    """FORBIDDEN_PATHS의 'supabase' 블랭킷 차단 대신, 신규 .from()/.rpc() 접촉만 잡는다."""

    def setUp(self):
        super().setUp()
        # baseline은 master 위에 커밋한 뒤에 분기한다 — 그래야 이 .from() 줄이
        # merge-base 기준 '기존 context'로 잡혀, 안 건드리면 diff에 안 나타난다.
        self._write("projects/petnna/js/supabase.js",
                     "async function uploadHealthLog(row) {\n"
                     "    return await this.client.from('health_logs').insert(row);\n"
                     "}\n")
        self._commit("add supabase.js base")
        self._branch()

    def test_pure_logic_edit_in_supabase_js_passes(self):
        """오프라인 큐 가드처럼 기존 호출을 안 건드리는 순수 로직 추가는 통과해야 한다."""
        self._write("projects/petnna/js/supabase.js",
                     "async function uploadHealthLog(row) {\n"
                     "    if (!navigator.onLine) { enqueueOffline('healthLog', row); return; }\n"
                     "    return await this.client.from('health_logs').insert(row);\n"
                     "}\n")
        self._commit()
        ok, msg, _ = self._gate()
        self.assertTrue(ok, f"supabase.js의 순수 로직 편집이 여전히 차단됨: {msg}")

    def test_new_from_call_in_supabase_js_is_blocked(self):
        self._write("projects/petnna/js/supabase.js",
                     "async function uploadHealthLog(row) {\n"
                     "    return await this.client.from('health_logs').insert(row);\n"
                     "}\n"
                     "async function newFeature() {\n"
                     "    return await this.client.from('new_table').select('*');\n"
                     "}\n")
        self._commit()
        ok, msg, _ = self._gate()
        self.assertFalse(ok, "신규 .from() 계약 호출을 통과시켰다")
        self.assertIn("계약", msg)

    def test_new_rpc_call_in_supabase_js_is_blocked(self):
        self._write("projects/petnna/js/supabase.js",
                     "async function uploadHealthLog(row) {\n"
                     "    return await this.client.from('health_logs').insert(row);\n"
                     "}\n"
                     "async function callSomeRpc() {\n"
                     "    return await this.client.rpc('compute_score', {});\n"
                     "}\n")
        self._commit()
        ok, msg, _ = self._gate()
        self.assertFalse(ok, "신규 .rpc() 계약 호출을 통과시켰다")

    def test_migrations_path_is_still_hard_blocked(self):
        """FORBIDDEN_PATHS 정밀화가 다른 항목(migrations/)까지 건드리지 않았는지 확인."""
        self._write("projects/petnna/migrations/add_x.sql", "alter table x add column y int;\n")
        self._commit()
        ok, msg, _ = self._gate()
        self.assertFalse(ok, "migrations/ 경로 차단이 사라졌다")
        self.assertIn("금지 경로", msg)


class SingleNewFileSizeExceptionTests(_RepoCase):
    """크기 상한 — 응집된 신규 단일 파일만 예외, 산개 변경·상한 자체는 그대로.

    wiring(배선) 변경은 index.html 또는 index.html이 참조하지 않는 js 파일에만
    가해 캐시버전 게이트와 뒤섞이지 않게 한다.
    """

    def test_single_new_large_file_with_small_wiring_passes(self):
        self._branch()
        over = self.suri.MAX_LINES + 10  # 총합이 확실히 상한을 넘도록
        big = "\n".join(f"// line {i}" for i in range(over))
        self._write("projects/petnna/js/booking-bridge.js", big + "\n")
        self._write("projects/petnna/index.html",
                     '<script defer src="js/app.js?v=1"></script>\n'
                     '<script defer src="js/booking-bridge.js?v=1"></script>\n')  # 배선 1줄
        self._commit()
        ok, msg, _ = self._gate()
        self.assertTrue(ok, f"응집된 신규 단일파일(배선 소규모)이 여전히 차단됨: {msg}")
        self.assertIn("예외", msg)

    def test_single_new_large_file_with_large_wiring_still_blocked(self):
        self._branch()
        over = self.suri.MAX_LINES + 10
        big = "\n".join(f"// line {i}" for i in range(over))
        self._write("projects/petnna/js/booking-bridge.js", big + "\n")
        wiring = "\n".join(f"// wiring {i}" for i in range(self.suri.WIRING_MAX + 10))
        self._write("projects/petnna/js/util.js", wiring + "\n")  # 캐시버전 무관 파일
        self._commit()
        ok, msg, _ = self._gate()
        self.assertFalse(ok, "배선이 WIRING_MAX를 넘었는데도 예외가 적용됐다")
        self.assertIn("변경 과대", msg)

    def test_multiple_new_files_over_limit_still_blocked(self):
        """신규 파일이 2개 이상이면 '단일 응집' 전제가 깨지므로 예외 없음."""
        self._branch()
        half = self.suri.MAX_LINES // 2 + 10
        self._write("projects/petnna/js/new-a.js", "\n".join(f"// a{i}" for i in range(half)))
        self._write("projects/petnna/js/new-b.js", "\n".join(f"// b{i}" for i in range(half)))
        self._commit()
        ok, msg, _ = self._gate()
        self.assertFalse(ok, "신규 파일이 여러 개인데도 단일파일 예외가 적용됐다")

    def test_scattered_edits_across_many_existing_files_still_blocked(self):
        """진짜 위험한 패턴(여러 기존 파일에 걸친 대규모 산개)은 그대로 막혀야 한다."""
        for name in ("a", "b", "c", "d"):
            self._write(f"projects/petnna/js/{name}.js", f"// base {name}\n")
        self._commit("add existing files")
        self._branch()
        chunk = "\n".join(f"// change {i}" for i in range(55))
        for name in ("a", "b", "c", "d"):
            self._write(f"projects/petnna/js/{name}.js", f"// base {name}\n" + chunk + "\n")
        self._commit("scattered change")
        ok, msg, _ = self._gate()
        self.assertFalse(ok, "여러 기존 파일에 걸친 산개 대규모 변경을 통과시켰다 — "
                             "단일파일 예외가 과하게 넓다")

    def test_file_count_cap_enforced_regardless_of_new_file_exception(self):
        """파일 수 상한(MAX_FILES)은 신규 단일파일 예외와 무관하게 항상 강제된다."""
        self._branch()
        for i in range(self.suri.MAX_FILES + 1):
            self._write(f"projects/petnna/js/extra{i}.js", f"// f{i}\n")
        self._commit()
        ok, msg, _ = self._gate()
        self.assertFalse(ok, "파일 수 상한을 넘었는데도 통과시켰다")
        self.assertIn("파일", msg)


if __name__ == "__main__":
    unittest.main()
