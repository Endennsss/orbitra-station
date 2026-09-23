using System.Linq;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Keeps table headers aligned with the actual list viewport when its scrollbar appears.</summary>
internal sealed class OrbitraTableHeader : Control
{
    private readonly Control _header;
    private readonly VScrollBar _bar;
    private readonly Thickness _margin;
    private bool _queued;

    public OrbitraTableHeader(Control table)
    {
        MouseFilter = MouseFilterMode.Ignore;
        _header = table.FindControl<Control>("ListHeader");
        _margin = _header.Margin;
        _bar = table.FindControl<Content.Client.UserInterface.Controls.SearchListContainer>("SearchList")
            .Children.OfType<VScrollBar>().Single();
        _bar.OnVisibilityChanged += Visibility;
        _bar.OnResized += Refresh;
    }

    private void Visibility(Control _) => Refresh();

    private void Refresh()
    {
        if (_queued)
            return;
        _queued = true;
        // Видимость scrollbar меняется внутри Arrange: переносим инвалидирование за его пределы.
        UserInterfaceManager.DeferAction(() =>
        {
            _queued = false;
            if (Disposed || _header.Disposed || _bar.Disposed)
                return;
            var width = _bar.Visible ? _bar.DesiredSize.X : 0;
            _header.Margin = new Thickness(_margin.Left, _margin.Top, _margin.Right + width, _margin.Bottom);
        });
    }

    protected override void Dispose(bool disposing)
    {
        _bar.OnVisibilityChanged -= Visibility;
        _bar.OnResized -= Refresh;
        base.Dispose(disposing);
    }
}
