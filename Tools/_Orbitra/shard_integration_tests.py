"""Распределяет обнаруженные VSTest тесты по непересекающимся группам."""

import argparse
from collections import Counter
from pathlib import Path


HEAVY_FIXTURES = {"EntityTest", "StartTest", "TestRoundRestartCleanupEvent"}
MAP_FIXTURES = {"PostMapInitTest", "SaveLoadMapTest"}
FILTER_SPECIAL = r"\!~=|&()"


def shard_filters(test_names: list[str], count: int) -> list[list[str]]:
    tests = {line.strip() for line in test_names if line.strip()}
    fixtures = Counter()
    map_cases = set()
    for test in tests:
        fixture, dot, _ = test.split("(", 1)[0].rpartition(".")
        if not dot or not fixture or any(char in fixture for char in FILTER_SPECIAL):
            raise ValueError(f"Неподдерживаемое имя теста: {test}")
        if fixture.rpartition(".")[2] in MAP_FIXTURES:
            map_cases.add(test)
        else:
            fixtures[fixture] += 1
    if not 1 <= count <= len(fixtures) + len(map_cases):
        raise ValueError("Число групп должно быть положительным и не превышать число групп тестов")
    if len(map_cases) != len({test.lower() for test in map_cases}):
        raise ValueError("Имена тестов карт различаются только регистром")

    # Для групп по классу VSTest ищет подстроку. Точка исключает классы с похожими именами.
    # Проверяем фильтры по реальному списку, чтобы и необычные имена не дали повторов.
    prefixes = {fixture.lower() + "." for fixture in fixtures}
    if len(prefixes) != len(fixtures):
        raise ValueError("Имена тестовых классов различаются только регистром")
    for test in tests:
        if sum(prefix in test.lower() for prefix in prefixes) + (test in map_cases) != 1:
            raise ValueError(f"Неоднозначное распределение теста: {test}")

    groups = []
    for fixture, size in fixtures.items():
        minimum = 40 if fixture.rpartition(".")[2] in HEAVY_FIXTURES else 1
        groups.append((max(size, minimum), f"FullyQualifiedName~{fixture}."))
    # Карты запускаются по отдельности, чтобы один класс не занял целый шард.
    for test in map_cases:
        escaped = "".join(f"\\{char}" if char in FILTER_SPECIAL else char for char in test)
        groups.append((6, f"FullyQualifiedName={escaped}"))

    shards: list[list[str]] = [[] for _ in range(count)]
    loads = [0] * count
    for weight, condition in sorted(groups, key=lambda group: (-group[0], group[1])):
        shard = min(range(count), key=loads.__getitem__)
        shards[shard].append(condition)
        loads[shard] += weight
    return shards


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("tests", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--count", type=int, required=True)
    args = parser.parse_args()
    shards = shard_filters(args.tests.read_text(encoding="utf-8-sig").splitlines(), args.count)
    args.output.mkdir(parents=True, exist_ok=True)
    for index, shard in enumerate(shards):
        (args.output / f"{index}.filter").write_text("|".join(shard), encoding="utf-8")


if __name__ == "__main__":
    main()
