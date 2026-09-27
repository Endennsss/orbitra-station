using Content.Shared._Orbitra.Ratvar;
using Robust.Client.Graphics;

namespace Content.Client._Orbitra.Ratvar;

/// <summary>Owns the decorative tether overlay; never adds PVS or camera overrides.</summary>
public sealed partial class OrbitraRatvarProjectionVisualSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay.AddOverlay(new OrbitraRatvarProjectionOverlay(EntityManager));
    }

    public override void Shutdown()
    {
        _overlay.RemoveOverlay<OrbitraRatvarProjectionOverlay>();
        base.Shutdown();
    }
}
