using Content.Client._Orbitra.NightVision;
using Content.Shared._Orbitra.NightVision;
using Content.Shared.Overlays;
using Robust.Shared.Timing;

#pragma warning disable IDE0130 // Расширение штатной системы без переноса её основного файла.
namespace Content.Client.NightVision;

public sealed partial class NightVisionSystem
{
    // Оформление приборов после штатного выбора источника ночного зрения.
    [Dependency] private IGameTiming _timing = default!;

    private readonly OrbitraNightVisionPresentation _orbitraPresentation = new();
    private OrbitraNightVisionLightOverlay _orbitraLight = default!;
    private OrbitraNightVisionOverlay _orbitraPhosphor = default!;

    private void InitializeOrbitraNightVision()
    {
        _orbitraLight = new(_orbitraPresentation);
        _orbitraPhosphor = new(_orbitraPresentation);
    }

    private bool TryShowOrbitraNightVision(EntityUid viewer, Entity<NightVisionComponent> source)
    {
        if (!HasComp<OrbitraNightVisionDeviceComponent>(source))
        {
            HideOrbitraNightVision();
            return false;
        }

        _overlayMan.RemoveOverlay(_overlay);
        _orbitraPresentation.SetSource(viewer, source, source.Comp.LightingColor, _timing.RealTime);
        if (!_overlayMan.HasOverlay<OrbitraNightVisionLightOverlay>())
        {
            _overlayMan.AddOverlay(_orbitraLight);
            _overlayMan.AddOverlay(_orbitraPhosphor);
        }

        return true;
    }

    private void HideOrbitraNightVision()
    {
        _overlayMan.RemoveOverlay(_orbitraLight);
        _overlayMan.RemoveOverlay(_orbitraPhosphor);
        _orbitraPhosphor.ResetExposure();
        _orbitraPresentation.Reset();
    }

    public override void Shutdown()
    {
        HideOrbitraNightVision();
        _overlayMan.RemoveOverlay(_overlay);
        _orbitraLight.Dispose();
        _orbitraPhosphor.Dispose();
        _overlay.Dispose();
        base.Shutdown();
    }
}
