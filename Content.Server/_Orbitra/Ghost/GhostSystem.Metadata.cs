using Content.Shared.Ghost.Systems;
using Content.Shared.Roles;

namespace Content.Server.Ghost;

public sealed partial class GhostSystem
{
    [Dependency] private readonly SharedRoleSystem _roles = default!;

    /// <summary>Supplies presentation metadata without changing target eligibility or PVS.</summary>
    private GhostWarp CreateOrbitraPlayerWarp(EntityUid target, EntityUid? mind, string displayName)
    {
        _jobs.MindTryGetJob(mind, out var job);
        return new GhostWarp(GetNetEntity(target), displayName, false)
        {
            CharacterName = Name(target),
            Job = job?.ID,
            IsAntagonist = _roles.MindIsAntagonist(mind),
        };
    }
}
