using System.Diagnostics.CodeAnalysis;
using Content.Shared._Orbitra.Surgery;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Containers;

namespace Content.Server._Orbitra.Surgery;

public sealed partial class OrbitraSurgerySystem
{
    // Перенос органов между телами сохраняет идентичность кистей и стоп.
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private DetachableOrganSystem _detach = default!;
    [Dependency] private OrganRelationSystem _relations = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private MetaDataSystem _metadata = default!;

    private void InitializeLimbs()
    {
        SubscribeLocalEvent<InitialBodyComponent, OrbitraDismemberAttemptEvent>(OnDismember);
        SubscribeLocalEvent<InitialBodyComponent, OrganRemovedFromEvent>(OnOrganRemoved);
        SubscribeLocalEvent<InitialBodyComponent, OrganInsertedIntoEvent>(OnOrganInserted);
    }

    private void OnDismember(Entity<InitialBodyComponent> patient, ref OrbitraDismemberAttemptEvent args)
    {
        if (!args.Handled && TryDetachLimb(patient, args.Zone, out _))
            args.Handled = true;
    }

    private void OnOrganRemoved(Entity<InitialBodyComponent> patient, ref OrganRemovedFromEvent args)
    {
        if (TerminatingOrDeleted(patient) || !HasComp<OrbitraSurgicalPatientComponent>(patient) ||
            !TryComp<OrganComponent>(args.Organ, out var organ) || organ.Category is not { } category)
            return;

        if (TryComp<OrbitraProstheticJointComponent>(args.Organ, out var joint))
            OnProsthesisChanged(patient, joint, false);

        if (HasComp<OrbitraOrganComponent>(args.Organ) && HasComp<OrbitraHumanOrganPatientComponent>(patient))
            OnTraumaChanged(patient);

        foreach (var zone in Zones)
        {
            if (OrbitraSurgeryAnatomySystem.Category(zone) != category.Id || !_anatomy.IsMissing(patient, zone))
                continue;

            if (zone is SurgeryZone.LeftLeg or SurgeryZone.RightLeg)
                RefreshLegs(patient);

            // Штатное отделение и операции используют один и тот же момент фактической потери органа.
            _trauma.NotifyAnatomyChanged(patient, zone, true);
            return;
        }
    }

    private void OnOrganInserted(Entity<InitialBodyComponent> patient, ref OrganInsertedIntoEvent args)
    {
        if (TryComp<OrbitraProstheticJointComponent>(args.Organ, out var joint))
            OnProsthesisChanged(patient, joint, true);

        if ((joint != null || HasComp<OrbitraMissingLegsComponent>(patient)) && IsLeg(args.Organ))
            RefreshLegs(patient);

        // Вставка штатной системой тела тоже может устранить показание к пришиванию.
        if (HasComp<OrbitraSurgeryOperationComponent>(patient) ||
            TryComp<BuckleComponent>(patient, out var buckle) && buckle.Buckled)
            OnTraumaChanged(patient);
    }

    private bool IsLeg(EntityUid organ) =>
        TryComp<OrganComponent>(organ, out var part) && part.Category?.Id is "LegLeft" or "LegRight";

    private void RefreshLegs(EntityUid patient)
    {
        var missing = (_anatomy.IsMissing(patient, SurgeryZone.LeftLeg) ? 1 : 0) +
                      (_anatomy.IsMissing(patient, SurgeryZone.RightLeg) ? 1 : 0);
        if (missing == 0)
        {
            RemComp<OrbitraMissingLegsComponent>(patient);
        }
        else
        {
            var legs = EnsureComp<OrbitraMissingLegsComponent>(patient);
            legs.Count = missing;
            Dirty(patient, legs);
        }

        _movement.RefreshMovementSpeedModifiers(patient);
    }

    /// <summary>Отделяет только руку с кистью или ногу со стопой. Обе остаются в штатном контейнере тела.</summary>
    public bool TryDetachLimb(EntityUid patient, SurgeryZone zone, [NotNullWhen(true)] out EntityUid? item)
    {
        item = null;
        if (!TryGetLimb(patient, zone, out var parent, out var root, out var extremity) ||
            !_containers.TryGetContainer(patient, BodyComponent.ContainerID, out var source) ||
            !_containers.CanRemove(root, source) || !_containers.CanRemove(extremity, source))
            return false;

        if (HasComp<OrbitraProstheticJointComponent>(root))
            return TryRemoveProsthesis(patient, zone, out item);

        if (_detach.Detach(root) is not { } detached)
            return false;

        var limb = EnsureComp<OrbitraDetachedLimbComponent>(detached);
        limb.OriginalOwner = patient;
        limb.OriginalParent = parent;
        limb.Zone = zone;
        limb.Root = root;
        limb.Extremity = extremity;
        Dirty(detached, limb);
        _metadata.SetEntityName(detached, Name(root));
        var profile = ProtoMan.Index(Comp<OrbitraSurgicalPatientComponent>(patient).TraumaProfile);
        _bloodstream.TryBleedOut(patient, profile.DismemberBloodLoss);
        _bloodstream.TryModifyBleedAmount(patient, profile.DismemberBleedRate);
        item = detached;
        return true;
    }

