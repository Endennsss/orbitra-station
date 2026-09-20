using Content.Client._Lime.NightVision;
using Content.Shared._Lime.NightVision;
using Content.Shared.Overlays;
using Robust.Shared.Timing;

#pragma warning disable IDE0130 // Расширение штатной системы без переноса её основного файла.
namespace Content.Client.NightVision;

public sealed partial class NightVisionSystem
{
    // Оформление приборов после штатного выбора источника ночного зрения.
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly LimeNightVisionPresentation _limePresentation = new();
    private LimeNightVisionLightOverlay _limeLight = default!;
    private LimeNightVisionOverlay _limePhosphor = default!;

    private void InitializeLimeNightVision()
    {
        _limeLight = new(_limePresentation);
        _limePhosphor = new(_limePresentation);
    }

    private bool TryShowLimeNightVision(EntityUid viewer, Entity<NightVisionComponent> source)
    {
        if (!HasComp<LimeNightVisionDeviceComponent>(source))
        {
            HideLimeNightVision();
            return false;
        }

        _overlayMan.RemoveOverlay(_overlay);
        _limePresentation.SetSource(viewer, source, source.Comp.LightingColor, _timing.RealTime);
        if (!_overlayMan.HasOverlay<LimeNightVisionLightOverlay>())
        {
            _overlayMan.AddOverlay(_limeLight);
            _overlayMan.AddOverlay(_limePhosphor);
        }

        return true;
    }

    private void HideLimeNightVision()
    {
        _overlayMan.RemoveOverlay(_limeLight);
        _overlayMan.RemoveOverlay(_limePhosphor);
        _limePhosphor.ResetExposure();
        _limePresentation.Reset();
    }

    public override void Shutdown()
    {
        HideLimeNightVision();
        _overlayMan.RemoveOverlay(_overlay);
        _limeLight.Dispose();
        _limePhosphor.Dispose();
        _overlay.Dispose();
        base.Shutdown();
    }
}
