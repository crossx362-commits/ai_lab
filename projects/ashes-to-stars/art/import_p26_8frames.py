#!/usr/bin/env python3
"""5직업 모션 8프레임 통일 재추출 (2026-08-21 오너 지시 "모션마다 이미지 수가 왜 달라").

원본 시트(unity/Assets/TestSpriteSheets/*.png)는 전부 4x2=8프레임 균일.
옛 6칸 계약에 맞추느라 모션당 2장씩 버렸던 것을 8장 전량으로 다시 잘라 넣는다.

매핑: idle→idle_00~07, run→walk_00~07, attack→attack_00~07, skill→special_00~07,
death→death_00~07(마지막 장이 시체), dash=run[0,2,4,6], hurt=death[0],
invuln=idle_01 시트[0]. dash·hurt·invuln은 원본 시트가 없어 파생 유지.
"""
from __future__ import annotations

from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve().parent
UNITY = HERE.parent / "unity"
SRC = UNITY / "Assets" / "TestSpriteSheets"
RES = UNITY / "Assets" / "Resources" / "sprites"

JOBS = {  # 시트 접두 → 게임 폴더/접두
    "tanker": "tank",
    "assassin": "dps",
    "magican": "mage",
    "supporter": "buffer",
    "healer": "healer",
}
MOTIONS = {  # 시트 모션명 → 게임 모션명
    "idle": "idle",
    "run": "walk",
    "attack": "attack",
    "skill": "special",
    "death": "death",
}
COLS, ROWS, H = 4, 2, 124


def frames(sheet: Path) -> list[Image.Image]:
    im = Image.open(sheet).convert("RGBA")
    w, h = im.size
    out = []
    for r in range(ROWS):
        for c in range(COLS):
            box = (round(c * w / COLS), round(r * h / ROWS),
                   round((c + 1) * w / COLS), round((r + 1) * h / ROWS))
            cell = im.crop(box)
            cw, ch = cell.size
            out.append(cell.resize((round(cw * H / ch), H), Image.LANCZOS))
    return out


def main() -> None:
    for sheet_job, game_job in JOBS.items():
        dest = RES / game_job
        dest.mkdir(parents=True, exist_ok=True)
        cache: dict[str, list[Image.Image]] = {}
        for sheet_m, game_m in MOTIONS.items():
            fs = frames(SRC / f"{sheet_job}_{sheet_m}.png")
            cache[sheet_m] = fs
            for i, f in enumerate(fs):
                f.save(dest / f"{game_job}_{game_m}_{i:02d}.png")
        for i, src_i in enumerate((0, 2, 4, 6)):  # dash: run 파생
            cache["run"][src_i].save(dest / f"{game_job}_dash_{i:02d}.png")
        cache["death"][0].save(dest / f"{game_job}_hurt_00.png")
        frames(SRC / f"{sheet_job}_idle_01.png")[0].save(dest / f"{game_job}_invuln_00.png")
        n = len(list(dest.glob(f"{game_job}_*.png")))
        print(game_job, n)


if __name__ == "__main__":
    main()
