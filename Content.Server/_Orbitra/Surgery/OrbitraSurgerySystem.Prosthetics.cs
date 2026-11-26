using System.Diagnostics.CodeAnalysis;
using Content.Shared._Orbitra.Surgery;
using Content.Shared.Body;
using Robust.Shared.Containers;

namespace Content.Server._Orbitra.Surgery;

public sealed partial class OrbitraSurgerySystem
{
    [Dependency] private InitialBodySystem _initialBody = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;

    private bool CanFitProsthesis(EntityUid patient, SurgeryZone zone, out EntityUid torso,
        [NotNullWhen(true)] out BaseContainer? body)
    {
        torso = default;
        body = null;
        if (TerminatingOrDeleted(patient) || !HasComp<OrbitraHumanOrganPatientComponent>(patient) ||
            !_anatomy.IsMissing(patient, zone) ||
            OrbitraSurgeryAnatomySystem.Extremity(zone) is not { } distal ||
            !_initialBody.HasInitialOrgan(patient, distal) ||
            _anatomy.FindOrgan(patient, distal) != null || _anatomy.FindOrgan(patient, "Torso") is not { } parent ||
            !HasComp<ParentOrganComponent>(parent) || !_containers.TryGetContainer(patient, BodyComponent.ContainerID, out body))
            return false;

        torso = parent;
        return true;
    }

    private bool CanUseProsthesis(EntityUid item, SurgeryZone zone,
        [NotNullWhen(true)] out OrbitraProsthesisComponent? kit)
    {
        kit = null;
        return !TerminatingOrDeleted(item) && !EntityManager.IsQueuedForDeletion(item) &&
               TryComp(item, out kit) && kit.Fits(zone);
    }

    /// <summary>Собирает пару штатных органов выбранной стороны и расходует комплект только после их установки.</summary>
    public bool TryInstallProsthesis(EntityUid patient, SurgeryZone zone, EntityUid item)
    {
        if (!CanFitProsthesis(patient, zone, out var torso, out var body) || !CanUseProsthesis(item, zone, out var kit) ||
            MetaData(item).EntityPrototype is not { } prototype)
            return false;

        var proximal = OrbitraSurgeryAnatomySystem.Category(zone)!;
        var distal = OrbitraSurgeryAnatomySystem.Extremity(zone)!;
        var coords = Transform(patient).Coordinates;
        var root = Spawn($"OrbitraProsthetic{proximal}", coords);
        var tip = Spawn($"OrbitraProsthetic{distal}", coords);
        var joint = EnsureComp<OrbitraProstheticJointComponent>(root);
        joint.Kit = prototype.ID;
        joint.Zone = zone;
        joint.Advanced = kit.Advanced;

        if (!_containers.CanInsert(root, body) || !_containers.CanInsert(tip, body) ||
            !_containers.Insert(root, body) || !_containers.Insert(tip, body))
        {
            Del(tip);
            Del(root);
            return false;
        }

        _relations.Relate(torso, root);
        _relations.Relate(root, tip);
        var finish = new OrganProfileData { SkinColor = kit.Finish };
        _visualBody.ApplyProfiles(patient, new() { [proximal] = finish, [distal] = finish });
        QueueDel(item);
        _trauma.NotifyAnatomyChanged(patient, zone, false);
        return true;
    }

    private bool CanRemoveProsthesis(EntityUid patient, SurgeryZone zone, out EntityUid root,
        [NotNullWhen(true)] out OrbitraProstheticJointComponent? joint)
    {
        joint = null;
        return TryGetLimb(patient, zone, out _, out root, out var tip) &&
               TryComp(root, out joint) && joint.Zone == zone &&
               _containers.TryGetContainer(patient, BodyComponent.ContainerID, out var body) &&
               _containers.CanRemove(root, body) && _containers.CanRemove(tip, body);
    }

    /// <summary>Возвращает комплект исходной модели, освобождая место для родной конечности или другого протеза.</summary>
    public bool TryRemoveProsthesis(EntityUid patient, SurgeryZone zone, [NotNullWhen(true)] out EntityUid? kit)
    {
        kit = null;
        if (!CanRemoveProsthesis(patient, zone, out var root, out var joint) || _detach.Detach(root) is not { } parts)
            return false;

        kit = SpawnNextToOrDrop(joint.Kit, patient);
        QueueDel(parts);
        return true;
    }

    private void OnProsthesisChanged(EntityUid patient, OrbitraProstheticJointComponent joint, bool installed)
    {
        if (installed)
        {
            var fitted = EnsureComp<OrbitraFittedProsthesesComponent>(patient);
            fitted.Parts[joint.Zone] = joint.Advanced;
            Dirty(patient, fitted);
        }
        else if (TryComp<OrbitraFittedProsthesesComponent>(patient, out var fitted))
        {
            fitted.Parts.Remove(joint.Zone);
            if (fitted.Parts.Count == 0)
                RemComp<OrbitraFittedProsthesesComponent>(patient);
            else
                Dirty(patient, fitted);
        }
    }
}
