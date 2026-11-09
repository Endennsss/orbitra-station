---
trigger: always_on
---

# Verification

Run the narrowest checks for the change. Report actual results and important unverified behavior.

- **C#** — `dotnet build <project.csproj> -c Debug`. Use Release when checking release behavior.
- **Game prototypes, YAML, or FTL** — `dotnet run --project Content.YAMLLinter/Content.YAMLLinter.csproj -c Release --no-build`. Build that project in Release first if needed.
- **Client behavior** — run `dotnet run --project Content.Client/Content.Client.csproj` (or `-c Tools` for tool features). Check startup and IL verification. Test changed behavior separately.
- **Tests** — target `Content.Tests` or `Content.IntegrationTests` with `dotnet test <project.csproj> -c <built-config> --no-build --filter 'FullyQualifiedName~<fixture>'`. Use `SpaceStation14.slnx` only when solution-wide coverage is needed.
- **Agent guidance** — run both bridge checks from `AGENTS.md`.

Prefer one test command at a time. Never run more than two concurrently. Limit build parallelism when RAM is tight and stop any client or server started for verification.
