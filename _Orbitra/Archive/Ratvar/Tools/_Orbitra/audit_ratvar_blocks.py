"""Read-only provenance check for the Cache and Cogscarab DMI subsets."""
import concurrent.futures
import hashlib
import io
import json
import re
import urllib.error

from PIL import Image
import convert_ratvar_sprites as source

SETS = {
    "icons/mob/drone.dmi": ["drone_clock", "drone_clock_moving", "drone_clock_dead", "drone_clock_hat"],
    "icons/obj/clockwork_objects.dmi": ["tinkerers_cache"],
    "icons/obj/clothing/clockwork_garb.dmi": ["clockwork_cuirass_speed", "clockwork_cloak", "wraith_specs"],
    "icons/mob/clothing/suits/armor.dmi": ["clockwork_cuirass_speed", "clockwork_cloak"],
    "icons/mob/clothing/eyes.dmi": ["wraith_specs"],
    "icons/mob/suit.dmi": ["clockwork_cuirass_speed", "clockwork_cloak"],
    "icons/mob/eyes.dmi": ["wraith_specs"],
}


def snapshot(path, revision, wanted):
    try:
        blob = source.fetch(f"https://raw.githubusercontent.com/{source.REPOSITORY}/{revision}/{path}")
    except urllib.error.HTTPError as error:
        if error.code == 404:
            return {}
        raise
    image = Image.open(io.BytesIO(blob))
    description = image.info["Description"]
    width = int(re.search(r"width = (\d+)", description)[1])
    height = int(re.search(r"height = (\d+)", description)[1])
    offset = 0
    result = {}
    for block in re.split(r'\nstate = "', description)[1:]:
        name = block.split('"', 1)[0]
        if re.search(r"movement = 1", block):
            name += "_moving"
        directions = int(re.search(r"dirs = (\d+)", block)[1])
        frames = int(re.search(r"frames = (\d+)", block)[1])
        if name in wanted:
            digest = hashlib.sha256()
            for frame in range(offset, offset + directions * frames):
                x = frame % (image.width // width) * width
                y = frame // (image.width // width) * height
                digest.update(image.crop((x, y, x + width, y + height)).convert("RGBA").tobytes())
            result[name] = digest.hexdigest()
        offset += directions * frames
    return result


if __name__ == "__main__":
    for path, states in SETS.items():
        history = source.history(path)
        with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
            revisions = list(pool.map(lambda row: snapshot(path, row["commit"], states), history))
        print(path, "revisions", len(history), flush=True)
        for state in states:
            changes = []
            previous = None
            for row, revision in reversed(list(zip(history, revisions))):
                current = revision.get(state)
                if current != previous:
                    changes.append(row)
                previous = current
            print(state, json.dumps(changes), flush=True)
