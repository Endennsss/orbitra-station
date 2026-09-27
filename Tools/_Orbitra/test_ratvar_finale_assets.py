"""Validate the pinned god animation and its licensed audio subset offline."""
import hashlib
import json
import unittest
from pathlib import Path

from PIL import Image

from import_ratvar_audio import COMMIT, ROOT as AUDIO, SELECTION, probe

ROOT = Path(__file__).resolve().parents[2]


class RatvarFinaleAssetsTest(unittest.TestCase):
    def test_god_frames_and_attribution(self):
        folder = ROOT / "Resources/Textures/_Orbitra/Ratvar/ratvar.rsi"
        meta = json.loads((folder / "meta.json").read_text(encoding="utf-8"))
        self.assertEqual(meta["license"], "CC-BY-SA-3.0")
        self.assertIn(COMMIT, meta["copyright"])
        self.assertIn("Xhuis", meta["copyright"])
        self.assertFalse(meta["metaAtlas"])
        self.assertEqual(meta["size"], {"x": 512, "y": 512})
        self.assertEqual(meta["states"], [{"name": "ratvar", "directions": 1, "delays": [[0.1] * 16]}])
        with Image.open(folder / "ratvar.png") as image:
            self.assertEqual(image.size, (2048, 2048))
            digest = hashlib.sha256()
            for i in range(16):
                frame = image.crop((i % 4 * 512, i // 4 * 512, (i % 4 + 1) * 512, (i // 4 + 1) * 512))
                self.assertIsNotNone(frame.getbbox())
                digest.update(frame.convert("RGBA").tobytes())
            # Хеш последовательности RGBA-кадров закреплённого исходного DMI.
            self.assertEqual(digest.hexdigest(), "f01b0f0b11456dea99c30476105bbd08e93c24c45d472b49e97120421040c3ff")

    def test_audio_format_hashes_and_notices(self):
        manifest = json.loads((AUDIO / "provenance.json").read_text(encoding="utf-8"))
        self.assertEqual({r["file"] for r in manifest["resources"]}, {name + ".ogg" for name in SELECTION})
        notices = (AUDIO / "attributions.yml").read_text(encoding="utf-8")
        self.assertTrue((AUDIO / "LICENSE.txt").exists())
        for record in manifest["resources"]:
            with self.subTest(file=record["file"]):
                path = AUDIO / record["file"]
                metadata = probe(path)
                stream = metadata["streams"][0]
                self.assertEqual(stream["codec_name"], "vorbis")
                self.assertEqual(stream["channels"], 2 if path.stem == "ratvar_reveal" else 1)
                self.assertEqual(stream["sample_rate"], "44100")
                self.assertAlmostEqual(float(metadata["format"]["duration"]),
                                       float(record["source_format"]["format"]["duration"]), places=2)
                self.assertEqual(record["output_sha256"], hashlib.sha256(path.read_bytes()).hexdigest())
                self.assertEqual(record["commit"], COMMIT)
                self.assertEqual(record["license"], "CC-BY-SA-3.0")
                self.assertIn("Xhuis", record["credits"])
                self.assertIn(record["source"], notices)
                if not SELECTION[path.stem][1]:
                    self.assertEqual(record["source_sha256"], record["output_sha256"])


if __name__ == "__main__":
    unittest.main()
