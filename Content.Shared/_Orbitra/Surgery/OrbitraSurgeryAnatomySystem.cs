using Content.Shared.Body;
using Content.Shared.Movement.Systems;

namespace Content.Shared._Orbitra.Surgery;

/// <summary>Сопоставляет зоны травм со штатными категориями органов конкретного вида.</summary>
public sealed partial class OrbitraSurgeryAnatomySystem : EntitySystem
{
    [Dependency] private InitialBodySystem _initialBody = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrbitraMissingLegsComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnRefreshSpeed(Entity<OrbitraMissingLegsComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(MissingLegSpeed(ent.Comp.Count));
    }

    public static float MissingLegSpeed(int count) => count switch { 1 => 0.55f, 2 => 0.25f, _ => 1f };

    /// <summary>Категория основного органа выбранной области.</summary>
    public static string? Category(SurgeryZone zone) => zone switch
    {
        SurgeryZone.Head => "Head",
        SurgeryZone.Torso => "Torso",
        SurgeryZone.LeftArm => "ArmLeft",
        SurgeryZone.RightArm => "ArmRight",
        SurgeryZone.LeftLeg => "LegLeft",
        SurgeryZone.RightLeg => "LegRight",
        _ => null,
    };

    /// <summary>Категория кисти или стопы, которая отделяется вместе с конечностью.</summary>
    public static string? Extremity(SurgeryZone zone) => zone switch
    {
        SurgeryZone.LeftArm => "HandLeft",
        SurgeryZone.RightArm => "HandRight",
        SurgeryZone.LeftLeg => "FootLeft",
        SurgeryZone.RightLeg => "FootRight",
        _ => null,
    };

    /// <summary>Для доступности области учитываются органы, предусмотренные анатомией вида.</summary>
    public bool SupportsZone(EntityUid patient, SurgeryZone zone)
    {
        return Category(zone) is { } category && HasComp<OrbitraSurgicalPatientComponent>(patient) &&
               HasComp<BodyComponent>(patient) && _initialBody.HasInitialOrgan(patient, category);
    }

    /// <summary>Показывает утрату органа, который у этого пациента должен присутствовать.</summary>
    public bool IsMissing(EntityUid patient, SurgeryZone zone)
    {
        return SupportsZone(patient, zone) && FindOrgan(patient, Category(zone)!) == null;
    }

    /// <summary>Находит фактический орган в контейнере пациента или отделённой части тела.</summary>
    public EntityUid? FindOrgan(EntityUid body, string category)
    {
        if (!TryComp<BodyComponent>(body, out var comp) || comp.Organs == null)
            return null;

        foreach (var organ in comp.Organs.ContainedEntities)
        {
            if (TryComp<OrganComponent>(organ, out var part) && part.Category?.Id == category)
                return organ;
        }

        return null;
    }
}
