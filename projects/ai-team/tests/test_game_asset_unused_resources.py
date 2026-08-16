"""Resources 미사용 파일 검출 (2026-08-16).

사고
----
`game_asset_names.py`는 코드가 찾는 이름의 **누락**만 봤다. Resources에 코드가
안 읽는 그림(미사용 FX 28장, estate 통, battle_background_atlas)이 쌓여도
검사기는 ✅였다. 목록에 파일이 있으면 쓰는 줄 안다.

수리
----
`unused_resource_problems()`가 코드 소비 목록과 Resources PNG를 대조한다.
이 테스트는 ①함수가 main에 실제로 물려 있는지 ②고의로 넣은 미사용 파일을
잡는지 ③지금 트리의 소비 파일이 오탐이 아닌지를 지킨다.
"""
from __future__ import annotations

import ast
import importlib.util
import tempfile
import unittest
from pathlib import Path

AI_TEAM_ROOT = Path(__file__).resolve().parents[1]
TOOL = AI_TEAM_ROOT / "skills/마루_게임개발/tools/game_asset_names.py"


def _load():
    spec = importlib.util.spec_from_file_location("game_asset_names", TOOL)
    mod = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(mod)
    return mod


class UnusedResourceDetectionTests(unittest.TestCase):
    def test_main_calls_unused_check(self):
        src = TOOL.read_text(encoding="utf-8")
        tree = ast.parse(src)
        names = {n.name for n in ast.walk(tree) if isinstance(n, ast.FunctionDef)}
        self.assertIn(
            "unused_resource_problems", names,
            "미사용 검사 함수가 없다 — 누락만 보면 Resources 고아는 영원히 안 잡힌다",
        )
        called = any(
            isinstance(n, ast.Call)
            and isinstance(n.func, ast.Name)
            and n.func.id == "unused_resource_problems"
            for n in ast.walk(tree)
        )
        self.assertTrue(
            called,
            "unused_resource_problems가 정의만 되고 main에서 안 불린다",
        )

    def test_expected_covers_runtime_channels(self):
        """직업/몹/프랍만 보면 FX·배경·UI·바닥은 미사용으로 오탐된다."""
        exp = _load().expected()
        flat = {f"{folder}/{name}" for folder, names in exp.items() for name in names}
        lower = {x.lower() for x in flat}
        self.assertTrue(any(x.endswith("/fx_hit.png") for x in lower), flat)
        self.assertTrue(any(x.endswith("/bg_title.png") for x in lower), flat)
        self.assertTrue(any("ashes_to_stars_ui_atlas.png" in x for x in lower), flat)
        self.assertTrue(any(x.endswith("/field_plain_albedo.png") for x in lower), flat)
        self.assertTrue(any(x.endswith("/player_knight_0.png") for x in lower), flat)
        self.assertTrue(any(x.endswith("/dungeon_cover_0.png") for x in lower), flat)
        self.assertTrue(any(x.endswith("/dungeon_wall_0.png") for x in lower), flat)
        self.assertTrue(any(x.endswith("/dungeon_crystal_0.png") for x in lower), flat)
        self.assertTrue(any(x.endswith("/dungeon_rubble_0.png") for x in lower), flat)
        self.assertTrue(any(x.endswith("/ash_bone_0.png") for x in lower), flat)

    def test_injected_unused_png_is_detected(self):
        """네거티브 컨트롤 — 고의 미사용 파일을 넣으면 반드시 잡힌다."""
        mod = _load()
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "FX").mkdir()
            (root / "FX" / "fx_hit.png").write_bytes(b"ok")
            (root / "FX" / "orphan_unused.png").write_bytes(b"junk")
            msgs = mod.unused_resource_problems(root)
        self.assertTrue(msgs, "미사용 PNG를 넣었는데 침묵한다")
        blob = " ".join(msgs)
        self.assertIn("orphan_unused.png", blob)
        self.assertNotIn("fx_hit.png", blob)

    def test_live_resources_have_no_unused(self):
        msgs = _load().unused_resource_problems()
        self.assertEqual(
            [], msgs,
            "지금 Resources에 코드가 안 읽는 PNG가 있다 — "
            f"{msgs}",
        )


if __name__ == "__main__":
    unittest.main()
