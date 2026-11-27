using Content.Shared.Body;
using Content.Shared.Eye.Blinding.Systems;
using Robust.Shared.Network;

namespace Content.Shared._Orbitra.Surgery;

public sealed partial class OrbitraOrganSystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private BlindableSystem _blindable = default!;
    [Dependency] private BlurryVisionSystem _blurry = default!;

    private void InitializeSenses()
    {
        SubscribeLocalEvent<OrbitraHumanOrganPatientComponent, GetBlurEvent>(OnGetBlur);
        SubscribeLocalEvent<OrbitraHumanOrganPatientComponent, CanSeeAttemptEvent>(OnCanSee);
        if (_net.IsServer)
        {
            SubscribeLocalEvent<OrbitraHumanOrganPatientComponent, MapInitEvent>(OnPatientInit, after: [typeof(InitialBodySystem)]);
            SubscribeLocalEvent<OrbitraHumanOrganPatientComponent, OrganInsertedIntoEvent>(OnOrganInserted);
            SubscribeLocalEvent<OrbitraHumanOrganPatientComponent, OrganRemovedFromEvent>(OnOrganRemoved);
        }
    }

    private void OnGetBlur(EntityUid uid, OrbitraHumanOrganPatientComponent patient, GetBlurEvent args)
    {
        if (patient.Eyes == SurgeryOrganCondition.Damaged)
            args.Blur += 3f;
    }

    private void OnCanSee(EntityUid uid, OrbitraHumanOrganPatientComponent patient, CanSeeAttemptEvent args)
    {
        if (patient.Eyes is SurgeryOrganCondition.Nonfunctional or SurgeryOrganCondition.Missing)
            args.Cancel();
    }

    private void OnPatientInit(Entity<OrbitraHumanOrganPatientComponent> ent, ref MapInitEvent args) => RefreshVision(ent);

    private void OnOrganInserted(Entity<OrbitraHumanOrganPatientComponent> ent, ref OrganInsertedIntoEvent args)
    {
        if (TryComp<OrganComponent>(args.Organ, out var organ) && organ.Category?.Id == "Eyes")
            RefreshVision(ent);
    }

    private void OnOrganRemoved(Entity<OrbitraHumanOrganPatientComponent> ent, ref OrganRemovedFromEvent args)
    {
        if (TryComp<OrganComponent>(args.Organ, out var organ) && organ.Category?.Id == "Eyes")
            RefreshVision(ent);
    }

    /// <summary>Пересчитывает зрение по всем причинам после изменения или замены глаз.</summary>
    public void RefreshVision(EntityUid patient)
    {
        if (TerminatingOrDeleted(patient) || !TryComp<OrbitraHumanOrganPatientComponent>(patient, out var physiology))
            return;

        var eyes = Condition(patient, SurgeryOrgan.Eyes);
        if (physiology.Eyes == eyes)
            return;
        physiology.Eyes = eyes;
        Dirty(patient, physiology);
        _blindable.UpdateIsBlind(patient);
        _blurry.UpdateBlurMagnitude(patient);
    }

    public float HearingRangeMultiplier(EntityUid? listener)
    {
        if (listener is not { } uid || !HasComp<OrbitraHumanOrganPatientComponent>(uid))
            return 1f;
        return Condition(uid, SurgeryOrgan.Ears) switch
        {
            SurgeryOrganCondition.Damaged => 0.5f,
            SurgeryOrganCondition.Nonfunctional or SurgeryOrganCondition.Missing => 0f,
            _ => 1f,
        };
    }
}
