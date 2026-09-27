"""Validate the licensed Stargazer layers and source-state attribution."""
import json
import unittest
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[2] / "Resources/Textures/_Orbitra/Ratvar/stargazer.rsi"


class StargazerSpritesTest(unittest.TestCase):
    def test_layers_and_attribution(self):
        meta = json.loads((ROOT / "meta.json").read_text(encoding="utf-8"))
        self.assertEqual(meta["license"], "CC-BY-SA-3.0")
        self.assertEqual(meta["size"], {"x": 32, "y": 32})
        for notice in ("a24fe414", "b7e7779c", "8168ce7f", "Xhuis", "PowerfulBacon"):
            self.assertIn(notice, meta["copyright"])
        self.assertEqual({s["name"] for s in meta["states"]}, {"active", "idle", "unanchored", "light"})
        for state in meta["states"]:
            self.assertEqual(state["directions"], 1)
            self.assertEqual(state["delays"], [[0.1]])
            with Image.open(ROOT / (state["name"] + ".png")) as image:
                self.assertEqual(image.size, (32, 32))
                self.assertIsNotNone(image.getbbox())


if __name__ == "__main__":
    unittest.main()
