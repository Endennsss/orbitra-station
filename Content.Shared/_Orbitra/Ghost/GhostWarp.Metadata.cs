using Content.Shared.Roles;
using System.Runtime.InteropServices;
using Robust.Shared.Prototypes;

namespace Content.Shared.Ghost.Systems;

// Сетевой сериализатор работает с полями, а не с их расположением в памяти между partial-файлами.
[StructLayout(LayoutKind.Auto)]
public partial struct GhostWarp
{
    /// <summary>Unformatted character name, independent of the localized display label.</summary>
    public string? CharacterName { get; init; }

    /// <summary>Authoritative mind job; clients can resolve its icon and department outside PVS.</summary>
    public ProtoId<JobPrototype>? Job { get; init; }

    /// <summary>Server-resolved antagonist status, sent only in an authorized ghost warp response.</summary>
    public bool IsAntagonist { get; init; }
}
