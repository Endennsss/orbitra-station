using Content.Shared._Orbitra.Lavaland;
using Content.Shared.StatusEffectNew.Components;

namespace Content.Client.Weather;

public sealed partial class WeatherSystem
{
    private float GetAshFrontGain(EntityUid uid, StatusEffectComponent effect, TransformComponent listener)
    {
        if (!TryComp<OrbitraAshBandComponent>(uid, out var ash))
            return 1f;

        var progress = OrbitraAshFootprint.Progress(Timing.CurTime, effect.StartEffectTime, effect.Duration);
        return ash.Footprint(progress).Strength(_transform.GetWorldPosition(listener));
    }
}
