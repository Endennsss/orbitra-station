using Content.Shared.Body;
using Content.Shared.Examine;

namespace Content.Shared._Orbitra.Surgery;

/// <summary>Определяет совместимость и функцию органов по фактическому содержимому тела.</summary>
public sealed partial class OrbitraOrganSystem : EntitySystem
{
    [Dependency] private OrbitraSurgeryAnatomySystem _anatomy = default!;
    public static readonly SurgeryOrgan[] Kinds = Enum.GetValues<SurgeryOrgan>();

    public override void Initialize()
    {
        SubscribeLocalEvent<OrbitraOrganComponent, ExaminedEvent>(OnExamined);
    }

    public static string Prototype(SurgeryOrgan kind) => kind switch
    {
        SurgeryOrgan.Heart => "OrganHumanHeart",
        SurgeryOrgan.Lungs => "OrganHumanLungs",
        SurgeryOrgan.Liver => "OrganHumanLiver",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static bool IsProcedure(SurgeryProcedure procedure) => procedure is
        SurgeryProcedure.RepairOrgan or SurgeryProcedure.ExtractOrgan or SurgeryProcedure.TransplantOrgan;

    public static bool Indicated(SurgeryProcedure procedure, SurgeryOrganCondition condition) => procedure switch
    {
        SurgeryProcedure.RepairOrgan => condition == SurgeryOrganCondition.Damaged,
        SurgeryProcedure.ExtractOrgan => condition != SurgeryOrganCondition.Missing,
        SurgeryProcedure.TransplantOrgan => condition == SurgeryOrganCondition.Missing,
        _ => false,
    };

    public bool IsHumanOrgan(EntityUid uid, SurgeryOrgan kind) =>
        Enum.IsDefined(kind) && TryComp<OrbitraOrganComponent>(uid, out var organ) && organ.Kind == kind &&
        MetaData(uid).EntityPrototype?.ID == Prototype(kind);

    public bool IsDonor(EntityUid uid, SurgeryOrgan kind) =>
        IsHumanOrgan(uid, kind) && Comp<OrbitraOrganComponent>(uid).Condition is SurgeryOrganCondition.Healthy or SurgeryOrganCondition.Damaged &&
        TryComp<OrganComponent>(uid, out var organ) && organ.Body == null && organ.Category?.Id == kind.ToString() &&
        TryComp<ChildOrganComponent>(uid, out var child) && child.Parent == null;

    public EntityUid? Installed(EntityUid patient, SurgeryOrgan kind) => _anatomy.FindOrgan(patient, kind.ToString());

    public SurgeryOrganCondition Condition(EntityUid patient, SurgeryOrgan kind)
    {
        if (Installed(patient, kind) is not { } uid)
            return SurgeryOrganCondition.Missing;
        return TryComp<OrbitraOrganComponent>(uid, out var organ) ? organ.Condition : SurgeryOrganCondition.Healthy;
    }

    public Dictionary<SurgeryOrgan, SurgeryOrganCondition> GetConditions(EntityUid patient)
    {
        var result = new Dictionary<SurgeryOrgan, SurgeryOrganCondition>();
        if (HasComp<OrbitraHumanOrganPatientComponent>(patient))
        {
            foreach (var kind in Kinds)
                result[kind] = Condition(patient, kind);
        }
        return result;
    }

    /// <summary>Регуляция крови за штатный тик с учётом сердца, включая постепенную кровопотерю при отказе.</summary>
    public (float Factor, float Loss) HeartFunction(EntityUid patient)
    {
        if (!TryComp<OrbitraHumanOrganPatientComponent>(patient, out var comp))
            return (1f, 0f);

        return Condition(patient, SurgeryOrgan.Heart) switch
        {
            SurgeryOrganCondition.Damaged => (comp.DamagedHeartRecovery, 0f),
            SurgeryOrganCondition.Nonfunctional or SurgeryOrganCondition.Missing => (0f, comp.FailedHeartBloodLoss),
            _ => (1f, 0f),
        };
    }

    public float BreathFraction(EntityUid patient)
    {
        if (!TryComp<OrbitraHumanOrganPatientComponent>(patient, out var comp))
            return 1f;

        return Condition(patient, SurgeryOrgan.Lungs) switch
        {
            SurgeryOrganCondition.Damaged => comp.DamagedBreathFraction,
            SurgeryOrganCondition.Nonfunctional or SurgeryOrganCondition.Missing => 0f,
            _ => 1f,
        };
    }

    /// <summary>Нулевой множитель отключает только обработку реагентов поражённой печенью.</summary>
    public float LiverInterval(EntityUid organ)
    {
        if (!TryComp<OrbitraOrganComponent>(organ, out var comp) || comp.Kind != SurgeryOrgan.Liver ||
            comp.Condition == SurgeryOrganCondition.Healthy || !TryComp<OrganComponent>(organ, out var slot) ||
            !TryComp<OrbitraHumanOrganPatientComponent>(slot.Body, out var patient))
            return 1f;

        return comp.Condition == SurgeryOrganCondition.Damaged ? patient.DamagedLiverInterval : 0f;
    }

    private void OnExamined(Entity<OrbitraOrganComponent> organ, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange && IsHumanOrgan(organ, organ.Comp.Kind))
            args.PushMarkup(Loc.GetString("orbitra-surgery-organ-examine",
                ("condition", Loc.GetString($"orbitra-surgery-organ-condition-{organ.Comp.Condition.ToString().ToLowerInvariant()}"))));
    }
}
