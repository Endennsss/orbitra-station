using Content.Client._Orbitra.Parallax;
using Robust.Client.Graphics;

#pragma warning disable IDE0130
namespace Content.Client.Parallax;

public sealed partial class ParallaxControl
{
    private OrbitraSkyRenderer? _orbitraSky;

    internal bool UseOrbitraSky { get; set; }

    private bool TryDrawOrbitraSky(DrawingHandleScreen handle)
    {
        if (!UseOrbitraSky)
            return false;

        _orbitraSky ??= new OrbitraSkyRenderer();
        return _orbitraSky.TryDrawMenu(handle, PixelSize);
    }

    protected override void ExitedTree()
    {
        _orbitraSky?.Dispose();
        _orbitraSky = null;
        base.ExitedTree();
    }
}
