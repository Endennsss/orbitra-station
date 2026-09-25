using Content.Server._Orbitra.Ratvar;
using Content.Shared.Administration;
using Content.Shared.Antag;
using Content.Shared.Database;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Roles;
using Content.Shared.Storage;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Administration.Systems;

public sealed partial class AdminVerbSystem
{
    [Dependency] private readonly OrbitraRatvarRuleSystem _orbitraRatvarRule = default!;

    private static readonly EntProtoId OrbitraRatvarRuleId = "OrbitraRatvarRule";
    private static readonly ProtoId<AntagSpecifierPrototype> OrbitraRatvarAntagId = "OrbitraRatvarCultist";
    private static readonly ProtoId<StartingGearPrototype> OrbitraRatvarGearId = "OrbitraRatvarGear";

    private void AddOrbitraRatvarVerb(GetVerbsEvent<Verb> args, ICommonSession admin)
    {
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("orbitra-ratvar-admin-make"),
            Category = VerbCategory.Antag,
            Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/_Orbitra/Ratvar/garb.rsi"), "helmet"),
            Disabled = !CanMakeOrbitraRatvarCultist(admin, args.Target),
            Message = Loc.GetString("orbitra-ratvar-admin-description"),
            Impact = LogImpact.High,
            Act = () => TryMakeOrbitraRatvarCultist(admin, args.Target),
        });
    }

    /// <summary>Assigns the cult through native antag selection, including gear and rule membership.</summary>
    public bool TryMakeOrbitraRatvarCultist(ICommonSession admin, EntityUid target)
    {
        if (!CanMakeOrbitraRatvarCultist(admin, target))
            return false;

        var session = Comp<ActorComponent>(target).PlayerSession;
        _antag.ForceMakeAntag<OrbitraRatvarRuleComponent>(session, OrbitraRatvarRuleId, OrbitraRatvarAntagId);
        if (!_orbitraRatvarRule.TryGetCult(target, out _))
            return false;

        // Штатная выдача пропускает storage-предметы, если соответствующего хранилища нет.
        foreach (var (slot, items) in ProtoMan.Index(OrbitraRatvarGearId).Storage)
        {
            if (_inventorySystem.TryGetSlotEntity(target, slot, out var storage) && HasComp<StorageComponent>(storage))
                continue;

            foreach (var item in items)
            {
                var spawned = Spawn(item, Transform(target).Coordinates);
                _handsSystem.TryPickupAnyHand(target, spawned, checkActionBlocker: false);
            }
        }

        return true;
    }

    /// <summary>Rechecks admin authority and the current body; repeated grants cannot duplicate equipment.</summary>
    public bool CanMakeOrbitraRatvarCultist(ICommonSession admin, EntityUid target)
    {
        if (!_adminManager.HasAdminFlag(admin, AdminFlags.Fun) || Deleted(target) ||
            !TryComp<ActorComponent>(target, out var actor) || actor.PlayerSession.AttachedEntity != target ||
            !TryComp<HumanoidProfileComponent>(target, out var profile) || profile.Species != "Human" ||
            !TryComp<MobStateComponent>(target, out var mob) || mob.CurrentState == MobState.Dead ||
            !_mindSystem.TryGetMind(target, out var mind, out var mindComp) || mindComp.OwnedEntity != target ||
            _role.MindIsAntagonist(mind) || !_antag.IsEntityValid(target, OrbitraRatvarAntagId))
            return false;

        // Завершённый культ нельзя повторно активировать выдачей роли через контекстное меню.
        var rules = EntityQueryEnumerator<OrbitraRatvarRuleComponent>();
        while (rules.MoveNext(out var uid, out var rule))
        {
            if (rule.Won || rule.Lost || !_gameTicker.IsGameRuleActive(uid))
                return false;
        }

        return true;
    }
}
