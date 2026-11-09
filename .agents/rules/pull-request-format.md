---
trigger: always_on
---

# Pull requests

Follow Git authorization and title formatting in [the commit rule](git-commit-format.md). Use `.github/PULL_REQUEST_TEMPLATE.md`. Write descriptions and player notes in Russian. Preserve commands, paths, names, and identifiers.

- Keep one coherent change per PR. Review the diff for unrelated edits, secrets, and temporary files.
- Describe the outcome, reason, balance impact, related work, and real contributors as applicable. Preserve genuine `Co-authored-by` trailers during squash.
- Report checks actually run and manual reproduction steps. For visual changes attach in-game evidence or state that it is missing. Use a draft for materially unverified work and disclose why.
- Document breaking changes, migrations, limitations, and rollback when applicable. After changes to an approved revision, request renewed review and wait for checks on the current SHA. Respect CI gates.
- For upstream sync, record the source revision or range, conflict resolutions, and effects on Orbitra.
- For imported assets, record the source repository, exact commit, authors, per-asset licenses, and attribution location. Exclude assets with unclear or incompatible rights.
- For player-visible changes include a Russian `:cl:` entry using `add`, `remove`, `tweak`, or `fix`. For technical-only changes state that no changelog is required.
