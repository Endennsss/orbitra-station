---
trigger: always_on
---

# Orbitra identity and upstream edits

Use `Orbitra` for the product and fork-owned code and prototype prefixes. Put new code under `Content.{Client,Shared,Server}/_Orbitra` and assets under the matching `Resources/**/_Orbitra` tree. Retain upstream and third-party names where they identify original code or data.

Keep upstream edits small. Prefer a hook into an Orbitra partial or helper. Use the file's comment syntax and these exact markers:

- `Orbitra-Edit` for a single line.
- `Orbitra edit start` / `Orbitra edit end` for a replaced block.
- `Orbitra added start` / `Orbitra added end` for an inserted block.

Mark only the touched region. Write inline explanations and marker reasons in Russian. Preserve original identifiers, API names, localization keys, and marker tokens.
