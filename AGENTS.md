# Orbitra agent guide

This is Orbitra, a Space Station 14 fork, regardless of checkout name or Git remote.

Read the always-on rules in `.agents/rules` at task start. Reapply the [preflight rule](.agents/rules/ss14-skill-preflight-and-refresh.md) after context compaction or a scope change. Load only relevant skills from `.agents/skills`. The `.agents` sources take precedence over compatibility bridges.

For guidance changes, follow the [rule policy](.agents/rules/AUTHORING_POLICY.md) and [skill policy](.agents/skills/AUTHORING_POLICY.md), update the affected bridges in the same change, and run both bridge checks:

```sh
pwsh -File .agents/rules/check-rule-bridges.ps1
pwsh -File .agents/skills/check-skill-bridges.ps1
```
