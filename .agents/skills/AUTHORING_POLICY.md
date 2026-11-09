# Skill authoring

Keep skill instructions and resources under `.agents/skills/<name>`. Maintain one `SKILL.md` bridge under each of `{.agent,.claude,.cursor,.github}/skills/<name>`.

Copy the canonical description and use the hyphen-case folder name for `name`. Link the body to `../../../.agents/skills/<name>/SKILL.md`. Antigravity and GitHub bridges also set `metadata.source_skill` to that path.

Update all affected bridges with the source change. Run both checks listed in `AGENTS.md`.
