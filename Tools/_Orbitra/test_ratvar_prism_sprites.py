"""Check the licensed, unmodified prism animation imported from pinned Bee."""
import json
import math
import unittest
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2] / "Resources/Textures/_Orbitra/Ratvar/prism.rsi"


class PrismSpritesTest(unittest.TestCase):
    def test_frames_delays_and_credit(self):
        metadata = json.loads((ROOT / "meta.json").read_text(encoding="utf-8"))
        self.assertEqual(metadata["license"], "CC-BY-SA-3.0")
        self.assertIn("Joan Lung", metadata["copyright"])
        self.assertIn("405fc7b0fef61c396f102791f684d13e4dbb76ec", metadata["copyright"])
        expected = {"active": [2, .12] + [.09] * 30, "inactive": [.1], "unanchored": [.1]}
        for state in metadata["states"]:
            delays = expected.pop(state["name"])
            self.assertEqual(state["directions"], 1)
            self.assertEqual(state["delays"], [delays])
            columns = math.ceil(math.sqrt(len(delays)))
            with Image.open(ROOT / (state["name"] + ".png")) as sheet:
                for frame in range(len(delays)):
                    x, y = frame % columns * 32, frame // columns * 32
                    self.assertIsNotNone(sheet.crop((x, y, x + 32, y + 32)).getbbox())
        self.assertFalse(expected)


if __name__ == "__main__":
    unittest.main()
