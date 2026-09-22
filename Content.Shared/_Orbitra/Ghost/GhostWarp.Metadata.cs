using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.Ghost.Systems;

public partial struct GhostWarp
{
    /// <summary>Unformatted character name, independent of the localized display label.</summary>
    public string? CharacterName { get; init; }

    /// <summary>Authoritative mind job; clients can resolve its icon and department outside PVS.</summary>
    public ProtoId<JobPrototype>? Job { get; init; }
}
