# Rule authoring

Write canonical rules in concise English under `.agents/rules`. Keep rule content in one place. Use `trigger` frontmatter for rules. This policy is maintained directly and has no bridge.

For each rule `<name>.md`, maintain a matching file under `{.agent,.claude,.cursor,.github}/rules`. Copy the canonical `trigger` and link to `../../.agents/rules/<name>.md` relative to the bridge directory. In Antigravity and GitHub bridges, set `metadata.source_rule` to the same path. Keep bridge bodies to a source pointer.

Update all affected bridges with the source change. Run both checks listed in `AGENTS.md`.
