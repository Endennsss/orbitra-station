using Content.Shared._Orbitra.Surgery;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Components;
using Content.Shared.Buckle.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Rejuvenate;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Server._Orbitra.Surgery;

public sealed partial class OrbitraSurgerySystem
{
    [Dependency] private IPrototypeManager _profiles = default!;
    [Dependency] private SharedStackSystem _stacks = default!;

    private SurgeryAccess Access(EntityUid patient, SurgeryZone zone) =>
        CompOrNull<OrbitraSurgicalAccessComponent>(patient)?.Zones.GetValueOrDefault(zone) ?? SurgeryAccess.Closed;

    private bool IsDressed(EntityUid patient, SurgeryZone zone) =>
        CompOrNull<OrbitraSurgicalAccessComponent>(patient)?.DressingRemaining.ContainsKey(zone) == true;

    private int ExpectedStep(EntityUid patient, SurgeryProcedure procedure, SurgeryZone zone) =>
        TryComp<OrbitraSurgeryOperationComponent>(patient, out var op) ? op.Step :
        procedure == SurgeryProcedure.OpenAccess ? (int) Access(patient, zone) : 0;

    private bool IsPrepared(EntityUid patient) => HasComp<SleepingComponent>(patient) ||
        CompOrNull<MobStateComponent>(patient)?.CurrentState == MobState.Critical;

    private bool CanSelect(EntityUid table, EntityUid doctor, EntityUid patient, SurgeryZone zone,
        SurgeryProcedure procedure, int step, SurgeryOrgan? organ)
    {
        if (!CanOperate(table, doctor, patient) || !Enum.IsDefined(procedure) ||
            !TryComp<OrbitraSurgeryTableComponent>(table, out var station) ||
            !OrbitraSurgeryTableComponent.Supports(station.FieldTable, procedure) ||
            !HasIndication(patient, zone, procedure, organ) || ExpectedStep(patient, procedure, zone) != step)
            return false;

        if (station.FieldTable && procedure == SurgeryProcedure.OpenAccess && Access(patient, zone) == SurgeryAccess.Closed &&
            (_trauma.GetInjuries(patient, zone) & (SurgeryInjury.InternalBleeding | SurgeryInjury.Shrapnel)) == 0)
            return false;

        return !TryComp<OrbitraSurgeryOperationComponent>(patient, out var op) ||
               op.Zone == zone && op.Procedure == procedure && op.Organ == organ && MatchesOriginalOrgan(patient, op);
    }

    private void ClearSelection(EntityUid doctor, EntityUid table, EntityUid? patient = null)
    {
        if (TryComp<OrbitraSurgerySelectionComponent>(doctor, out var selected) && selected.Table == table &&
            (patient == null || selected.Patient == patient))
            RemComp<OrbitraSurgerySelectionComponent>(doctor);
    }

    private void OnDeselect(Entity<OrbitraSurgeryTableComponent> table, ref OrbitraSurgeryDeselectMessage args)
    {
        if (TryGetEntity(args.Patient, out var patient))
            ClearSelection(args.Actor, table, patient);
    }

    private void OnSurgicalTool(Entity<OrbitraSurgerySelectionComponent> doctor, ref OrbitraSurgeryToolUsedEvent ev)
    {
        var choice = doctor.Comp;
        var step = ExpectedStep(choice.Patient, choice.Procedure, choice.Zone);
        if (!CanSelect(choice.Table, doctor, choice.Patient, choice.Zone, choice.Procedure, step, choice.Organ) ||
            choice.Organ is { } kind && _organs.Installed(choice.Patient, kind) != choice.OriginalOrgan)
        {
            RemComp<OrbitraSurgerySelectionComponent>(doctor);
            return;
        }

        if (_hands.GetActiveItem(doctor.Owner) != ev.Tool ||
            !TryStartStep(choice.Table, doctor, choice.Patient, choice.Zone, choice.Procedure, step, true, choice.Organ))
            return;

        _interaction.DoContactInteraction(doctor, ev.Tool);
        _interaction.DoContactInteraction(doctor, choice.Patient);
    }

    private void ClearZoneAccess(EntityUid patient, SurgeryZone zone)
    {
        if (!TryComp<OrbitraSurgicalAccessComponent>(patient, out var access))
            return;

        access.Zones.Remove(zone);
        access.DressingRemaining.Remove(zone);
        if (access.Zones.Count == 0)
            RemComp<OrbitraSurgicalAccessComponent>(patient);
        else
            Dirty(patient, access);
    }

    private void OnAccessRejuvenate(Entity<OrbitraSurgicalAccessComponent> patient, ref RejuvenateEvent args)
    {
        RemComp<OrbitraSurgicalAccessComponent>(patient);
        OnTraumaChanged(patient);
    }

    private void UpdateAccess(float frameTime)
    {
        var patients = EntityQueryEnumerator<OrbitraSurgicalAccessComponent, OrbitraSurgicalPatientComponent, BloodstreamComponent>();
        while (patients.MoveNext(out var patient, out var access, out var surgical, out var blood))
        {
            var profile = _profiles.Index(surgical.TraumaProfile);
            access.Elapsed += frameTime / Math.Max(0.01f, blood.UpdateIntervalMultiplier);
            if (access.Elapsed < profile.TickSeconds)
                continue;

            var expired = false;
            var bleeding = 0;
            foreach (var (zone, stage) in access.Zones)
            {
                if (access.DressingRemaining.TryGetValue(zone, out var duration))
                {
                    if (duration > access.Elapsed)
                    {
                        access.DressingRemaining[zone] = duration - access.Elapsed;
                        continue;
                    }
                    access.DressingRemaining.Remove(zone);
                    expired = true;
                }
                if (stage == SurgeryAccess.Incised)
                    bleeding++;
            }

            access.Elapsed = 0f;
            if (bleeding > 0)
                _bloodstream.TryModifyBloodLevel((patient, blood), -profile.IncisionBloodLoss * bleeding);
            if (expired)
            {
                Dirty(patient, access);
                if (TryComp<BuckleComponent>(patient, out var buckle) && buckle.BuckledTo is { } table)
                    Publish(table);
            }
        }
    }
}
