import unittest
from convert_map import convert


class MapMigrationTest(unittest.TestCase):
    def test_components_and_entity(self):
        source = "entities:\n- proto: LimeAmbientDustZone\n  components:\n  - type: LimeAmbientDustZone\n    width: 6\n"
        result = convert(source)
        self.assertIn("proto: OrbitraAmbientDustZone", result)
        self.assertIn("type: OrbitraAmbientDustZone", result)
        self.assertEqual(result, convert(result))

    def test_ordinary_words_and_attribution(self):
        source = "name: Lime Station\nauthor: Lime\nproto: FoodLime\ntext: slime\n"
        self.assertEqual(source, convert(source))

    def test_unknown_id_rejected(self):
        with self.assertRaises(ValueError):
            convert("proto: LimeUnlistedEntity\n")

    def test_missing_resource_rejected(self):
        with self.assertRaises(ValueError):
            convert("sprite: _Lime/not_present.rsi\n")

    def test_type_tags_not_comments(self):
        source = "event: !type:LimeGunEffectsComponent {}\n# !type:LimeGunEffectsComponent\n"
        result = convert(source)
        self.assertIn("event: !type:OrbitraGunEffectsComponent", result)
        self.assertIn("# !type:LimeGunEffectsComponent", result)
        self.assertEqual(result, convert(result))
        with self.assertRaises(ValueError):
            convert("event: !type:LimeUnknown {}\n")


if __name__ == "__main__":
    unittest.main()
