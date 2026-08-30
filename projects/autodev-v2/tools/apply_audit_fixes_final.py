#!/usr/bin/env python3
from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]


def replace_once(path: Path, old: str, new: str, label: str) -> None:
    text = path.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"{label}: expected 1 match, got {count}")
    path.write_text(text.replace(old, new, 1), encoding="utf-8")


def patch_runner() -> None:
    path = ROOT / "projects/autodev-v2/runner.py"
    replace_once(
        path,
        'AREA_KEYWORDS: dict[str, tuple[str, ...]] = {\n    "combat":',
        'PROJECT_AREA_NAMES = ("estate", "formation", "raid", "fusion", "class_change")\n\nAREA_KEYWORDS: dict[str, tuple[str, ...]] = {\n    "estate": ("estate", "estatescreen", "territory", "영지"),\n    "formation": ("formation", "w3party", "party formation", "편성"),\n    "raid": ("raid", "bossbattle", "boss battle", "레이드", "보스전"),\n    "fusion": ("fusion", "merge", "combine", "합성"),\n    "class_change": ("class change", "job change", "promotion", "전직"),\n    "combat":',
        "runner project areas",
    )
    old_infer = '''def infer_area(item: dict[str, Any]) -> str:\n    explicit = _norm_text(str(item.get("area", ""))).replace(" ", "_")\n    if explicit in AREA_KEYWORDS:\n        return explicit\n    text = _task_text(item)\n    scores: list[tuple[int, str]] = []\n    for area, words in AREA_KEYWORDS.items():\n        score = sum(1 for w in words if w.lower() in text)\n        scores.append((score, area))\n    score, area = max(scores)\n    return area if score > 0 else "systems"\n'''
    new_infer = '''def infer_area(item: dict[str, Any]) -> str:\n    explicit = _norm_text(str(item.get("area", ""))).replace(" ", "_")\n    text = _task_text(item)\n\n    # Ashes-to-Stars core areas override a broad Director label such as systems/combat\n    # when the task text itself clearly names the domain.\n    project_scores = []\n    for area in PROJECT_AREA_NAMES:\n        words = AREA_KEYWORDS[area]\n        project_scores.append((sum(1 for w in words if w.lower() in text), area))\n    project_score, project_area = max(project_scores)\n    if project_score > 0:\n        return project_area\n\n    if explicit in AREA_KEYWORDS:\n        return explicit\n    scores: list[tuple[int, str]] = []\n    for area, words in AREA_KEYWORDS.items():\n        score = sum(1 for w in words if w.lower() in text)\n        scores.append((score, area))\n    score, area = max(scores)\n    return area if score > 0 else "systems"\n'''
    replace_once(path, old_infer, new_infer, "runner project-first inference")
    replace_once(
        path,
        '"area": "combat|character|progression|items|ui|stage|systems|qa 중 하나",',
        '"area": "estate|formation|raid|fusion|class_change|combat|character|progression|items|ui|stage|systems|qa 중 하나",',
        "runner director allowed areas",
    )


def patch_core_test() -> None:
    path = ROOT / "projects/autodev-v2/tests/test_core.py"
    old = '''    def test_grok_command_fails_closed_when_saving_flags_are_missing(self):\n        old_help = "Usage: grok --single <PROMPT> --cwd <CWD> --output-format <FMT> --max-turns <N> --always-approve"\n        with self.assertRaises(RuntimeError) as cm:\n            M.build_grok_command(\n                "/usr/local/bin/grok", "x", REPO,\n                max_turns=2, allow_edits=False, help_text=old_help,\n            )\n        self.assertIn("절약 옵션", str(cm.exception))\n'''
    new = '''    def test_grok_command_skips_missing_optional_saving_flags(self):\n        old_help = "Usage: grok --single <PROMPT> --cwd <CWD> --output-format <FMT> --max-turns <N> --always-approve"\n        cmd = M.build_grok_command(\n            "/usr/local/bin/grok", "x", REPO,\n            max_turns=2, allow_edits=False, help_text=old_help,\n        )\n        self.assertIn("--single", cmd)\n        self.assertNotIn("--no-plan", cmd)\n        self.assertNotIn("--no-memory", cmd)\n'''
    replace_once(path, old, new, "core optional savings policy")


def main() -> None:
    patch_runner()
    patch_core_test()
    print("final audit consistency fixes applied")


if __name__ == "__main__":
    main()
