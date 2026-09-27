using System.Numerics;
using Content.Client._Orbitra.UserInterface;
using Content.Client.UserInterface.Controls;
using Content.Shared._Orbitra.Ratvar;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.Ratvar;

[UsedImplicitly]
public sealed class OrbitraRatvarTravelBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private OrbitraRatvarTravelWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<OrbitraRatvarTravelWindow>();
        _window.Travel += target => SendMessage(new OrbitraRatvarTravelMessage(target));
        _window.Rename += name => SendMessage(new OrbitraRatvarTravelNameMessage(name));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is OrbitraRatvarTravelUiState travel) _window?.Update(travel);
    }
}

/// <summary>Only server-authorized endpoints are shown; labels are plain text.</summary>
public sealed class OrbitraRatvarTravelWindow : FancyWindow
{
    public event Action<NetEntity>? Travel;
    public event Action<string>? Rename;
    private readonly LineEdit _name = new() { HorizontalExpand = true };
    private readonly RichTextLabel _status = new();
    private readonly BoxContainer _points = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
    private bool _initialName;
    private Dictionary<NetEntity, string>? _lastPoints;
    private readonly Dictionary<NetEntity, (Button Button, RichTextLabel Reason)> _destinations = [];

    public OrbitraRatvarTravelWindow()
    {
        Title = Loc.GetString("orbitra-ratvar-travel-title");
        MinSize = new Vector2(420, 280);
        SetSize = new Vector2(500, 420);
        OrbitraEntryWindow.Attach(this);
        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8 };
        var row = new BoxContainer { SeparationOverride = 6 };
        _name.PlaceHolder = Loc.GetString("orbitra-ratvar-travel-name");
        var save = new Button { Text = Loc.GetString("orbitra-ratvar-travel-rename") };
        save.OnPressed += _ => Rename?.Invoke(_name.Text);
        row.AddChild(_name);
        row.AddChild(save);
        root.AddChild(row);
        root.AddChild(_status);
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        scroll.AddChild(_points);
        root.AddChild(scroll);
        ContentsContainer.AddChild(root);
    }

    public void Update(OrbitraRatvarTravelUiState state)
    {
        if (!_initialName)
        {
            _name.Text = state.Name;
            _initialName = true;
        }
        _status.SetMessage(Loc.GetString(state.Reason, ("energy", state.Energy), ("cost", state.Cost)));
        if (SamePoints(state.Destinations))
        {
            UpdateAvailability(state);
            return;
        }
        _lastPoints = state.Destinations;
        _destinations.Clear();
        _points.RemoveAllChildren();
        foreach (var (target, name) in state.Destinations)
        {
            var panel = new PanelContainer();
            panel.AddStyleClass("OrbitraRatvarCard");
            var button = new Button { Text = name, HorizontalExpand = true, ClipText = true, ToolTip = name };
            button.OnPressed += _ => Travel?.Invoke(target);
            var column = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
            var reason = new RichTextLabel();
            column.AddChild(button);
            column.AddChild(reason);
            panel.AddChild(column);
            _points.AddChild(panel);
            _destinations.Add(target, (button, reason));
        }
        if (state.Destinations.Count == 0)
        {
            var empty = new RichTextLabel();
            empty.SetMessage(Loc.GetString("orbitra-ratvar-travel-empty"));
            _points.AddChild(empty);
        }
        UpdateAvailability(state);
    }

    private void UpdateAvailability(OrbitraRatvarTravelUiState state)
    {
        foreach (var (target, controls) in _destinations)
        {
            var disabled = state.Unavailable.TryGetValue(target, out var reason);
            controls.Button.Disabled = disabled;
            controls.Reason.Visible = disabled;
            if (disabled) controls.Reason.SetMessage(Loc.GetString(reason!));
        }
    }

    private bool SamePoints(Dictionary<NetEntity, string> points)
    {
        if (_lastPoints == null || _lastPoints.Count != points.Count) return false;
        foreach (var (id, name) in points)
            if (!_lastPoints.TryGetValue(id, out var previous) || previous != name) return false;
        return true;
    }
}
