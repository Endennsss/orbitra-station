"""Copy a pre-Orbitra YAML map, migrating only known technical identifiers."""
import argparse
import json
import re
from pathlib import Path

import yaml

MANIFEST = Path(__file__).with_name("rename_manifest.json")
ROOT = Path(__file__).resolve().parents[3]


def convert(text: str, resource_root: Path = ROOT / "Resources") -> str:
    names = json.loads(MANIFEST.read_text(encoding="utf-8"))
    # Компоненты записаны на карте без суффикса Component.
    names.update({k[:-9]: v[:-9] for k, v in list(names.items()) if k.endswith("Component")})
    edits = []
    vanilla_ids = {"Lime", "LimePlants", "LimeJuice", "LimeSeeds"}

    def visit(node, field=""):
        if node.tag.startswith("!type:Lime"):
            legacy = node.tag[len("!type:"):]
            if legacy not in names:
                raise ValueError(f"Unknown legacy type tag: {legacy}")
            start = node.start_mark.index
            if text[start:start + len(node.tag)] != node.tag:
                raise ValueError(f"Unsupported tagged node at line {node.start_mark.line + 1}")
            edits.append((start, start + len(node.tag), "!type:" + names[legacy]))
        if isinstance(node, yaml.MappingNode):
            for key, value in node.value:
                visit(key)
                visit(value, key.value)
        elif isinstance(node, yaml.SequenceNode):
            for child in node.value:
                visit(child, field)
        elif isinstance(node, yaml.ScalarNode):
            if field in {"name", "desc", "description", "author", "copyright"}:
                return
            value = names.get(node.value, node.value)
            if "_Lime/" in value:
                value = value.replace("_Lime/", "_Orbitra/")
                relative = value.lstrip("/")
                candidates = [resource_root / relative, resource_root / "Textures" / relative]
                if not any(path.exists() and path.resolve().is_relative_to(resource_root.resolve()) for path in candidates):
                    raise ValueError(f"Missing migrated resource: {value}")
            if value != node.value:
                raw = text[node.start_mark.index:node.end_mark.index]
                # Сохраняем YAML-теги, кавычки, комментарии и остальную карту без переформатирования.
                if node.value not in raw:
                    raise ValueError(f"Unsupported scalar encoding at line {node.start_mark.line + 1}")
                edits.append((node.start_mark.index, node.end_mark.index, raw.replace(node.value, value, 1)))
            elif re.fullmatch(r"Lime[A-Z]\w*", value) and value not in vanilla_ids:
                raise ValueError(f"Unknown legacy identifier: {value}")

    for document in yaml.compose_all(text):
        if document is not None:
            visit(document)
    for start, end, value in sorted(edits, reverse=True):
        text = text[:start] + value + text[end:]
    return text


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("destination", type=Path)
    args = parser.parse_args()
    if args.source.resolve() == args.destination.resolve():
        parser.error("Source and destination must differ")
    result = convert(args.source.read_text(encoding="utf-8-sig"))
    # Exclusive create: существующая карта никогда не перезаписывается.
    with args.destination.open("x", encoding="utf-8", newline="") as output:
        output.write(result)
    print(f"Converted map: {args.destination}")


if __name__ == "__main__":
    main()
