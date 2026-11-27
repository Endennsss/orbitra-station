using System.Numerics;
using Content.Shared._Orbitra.Graphics;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Orbitra.Parallax;

// Значения соответствуют режимам шейдера world_sky.
internal enum OrbitraSkyLayer { Full, Stars, Accents, Distant, Near }

internal sealed class OrbitraSkyRenderer(bool dockingBackground = false) : IDisposable
{
    private static readonly ProtoId<ShaderPrototype> WorldShader = "OrbitraWorldSky";
    private static readonly ProtoId<ShaderPrototype> MenuShader = "OrbitraProceduralSky";

    private readonly IConfigurationManager _configuration = IoCManager.Resolve<IConfigurationManager>();
    private readonly IPrototypeManager _prototypes = IoCManager.Resolve<IPrototypeManager>();
    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();
    private readonly ISawmill _log = IoCManager.Resolve<ILogManager>().GetSawmill("orbitra.parallax");

    // Clyde исполняет команды после обхода слоёв. У дальних и ближних звёзд должны быть свои параметры.
    private readonly ShaderInstance?[] _worldShaders = new ShaderInstance?[(int) OrbitraSkyLayer.Near + 1];
    private ShaderInstance? _menuShader;
    private bool _unavailable;
    private TimeSpan _firstFrame;

    public bool TryDrawWorld(in OverlayDrawArgs args, Vector2 cameraPosition, float warpSpeed,
        OrbitraSkyLayer layer = OrbitraSkyLayer.Full, float warpDistance = 0f)
    {
        var shader = Prepare(args.Viewport.Size, WorldShader, ref _worldShaders[(int) layer]);
        if (shader == null)
            return false;

        var reducedMotion = _configuration.GetCVar(CCVars.ReducedMotion);
        shader.SetParameter("camera_drift", cameraPosition + (args.Viewport.Eye?.Offset ?? Vector2.Zero));
        shader.SetParameter("camera_rotation", (float) (args.Viewport.Eye?.Rotation.Theta ?? 0));
        shader.SetParameter("map_seed", GetMapSeed(args.MapId));
        shader.SetParameter("warp_speed", reducedMotion ? 0f : warpSpeed);
        shader.SetParameter("warp_distance", reducedMotion ? 0f : warpDistance);
        shader.SetParameter("sky_layer", (int) layer);
        var handle = args.WorldHandle;
        handle.UseShader(shader);
        handle.DrawRect(in args.WorldBounds, Color.White);
        handle.UseShader(null);
        return true;
    }

    // Все наблюдатели получают один MapId от сервера. Перемешиваем его без случайного состояния клиента.
    internal static Vector2 GetMapSeed(MapId map)
    {
        var bits = unchecked((uint) (int) map);
        bits = unchecked((bits ^ (bits >> 16)) * 0x7feb352du);
        bits = unchecked((bits ^ (bits >> 15)) * 0x846ca68bu);
        bits ^= bits >> 16;
        return new Vector2(bits & 0xffff, bits >> 16) / 65536f;
    }

    public bool TryDrawMenu(DrawingHandleScreen handle, Vector2 size)
    {
        var shader = Prepare(size, MenuShader, ref _menuShader);
        if (shader == null)
            return false;

        var now = _timing.RealTime;
        var phase = dockingBackground ? now - _firstFrame : now;
        shader.SetParameter("phase_time", _configuration.GetCVar(CCVars.ReducedMotion) ? 0f : (float) phase.TotalSeconds);
        shader.SetParameter("menu_background", true);
        shader.SetParameter("docking_background", dockingBackground);
        handle.UseShader(shader);
        handle.DrawRect(UIBox2.FromDimensions(Vector2.Zero, size), Color.White);
        handle.UseShader(null);
        return true;
    }

    private ShaderInstance? Prepare(Vector2 size, ProtoId<ShaderPrototype> id, ref ShaderInstance? shader)
    {
        if (_unavailable || size.X < 1f || size.Y < 1f ||
            !_configuration.GetCVar(OrbitraParallaxCVars.ProceduralParallax))
        {
            return null;
        }

        if (shader == null)
        {
            if (!_prototypes.TryIndex(id, out var prototype))
                return null;

            try
            {
                shader = prototype.InstanceUnique();
                _firstFrame = _timing.RealTime;
            }
            catch (Exception exception)
            {
                _unavailable = true;
                _log.Warning($"Не удалось загрузить процедурный параллакс: {exception}");
                return null;
            }
        }

        shader.SetParameter("viewport_size", size);
        shader.SetParameter("low_quality", _configuration.GetCVar(CCVars.ParallaxLowQuality));
        return shader;
    }

    public void Dispose()
    {
        _menuShader?.Dispose();
        foreach (var shader in _worldShaders)
            shader?.Dispose();
    }
}
