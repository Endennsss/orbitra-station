using System.Numerics;
using Content.Client._Orbitra.Parallax;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Client.Graphics;
using Robust.Client.Player;

// Расширение находится в папке Orbitra, чтобы не разносить интеграцию по апстриму.
#pragma warning disable IDE0130
namespace Content.Client.Parallax;

public sealed partial class ParallaxOverlay
{
    private const float OrbitraWarpAccelerationSeconds = 1.2f;
    private readonly IEyeManager _orbitraEye = IoCManager.Resolve<IEyeManager>();
    private readonly IPlayerManager _orbitraPlayer = IoCManager.Resolve<IPlayerManager>();
    private OrbitraSkyRenderer? _orbitraSky;

    private bool TryDrawOrbitraSky(in OverlayDrawArgs args, Vector2 position)
    {
        var hyperspace = _entManager.HasComponent<FTLMapComponent>(args.MapUid);
        if (!hyperspace && _parallax.GetParallax(args.MapUid) != "Default")
            return false;

        var warpSpeed = hyperspace ? GetOrbitraWarpSpeed(args) : 0f;
        _orbitraSky ??= new OrbitraSkyRenderer();
        return _orbitraSky.TryDrawWorld(args, position - _manager.ParallaxAnchor, warpSpeed);
    }

    private float GetOrbitraWarpSpeed(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye != _orbitraEye.CurrentEye ||
            !_entManager.TryGetComponent<TransformComponent>(_orbitraPlayer.LocalEntity, out var viewer) ||
            viewer.MapUid != args.MapUid || viewer.GridUid is not { } grid ||
            !_entManager.TryGetComponent<FTLComponent>(grid, out var flight))
        {
            return 1f;
        }

        var duration = (float) flight.StateTime.Length.TotalSeconds;
        var elapsed = (float) (_timing.CurTime - flight.StateTime.Start).TotalSeconds;
        return flight.State switch
        {
            // Даже короткий перелёт успевает выйти на полную скорость перед торможением.
            FTLState.Travelling => EaseOrbitraWarp(elapsed, Math.Min(OrbitraWarpAccelerationSeconds, duration * 0.5f)),
            FTLState.Arriving => 1f - EaseOrbitraWarp(elapsed, duration),
            _ => 0f,
        };
    }

    private static float EaseOrbitraWarp(float elapsed, float duration)
    {
        var progress = duration > 0f ? Math.Clamp(elapsed / duration, 0f, 1f) : 1f;
        return progress * progress * (3f - 2f * progress);
    }

    protected override void DisposeBehavior()
    {
        _orbitraSky?.Dispose();
        base.DisposeBehavior();
    }
}
