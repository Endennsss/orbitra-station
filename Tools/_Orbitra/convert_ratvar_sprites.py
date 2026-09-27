"""Reproduce the audited Ratvar DMI subset as RSI assets. Requires Pillow."""

import io
import argparse
import functools
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
    "traps": ("icons/obj/clockwork_objects.dmi", {
        "lever": "lever", "pressure_sensor": "plate", "delayer": "delay",
        "delayer_active": "delay_active", "brass_skewer": "skewer",
        "brass_skewer_extended": "skewer_extended", "brass_skewer_pokeybit": "skewer_tip",
        "flipper": "flipper",
    }),
    "cogscarab": ("icons/mob/drone.dmi", {"drone_clock": "alive", "drone_clock_dead": "dead", "drone_clock_hat": "shell"}),
    "cache": ("icons/obj/clockwork_objects.dmi", {"tinkerers_cache": "cache"}),
    "cache_items": ("icons/obj/clothing/clockwork_garb.dmi", {
        "clockwork_cuirass_speed": "robes", "clockwork_cloak": "cloak", "wraith_specs": "spectacles",
    }),
    "robes": ("icons/mob/clothing/suits/armor.dmi", {"clockwork_cuirass_speed": "equipped-OUTERCLOTHING"}),
    "cloak": ("icons/mob/clothing/suits/armor.dmi", {"clockwork_cloak": "equipped-OUTERCLOTHING"}),
    "spectacles": ("icons/mob/clothing/eyes.dmi", {"wraith_specs": "equipped-EYES"}),
    "stargazer": ("icons/obj/clockwork_objects.dmi", {
        "stargazer": "active", "stargazer_closed": "idle",
        "stargazer_unwrenched": "unanchored", "stargazer_light": "light",
    }),
    "ratvar": ("icons/effects/512x512.dmi", {"ratvar": "ratvar"}),
    "lens": ("icons/obj/clockwork_objects.dmi", {
        "interdiction_lens": "inactive", "interdiction_lens_active": "active",
        "interdiction_lens_unwrenched": "unanchored",
    }),
    "prism": ("icons/obj/clockwork_objects.dmi", {
        "prolonging_prism": "active", "prolonging_prism_inactive": "inactive",
        "prolonging_prism_unwrenched": "unanchored",
    }),
    "windows": ("icons/obj/smooth_structures/windows/clockwork_window.dmi", {
        **{f"clockwork_window-{mask}": f"source{mask}" for mask in
           [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 21, 23, 29, 31, 38, 39, 46, 47,
            55, 63, 74, 75, 78, 79, 95, 110, 111, 127, 137, 139, 141, 143, 157, 159, 175, 191, 203, 207, 223, 239, 255]},
    }),
    "doors": ("icons/obj/doors/airlocks/clockwork/pinion_airlock.dmi", {
        "closed": "source_closed", "left": "source_left", "right": "source_right",
        "fill_closed": "fill_closed", "fill_left": "fill_left", "fill_right": "fill_right",
    }),
    "manacles": ("icons/obj/items_and_weapons.dmi", {"brass_manacles": "manacles"}),
    "floors": ("icons/turf/floors.dmi", {"clockwork_floor": "floor"}),
    "grille": ("icons/obj/structures.dmi", {"ratvargrille": "full"}),
    "mechanisms": ("icons/obj/clockwork_objects.dmi", {
        "stargazer": "generator", "obelisk": "obelisk", "ocular_warden": "turret", "ratvarian_spear": "spear",
        "integration_cog": "integration_cog",
        "replica_fabricator": "fabricator", "dread_ipad": "tablet",
        "obelisk_inactive": "obelisk_inactive",
    }),
    "sigils": ("icons/effects/clockwork_effects.dmi", {
        "sigilsubmission": "conversion", "sigiltransgression": "slowing", "clock_shield": "shield",
        "sigiltransmission": "transmission",
        "clock_shield_deflect": "shield_hit", "clock_shield_break": "shield_break",
    }),
    "marauder": ("icons/mob/clockwork_mobs.dmi", {"clockwork_marauder": "alive"}),
    "garb": ("icons/obj/clothing/clockwork_garb.dmi", {
        "clockwork_helmet": "helmet", "clockwork_cuirass": "armor",
    }),
    "armor": ("icons/mob/clothing/suits/armor.dmi", {"clockwork_cuirass": "equipped-OUTERCLOTHING"}),
    "helmet": ("icons/mob/clothing/head/helmet.dmi", {"clockwork_helmet": "equipped-HELMET"}),
    "spear_left": ("icons/mob/inhands/antag/clockwork_lefthand.dmi", {"ratvarian_spear": "inhand-left"}),
    "spear_right": ("icons/mob/inhands/antag/clockwork_righthand.dmi", {"ratvarian_spear": "inhand-right"}),
    "tools_left": ("icons/mob/inhands/antag/clockwork_lefthand.dmi", {
        "replica_fabricator": "fabricator", "clockwork_slab": "tablet",
    }),
    "tools_right": ("icons/mob/inhands/antag/clockwork_righthand.dmi", {
        "replica_fabricator": "fabricator", "clockwork_slab": "tablet",
    }),
    "ark": ("icons/effects/96x96.dmi", {"clockwork_gateway_charging": "charging", "clockwork_gateway_active": "active"}),
}
NOTICES = {
    "traps": "All 33 pinned clockwork_objects.dmi revisions checked by selected-state pixel hashes. Lever, pressure sensor and skewer states introduced by Ashe Higgs/Xhuis in 32c68a60bf88b5e350f8ef78609ffccc45821483 (tgstation PR 32935, credit Xhuis), compressed by qwerty in 38254deeebd3d7e81ab282e36174e1713c5ead20. Delayer and flipper introduced by PowerfulBacon in 8168ce7f06afc301b3226661703df356c5936599 (BeeStation PR 2124). Both PR notices and pinned README asset licensing reviewed; no applicable exception found. Contributors are not asserted to be sole sprite authors. Directions, frames and timing retained; CC BY-SA 3.0 retained.",
    "robes": "Worn robes pixels match the original clockwork_cuirass_speed state in icons/mob/suit.dmi at 8168ce7f06afc301b3226661703df356c5936599, PowerfulBacon, BeeStation PR 2124. All 341 legacy suit revisions and 8 pinned armor-file revisions checked. Relocated by Tsar-Salat/rkz in 9106b1d8fc151acc9f113582799e291f762ba912, PR 10869. PR 10869 also credits sprite contributors maxymax13, TaG2e, LordVollkorn, Twaticus for that broad change, not necessarily this state. No applicable exception found; CC BY-SA 3.0 retained.",
    "cloak": "Worn cloak pixels match the original clockwork_cloak state in icons/mob/suit.dmi at 8168ce7f06afc301b3226661703df356c5936599, PowerfulBacon, BeeStation PR 2124. All 341 legacy suit revisions and 8 pinned armor-file revisions checked. Relocated by Tsar-Salat/rkz in 9106b1d8fc151acc9f113582799e291f762ba912, PR 10869. PR 10869 also credits sprite contributors maxymax13, TaG2e, LordVollkorn, Twaticus for that broad change, not necessarily this state. No applicable exception found; CC BY-SA 3.0 retained.",
    "spectacles": "Worn Wraith Spectacles pixels match icons/mob/eyes.dmi at original a28a7680cba9142ab80abc5ed480474ed0310462 by Xhuis. All 68 legacy eyes revisions and 2 pinned clothing/eyes revisions checked. Relocated by Tsar-Salat/rkz in 9106b1d8fc151acc9f113582799e291f762ba912, BeeStation PR 10869; broader sprite credits maxymax13, TaG2e, LordVollkorn, Twaticus preserved without claiming state authorship. No applicable exception found; CC BY-SA 3.0 retained.",
    "cogscarab": "Clock drone states introduced in b941a47a60020f55f9be1928b5be733196caab57 by Joan Lung (tgstation PR 18685, credit Joan); compression by qwerty in 38254deeebd3d7e81ab282e36174e1713c5ead20. All 14 pinned file revisions inspected for selected-state pixels. PR notice and pinned README reviewed; no applicable license exception found. CC BY-SA 3.0 retained; pixels and timing unchanged.",
    "cache": "Tinkerer's Cache artwork introduced in a28a7680cba9142ab80abc5ed480474ed0310462 by Xhuis, revised by Joan Lung in de1c7b074ab8f1fb8db222b2c4031070a77c2599 (tgstation PR 21485, credit Joan), compressed by qwerty. All 33 pinned revisions and introducing notices reviewed. No applicable license exception found. CC BY-SA 3.0 retained.",
    "cache_items": "Robes and cloak introduced in 8168ce7f06afc301b3226661703df356c5936599 by PowerfulBacon, BeeStation PR 2124. Wraith Spectacles introduced in a28a7680cba9142ab80abc5ed480474ed0310462 by Xhuis; revised by Joan Lung in b2fcbabb0b814e673fbd413df591b9debcfafff5, tgstation PR 22188 (idea credit Dagdammit). All 11 pinned file revisions and PR notices reviewed. Contributors are not asserted to be sole sprite authors. No applicable exception found; CC BY-SA 3.0 retained.",
    "stargazer": "Stargazer, light and unwrenched states introduced in b7e7779c19b76449c290aaf2150fb93545b1a79a, Ashe Higgs/Xhuis, tgstation PR 29741. Closed state introduced in 8168ce7f06afc301b3226661703df356c5936599, PowerfulBacon, BeeStation PR 2124. All 33 pinned file revisions checked for these states; introducing PR notices and pinned asset licensing reviewed. No applicable exception found in reviewed notices. Contributors are not asserted to be sole sprite authors. Selected pixels and timings preserved; CC BY-SA 3.0 retained.",
    "ratvar": "Ratvar artwork introduced by Xhuis in a28a7680cba9142ab80abc5ed480474ed0310462 (Clockwork sprites). All three pinned file revisions reviewed: original artwork, qwerty icon compression 38254deeebd3d7e81ab282e36174e1713c5ead20, unrelated supermatter additions by AgentCitrus/itsmeow in c7cb9bb7ec6aefea2abcd4d3fb804156fa157246. Only ratvar is extracted: 512x512, sixteen 0.1-second frames. No applicable exception found in reviewed notices and pinned README; CC BY-SA 3.0 retained. Existing SS14 kitbashed spawn animation is not imported.",
    "lens": "Interdiction lens active/inactive artwork originates in a28a7680cba9142ab80abc5ed480474ed0310462, Clockwork sprites, Xhuis. Unwrenched state added in de1c7b074ab8f1fb8db222b2c4031070a77c2599 by Joan Lung, tgstation PR 21485 (credit Joan). All 33 pinned file revisions checked for state presence; both introduction commits, PR 21485 and pinned README license reviewed. No applicable exception found in reviewed notices. CC BY-SA 3.0 retained. Inactive rewind is adapted to an explicit ping-pong sequence; transition flicks are not imported. Full file contributors retained without asserting individual state authorship.",
    "prism": "Prolonging prism artwork introduced in 405fc7b0fef61c396f102791f684d13e4dbb76ec by Joan Lung (tgstation PR 28164, changelog credit Joan). All 33 pinned file-history revisions checked for selected states; introduction notice and BeeStation PR 3908 reviewed. Original names refer to an older mechanic; behavior uses only pinned Bee. Selected pixels, directions, frames and delays preserved. File history contributors are retained, not claimed as individual sprite authors. No applicable license exception found in reviewed notices; CC BY-SA 3.0 retained.",
    "sigils": "Shield hit/break states first appear in 8168ce7f06afc301b3226661703df356c5936599, PowerfulBacon, BeeStation PR 2124. All 19 file-history revisions inspected for these states; PR author notifications and pinned README licensing reviewed. Commit contributor is not a claim of sole sprite authorship. Existing sigil and shield attribution remains in the full file history. Pixels, directions and frame delays preserved.",
    "windows": "Clockwork window art and smoothing: BeeStation contributors PestoVerde322 and dwasint (PR 8123, fb90d501cd6f5d948d9698611efe8f59af22a741), BarteG44 (PR 12909). Full two-revision file history and PR author notices reviewed. Window states only; unrelated table artwork is not imported.",
    "doors": "Clockwork door file history: Xhuis, Nerd Lord, PopNotes, qwerty, MNarath and PestoVerde322 (also credited as PigeonVerde322). Smooth-door port PR 2987 credits monster860, Yogstation PR 8901. Seven file-history revisions and source PR 2987/10605 notices reviewed. Orbitra composes the pinned leaf/fill artwork into native door animation frames; this is a derivative asset under CC BY-SA 3.0.",
    "manacles": "brass_manacles introduced in 32c68a60bf88b5e350f8ef78609ffccc45821483, committed by Ashe Higgs (Clockwork Cult Defenses Patch; includes Manacle sprites). This identifies the source commit, not a claim of sole sprite authorship. State pixels traced through all 69 file-history revisions at the pinned commit. Only brass_manacles is imported; source in-hand files do not contain this state.",
    "grille": "ratvargrille: introduced by Nerd Lord in 3793e6187a25aa3edd925f2c8984fd3bc5fa3581; icon compression by qwerty in 38254deeebd3d7e81ab282e36174e1713c5ead20. State pixel hashes traced through all 84 pinned file-history revisions. Other structure states are not imported.",
    "floors": "clockwork_floor: Joan Lung; introduced by fa4ca9877cfeaf18a828581c26824a4bb9a3db03 and revised by 92fe41e4d061b3f0d9ea299cf1a5ee317bd2c97b. Pixel hashes reviewed across all 81 file revisions from the addition to the pinned version; only that revision changes this state's pixels. Other floor states are not imported.",
}


