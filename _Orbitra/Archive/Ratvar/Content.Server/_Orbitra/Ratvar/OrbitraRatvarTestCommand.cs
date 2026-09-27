using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.Administration.Systems;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Opt-in cult testing for a connected administrator, without changing global prototypes.</summary>
[AdminCommand(AdminFlags.Fun)]
public sealed partial class OrbitraRatvarTestCommand : LocalizedEntityCommands
{
    [Dependency] private OrbitraRatvarRuleSystem _ratvarRule = default!;
    [Dependency] private AdminVerbSystem _adminVerb = default!;
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private OrbitraRatvarEminenceSystem _eminence = default!;

    public override string Command => "orbitra_ratvar_test";
    public override string Description => Loc.GetString("orbitra-ratvar-test-description");
    public override string Help => Loc.GetString("orbitra-ratvar-test-help");

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 1 && args[0] == "eminence")
        {
            if (shell.Player is not { AttachedEntity: { } caller } player || !_admin.HasAdminFlag(player, AdminFlags.Fun))
            {
                shell.WriteError(Loc.GetString("orbitra-ratvar-test-player"));
                return;
            }
            if (!_eminence.TryCreateTestInvitation(caller, out _))
            {
                shell.WriteError(Loc.GetString("orbitra-ratvar-eminence-test-failed"));
                return;
            }
            shell.WriteLine(Loc.GetString("orbitra-ratvar-eminence-test-created"));
            return;
        }
        var tier = 3;
        var energy = 10000;
        if (args.Length is < 1 or > 3 || args[0] is not ("on" or "off") ||
            args[0] == "off" && args.Length != 1 ||
            args.Length >= 2 && (!int.TryParse(args[1], out tier) || tier is < 1 or > 3) ||
            args.Length >= 3 && (!int.TryParse(args[2], out energy) || energy is < 0 or > 10000))
        {
            shell.WriteError(Help);
            return;
        }
        if (shell.Player is not { AttachedEntity: { } body } admin || !_admin.HasAdminFlag(admin, AdminFlags.Fun))
        {
            shell.WriteError(Loc.GetString("orbitra-ratvar-test-player"));
            return;
        }
        var enable = args[0] == "on";
        if (enable && !_ratvarRule.TryGetCult(body, out _) && !_adminVerb.TryMakeOrbitraRatvarCultist(admin, body))
        {
            shell.WriteError(Loc.GetString("orbitra-ratvar-test-grant-failed"));
            return;
        }
        if (!_ratvarRule.TryConfigureTestMode(admin, enable ? tier : null, enable ? energy : null))
        {
            shell.WriteError(Loc.GetString("orbitra-ratvar-test-failed"));
            return;
        }
        shell.WriteLine(Loc.GetString(enable ? "orbitra-ratvar-test-enabled" : "orbitra-ratvar-test-disabled",
            ("tier", tier), ("energy", energy)));
    }
}
