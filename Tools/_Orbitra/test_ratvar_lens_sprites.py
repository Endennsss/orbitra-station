"""Validate the licensed lens RSI and its explicit rewind animation."""
import json
import math
import unittest
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[2] / "Resources/Textures/_Orbitra/Ratvar/lens.rsi"


class LensSpritesTest(unittest.TestCase):
    def test_metadata_and_frames(self):
        meta = json.loads((ROOT / "meta.json").read_text(encoding="utf-8"))
        self.assertEqual(meta["license"], "CC-BY-SA-3.0")
        for credit in ("Xhuis", "Joan Lung", "de1c7b074", "a24fe414"):
            self.assertIn(credit, meta["copyright"])
        states = {state["name"]: state for state in meta["states"]}
        self.assertEqual(set(states), {"inactive", "active", "unanchored"})
        for name, count in (("inactive", 10), ("active", 4), ("unanchored", 1)):
            self.assertEqual(states[name]["directions"], 1)
            self.assertEqual(states[name]["delays"], [[0.1 if count == 1 else 0.05] * count])
            with Image.open(ROOT / f"{name}.png") as image:
                columns = math.ceil(math.sqrt(count))
                frames = [image.crop((i % columns * 32, i // columns * 32,
                                      (i % columns + 1) * 32, (i // columns + 1) * 32)) for i in range(count)]
                self.assertTrue(all(frame.getbbox() is not None for frame in frames))
                if name == "inactive":
                    for forward, backward in ((1, 9), (2, 8), (3, 7), (4, 6)):
                        self.assertEqual(frames[forward].tobytes(), frames[backward].tobytes())


if __name__ == "__main__":
    unittest.main()
