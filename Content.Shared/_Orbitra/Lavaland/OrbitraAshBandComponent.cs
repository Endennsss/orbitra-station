using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._Orbitra.Lavaland;

/// <summary>Положение фронта вычисляется по часам погодного эффекта на обеих сторонах.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrbitraAshBandComponent : Component
{
    [DataField, AutoNetworkedField]
    public Vector2 Site;

    [DataField, AutoNetworkedField]
    public Vector2 Travel = Vector2.UnitX;

    public OrbitraAshFootprint Footprint(float progress) =>
        new(Site + Travel * (Math.Clamp(progress, 0f, 1f) * 120f - 60f), Travel);
}

public readonly record struct OrbitraAshFootprint(Vector2 Center, Vector2 Travel)
{
    public const float HalfWidth = 24f;
    public const float HalfLength = 44f;
    private const float Feather = 12f;

    public Box2 Bounds
    {
        get
        {
            var axis = Vector2.Abs(Travel);
            var extent = axis * HalfWidth + new Vector2(axis.Y, axis.X) * HalfLength;
            return new Box2(Center - extent, Center + extent);
        }
    }

    public float Strength(Vector2 position)
    {
        var delta = position - Center;
        var along = MathF.Abs(Vector2.Dot(delta, Travel));
        var across = MathF.Abs(delta.X * Travel.Y - delta.Y * Travel.X);
        var edge = MathF.Min(HalfWidth - along, HalfLength - across);
        var linear = Math.Clamp(edge / Feather, 0f, 1f);
        return linear * linear * (3f - 2f * linear);
    }

    public static float Progress(TimeSpan now, TimeSpan start, TimeSpan duration) =>
        duration <= TimeSpan.Zero ? 1f : (float)((now - start) / duration);
}
