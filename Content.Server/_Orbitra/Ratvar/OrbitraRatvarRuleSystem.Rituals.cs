using System.Numerics;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.ActionBlocker;
using Content.Shared.Cuffs.Components;
using Content.Shared.DoAfter;
using Content.Shared.Database;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Mindshield.Components;
using Robust.Shared.Containers;

namespace Content.Server._Orbitra.Ratvar;

public sealed partial class OrbitraRatvarRuleSystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedContainerSystem _containers = default!;

    private void InitializeRituals()
    {
        SubscribeLocalEvent<OrbitraRatvarTabletComponent, AfterInteractEvent>(OnTabletInteract);
        SubscribeLocalEvent<OrbitraRatvarTabletComponent, OrbitraRatvarConversionEvent>(OnConversionFinished);
        SubscribeLocalEvent<OrbitraRatvarTabletComponent, DoAfterAttemptEvent<OrbitraRatvarConversionEvent>>(OnConversionAttempt);
    }

    private void OnConversionAttempt(Entity<OrbitraRatvarTabletComponent> ent, ref DoAfterAttemptEvent<OrbitraRatvarConversionEvent> args)
    {
        var ritual = args.DoAfter.Args;
        if (ritual.Target is not { } target || !CanConvert(ent, ritual.User, target, out _) ||
            !SameRitualMind(ent.Comp, ritual.User) || !_mind.TryGetMind(target, out var mind, out _) ||
            mind != ent.Comp.TargetMind) args.Cancel();
    }

    private void OnTabletInteract(Entity<OrbitraRatvarTabletComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.Handled && args.CanReach && args.Target is { } target)
            args.Handled = TryStartConversion(ent, args.User, target);
    }

    private void OnConversionFinished(Entity<OrbitraRatvarTabletComponent> ent, ref OrbitraRatvarConversionEvent args)
    {
        if (args.Handled) return;
        ent.Comp.Busy = false;
        ent.Comp.PendingTarget = null;
        args.Handled = true;
        if (!args.Cancelled && SameRitualMind(ent.Comp, args.User) && args.Target is { } target &&
            _mind.TryGetMind(target, out var mind, out _) && mind == ent.Comp.TargetMind)
            TryConvert(ent, args.User, target);
        ent.Comp.RitualMind = null;
        ent.Comp.TargetMind = null;
    }

    public bool TryStartConversion(Entity<OrbitraRatvarTabletComponent> tablet, EntityUid user, EntityUid target)
    {
        if (tablet.Comp.Busy || !CanConvert(tablet, user, target, out var rule)) return false;
        var tablets = EntityQueryEnumerator<OrbitraRatvarTabletComponent>();
        while (tablets.MoveNext(out _, out var other))
            if (other.Busy && other.PendingTarget == target) return false;
        var args = new DoAfterArgs(EntityManager, user, rule.Comp.ConversionDelay,
            new OrbitraRatvarConversionEvent(), tablet, target, tablet)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
            AttemptFrequency = AttemptFrequency.EveryTick,
        };
        if (!_doAfter.TryStartDoAfter(args)) return false;
        tablet.Comp.Busy = true;
        tablet.Comp.PendingTarget = target;
        _mind.TryGetMind(user, out var casterMind, out _);
        _mind.TryGetMind(target, out var targetMind, out _);
        tablet.Comp.RitualMind = casterMind;
        tablet.Comp.TargetMind = targetMind;
        return true;
    }

    public bool TryConvert(Entity<OrbitraRatvarTabletComponent> tablet, EntityUid user, EntityUid target)
    {
        if (!CanConvert(tablet, user, target, out var rule) || !_mind.TryGetMind(target, out var mind, out _)) return false;
        _roles.MindAddRole(mind, CultRole);
        BindMember(rule, mind);
        rule.Comp.Converted.Add(mind);
        _adminLog.Add(LogType.Mind, LogImpact.High, $"Ratvar cult: {ToPrettyString(user)} converted {ToPrettyString(target)} ({ToPrettyString(mind)}).");
        return true;
    }

    public bool CanConvert(Entity<OrbitraRatvarTabletComponent> tablet, EntityUid user, EntityUid target,
        out Entity<OrbitraRatvarRuleComponent> rule)
    {
        rule = default;
        if (!TryGetCult(user, out rule) || !CanReciteInBody(user) || !Living(user) || !_blocker.CanInteract(user, target) ||
            !_hands.IsHolding(user, tablet) || !Living(target) ||
            !TryComp<HumanoidProfileComponent>(target, out var humanoid) || humanoid.Species != "Human" ||
            !TryComp<CuffableComponent>(target, out var cuffs) || cuffs.CuffedHandCount == 0 ||
            _containers.IsEntityInContainer(target) || !_interaction.InRangeUnobstructed(user, target) ||
            !_mind.TryGetMind(target, out var mind, out _) || _roles.MindIsAntagonist(mind) ||
            TryComp<MindShieldStatusComponent>(target, out var shield) && shield.IsMindshielded ||
            _jobs.MindTryGetJobId(mind, out var job) && job == "Chaplain" ||
            rule.Comp.ProtectedUntil.TryGetValue(mind, out var until) && until > Timing.CurTime)
            return false;
        var sigils = EntityQueryEnumerator<OrbitraRatvarStructureComponent, TransformComponent>();
        while (sigils.MoveNext(out var uid, out var sigil, out var xform))
        {
            if (sigil.Rule == rule.Owner && sigil.ConversionSigil && xform.Anchored &&
                Near(uid, target, 0.65f) && Near(user, target, rule.Comp.RitualRange)) return true;
        }
        return false;
    }

    private bool Near(EntityUid first, EntityUid second, float range)
    {
        var a = _transform.GetMapCoordinates(first);
        var b = _transform.GetMapCoordinates(second);
        return a.MapId == b.MapId && Vector2.DistanceSquared(a.Position, b.Position) <= range * range;
    }

    private bool CanReciteInBody(EntityUid user) =>
        TryComp<HumanoidProfileComponent>(user, out var profile) && profile.Species == "Human" &&
        _mind.TryGetMind(user, out var mind, out _) &&
        _roles.MindHasRole<OrbitraRatvarRoleComponent>(mind, out var role) && !role.Value.Comp2.Marauder;

    private bool SameRitualMind(OrbitraRatvarTabletComponent tablet, EntityUid user) =>
        _mind.TryGetMind(user, out var mind, out _) && tablet.RitualMind == mind;
}
