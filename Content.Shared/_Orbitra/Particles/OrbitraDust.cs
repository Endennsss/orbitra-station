using System.Numerics;

namespace Content.Shared._Orbitra.Particles;

/// <summary>Shared, deterministic cosmetic dust limits and geometry.</summary>
public static class OrbitraDust
{
    /// <summary>Builds clamped bounds in grid-local world units.</summary>
    public static Box2 Bounds(Vector2 center, int width, int height, float tileSize = 1f) =>
        Box2.CenteredAround(center, new Vector2(Math.Clamp(width, 1, 32), Math.Clamp(height, 1, 32)) * tileSize);

    /// <summary>Tests the union of regions without adding density for overlaps.</summary>
    public static bool Contains(IReadOnlyList<Box2> regions, Vector2 point)
    {
        foreach (var region in regions)
        {
            if (region.Contains(point))
                return true;
        }
        return false;
    }

    /// <summary>Returns ambient caps within the existing total particle budget.</summary>
    public static (int Lamps, int Motes) AmbientBudget(int poolCapacity) => poolCapacity switch
    {
        128 => (4, 12),
        384 => (8, 32),
        768 => (12, 48),
        _ => (0, 0),
    };

    /// <summary>Projects relative velocity onto the normal directed from the first body to the second.</summary>
    public static float ClosingSpeed(Vector2 first, Vector2 second, Vector2 normal) =>
        Math.Max(0f, Vector2.Dot(first - second, normal));

    /// <summary>Muted, non-emissive dust colors for the existing material contract.</summary>
    public static Color MaterialColor(OrbitraParticleMaterial material) => material switch
    {
        OrbitraParticleMaterial.Wood => new Color(0.65f, 0.55f, 0.4f, 0.45f),
        OrbitraParticleMaterial.Metal => new Color(0.55f, 0.57f, 0.58f, 0.4f),
        OrbitraParticleMaterial.Glass => new Color(0.8f, 0.85f, 0.87f, 0.25f),
        _ => new Color(0.65f, 0.63f, 0.59f, 0.45f),
    };
}
