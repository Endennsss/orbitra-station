using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.Lobby;

/// <summary>One stable, horizontally scrollable line of journal sections.</summary>
public sealed class OrbitraJournalTabs : BoxContainer
{
    private readonly BoxContainer _items = new() { SeparationOverride = 4 };
    private readonly ScrollContainer _scroll;
    private readonly Button _previous = new Content.Client._Orbitra.UserInterface.OrbitraButton { SetSize = new Vector2(32, 36) };
    private readonly Button _next = new Content.Client._Orbitra.UserInterface.OrbitraButton { SetSize = new Vector2(32, 36) };
    private readonly Dictionary<string, Button> _buttons = new();
    private readonly ButtonGroup _group = new();
    public event Action<string>? Selected;

    public OrbitraJournalTabs()
    {
        SetHeight = 36;
        _previous.AddChild(new Content.Client._Orbitra.UserInterface.OrbitraIcon { Icon = "chevron_left" });
        _next.AddChild(new Content.Client._Orbitra.UserInterface.OrbitraIcon { Icon = "chevron_right" });
        _previous.ToolTip = Loc.GetString("orbitra-ui-previous-section");
        _next.ToolTip = Loc.GetString("orbitra-ui-next-section");
        Content.Client._Orbitra.UserInterface.OrbitraTooltips.Attach(_previous);
        Content.Client._Orbitra.UserInterface.OrbitraTooltips.Attach(_next);
        AddStyleClass("OrbitraJournalNavigation");
        SeparationOverride = 4;
        _scroll = new ScrollContainer { HorizontalExpand = true, VScrollEnabled = false, HScrollEnabled = true, HScrollBarHidden = true, Children = { _items } };
        AddChild(_previous);
        AddChild(_scroll);
        AddChild(_next);
        _previous.OnPressed += _ => _scroll.HScrollTarget -= Math.Max(100, _scroll.Width * 0.7f);
        _next.OnPressed += _ => _scroll.HScrollTarget += Math.Max(100, _scroll.Width * 0.7f);
        _previous.AddStyleClass("OrbitraJournalTab");
        _next.AddStyleClass("OrbitraJournalTab");
    }

    public void AddSection(string id, string title)
    {
        var button = new Content.Client._Orbitra.UserInterface.OrbitraButton { Text = title, ToggleMode = true, Group = _group, SetHeight = 36 };
        button.AddStyleClass("OrbitraJournalTab");
        button.OnPressed += _ => Selected?.Invoke(id);
        _buttons.Add(id, button);
        _items.AddChild(button);
    }

    public void SetAllowed(string id, bool allowed) => _buttons[id].Visible = allowed;

    public void Select(string id)
    {
        foreach (var (key, button) in _buttons)
            button.Pressed = key == id;
        UserInterfaceManager.DeferAction(() =>
        {
            if (Disposed || !_buttons.TryGetValue(id, out var selected))
                return;
            var left = selected.Position.X;
            if (left < _scroll.HScroll)
                _scroll.HScrollTarget = left;
            else if (left + selected.Width > _scroll.HScroll + _scroll.Width)
                _scroll.HScrollTarget = left + selected.Width - _scroll.Width;
        });
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        _items.Measure(new Vector2(float.PositiveInfinity, 36));
        _previous.Visible = _next.Visible = _items.DesiredSize.X > availableSize.X;
        return base.MeasureOverride(availableSize);
    }
}
