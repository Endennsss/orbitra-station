# Orbitra migration

The client and server must be updated together. No mixed-version network compatibility is provided.

`rename_manifest.json` records the exact fork-owned identifier renames. Vanilla lime/slime content and attribution are not renamed.

Archived `lime.bloom_*` and `lime.particles_quality` settings are imported on client startup. Explicit `orbitra.*` values win. New values are archived on the normal configuration save path; legacy keys are not used by rendering systems.

Entity prototype aliases are in `Resources/migration.yml`. They do not rename serialized component overrides in external map files. Convert these maps first:

```powershell
python -m pip install PyYAML
python Tools/_Orbitra/Migration/convert_map.py old_map.yml orbitra_map.yml
python Tools/_Orbitra/Migration/test_convert_map.py
```

The converter refuses an existing output file and never edits the source. Known component names, prototype IDs and resource paths are migrated. Missing migrated resources and unknown legacy identifiers are errors. Load the resulting copy on a test server before replacing a deployed map.

The renamed prototype set contains no species, marking, trait, job, roleLoadout or loadout IDs stored in character profiles. Those references and the profile format remain unchanged; characters must not be reset during this migration.

Original author and license information is retained. Historical brand references in attribution are intentional.
