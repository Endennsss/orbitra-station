"""Import the audited pinned Bee sound subset. Requires ffmpeg and ffprobe."""
import hashlib
import json
import subprocess
import tempfile
from pathlib import Path

from convert_ratvar_sprites import COMMIT, REPOSITORY, fetch, history

ROOT = Path(__file__).resolve().parents[2] / "Resources/Audio/_Orbitra/Ratvar"
SELECTION = {
    "stargazer_activate": ("sound/machines/clockcult/stargazer_activate.ogg", True, "Xhuis (Ashe Higgs), Rob Bailey (actioninja)", "b7e7779c19b76449c290aaf2150fb93545b1a79a"),
    "ratvar_reveal": ("sound/effects/ratvar_reveal.ogg", False, "Xhuis", "76c033cfdc71b75f5e12dc52939aefb21bb81588"),
    "ark_activation": ("sound/magic/clockwork/ark_activation.ogg", True, "Xhuis (Ashe Higgs), Rob Bailey (actioninja)", "b7e7779c19b76449c290aaf2150fb93545b1a79a"),
    "integration_cog_install": ("sound/machines/clockcult/integration_cog_install.ogg", False, "Xhuis (Ashe Higgs), Rob Bailey (actioninja)", "b7e7779c19b76449c290aaf2150fb93545b1a79a"),
    "invoke_general": ("sound/magic/clockwork/invoke_general.ogg", True, "Xhuis, Rob Bailey (actioninja)", "45ef8a5e00c95d8c80f4506f96fb7876ee206f48"),
    "ark_deathrattle": ("sound/machines/clockcult/ark_deathrattle.ogg", True, "Xhuis (Ashe Higgs), Rob Bailey (actioninja)", "32c68a60bf88b5e350f8ef78609ffccc45821483"),
}
CONVERSION = "7e569eb99797fa89a22100a758e09bdff5733099"
EVIDENCE = [
    f"https://github.com/{REPOSITORY}/blob/{COMMIT}/README.md#license",
    f"https://github.com/{REPOSITORY}/blob/{COMMIT}/sound/effects/license.txt",
    f"https://github.com/{REPOSITORY}/blob/{COMMIT}/sound/machines/license.txt",
    "https://github.com/tgstation/tgstation/pull/29741",
    "https://github.com/tgstation/tgstation/pull/32935",
    "https://github.com/tgstation/tgstation/pull/43550",
]


def probe(path):
    return json.loads(subprocess.check_output([
        "ffprobe", "-v", "error", "-show_entries",
        "format=duration:format_tags:stream=codec_name,channels,sample_rate:stream_tags",
        "-of", "json", str(path)], text=True))


def main():
    ROOT.mkdir(parents=True, exist_ok=True)
    records = []
    notices = []
    with tempfile.TemporaryDirectory(prefix="orbitra-ratvar-audio-") as temp:
        for name, (path, mono, credits, introduction) in SELECTION.items():
            entries = history(path)
            expected = {introduction} if name == "ratvar_reveal" else {introduction, CONVERSION}
            if {entry["commit"] for entry in entries} != expected:
                raise RuntimeError(f"Unreviewed history for {path}")
            data = fetch(f"https://raw.githubusercontent.com/{REPOSITORY}/{COMMIT}/{path}")
            source = Path(temp) / f"{name}.ogg"
            source.write_bytes(data)
            metadata = probe(source)
            # Проверенные файлы не содержат дополнительных уведомлений Vorbis.
            if metadata.get("format", {}).get("tags") or any(s.get("tags") for s in metadata["streams"]):
                raise RuntimeError(f"Unreviewed embedded notice in {path}")
            output = ROOT / source.name
            changes = "Byte-identical copy from pinned BeeStation."
            if mono:
                subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", str(source), "-map_metadata", "-1",
                                "-ac", "1", "-ar", "44100", "-c:a", "libvorbis", "-q:a", "5", "-bitexact",
                                str(output)], check=True)
                changes = "Downmixed stereo to mono, 44100 Hz Vorbis quality 5, for SS14 positional playback; no trimming or gain change."
            else:
                output.write_bytes(data)
            url = f"https://github.com/{REPOSITORY}/blob/{COMMIT}/{path}"
            notice = f"Source credits: {credits}. Full file history and introducing notices reviewed; contributors are not asserted to be sole sound authors. {changes} See provenance.json and LICENSE.txt."
            records.append({"file": output.name, "path": path, "source": url, "commit": COMMIT,
                            "license": "CC-BY-SA-3.0", "credits": credits, "history": entries,
                            "source_sha256": hashlib.sha256(data).hexdigest(),
                            "output_sha256": hashlib.sha256(output.read_bytes()).hexdigest(),
                            "source_format": metadata, "output_format": probe(output), "changes": changes})
            notices.append(f'- files: [{output.name}]\n  license: CC-BY-SA-3.0\n  copyright: {json.dumps(notice)}\n  source: {url}\n')
    (ROOT / "attributions.yml").write_text("\n".join(notices), encoding="utf-8")
    manifest = {"license": "https://creativecommons.org/licenses/by-sa/3.0/", "evidence": EVIDENCE,
                "audit": "Reviewed full pinned histories, introduction commits, PR author notices, ancestor-directory license exceptions and embedded Vorbis comments. No applicable exception found for the selected sounds. No DM code imported.",
                "resources": records}
    (ROOT / "provenance.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"Imported {len(records)} audited sounds to {ROOT}")


if __name__ == "__main__":
    main()
