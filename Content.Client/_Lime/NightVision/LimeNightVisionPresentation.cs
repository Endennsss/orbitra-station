using System.Numerics;
using Content.Shared.Overlays;

namespace Content.Client._Lime.NightVision;

/// <summary>
/// Shared activation state for the lighting and phosphor passes of a single local viewer.
/// </summary>
internal sealed class LimeNightVisionPresentation
{
    internal const float ActivationSeconds = 0.2f;
    internal const float ApertureScale = 0.62f;
    internal const float EdgeFraction = 0.18f;

    internal EntityUid? Source { get; private set; }
    internal EntityUid? Viewer { get; private set; }
    internal Color LightingColor { get; private set; }
    internal uint Revision { get; private set; }
    private TimeSpan _activatedAt;

    internal void SetSource(EntityUid viewer, EntityUid source, Color lightingColor, TimeSpan now)
    {
        if (Source != source || Viewer != viewer)
        {
            _activatedAt = now;
            Revision++;
        }

        Source = source;
        Viewer = viewer;
        LightingColor = lightingColor;
    }

    internal void Reset()
    {
        Revision++;
        Source = null;
        Viewer = null;
        LightingColor = Color.Transparent;
        _activatedAt = TimeSpan.Zero;
    }

    internal float GetActivation(TimeSpan now)
    {
        if (Source == null)
            return 0f;

        var progress = Math.Clamp((float) (now - _activatedAt).TotalSeconds / ActivationSeconds, 0f, 1f);
        return progress * progress * (3f - 2f * progress);
    }

    internal static Vector2 GetApertureRadii(Vector2 size) => size * ApertureScale;

    // Экспоненциальная адаптация не зависит от FPS: 95% реакции за 0,45 / 1,95 секунды.
    internal static Vector2 GetAdaptationFactors(float elapsed) => new(
        1f - MathF.Exp(-Math.Max(elapsed, 0f) / 0.15f),
        1f - MathF.Exp(-Math.Max(elapsed, 0f) / 0.65f));

    /// <summary>
    /// Preserves the existing priority, relay and minimum-noise selection rules.
    /// </summary>
    internal static Entity<NightVisionComponent>? SelectSource(EntityUid viewer, List<Entity<NightVisionComponent>> sources)
    {
        Entity<NightVisionComponent>? selected = null;
        var bestNoise = float.MaxValue;
        foreach (var source in sources)
        {
            if (!source.Comp.Enabled || source.Comp.RelayOverlay == (source.Owner == viewer))
                continue;

            if (source.Comp.Prioritized)
                return source;

            var noise = source.Comp.NoiseAmount * source.Comp.NoiseMultiplier;
            if (!(noise < bestNoise))
                continue;

            bestNoise = noise;
            selected = source;
        }

        return selected;
    }
}
