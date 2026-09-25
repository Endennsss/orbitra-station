"""Reproduce the audited Ratvar DMI subset as RSI assets. Requires Pillow."""

import io
import json
import math
import re
import urllib.request
from pathlib import Path

from PIL import Image

COMMIT = "a24fe414a304d3d73e30f4b9d8d3d7ade4a3b2e4"
REPOSITORY = "BeeStation/BeeStation-Hornet"
ROOT = Path(__file__).resolve().parents[2] / "Resources/Textures/_Orbitra/Ratvar"
SELECTION = {
    "mechanisms": ("icons/obj/clockwork_objects.dmi", {
        "stargazer": "generator", "obelisk": "obelisk", "ocular_warden": "turret", "ratvarian_spear": "spear",
        "integration_cog": "integration_cog",
    }),
    "sigils": ("icons/effects/clockwork_effects.dmi", {
        "sigilsubmission": "conversion", "sigiltransgression": "slowing", "clock_shield": "shield",
    }),
    "marauder": ("icons/mob/clockwork_mobs.dmi", {"clockwork_marauder": "alive"}),
    "garb": ("icons/obj/clothing/clockwork_garb.dmi", {
        "clockwork_helmet": "helmet", "clockwork_cuirass": "armor",
    }),
    "armor": ("icons/mob/clothing/suits/armor.dmi", {"clockwork_cuirass": "equipped-OUTERCLOTHING"}),
    "helmet": ("icons/mob/clothing/head/helmet.dmi", {"clockwork_helmet": "equipped-HELMET"}),
    "spear_left": ("icons/mob/inhands/antag/clockwork_lefthand.dmi", {"ratvarian_spear": "inhand-left"}),
    "spear_right": ("icons/mob/inhands/antag/clockwork_righthand.dmi", {"ratvarian_spear": "inhand-right"}),
    "ark": ("icons/effects/96x96.dmi", {"clockwork_gateway_charging": "charging", "clockwork_gateway_active": "active"}),
}


def fetch(url):
    request = urllib.request.Request(url, headers={"User-Agent": "Orbitra-Ratvar-asset-converter"})
    return urllib.request.urlopen(request).read()


def history(path):
    result = []
    for page in range(1, 50):
        url = f"https://api.github.com/repos/{REPOSITORY}/commits?sha={COMMIT}&path={path}&per_page=100&page={page}"
        batch = json.loads(fetch(url))
        result.extend({"commit": c["sha"], "contributor": c["commit"]["author"]["name"],
                       "subject": c["commit"]["message"].splitlines()[0]} for c in batch)
        if len(batch) < 100:
            return result
    raise RuntimeError("Incomplete history; refusing to import")


def convert(name, path, selected):
    source_url = f"https://github.com/{REPOSITORY}/blob/{COMMIT}/{path}"
    source = Image.open(io.BytesIO(fetch(f"https://raw.githubusercontent.com/{REPOSITORY}/{COMMIT}/{path}")))
    metadata = source.info["Description"]
    width = int(re.search(r"width = (\d+)", metadata)[1])
    height = int(re.search(r"height = (\d+)", metadata)[1])
    source = source.convert("RGBA")
    entries = history(path)
    contributors = sorted({entry["contributor"] for entry in entries})
    folder = ROOT / f"{name}.rsi"
    folder.mkdir(parents=True, exist_ok=True)
    output = {"version": 1, "size": {"x": width, "y": height}, "license": "CC-BY-SA-3.0",
              "copyright": f"From {source_url}. File history contributors (not individual state authors): {', '.join(contributors)}. Selected states extracted, renamed and repacked from DMI to RSI; no pixel edits. See ../provenance.json.",
              "states": []}
    offset = 0
    found = set()
    for match in re.finditer(r'state = "(.*?)"\n(.*?)(?=state = |# END DMI|\Z)', metadata, re.S):
        state, block = match.groups()
        dirs_match = re.search(r"dirs = (\d+)", block)
        frames_match = re.search(r"frames = (\d+)", block)
        directions = int(dirs_match[1]) if dirs_match else 1
        frames = int(frames_match[1]) if frames_match else 1
        if state in selected:
            found.add(state)
            delay = re.search(r"delay = ([^\n]+)", block)
            delays = [float(value) / 10 for value in delay[1].split(",")] if delay else [0.1] * frames
            if len(delays) != frames or directions not in (1, 4, 8):
                raise ValueError(f"Unsupported metadata for {path}:{state}")
            columns = math.ceil(math.sqrt(directions * frames))
            image = Image.new("RGBA", (columns * width, math.ceil(directions * frames / columns) * height))
            # DMI чередует направления внутри кадра; RSI хранит все кадры каждого направления подряд.
            for direction in range(directions):
                for frame in range(frames):
                    source_index = offset + frame * directions + direction
                    x = source_index % (source.width // width) * width
                    y = source_index // (source.width // width) * height
                    target_index = direction * frames + frame
                    image.paste(source.crop((x, y, x + width, y + height)),
                                (target_index % columns * width, target_index // columns * height))
            image.save(folder / f"{selected[state]}.png")
            output["states"].append({"name": selected[state], "directions": directions,
                                     "delays": [delays.copy() for _ in range(directions)]})
        offset += directions * frames
    if found != set(selected):
        raise ValueError(f"Missing states: {set(selected) - found}")
    (folder / "meta.json").write_text(json.dumps(output, indent=2) + "\n", encoding="utf-8")
    return {"rsi": f"{name}.rsi", "source": source_url, "path": path, "commit": COMMIT,
            "states": selected, "license": "CC-BY-SA-3.0", "history": entries,
            "changes": "Selected states extracted and renamed; frame order converted to RSI; delays converted from deciseconds to seconds; pixels unchanged."}


if __name__ == "__main__":
    records = [convert(name, path, states) for name, (path, states) in SELECTION.items()]
    manifest = {"license": "https://creativecommons.org/licenses/by-sa/3.0/",
                "license_evidence": f"https://github.com/{REPOSITORY}/blob/{COMMIT}/README.md#license",
                "audit": "Pinned repository asset-license declaration and full selected-file commit histories reviewed. No applicable per-file exception found. Commit contributors are not asserted to be individual sprite authors. DM source is not imported.",
                "resources": records}
    (ROOT / "provenance.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"Converted {len(records)} audited sets to {ROOT}")
