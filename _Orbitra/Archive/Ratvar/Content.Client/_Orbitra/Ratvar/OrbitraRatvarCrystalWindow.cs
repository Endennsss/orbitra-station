using System.Numerics;
using Content.Client._Orbitra.UserInterface;
using Content.Client.UserInterface.Controls;
using Content.Shared._Orbitra.Ratvar;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.Ratvar;

/// <summary>Bounded, searchable crystal catalogue; remote labels are rendered as plain text.</summary>
public sealed class OrbitraRatvarCrystalWindow : FancyWindow
{
    public event Action<NetEntity>? Project;
    public event Action<string>? Rename;
    private readonly LineEdit _name = new() { HorizontalExpand = true, IsValid = text => text.Length <= 40 };
    private readonly LineEdit _search = new() { HorizontalExpand = true };
    private readonly BoxContainer _points = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
    private OrbitraRatvarCrystalUiState? _state;
    private string? _lastName;

    public OrbitraRatvarCrystalWindow()
    {
        Title = Loc.GetString("orbitra-ratvar-crystal-title");
        MinSize = new Vector2(360, 280);
        SetSize = new Vector2(500, 460);
        OrbitraEntryWindow.Attach(this);
        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8 };
        var help = new RichTextLabel();
        help.SetMessage(Loc.GetString("orbitra-ratvar-crystal-help"));
        root.AddChild(help);
        var row = new BoxContainer { SeparationOverride = 6 };
        _name.PlaceHolder = Loc.GetString("orbitra-ratvar-crystal-name");
        var save = new Button { Text = Loc.GetString("orbitra-ratvar-crystal-save") };
        save.OnPressed += _ => Rename?.Invoke(_name.Text);
        _name.OnTextEntered += _ => Rename?.Invoke(_name.Text);
        row.AddChild(_name);
        row.AddChild(save);
        root.AddChild(row);
        _search.PlaceHolder = Loc.GetString("orbitra-ratvar-crystal-search");
        _search.OnTextChanged += _ => Populate();
        root.AddChild(_search);
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        scroll.AddChild(_points);
        root.AddChild(scroll);
        ContentsContainer.AddChild(root);
    }

    /// <summary>Refreshes readiness without discarding an unfinished name or search query.</summary>
    public void Update(OrbitraRatvarCrystalUiState state)
    {
        if (_lastName == null || _name.Text == _lastName) _name.Text = state.Name;
        _lastName = state.Name;
        _state = state;
        Populate();
    }

    private void Populate()
    {
        _points.RemoveAllChildren();
        if (_state == null) return;
        foreach (var point in _state.Destinations)
        {
            if (!point.Name.Contains(_search.Text, StringComparison.OrdinalIgnoreCase)) continue;
            var panel = new PanelContainer();
            panel.AddStyleClass("OrbitraRatvarCard");
            var column = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
            var text = Loc.GetString("orbitra-ratvar-crystal-project", ("name", point.Name), ("x", point.X), ("y", point.Y));
            var button = new Button
            {
                Text = text, ClipText = true, ToolTip = text, HorizontalExpand = true,
                Disabled = point.Reason != "orbitra-ratvar-crystal-point-ready",
            };
            button.OnPressed += _ => Project?.Invoke(point.Entity);
            column.AddChild(button);
            var reason = new RichTextLabel();
            reason.SetMessage(Loc.GetString(point.Reason));
            column.AddChild(reason);
            panel.AddChild(column);
            _points.AddChild(panel);
        }
        if (_points.ChildCount == 0)
        {
            var empty = new RichTextLabel();
            empty.SetMessage(Loc.GetString("orbitra-ratvar-crystal-empty"));
            _points.AddChild(empty);
        }
    }
}
