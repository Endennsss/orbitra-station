---
trigger: always_on
---

# Preflight

At task start, read the always-on rules in `.agents/rules` and select relevant `.agents/skills/<name>/SKILL.md` files before planning or editing. After compaction, reload missing guidance. When scope changes, load newly relevant guidance. Reuse current context for unchanged sources. Follow canonical files directly.

Select by changed behavior and file type:

| Scope | Skills |
| --- | --- |
| ECS changes | Relevant `ss14-ecs-*` and `ss14-events` |
| Game YAML and prototypes | `ss14-naming-conventions`, `ss14-ecs-prototypes`, `ss14-upstream-maintenance` |
| Fluent localization | `ss14-naming-conventions`, `ss14-localization-strings`, `ss14-upstream-maintenance` for upstream edits |
| UI and shaders | Relevant `ss14-ui-*` and `ss14-graphics-shaders` for SWSL |
| Networking or prediction | Relevant `ss14-netcode`, `ss14-pvs`, `ss14-prediction` |
| Audio, atmos, databases, tests | Relevant subsystem skills |

Add `ss14-upstream-maintenance` for upstream edits, `ss14-documentation-writing` for C# changes over roughly 300 lines, and `ss14-standard-optimizations` for hot paths. Planning uses the same scope as implementation.

Follow selected skill constraints. If a skill is materially wrong for the task, correct it with its bridges and report the change. Keep temporary notes for large investigations only when needed for continuity. Remove them before handoff.
