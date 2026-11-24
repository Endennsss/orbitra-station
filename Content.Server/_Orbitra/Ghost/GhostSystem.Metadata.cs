using Content.Shared.Ghost.Systems;
using Content.Shared.Roles;

namespace Content.Server.Ghost;

public sealed partial class GhostSystem
{
    [Dependency] private SharedRoleSystem _roles = default!;

    /// <summary>Передаёт метаданные для интерфейса с сохранением правил выбора цели и PVS.</summary>
    private GhostWarp CreateOrbitraPlayerWarp(EntityUid target, EntityUid? mind, string displayName)
    {
        _jobs.MindTryGetJob(mind, out var job);
        return new GhostWarp(GetNetEntity(target), displayName, false)
        {
            CharacterName = Name(target),
            Job = job?.ID,
            IsAntagonist = _roles.MindIsAntagonist(mind),
            IsGhost = _ghostQuery.HasComp(target),
        };
    }
}
