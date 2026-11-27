using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;

namespace Content.Client._Orbitra.Parallax;

internal static class OrbitraWarpMotion
{
    private const double Cruise = 0.70;

    // Интеграл плавного разгона. Общие часы симуляции дают одинаковый путь после переподключения и при любом FPS.
    private static double RampDistance(double progress) => progress * progress * progress * (1.0 - progress / 2.0);

    internal static (float Speed, float Distance) Sample(FTLComponent? flight, TimeSpan now)
    {
        var seconds = now.TotalSeconds;
        if (flight == null)
            return (1f, (float) (seconds * Cruise));

        var duration = flight.StateTime.Length.TotalSeconds;
        var elapsed = Math.Clamp((now - flight.StateTime.Start).TotalSeconds, 0, Math.Max(0, duration));
        var start = flight.StateTime.Start.TotalSeconds;
        if (flight.State == FTLState.Travelling)
        {
            var ramp = Math.Min(1.2, duration / 2.0);
            if (ramp <= 0 || elapsed >= ramp)
                return (1f, (float) ((start + elapsed) * Cruise));

            var t = elapsed / ramp;
            var distance = start + ramp * (0.5 + RampDistance(t));
            return ((float) (t * t * (3 - 2 * t)), (float) (distance * Cruise));
        }

        if (flight.State == FTLState.Arriving && duration > 0)
        {
            var t = elapsed / duration;
            var distance = start + elapsed - duration * RampDistance(t);
            return ((float) (1 - t * t * (3 - 2 * t)), (float) (distance * Cruise));
        }

        return default;
    }
}
