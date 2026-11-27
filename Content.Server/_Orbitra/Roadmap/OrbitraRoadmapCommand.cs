using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Orbitra.Roadmap;

[AnyCommand]
public sealed class OrbitraRoadmapCommand : LocalizedEntityCommands
{
    public override string Command => "roadmap";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is {} player)
            EntityManager.System<OrbitraAtlasSystem>().TryOpen(player);
    }
}