@functools.cache
def fetch(url):
    request = urllib.request.Request(url, headers={"User-Agent": "Orbitra-Ratvar-asset-converter"})
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read()


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
    if name in NOTICES:
        output["copyright"] += " " + NOTICES[name]
    if name == "ratvar":
        output["metaAtlas"] = False
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
            # DMI допускает одинаковое имя для покоя и движения, RSI требует разные ключи.
            output_name = selected[state] + ("_moving" if re.search(r"movement = 1", block) else "")
            if any(item["name"] == output_name for item in output["states"]):
                raise ValueError(f"Duplicate RSI state: {path}:{output_name}")
            delay = re.search(r"delay = ([^\n]+)", block)
            delays = [float(value) / 10 for value in delay[1].split(",")] if delay else [0.1] * frames
            if len(delays) != frames or directions not in (1, 4, 8):
                raise ValueError(f"Unsupported metadata for {path}:{state}")
            sequence = list(range(frames))
            # RSI не имеет флага rewind: разворачиваем цикл линзы без дублирования крайних кадров.
            if name == "lens" and re.search(r"rewind = 1", block) and frames > 1:
                sequence += list(range(frames - 2, 0, -1))
            output_frames = len(sequence)
            columns = math.ceil(math.sqrt(directions * output_frames))
            image = Image.new("RGBA", (columns * width, math.ceil(directions * output_frames / columns) * height))
            # DMI чередует направления внутри кадра; RSI хранит все кадры каждого направления подряд.
            for direction in range(directions):
                for target_frame, frame in enumerate(sequence):
                    source_index = offset + frame * directions + direction
                    x = source_index % (source.width // width) * width
                    y = source_index // (source.width // width) * height
                    target_index = direction * output_frames + target_frame
                    image.paste(source.crop((x, y, x + width, y + height)),
                                (target_index % columns * width, target_index // columns * height))
            image.save(folder / f"{output_name}.png")
            output["states"].append({"name": output_name, "directions": directions,
                                     "delays": [[delays[frame] for frame in sequence] for _ in range(directions)]})
        offset += directions * frames
    if found != set(selected):
        raise ValueError(f"Missing states: {set(selected) - found}")
    changes = "Selected states extracted and renamed; frame order converted to RSI; delays converted from deciseconds to seconds; pixels unchanged."
    if any(item["name"].endswith("_moving") for item in output["states"]):
        changes += " DMI movement variants retain their frames under distinct _moving RSI names; idle states are not overwritten."
    if name == "lens":
        changes += " Inactive DMI rewind adapted into ten-frame ping-pong playback, omitting repeated endpoints."
    if name in ("windows", "doors"):
        changes = adapt_territory(name, folder, output)
        output["copyright"] = output["copyright"].replace("no pixel edits", "derived layout; see changes below")
    (folder / "meta.json").write_text(json.dumps(output, indent=2) + "\n", encoding="utf-8")
    return {"rsi": f"{name}.rsi", "source": source_url, "path": path, "commit": COMMIT,
            "states": selected, "license": "CC-BY-SA-3.0", "history": entries,
            "notice": NOTICES.get(name, "See file-history contributors and the shared LICENSE.txt."),
            "changes": changes}


def adapt_territory(name, folder, output):
    """Adapt audited Bee images to native SS14 smoothing and door states."""
    if name == "windows":
        # Углы RSI: юго-восток, северо-запад, северо-восток, юго-запад (S/N/E/W).
        corners = [(4, 2, 32, (16, 16, 32, 32)), (8, 1, 128, (0, 0, 16, 16)),
                   (1, 4, 16, (16, 0, 32, 16)), (2, 8, 64, (0, 16, 16, 32))]
        for flags in range(8):
            sheet = Image.new("RGBA", (64, 64))
            for direction, (ccw, cw, diagonal, box) in enumerate(corners):
                mask = (ccw if flags & 1 else 0) | (cw if flags & 4 else 0)
                if flags & 1 and flags & 4 and flags & 2:
                    mask |= diagonal
                with Image.open(folder / f"source{mask}.png") as source:
                    sheet.paste(source.crop(box), (direction % 2 * 32 + box[0], direction // 2 * 32 + box[1]))
            sheet.save(folder / f"window{flags}.png")
            output["states"].append({"name": f"window{flags}", "directions": 4})
        with Image.open(folder / "source0.png") as source:
            source.save(folder / "full.png")
        output["states"].append({"name": "full"})
        changes = "Bee 8-bit adjacency images cropped into SS14 four-direction corner layers; no repainting. Original source states retained."
    else:
        def leaf(part):
            with Image.open(folder / f"fill_{part}.png") as fill, Image.open(folder / f"source_{part}.png") as outline:
                return Image.alpha_composite(fill.convert("RGBA"), outline.convert("RGBA"))

        left, right = leaf("left"), leaf("right")
        frames = []
        for offset in range(0, 17, 2):
            frame = Image.new("RGBA", (32, 32))
            frame.alpha_composite(left, (-offset, 0))
            frame.alpha_composite(right, (offset, 0))
            frames.append(frame)
        frames[0] = leaf("closed")
        for state, images in {"closed": [frames[0]], "open": [frames[-1]],
                              "opening": frames, "closing": list(reversed(frames))}.items():
            sheet = Image.new("RGBA", (32 * len(images), 32))
            for index, frame in enumerate(images):
                sheet.paste(frame, (index * 32, 0))
            sheet.save(folder / f"{state}.png")
            output["states"].append({"name": state, "delays": [[0.05] * len(images)]})
        changes = "Bee fill/outline layers composited; door leaves translated into nine opening and closing frames (0.05 seconds each). No repainting. Source layers retained."
    output["copyright"] += " " + changes
    return changes


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--only", nargs="+", choices=SELECTION,
                        help="Regenerate selected audited sets, preserving the remaining provenance records.")
    args = parser.parse_args()
    previous = ROOT / "provenance.json"
    records = {record["rsi"]: record for record in json.loads(previous.read_text(encoding="utf-8"))["resources"]} if args.only else {}
    for name in args.only or SELECTION:
        path, states = SELECTION[name]
        records[f"{name}.rsi"] = convert(name, path, states)
    manifest = {"license": "https://creativecommons.org/licenses/by-sa/3.0/",
                "license_evidence": f"https://github.com/{REPOSITORY}/blob/{COMMIT}/README.md#license",
                "audit": "Pinned repository asset-license declaration and full selected-file commit histories reviewed. No applicable per-file exception found. Commit contributors are not asserted to be individual sprite authors. DM source is not imported.",
                "resources": list(records.values())}
    (ROOT / "provenance.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"Converted {len(records)} audited sets to {ROOT}")
