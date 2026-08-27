"""보간 `$"..."` 접두 png 실로드 (회의 20260827-081437 채택 #1).

`Resources.Load<Sprite>($"FX/fx_dash_trail_{i}")` 처럼 보간으로 부르면
기존 배열 추출이 이름을 못 뽑아, 새 PNG 가 없어도 커밋이 통과하고
실사용 3장은 미사용으로 오탐됐다. 접두(첫 `{` 앞) + 접미(마지막 `}` 뒤)로
Resources 아래 파일을 대조한다.
"""
from __future__ import annotations

import ast
import importlib.util
import os
import tempfile
import unittest
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


class InterpPngLoadTests(unittest.TestCase):
    def test_parse_interp_prefix_before_first_brace(self):
        mod = _load()
        self.assertEqual(mod.parse_interp("FX/fx_dash_trail_{i}"), ("FX/fx_dash_trail_", ""))
        self.assertEqual(mod.parse_interp("props/{id}_0"), ("props/", "_0"))
        self.assertEqual(mod.parse_interp("sprites/{d}/{d}_idle_00"), ("sprites/", "_idle_00"))

    def test_temp_res_prefix_glob(self):
        """네거티브 컨트롤 — 고의 없는 접두는 0건, 있는 접두는 estate_keep_0 을 찾는다."""
        mod = _load()
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "FX").mkdir()
            (root / "FX" / "fx_dash_trail_0.png").write_bytes(b"x")
            (root / "props").mkdir()
            (root / "props" / "estate_keep_0.png").write_bytes(b"x")
            (root / "props" / "tree_1.png").write_bytes(b"x")
            dash = mod.match_interp_pngs("FX/fx_dash_trail_", "", root)
            self.assertEqual(["FX/fx_dash_trail_0.png"], dash)
            props0 = mod.match_interp_pngs("props/", "_0", root)
            self.assertEqual(["props/estate_keep_0.png"], props0)
            self.assertEqual([], mod.match_interp_pngs("FX/qa_missing_interp_", "", root))

    def test_negative_missing_interp_is_caught(self):
        extra = 'Resources.Load<Sprite>($"FX/qa_missing_interp_{i}");\n'
        msgs = _load().png_load_problems(extra_src=extra)
        blob = " ".join(msgs)
        self.assertIn("qa_missing_interp", blob, msgs)

    def test_live_dash_trail_consumed_not_unused(self):
        """보간 접두가 fx_dash_trail_0~2 를 소비로 친다 (미사용 오탐 해소)."""
        mod = _load()
        consumed = mod.consumed_relpaths()
        for n in ("fx_dash_trail_0.png", "fx_dash_trail_1.png", "fx_dash_trail_2.png"):
            self.assertTrue(
                any(c.endswith(n) for c in consumed),
                f"{n} not in consumed ({len(consumed)} keys)",
            )
        blob = " ".join(mod.unused_resource_problems())
        self.assertNotIn("fx_dash_trail", blob)

    def test_live_png_load_clean(self):
        self.assertEqual([], _load().png_load_problems())

    def test_main_calls_png_load_and_self_test_exists(self):
        src = TOOL.read_text(encoding="utf-8")
        tree = ast.parse(src)
        names = {n.name for n in ast.walk(tree) if isinstance(n, ast.FunctionDef)}
        for fn in ("parse_interp", "png_load_problems", "self_test",
                   "interpolated_resource_patterns"):
            self.assertIn(fn, names, f"{fn} 없음")
        called = any(
            isinstance(n, ast.Call)
            and isinstance(n.func, ast.Name)
            and n.func.id == "png_load_problems"
            for n in ast.walk(tree)
        )
        self.assertTrue(called, "png_load_problems 가 main 에서 안 불린다")
        self.assertIn("--png-load", src)
        self.assertIn("--self-test", src)

    def test_commit_guard_wires_png_load(self):
        src = GUARD.read_text(encoding="utf-8")
        self.assertIn("game_asset_names.py", src)
        self.assertIn("--png-load", src)
        self.assertIn("QA_NO_PNG_LOAD", src)

    def test_qa_no_skips_live(self):
        mod = _load()
        old = os.environ.get("QA_NO_PNG_LOAD")
        os.environ["QA_NO_PNG_LOAD"] = "1"
        try:
            self.assertEqual([], mod.png_load_problems())
        finally:
            if old is None:
                os.environ.pop("QA_NO_PNG_LOAD", None)
            else:
                os.environ["QA_NO_PNG_LOAD"] = old


if __name__ == "__main__":
    unittest.main()
