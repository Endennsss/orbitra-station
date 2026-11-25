using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Client.Graphics;
using Content.Shared._Orbitra.Graphics;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Orbitra.Parallax;

internal sealed class OrbitraSkyRenderer : IDisposable
{
    private static readonly ProtoId<ShaderPrototype> ShaderId = "OrbitraProceduralSky";

    private readonly IConfigurationManager _configuration = IoCManager.Resolve<IConfigurationManager>();
    private readonly IPrototypeManager _prototypes = IoCManager.Resolve<IPrototypeManager>();
    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();
    private readonly ISawmill _log = IoCManager.Resolve<ILogManager>().GetSawmill("orbitra.parallax");
    private readonly OverlayResourceCache<WarpMotion> _warpMotion = new();

    private ShaderInstance? _shader;
    private bool _unavailable;

    public bool TryDrawWorld(in OverlayDrawArgs args, Vector2 cameraPosition, float warpSpeed)
    {
        var size = (Vector2) args.Viewport.Size;
        var rotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        if (!TryPrepare(size, cameraPosition, (float) rotation.Theta, warpSpeed, args.Viewport, out var shader))
            return false;

        var handle = args.WorldHandle;
        handle.UseShader(shader);
        handle.DrawRect(in args.WorldBounds, Color.White);
        handle.UseShader(null);
        return true;
    }

    public bool TryDrawMenu(DrawingHandleScreen handle, Vector2 size)
    {
        if (!TryPrepare(size, Vector2.Zero, 0f, 0f, null, out var shader))
            return false;

        handle.UseShader(shader);
        handle.DrawRect(UIBox2.FromDimensions(Vector2.Zero, size), Color.White);
        handle.UseShader(null);
        return true;
    }

    private bool TryPrepare(
        Vector2 size,
        Vector2 cameraPosition,
        float rotation,
        float warpSpeed,
        IClydeViewport? viewport,
        [NotNullWhen(true)] out ShaderInstance? shader)
    {
        shader = null;
        if (_unavailable || size.X < 1f || size.Y < 1f ||
            !_configuration.GetCVar(OrbitraParallaxCVars.ProceduralParallax))
        {
            return false;
        }

        if (_shader == null)
        {
            if (!_prototypes.TryIndex(ShaderId, out var prototype))
                return false;

            try
            {
                _shader = prototype.InstanceUnique();
            }
            catch (Exception exception)
            {
                _unavailable = true;
                _log.Warning($"Не удалось загрузить процедурный параллакс: {exception}");
                return false;
            }
        }

        // Ограничиваем смещение звёзд при удалении камеры от центра карты.
        var motion = cameraPosition * 0.016f;
        var drift = motion / (Vector2.One + Vector2.Abs(motion));
        var reducedMotion = _configuration.GetCVar(CCVars.ReducedMotion);
        var now = _timing.RealTime;
        var time = reducedMotion ? 0f : (float) now.TotalSeconds;
        if (reducedMotion)
            warpSpeed = 0f;

        var warp = viewport == null ? null : _warpMotion.GetForViewport(viewport, static _ => new WarpMotion());
        if (warp != null && warpSpeed > 0f)
        {
            // После торможения сохраняем смещение фона этой камеры, чтобы он не прыгал при выходе из БСС.
            var elapsed = (float) (now - warp.LastDraw).TotalSeconds;
            if (elapsed <= 0.5f)
                warp.Distance += Math.Min(elapsed, 0.1f) * warpSpeed * 0.70f;
            warp.LastDraw = now;
        }

        _shader.SetParameter("viewport_size", size);
        _shader.SetParameter("camera_drift", drift);
        _shader.SetParameter("camera_rotation", rotation);
        _shader.SetParameter("phase_time", time);
        _shader.SetParameter("menu_background", viewport == null);
        _shader.SetParameter("low_quality", _configuration.GetCVar(CCVars.ParallaxLowQuality));
        _shader.SetParameter("warp_speed", warpSpeed);
        _shader.SetParameter("warp_distance", warp?.Distance ?? 0f);
        shader = _shader;
        return true;
    }

    public void Dispose()
    {
        _warpMotion.Dispose();
        _shader?.Dispose();
        _shader = null;
    }

    private sealed class WarpMotion : IDisposable
    {
        public TimeSpan LastDraw;
        public float Distance;

        public void Dispose() { }
    }
}
