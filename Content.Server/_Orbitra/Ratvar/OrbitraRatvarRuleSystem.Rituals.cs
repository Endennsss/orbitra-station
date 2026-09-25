using System.Numerics;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.Database;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Mindshield.Components;
using Content.Shared.Popups;
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
    [Dependency] private SharedPopupSystem _popup = default!;

    private void InitializeRituals()
    {
        SubscribeLocalEvent<OrbitraRatvarTabletComponent, AfterInteractEvent>(OnTabletInteract);
        SubscribeLocalEvent<OrbitraRatvarTabletComponent, OrbitraRatvarConversionEvent>(OnConversionFinished);
        SubscribeLocalEvent<OrbitraRatvarTabletComponent, DoAfterAttemptEvent<OrbitraRatvarConversionEvent>>(OnConversionAttempt);
    }

    private void OnConversionAttempt(Entity<OrbitraRatvarTabletComponent> ent, ref DoAfterAttemptEvent<OrbitraRatvarConversionEvent> args)
    {
        var ritual = args.DoAfter.Args;
        if (ritual.Target is not { } target || ent.Comp.RitualSigil is not { } sigil ||
            !CanConvert(ent, ritual.User, target, out _, requiredSigil: sigil) ||
            ent.Comp.RitualOrigin is not { } origin ||
            !_transform.InRange(Transform(ritual.User).Coordinates, origin, 0.3f) ||
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
        ent.Comp.RitualOrigin = null;
        ent.Comp.RitualSigil = null;
    }

    public bool TryStartConversion(Entity<OrbitraRatvarTabletComponent> tablet, EntityUid user, EntityUid target)
    {
        if (tablet.Comp.Busy)
            return ConversionDenied(user, "orbitra-ratvar-unavailable-busy", false);
        if (!CanConvert(tablet, user, target, out var rule, quiet: false)) return false;
        var tablets = EntityQueryEnumerator<OrbitraRatvarTabletComponent>();
        while (tablets.MoveNext(out _, out var other))
            if (other.Busy && other.PendingTarget == target)
                return ConversionDenied(user, "orbitra-ratvar-conversion-busy", false);
        if (!TryGetConversionSigil(target, rule.Owner, out var sigil)) return false;
        var args = new DoAfterArgs(EntityManager, user, rule.Comp.ConversionDelay,
            new OrbitraRatvarConversionEvent(), tablet, target, tablet)
        {
            // Стандартный BreakOnMove отменяет действие и при движении цели внутри печати.
            BreakOnMove = false,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
            AttemptFrequency = AttemptFrequency.EveryTick,
        };
        tablet.Comp.Busy = true;
        tablet.Comp.PendingTarget = target;
        _mind.TryGetMind(user, out var casterMind, out _);
        _mind.TryGetMind(target, out var targetMind, out _);
        tablet.Comp.RitualMind = casterMind;
        tablet.Comp.TargetMind = targetMind;
        tablet.Comp.RitualOrigin = Transform(user).Coordinates;
        tablet.Comp.RitualSigil = sigil;
        // EveryTick вызывает проверку уже внутри TryStartDoAfter, поэтому контекст готов заранее.
        if (!_doAfter.TryStartDoAfter(args))
        {
            tablet.Comp.Busy = false;
            tablet.Comp.PendingTarget = null;
            tablet.Comp.RitualMind = null;
            tablet.Comp.TargetMind = null;
            tablet.Comp.RitualOrigin = null;
            tablet.Comp.RitualSigil = null;
            return false;
        }
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
        out Entity<OrbitraRatvarRuleComponent> rule, bool quiet = true, EntityUid? requiredSigil = null)
    {
        rule = default;
        if (!TryGetCult(user, out rule) || !CanReciteInBody(user)) return false;
        if (!Living(user) || !_blocker.CanInteract(user, target) || !_hands.IsHolding(user, tablet) ||
            _containers.IsEntityInContainer(user))
            return ConversionDenied(user, "orbitra-ratvar-conversion-caster", quiet);
        if (!Living(target) ||
            !TryComp<HumanoidProfileComponent>(target, out var humanoid) || humanoid.Species != "Human" ||
            !_mind.TryGetMind(target, out var mind, out _))
            return ConversionDenied(user, "orbitra-ratvar-conversion-target", quiet);
        if (_roles.MindIsAntagonist(mind) ||
            TryComp<MindShieldStatusComponent>(target, out var shield) && shield.IsMindshielded ||
            _jobs.MindTryGetJobId(mind, out var job) && job == "Chaplain" ||
            rule.Comp.ProtectedUntil.TryGetValue(mind, out var until) && until > Timing.CurTime)
            return ConversionDenied(user, "orbitra-ratvar-conversion-protected", quiet);
        if (_containers.IsEntityInContainer(target) || !_interaction.InRangeUnobstructed(user, target) ||
            !Near(user, target, rule.Comp.RitualRange))
            return ConversionDenied(user, "orbitra-ratvar-conversion-range", quiet);
        // Во время ритуала проверяется одна выбранная печать, без обхода построек каждый тик.
        var onSigil = requiredSigil is { } sigil
            ? ValidConversionSigil(sigil, target, rule.Owner)
            : TryGetConversionSigil(target, rule.Owner, out _);
        return onSigil ||
            ConversionDenied(user, "orbitra-ratvar-conversion-sigil", quiet);
    }

    private bool ConversionDenied(EntityUid user, string message, bool quiet)
    {
        if (!quiet) _popup.PopupEntity(Loc.GetString(message), user, user);
        return false;
    }

    private bool ValidConversionSigil(EntityUid uid, EntityUid target, EntityUid rule) =>
        TryComp<OrbitraRatvarStructureComponent>(uid, out var sigil) && sigil.Rule == rule &&
        sigil.ConversionSigil && Transform(uid).Anchored && Near(uid, target, 0.65f);

    private bool TryGetConversionSigil(EntityUid target, EntityUid rule, out EntityUid sigil)
    {
        var sigils = EntityQueryEnumerator<OrbitraRatvarStructureComponent>();
        while (sigils.MoveNext(out var uid, out _))
        {
            if (!ValidConversionSigil(uid, target, rule)) continue;
            sigil = uid;
            return true;
        }
        sigil = default;
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
