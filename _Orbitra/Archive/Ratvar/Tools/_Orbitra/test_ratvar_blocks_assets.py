"""Validate the Cache/Cogscarab RSI exports and their pinned provenance."""
import json
import unittest
from pathlib import Path

from PIL import Image


class RatvarBlocksAssetsTest(unittest.TestCase):
    def test_cogscarab_idle_and_movement_are_distinct(self):
        root = Path(__file__).resolve().parents[2] / "Resources/Textures/_Orbitra/Ratvar"
        meta = json.loads((root / "cogscarab.rsi/meta.json").read_text(encoding="utf-8"))
        states = {state["name"]: state for state in meta["states"]}
        self.assertEqual(len(states["alive"]["delays"][0]), 1)
        self.assertEqual(len(states["alive_moving"]["delays"][0]), 2)

    def test_exports(self):
        root = Path(__file__).resolve().parents[2] / "Resources/Textures/_Orbitra/Ratvar"
        provenance = json.loads((root / "provenance.json").read_text(encoding="utf-8"))
        records = {entry["rsi"]: entry for entry in provenance["resources"]}
        for name in ("cogscarab", "cache", "cache_items", "robes", "cloak", "spectacles", "traps"):
            with self.subTest(name=name):
                directory = root / f"{name}.rsi"
                meta = json.loads((directory / "meta.json").read_text(encoding="utf-8"))
                state_names = [state["name"] for state in meta["states"]]
                self.assertEqual(len(state_names), len(set(state_names)), "Duplicate RSI state names")
                record = records[directory.name]
                self.assertEqual(meta["license"], "CC-BY-SA-3.0")
                self.assertEqual(record["license"], meta["license"])
                self.assertEqual(record["commit"], "a24fe414a304d3d73e30f4b9d8d3d7ade4a3b2e4")
                self.assertTrue(record["history"])
                self.assertIn("https://github.com/BeeStation/BeeStation-Hornet/", record["source"])
                self.assertIn("provenance.json", meta["copyright"])
                for state in meta["states"]:
                    with Image.open(directory / (state["name"] + ".png")) as image:
                        self.assertIsNotNone(image.convert("RGBA").getchannel("A").getbbox())
                        self.assertEqual(image.width % meta["size"]["x"], 0)
                        self.assertEqual(image.height % meta["size"]["y"], 0)
                        frames = sum(len(row) for row in state["delays"])
                        self.assertEqual(len(state["delays"]), state.get("directions", 1))
                        self.assertGreaterEqual(image.width * image.height //
                                                (meta["size"]["x"] * meta["size"]["y"]), frames)
                        self.assertTrue(all(delay > 0 for row in state["delays"] for delay in row))


if __name__ == "__main__":
    unittest.main()
