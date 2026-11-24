import unittest

from shard_integration_tests import shard_filters


class ShardIntegrationTestsTest(unittest.TestCase):
    def test_similar_fixture_names_get_disjoint_prefixes(self):
        names = [
            "Tests.Atmos.TileAtmosphereTest.LINDA",
            "Tests.Atmos.TileAtmosphereTest_Snake.LINDA",
        ]
        filters = shard_filters(names, 2)
        self.assertEqual({tuple(shard) for shard in filters}, {
            ("FullyQualifiedName~Tests.Atmos.TileAtmosphereTest.",),
            ("FullyQualifiedName~Tests.Atmos.TileAtmosphereTest_Snake.",),
        })

    def test_map_fixture_splits_parameterized_cases(self):
        names = [
            'Tests.PostMapInitTest.Load("A & B")',
            'Tests.PostMapInitTest.Load("C")',
        ]
        filters = shard_filters(names, 2)
        self.assertEqual({tuple(shard) for shard in filters}, {
            (r'FullyQualifiedName=Tests.PostMapInitTest.Load\("A \& B"\)',),
            (r'FullyQualifiedName=Tests.PostMapInitTest.Load\("C"\)',),
        })
