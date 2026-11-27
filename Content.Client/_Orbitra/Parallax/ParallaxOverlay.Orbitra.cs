using System.Numerics;
using Content.Client._Orbitra.Parallax;
using Content.Client.Parallax.Data;
using Content.Shared.Shuttles.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;

// Расширение находится в папке Orbitra, чтобы не разносить интеграцию по апстриму.
#pragma warning disable IDE0130
namespace Content.Client.Parallax;

public sealed partial class ParallaxOverlay
{
    private readonly IEyeManager _orbitraEye = IoCManager.Resolve<IEyeManager>();
    private readonly IPlayerManager _orbitraPlayer = IoCManager.Resolve<IPlayerManager>();
    private OrbitraSkyRenderer? _orbitraSky;
    private OrbitraSkyRenderer OrbitraSky => _orbitraSky ??= new OrbitraSkyRenderer();

    private bool TryDrawOrbitraSky(in OverlayDrawArgs args, Vector2 position)
    {
        var hyperspace = _entManager.HasComponent<FTLMapComponent>(args.MapUid);
        if (!hyperspace && _parallax.GetParallax(args.MapUid) != "Default")
            return false;

        var camera = position - _manager.ParallaxAnchor;
        if (!hyperspace)
            return OrbitraSky.TryDrawWorld(args, camera, 0f);

        var (speed, distance) = OrbitraWarpMotion.Sample(GetOrbitraFlight(args), _timing.CurTime);
        return OrbitraSky.TryDrawWorld(args, camera, speed, warpDistance: distance);
    }

    private FTLComponent? GetOrbitraFlight(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye != _orbitraEye.CurrentEye ||
            !_entManager.TryGetComponent<TransformComponent>(_orbitraPlayer.LocalEntity, out var viewer) ||
            viewer.MapUid != args.MapUid || viewer.GridUid is not { } grid ||
            !_entManager.TryGetComponent<FTLComponent>(grid, out var flight))
        {
            return null;
        }

        return flight;
    }

    private bool TryReplaceOrbitraLayer(in OverlayDrawArgs args, Vector2 position, ParallaxLayerConfig layer, ref OrbitraSkyLayer? drawn)
    {
        if (layer.OrbitraSky is not { } sky || sky == OrbitraSkyLayer.Accents)
            return false;

        if (drawn == sky)
            return true;

        if (!OrbitraSky.TryDrawWorld(args, position - _manager.ParallaxAnchor, 0f, sky))
            return false;

        drawn = sky;
        return true;
    }

    private void DrawOrbitraAccents(in OverlayDrawArgs args, Vector2 position, ParallaxLayerConfig layer)
    {
        if (layer.OrbitraSky == OrbitraSkyLayer.Accents)
            OrbitraSky.TryDrawWorld(args, position - _manager.ParallaxAnchor, 0f, OrbitraSkyLayer.Accents);
    }

    protected override void DisposeBehavior()
    {
        _orbitraSky?.Dispose();
        base.DisposeBehavior();
    }
}