    private bool TryGetLimb(EntityUid patient, SurgeryZone zone, out EntityUid parent, out EntityUid root,
        out EntityUid extremity)
    {
        parent = root = extremity = default;
        if (!_anatomy.SupportsZone(patient, zone) || OrbitraSurgeryAnatomySystem.Extremity(zone) is not { } distal ||
            _anatomy.FindOrgan(patient, "Torso") is not { } torso ||
            _anatomy.FindOrgan(patient, OrbitraSurgeryAnatomySystem.Category(zone)!) is not { } proximal ||
            _anatomy.FindOrgan(patient, distal) is not { } child ||
            !HasComp<DetachableOrganComponent>(proximal) ||
            !TryComp<ChildOrganComponent>(proximal, out var upper) || upper.Parent != torso ||
            !TryComp<ParentOrganComponent>(proximal, out var joint) || joint.Children.Count != 1 ||
            !_relations.HasDirectChild((proximal, joint), child) || !TryComp<ChildOrganComponent>(child, out var lower) ||
            lower.Parent != proximal || (TryComp<ParentOrganComponent>(child, out var tip) && tip.Children.Count > 0))
            return false;

        parent = torso;
        root = proximal;
        extremity = child;
        return true;
    }

    private bool CanReattachLimb(EntityUid patient, SurgeryZone zone, EntityUid item,
        [NotNullWhen(true)] out OrbitraDetachedLimbComponent? limb,
        [NotNullWhen(true)] out BaseContainer? source, [NotNullWhen(true)] out BaseContainer? destination)
    {
        limb = null;
        source = destination = null;
        return !TerminatingOrDeleted(patient) && !TerminatingOrDeleted(item) &&
               _anatomy.IsMissing(patient, zone) && OrbitraSurgeryAnatomySystem.Extremity(zone) is { } distal &&
               _anatomy.FindOrgan(patient, distal) == null &&
               TryComp(item, out limb) && limb.OriginalOwner == patient && limb.Zone == zone &&
               _anatomy.FindOrgan(patient, "Torso") == limb.OriginalParent &&
               HasComp<ParentOrganComponent>(limb.OriginalParent) &&
               _anatomy.FindOrgan(item, OrbitraSurgeryAnatomySystem.Category(zone)!) == limb.Root &&
               _anatomy.FindOrgan(item, distal) == limb.Extremity &&
               TryComp<ChildOrganComponent>(limb.Root, out var upper) && upper.Parent == null &&
               TryComp<ParentOrganComponent>(limb.Root, out var joint) && joint.Children.Count == 1 &&
               _relations.HasDirectChild((limb.Root, joint), limb.Extremity) &&
               TryComp<ChildOrganComponent>(limb.Extremity, out var lower) && lower.Parent == limb.Root &&
               _containers.TryGetContainer(item, BodyComponent.ContainerID, out source) &&
               source.ContainedEntities.Count == 2 &&
               _containers.TryGetContainer(patient, BodyComponent.ContainerID, out destination) &&
               _containers.CanRemove(limb.Root, source) && _containers.CanRemove(limb.Extremity, source) &&
               _containers.CanInsert(limb.Root, destination) && _containers.CanInsert(limb.Extremity, destination);
    }

    /// <summary>Возвращает те же органы и восстанавливает их исходную связь с торсом.</summary>
    public bool TryReattachLimb(EntityUid patient, SurgeryZone zone, EntityUid item)
    {
        if (!CanReattachLimb(patient, zone, item, out var limb, out var source, out var destination) ||
            !_containers.Insert(limb.Root, destination))
            return false;

        if (!_containers.Insert(limb.Extremity, destination))
        {
            // Возвращаем первую часть в предмет, если вторая не прошла окончательную проверку контейнера.
            _containers.Remove(limb.Root, destination, reparent: false, force: true);
            _containers.Insert(limb.Root, source, force: true);
            return false;
        }

        _relations.Relate(limb.OriginalParent, limb.Root);
        QueueDel(item);
        _trauma.NotifyAnatomyChanged(patient, zone, false);
        return true;
    }
}
