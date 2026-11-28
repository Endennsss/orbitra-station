using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Orbitra.Lavaland;

public enum OrbitraAshPhase : byte
{
    Clear,
    Forecast,
    Passing,
}

/// <summary>Каждая доступная для прогноза точка принадлежит реальному маршруту этой смены.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class OrbitraAshFrontComponent : Component
{
    [DataField]
    public Dictionary<Direction, Vector2> Sites = new();

    /// <summary>Сектор первой разведки, выбранный при создании поверхности.</summary>
    [DataField]
    public Direction FirstSector = Direction.Invalid;

    /// <summary>Реальные следы смены, сгруппированные по сектору прогноза и клетке поверхности.</summary>
    [DataField]
    public Dictionary<Direction, Dictionary<Vector2i, OrbitraAshTrailTile>> Trails = new();

    /// <summary>Положение полосы при предыдущей проверке, от начала до конца прохода.</summary>
    [DataField]
    public float TrailProgress;

    [DataField]
    public OrbitraAshPhase Phase;

    [DataField]
    public Direction Sector = Direction.Invalid;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextChange;

    [DataField]
    public EntityUid? WeatherEntity;
}

[DataDefinition]
public sealed partial class OrbitraAshTrailTile
{
    [DataField(required: true)]
    public EntProtoId Marker;

    [DataField(required: true)]
    public Direction Heading;

    /// <summary>Сущность открытого следа, пока его снова не занесло пеплом.</summary>
    [DataField]
    public EntityUid? Visible;
}
