using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

using Content.Client._Orbitra.UserInterface; // Orbitra-Edit

namespace Content.Client._Orbitra.Lobby;

/// <summary>Responsive loadout categories sharing one content tree and one selection.</summary>
public sealed class OrbitraLoadoutTabs : BoxContainer
{
    private readonly OptionButton _selector = new() { Visible = false };
    private readonly BoxContainer _buttons = new() { Orientation = LayoutOrientation.Vertical, SeparationOverride = 4 };
    private readonly BoxContainer _contents = new() { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true };
    private readonly ScrollContainer _navigation;
    private readonly List<Button> _categories = new();
    private int _selected;

    public OrbitraLoadoutTabs()
    {
        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 8;
        AddChild(_selector);
        var body = new BoxContainer { VerticalExpand = true, SeparationOverride = 12 };
        _navigation = new ScrollContainer { HScrollEnabled = false, SetWidth = 176, Children = { _buttons } };
        body.AddChild(_navigation);
        body.AddChild(new ScrollContainer { HScrollEnabled = false, HorizontalExpand = true, Children = { _contents } });
        AddChild(body);
        _selector.OnItemSelected += args => Select(args.Id);
    }

    /// <summary>Adds existing loadout controls without changing equipment or restrictions.</summary>
    public int AddTab(Control control, string title)
    {
        var index = _categories.Count;
        var button = new OrbitraLobbyButton { ToggleMode = true, ToolTip = title };
        button.Label.Visible = false;
        var caption = new RichTextLabel { HorizontalExpand = true, VerticalAlignment = VAlignment.Center };
        caption.SetMessage(title);
        button.AddChild(caption);
        button.AddStyleClass("OrbitraNavigationButton");
        button.OnPressed += _ => Select(index);
        _categories.Add(button);
        _buttons.AddChild(button);
        _contents.AddChild(control);
        control.Visible = index == _selected;
        button.Pressed = index == _selected;
        _selector.AddItem(title, index);
        return index;
    }

    private void Select(int index)
    {
        var changed = _selected != index;
        if (changed && _selected < _contents.ChildCount)
            OrbitraMotion.Finish(_contents.GetChild(_selected));
        _selected = index;
        _selector.SelectId(index);
        for (var i = 0; i < _categories.Count; i++)
        {
            _categories[i].Pressed = i == index;
            _contents.GetChild(i).Visible = i == index;
        }
        if (changed)
            OrbitraMotion.Reveal(_contents.GetChild(index), OrbitraMotion.SectionDuration);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        _navigation.Visible = availableSize.X >= 600;
        _selector.Visible = !_navigation.Visible;
        return base.MeasureOverride(availableSize);
    }
}
