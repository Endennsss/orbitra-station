using Content.Shared._Orbitra.Surgery;
using Content.Shared.Body;
using Content.Shared.FixedPoint;
using Robust.Shared.Random;

namespace Content.Server._Orbitra.Surgery;

public sealed partial class OrbitraTraumaSystem
{
    [Dependency] private OrbitraOrganSystem _organs = default!;

    private void OnFlashExposure(Entity<OrbitraSurgicalPatientComponent> patient, ref OrbitraFlashExposureEvent args)
    {
        if (!HasComp<OrbitraHumanOrganPatientComponent>(patient))
            return;

        var profile = _prototype.Index(patient.Comp.TraumaProfile);
        if (args.Duration >= profile.FlashEyeDuration &&
            EligibleOrgan(patient, SurgeryOrgan.Eyes, SurgeryOrganCondition.Healthy) is { } eyes &&
            _random.Prob(profile.FlashEyeChance))
            SetOrganCondition(patient, eyes, SurgeryOrganCondition.Damaged);
    }

    /// <summary>Один удар может изменить состояние только одного органа на одну ступень.</summary>
    private void ApplyOrganTrauma(EntityUid patient, FixedPoint2 damage, OrbitraTraumaPrototype profile, bool isExplosion)
    {
        if (!HasComp<OrbitraHumanOrganPatientComponent>(patient))
            return;

        if (Roll(damage, profile.OrganFailure) &&
            TryInjureOrgan(patient, SurgeryOrganCondition.Damaged, profile, isExplosion))
            return;

        if (Roll(damage, profile.OrganInjury))
            TryInjureOrgan(patient, SurgeryOrganCondition.Healthy, profile, isExplosion);
    }

    private void ApplyBallisticOrganTrauma(EntityUid patient, FixedPoint2 piercing, OrbitraTraumaPrototype profile)
    {
        if (!TryComp<OrbitraHumanOrganPatientComponent>(patient, out var organs))
            return;

        if (organs.BallisticOrganThreshold <= 0)
            organs.BallisticOrganThreshold = FixedPoint2.FromHundredths(_random.Next(profile.MaxBallisticOrganThreshold.Value) + 1);
        organs.BallisticDamage += piercing;
        if (organs.BallisticDamage < organs.BallisticOrganThreshold)
            return;

        organs.BallisticDamage = 0;
        organs.BallisticOrganThreshold = 0;
        TryInjureOrgan(patient, null, profile);
    }

    private bool TryInjureOrgan(EntityUid patient, SurgeryOrganCondition? from,
        OrbitraTraumaPrototype profile, bool isExplosion = false)
    {
        Span<EntityUid> torso = stackalloc EntityUid[OrbitraOrganSystem.Kinds.Length];
        Span<EntityUid> head = stackalloc EntityUid[OrbitraOrganSystem.Kinds.Length];
        var torsoCount = 0;
        var headCount = 0;
        EntityUid? ears = null;
        foreach (var kind in OrbitraOrganSystem.Kinds)
        {
            if (EligibleOrgan(patient, kind, from) is not { } organ)
                continue;

            if (isExplosion && kind == SurgeryOrgan.Ears)
                ears = organ;
            else if (OrbitraOrganSystem.ZoneFor(kind) == SurgeryZone.Head)
                head[headCount++] = organ;
            else
                torso[torsoCount++] = organ;
        }

        EntityUid selected;
        if (ears is { } eligibleEars && (headCount + torsoCount == 0 || _random.Prob(profile.ExplosionEarChance)))
            selected = eligibleEars;
        else if (torsoCount > 0 && (headCount == 0 ||
                     _random.NextFloat(profile.TorsoOrganWeight + profile.HeadOrganWeight) < profile.TorsoOrganWeight))
            selected = torso[_random.Next(torsoCount)];
        else if (headCount > 0)
            selected = head[_random.Next(headCount)];
        else
            return false;

        var next = Comp<OrbitraOrganComponent>(selected).Condition == SurgeryOrganCondition.Healthy
            ? SurgeryOrganCondition.Damaged
            : SurgeryOrganCondition.Nonfunctional;
        SetOrganCondition(patient, selected, next);
        return true;
    }

    private EntityUid? EligibleOrgan(EntityUid patient, SurgeryOrgan kind, SurgeryOrganCondition? required)
    {
        if (_organs.Installed(patient, kind) is not { } uid || !_organs.IsHumanOrgan(uid, kind))
            return null;
        var condition = Comp<OrbitraOrganComponent>(uid).Condition;
        return condition is SurgeryOrganCondition.Healthy or SurgeryOrganCondition.Damaged &&
               (required == null || condition == required) ? uid : null;
    }

    internal void SetOrganCondition(EntityUid patient, EntityUid organ, SurgeryOrganCondition condition)
    {
        if (ApplyOrganCondition(patient, (organ, Comp<OrbitraOrganComponent>(organ)), condition))
            Changed?.Invoke(patient);
    }

    private bool ApplyOrganCondition(EntityUid patient, Entity<OrbitraOrganComponent> organ, SurgeryOrganCondition condition)
    {
        if (organ.Comp.Condition == condition)
            return false;
        organ.Comp.Condition = condition;
        Dirty(organ);
        if (organ.Comp.Kind == SurgeryOrgan.Eyes)
            _organs.RefreshVision(patient);
        return true;
    }

    private bool RestoreInstalledOrgans(EntityUid patient)
    {
        if (!HasComp<OrbitraHumanOrganPatientComponent>(patient) || !TryComp<BodyComponent>(patient, out var body) ||
            body.Organs == null)
            return false;

        var changed = false;
        foreach (var uid in body.Organs.ContainedEntities)
        {
            if (TryComp<OrbitraOrganComponent>(uid, out var organ) && _organs.IsHumanOrgan(uid, organ.Kind))
                changed |= ApplyOrganCondition(patient, (uid, organ), SurgeryOrganCondition.Healthy);
        }
        return changed;
    }
}
