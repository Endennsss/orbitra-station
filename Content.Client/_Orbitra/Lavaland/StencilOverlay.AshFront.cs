using System.Numerics;
using Content.Shared._Orbitra.Lavaland;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Weather;
using Robust.Client.Graphics;

namespace Content.Client.Overlays;

public sealed partial class StencilOverlay
{
    private bool HasWeatherInView(Box2 view, HashSet<Entity<WeatherStatusEffectComponent, StatusEffectComponent>> weathers)
    {
        foreach (var (uid, _, status) in weathers)
        {
            if (!_entManager.TryGetComponent(uid, out OrbitraAshBandComponent? ash) ||
                GetFootprint(ash, status).Bounds.Intersects(view))
                return true;
        }

        return false;
    }

    private OrbitraAshFootprint GetFootprint(OrbitraAshBandComponent ash, StatusEffectComponent effect) =>
        ash.Footprint(OrbitraAshFootprint.Progress(_timing.CurTime, effect.StartEffectTime, effect.Duration));

    /// <summary>Штатная маска погоды вырезает крыши, а плотность пепла плавно меняется по ширине фронта.</summary>
    private bool DrawAshFront(DrawingHandleWorld world, EntityUid weather, StatusEffectComponent effect, Box2 view,
        Texture sprite, Color color)
    {
        if (!_entManager.TryGetComponent(weather, out OrbitraAshBandComponent? ash))
            return false;

        var band = GetFootprint(ash, effect);
        var area = band.Bounds;
        if (!area.Intersects(view))
            return true;

        var visible = area.Intersect(view);
        var size = sprite.Size / (float) EyeManager.PixelsPerMeter;
        var first = (visible.BottomLeft / size).Floored() * size;
        for (var x = first.X; x < visible.Right; x += size.X)
        for (var y = first.Y; y < visible.Top; y += size.Y)
        {
            var rect = Box2.FromDimensions(new Vector2(x, y), size);
            var alpha = band.Strength(rect.Center) * color.A;
            if (alpha > 0f)
                world.DrawTextureRect(sprite, rect, color.WithAlpha(alpha));
        }
        return true;
    }
}
