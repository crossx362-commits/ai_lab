#!/usr/bin/env python3
import re
import unittest
from pathlib import Path
from PIL import Image, ImageChops


HERE = Path(__file__).resolve().parent
KEYS = ["yewon", "youngsuk", "bomi", "teo", "baekho", "suri", "mio", "namu"]


def mean_alpha_diff(a: Image.Image, b: Image.Image) -> float:
    diff = ImageChops.difference(a.convert("RGBA"), b.convert("RGBA"))
    hist = diff.getchannel("A").histogram()
    total = sum(hist) or 1
    return sum(i * count for i, count in enumerate(hist)) / total


def alpha_center_x(im: Image.Image) -> float | None:
    alpha = im.getchannel("A")
    pixels = alpha.load()
    total = 0
    weighted = 0
    for y in range(alpha.height):
        for x in range(alpha.width):
            a = pixels[x, y]
            if a:
                total += a
                weighted += x * a
    return weighted / total if total else None


class ChibiSpriteSheetTest(unittest.TestCase):
    def test_all_eight_agents_use_two_centered_rows_of_four_desks(self):
        html = (HERE / "fleet_view.html").read_text(encoding="utf-8")
        seat_block = html.split("const SEATS = {", 1)[1].split("};", 1)[0]
        seats = {
            key: (float(x), float(y))
            for key, x, y in re.findall(r"(\w+):\s*\{fx:(\.\d+),\s*fy:(\.\d+)", seat_block)
        }
        self.assertEqual(set(seats), set(KEYS))
        rows = {}
        for x, y in seats.values():
            rows.setdefault(round(y, 3), []).append(x)
        self.assertEqual(sorted(len(xs) for xs in rows.values()), [4, 4])
        self.assertTrue(all(0.30 <= x <= 0.69 for x, _ in seats.values()))
        self.assertTrue(all(0.39 <= y <= 0.61 for _, y in seats.values()))
        self.assertEqual(len(set(seats.values())), 8)

    def test_each_chibi_character_has_eight_animation_frames(self):
        for key in KEYS:
            with self.subTest(key=key):
                sheet_path = HERE / "sprites" / "chibi_sheets" / f"{key}.png"
                self.assertTrue(sheet_path.is_file(), f"missing {sheet_path}")
                sheet = Image.open(sheet_path).convert("RGBA")
                self.assertEqual(sheet.width % 8, 0)
                frame_w = sheet.width // 8
                self.assertGreater(frame_w, 0)
                self.assertGreater(sheet.height, 0)

                frames = [
                    sheet.crop((i * frame_w, 0, (i + 1) * frame_w, sheet.height))
                    for i in range(8)
                ]
                original = Image.open(HERE / "sprites" / "chibi" / f"{key}.png").convert("RGBA")
                self.assertEqual(
                    ImageChops.difference(frames[0], original).getbbox(),
                    None,
                    "standing frame should preserve the existing chibi image",
                )
                for frame in frames:
                    corners = [
                        frame.getpixel((0, 0))[3],
                        frame.getpixel((frame.width - 1, 0))[3],
                        frame.getpixel((0, frame.height - 1))[3],
                        frame.getpixel((frame.width - 1, frame.height - 1))[3],
                    ]
                self.assertEqual(corners, [0, 0, 0, 0])

                stand_box = frames[0].getchannel("A").getbbox()
                self.assertIsNotNone(stand_box)
                walk_delta = ImageChops.difference(frames[2], frames[4]).getbbox()
                self.assertIsNotNone(walk_delta, "walk frames should differ")
                walk_a_box = frames[2].getchannel("A").getbbox()
                walk_b_box = frames[4].getchannel("A").getbbox()
                self.assertIsNotNone(walk_a_box)
                self.assertIsNotNone(walk_b_box)
                stand_h = stand_box[3] - stand_box[1]
                walk_h = walk_a_box[3] - walk_a_box[1]
                self.assertGreater(
                    walk_h,
                    stand_h * 0.82,
                    "walking frames should stay close to the standing character size",
                )
                center_a = (walk_a_box[0] + walk_a_box[2]) / 2
                center_b = (walk_b_box[0] + walk_b_box[2]) / 2
                self.assertLess(
                    abs(center_a - center_b),
                    14,
                    "walking should move limbs, not slide the whole character image",
                )
                lower_a = frames[2].crop((0, frame_w // 2, frame_w, sheet.height))
                lower_b = frames[4].crop((0, frame_w // 2, frame_w, sheet.height))
                lower_delta = ImageChops.difference(lower_a, lower_b).getbbox()
                self.assertIsNotNone(lower_delta, "walk frames should move arms and legs")
                for idx in (2, 3, 4, 5):
                    self.assertIsNotNone(
                        ImageChops.difference(frames[idx], frames[0]).getbbox(),
                        f"walk frame {idx} should be an actual walking pose, not a standing hold",
                    )
                unique_walks = {
                    frames[idx].tobytes()
                    for idx in (2, 3, 4, 5)
                }
                self.assertEqual(len(unique_walks), 4, "walk cycle should have four distinct poses")
                sit_box = frames[6].getchannel("A").getbbox()
                self.assertIsNotNone(sit_box)
                self.assertLess(
                    sit_box[3] - sit_box[1],
                    stand_box[3] - stand_box[1],
                    "sitting frame should have a shorter silhouette",
                )
                self.assertGreater(
                    sit_box[1],
                    stand_box[1] + 20,
                    "sitting frame should lower the existing character onto the chair",
                )
                self.assertLess(
                    sit_box[3] - sit_box[1],
                    stand_box[3] - stand_box[1] - 30,
                    "sitting frame should not look like a standing image with a chair behind it",
                )
                sit = frames[6].crop(sit_box)
                center_x = sit.width // 2
                face_area = sit.crop((center_x - sit.width // 7, 0, center_x + sit.width // 7, sit.height // 2))
                dark_or_hair_pixels = 0
                skin_pixels = 0
                for r, g, b, a in face_area.getdata():
                    if a < 30:
                        continue
                    if r > 205 and 125 < g < 225 and 80 < b < 190:
                        skin_pixels += 1
                    if (r + g + b) / 3 < 120 or max(r, g, b) - min(r, g, b) > 55:
                        dark_or_hair_pixels += 1
                self.assertGreater(
                    dark_or_hair_pixels,
                    skin_pixels,
                    "sitting frame should read as a back-of-head view, not a front face",
                )
                self.assertGreater(
                    sit_box[3],
                    int(sheet.height * 0.90),
                    "sitting character should continue through the hips instead of ending abruptly at the waist",
                )
                sit_alpha = list(frames[6].getchannel("A").getdata())
                visible_alpha = [value for value in sit_alpha if value]
                self.assertGreater(
                    sum(visible_alpha) / len(visible_alpha),
                    245,
                    "seated character colors must remain opaque after chroma-key removal",
                )

    def test_fleet_view_uses_chibi_sprite_sheets(self):
        html = (HERE / "fleet_view.html").read_text(encoding="utf-8")
        self.assertIn("/sprites/chibi_sheets/", html)
        self.assertIn("/office_fg", html)
        self.assertIn("drawForeground()", html)
        self.assertIn("fi*chibi.cw", html)
        self.assertIn("seated?100:72", html)
        self.assertNotIn("seated?82:72", html)
        self.assertIn("p.mode==='sit'", html)
        self.assertIn("const atDesk=", html)
        self.assertIn("p.mode==='sit'||atDesk", html)
        self.assertNotIn("p.mode==='sit'||p.mode==='rest'", html)
        self.assertNotIn("[.405,.812]", html)
        self.assertNotIn("[.883,.735]", html)
        self.assertIn("#app{ flex-direction:column; }", html)
        self.assertIn("#stage{ flex:0 0 52vh;", html)

    def test_office_foreground_overlay_exists(self):
        fg = HERE / "office_fg_chibi.png"
        self.assertTrue(fg.is_file(), "foreground overlay should be generated from the office background")
        im = Image.open(fg).convert("RGBA")
        self.assertGreater(im.getchannel("A").getbbox()[2], 100)
        src = (HERE / "gen_office_foreground.py").read_text(encoding="utf-8")
        self.assertIn("CHAIR_FRONT_MASKS", src)
        self.assertIn("len(CHAIR_FRONT_MASKS) == 8", src)

    def test_working_agents_animate_their_monitor_screens(self):
        html = (HERE / "fleet_view.html").read_text(encoding="utf-8")
        self.assertIn("const MONITORS", html)
        self.assertIn("drawWorkingMonitors(t)", html)
        self.assertIn("a.status!=='working'", html)
        self.assertIn("function clipMonitorScreen", html)
        self.assertIn("ctx.clip()", html)
        self.assertNotIn("ctx.shadowBlur=9", html)
        self.assertNotIn("rgba(12,20,34,.92)", html)
        for key in KEYS:
            self.assertIn(f"{key}:", html)

    def test_sprite_generator_draws_articulated_poses(self):
        src = (HERE / "gen_chibi_sheets.py").read_text(encoding="utf-8")
        self.assertIn("SRC = HERE / \"sprites\" / \"chibi\"", src)
        self.assertIn("build_walk_frame", src)
        self.assertIn("build_sit_frame", src)
        self.assertIn("SEATED_SRC", src)
        self.assertIn("load_generated_seated_frames", src)
        self.assertNotIn("build_seated_back", src)
        self.assertIn("paste_transformed", src)
        self.assertNotIn("draw_limb", src)
        self.assertNotIn("draw_walk_limbs", src)
        self.assertNotIn("draw_walk_torso", src)
        self.assertIn('"pass_a"', src)
        self.assertIn('"pass_b"', src)
        self.assertNotIn("draw_chair_back", src)
        self.assertNotIn("draw_chair_seat", src)
        self.assertNotIn("draw_chair_base", src)
        self.assertNotIn("draw_lap_keyboard", src)
        self.assertIn('"walk_a"', src)
        self.assertIn('"sit"', src)
        self.assertNotIn("CHARS =", src)


if __name__ == "__main__":
    unittest.main()
