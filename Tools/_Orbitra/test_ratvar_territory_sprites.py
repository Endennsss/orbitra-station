"""Offline validation of the checked-in Bee-derived territory RSI assets."""

import json
import unittest
from pathlib import Path

from PIL import Image, ImageChops

ROOT = Path(__file__).resolve().parents[2] / "Resources/Textures/_Orbitra/Ratvar"


class TerritorySpritesTest(unittest.TestCase):
    def test_window_corners_reconstruct_source_masks(self):
        corners = [(4, 2, 32), (8, 1, 128), (1, 4, 16), (2, 8, 64)]
        sources = list((ROOT / "windows.rsi").glob("source*.png"))
        self.assertEqual(len(sources), 47)
        for source in sources:
            mask = int(source.stem[6:])
            with self.subTest(mask=mask), Image.open(source) as expected:
                result = Image.new("RGBA", (32, 32))
                for direction, (ccw, cw, diagonal) in enumerate(corners):
                    flags = (1 if mask & ccw else 0) | (4 if mask & cw else 0) | (2 if mask & diagonal else 0)
                    with Image.open(source.parent / f"window{flags}.png") as sheet:
                        x, y = direction % 2 * 32, direction // 2 * 32
                        result.alpha_composite(sheet.crop((x, y, x + 32, y + 32)))
                self.assertIsNone(ImageChops.difference(result, expected).getbbox(alpha_only=False))

    def test_door_animation_endpoints(self):
        folder = ROOT / "doors.rsi"
        with Image.open(folder / "opening.png") as opening, Image.open(folder / "closing.png") as closing:
            self.assertEqual(opening.size, (288, 32))
            for index in range(9):
                forward = opening.crop((index * 32, 0, (index + 1) * 32, 32))
                reverse = closing.crop(((8 - index) * 32, 0, (9 - index) * 32, 32))
                self.assertEqual(forward.tobytes(), reverse.tobytes())
            for state, index in [("closed", 0), ("open", 8)]:
                with Image.open(folder / f"{state}.png") as endpoint:
                    self.assertEqual(endpoint.tobytes(), opening.crop((index * 32, 0, (index + 1) * 32, 32)).tobytes())

    def test_metadata_and_provenance(self):
        manifest = json.loads((ROOT / "provenance.json").read_text(encoding="utf-8"))
        for name in ("windows", "doors"):
            folder = ROOT / f"{name}.rsi"
            metadata = json.loads((folder / "meta.json").read_text(encoding="utf-8"))
            self.assertEqual(metadata["license"], "CC-BY-SA-3.0")
            record = next(entry for entry in manifest["resources"] if entry["rsi"] == folder.name)
            self.assertEqual(record["commit"], "a24fe414a304d3d73e30f4b9d8d3d7ade4a3b2e4")
            self.assertTrue(record["history"])
            for state in metadata["states"]:
                with Image.open(folder / f'{state["name"]}.png') as image:
                    self.assertEqual(image.width % 32, 0)
                    self.assertEqual(image.height % 32, 0)


if __name__ == "__main__":
    unittest.main()
