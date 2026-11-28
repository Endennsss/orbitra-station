namespace Content.Server._Orbitra.Lavaland;

/// <summary>Связывает временный след на поверхности с сектором прошедшего фронта.</summary>
[RegisterComponent]
public sealed partial class OrbitraAshTrackComponent : Component
{
    [DataField]
    public Direction Sector = Direction.Invalid;
}
