"""Validate the pinned Bee shield animation metadata used by marauders."""

import json
import unittest
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2] / "Resources/Textures/_Orbitra/Ratvar"


class MarauderSpritesTest(unittest.TestCase):
    def test_shield_frames_delays_and_attribution(self):
        metadata = json.loads((ROOT / "sigils.rsi/meta.json").read_text(encoding="utf-8"))
        self.assertEqual(metadata["license"], "CC-BY-SA-3.0")
        self.assertIn("8168ce7f06afc301b3226661703df356c5936599", metadata["copyright"])
        for name, delays in {
            "shield_hit": [0.04, 0.04, 0.06, 0.08, 0.1, 0.1, 0.1],
            "shield_break": [0.1] * 5,
        }.items():
            state = next(state for state in metadata["states"] if state["name"] == name)
            self.assertEqual(state["directions"], 1)
            self.assertEqual(state["delays"], [delays])
            with Image.open(ROOT / f"sigils.rsi/{name}.png") as sheet:
                for frame in range(len(delays)):
                    x, y = frame % 3 * 32, frame // 3 * 32
                    self.assertIsNotNone(sheet.crop((x, y, x + 32, y + 32)).getbbox())


if __name__ == "__main__":
    unittest.main()
