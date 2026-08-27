"""zip 실로드 (회의 20260827-081437 보류 #5, png `d30a135c` 후속).

런타임 zip 파이프라인은 아직 없다. 검사기는 C# 리터럴·보간 `.zip` 경로가
Resources/StreamingAssets 에 있고 unzip 목록이 비지 않는지 본다.
없는 이름은 extra_src 네거티브로 증명한다(Resources 에 가짜 zip 커밋 금지).
"""
from __future__ import annotations

import ast
import importlib.util
import os
import tempfile
import unittest
import zipfile
from pathlib import Path

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
TOOL = next((AI_TEAM_ROOT / "skills").glob("*/tools/game_asset_names.py"))
GUARD = AI_TEAM_ROOT.parents[1] / "loop" / "commit_guard.sh"


def _load():
    spec = importlib.util.spec_from_file_location("game_asset_names", TOOL)
    mod = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(mod)
    return mod


class ZipLoadTests(unittest.TestCase):
    def test_parse_interp_zip_affix(self):
        mod = _load()
        self.assertEqual(mod.parse_interp("packs/{id}.zip"), ("packs/", ".zip"))
        self.assertEqual(mod.parse_interp("FX/pack_{i}.zip"), ("FX/pack_", ".zip"))
        self.assertEqual(mod.parse_interp("data.zip"), ("data.zip", ""))

    def test_temp_res_prefix_glob(self):
        """네거티브 컨트롤 — 고의 없는 접두는 0건, 있는 접두는 data_0.zip 을 찾는다."""
        mod = _load()
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "packs").mkdir()
            zpath = root / "packs" / "data_0.zip"
            with zipfile.ZipFile(zpath, "w") as z:
                z.writestr("ok.txt", "x")
            hits = mod.match_interp_zips("packs/data_", "", root)
            self.assertEqual(["packs/data_0.zip"], hits)
            self.assertEqual([], mod.match_interp_zips("packs/qa_missing_zip_", "", root))

    def test_negative_missing_zip_is_caught(self):
        extra = (
            'Resources.Load<TextAsset>($"packs/qa_missing_zip_{i}.zip");'
            + "\n"
        )
        msgs = _load().zip_load_problems(extra_src=extra)
        blob = " ".join(msgs)
        self.assertIn("qa_missing_zip", blob, msgs)

    def test_empty_zip_listing_is_caught(self):
        mod = _load()
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "packs").mkdir()
            empty = root / "packs" / "qa_empty.zip"
            with zipfile.ZipFile(empty, "w"):
                pass
            extra = (
                'Resources.Load<TextAsset>("packs/qa_empty.zip");'
                + "\n"
            )
            msgs = mod.zip_load_problems(res=root, extra_src=extra, assets=root)
            blob = " ".join(msgs)
            self.assertIn("qa_empty", blob, msgs)
            self.assertIn("비어", blob, msgs)

    def test_good_zip_load_clean_on_temp(self):
        mod = _load()
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "packs").mkdir()
            good = root / "packs" / "data_0.zip"
            with zipfile.ZipFile(good, "w") as z:
                z.writestr("ok.txt", "x")
            extra = (
                'Resources.Load<TextAsset>($"packs/data_{i}.zip");'
                + "\n"
            )
            msgs = mod.zip_load_problems(res=root, extra_src=extra, assets=root)
            self.assertEqual([], msgs, msgs)

    def test_live_zip_load_clean(self):
        self.assertEqual([], _load().zip_load_problems())

    def test_main_calls_zip_load_and_self_test_exists(self):
        src = TOOL.read_text(encoding="utf-8")
        tree = ast.parse(src)
        names = {n.name for n in ast.walk(tree) if isinstance(n, ast.FunctionDef)}
        for fn in (
            "parse_interp", "zip_load_problems", "zip_self_test",
            "zip_resource_patterns", "match_interp_zips",
        ):
            self.assertIn(fn, names, f"{fn} 없음")
        called = any(
            isinstance(n, ast.Call)
            and isinstance(n.func, ast.Name)
            and n.func.id == "zip_load_problems"
            for n in ast.walk(tree)
        )
        self.assertTrue(called, "zip_load_problems 가 main 에서 안 불린다")
        self.assertIn("--zip-load", src)
        self.assertIn("--zip-self-test", src)

    def test_commit_guard_wires_zip_load(self):
        src = GUARD.read_text(encoding="utf-8")
        self.assertIn("game_asset_names.py", src)
        self.assertIn("--zip-load", src)
        self.assertIn("QA_NO_ZIP_LOAD", src)

    def test_qa_no_skips_live(self):
        mod = _load()
        old = os.environ.get("QA_NO_ZIP_LOAD")
        os.environ["QA_NO_ZIP_LOAD"] = "1"
        try:
            self.assertEqual([], mod.zip_load_problems())
        finally:
            if old is None:
                os.environ.pop("QA_NO_ZIP_LOAD", None)
            else:
                os.environ["QA_NO_ZIP_LOAD"] = old

    def test_zip_self_test_pass(self):
        ok, note = _load().zip_self_test()
        self.assertTrue(ok, note)


if __name__ == "__main__":
    unittest.main()
