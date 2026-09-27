using System.Numerics;
using System.Linq;
using Content.Client._Orbitra.UserInterface;
using Content.Client.UserInterface.Controls;
using Content.Shared._Orbitra.Ratvar;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.Ratvar;

/// <summary>A bounded private roster and server-authorized recall controls.</summary>
public sealed class OrbitraRatvarEminenceWindow : FancyWindow
{
    public event Action<NetEntity>? SelectMember;
    public event Action? ClearSelection;
    public event Action<NetEntity>? Recall;
    public event Action<NetEntity>? MassRecall;
    public event Action<OrbitraRatvarEminenceReality>? Manipulate;
    private readonly BoxContainer _abilities = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
    private readonly LineEdit _search = new() { HorizontalExpand = true };
    private readonly BoxContainer _members = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
    private readonly Button _clear = new() { HorizontalExpand = true };
    private OrbitraRatvarEminenceUiState? _state;
    private OrbitraRatvarEminenceUiState? _abilitiesState;
    private readonly RichTextLabel _abilityStatus = new();
    private readonly RichTextLabel _realityStatus = new();

    public OrbitraRatvarEminenceWindow()
    {
        Title = Loc.GetString("orbitra-ratvar-eminence-menu-title");
        MinSize = new Vector2(360, 280);
        SetSize = new Vector2(500, 440);
        OrbitraEntryWindow.Attach(this);
        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8 };
        var help = new RichTextLabel();
        help.SetMessage(Loc.GetString("orbitra-ratvar-eminence-menu-help"));
        root.AddChild(help);
        _search.PlaceHolder = Loc.GetString("orbitra-ratvar-eminence-menu-search");
        _search.OnTextChanged += _ => Populate();
        root.AddChild(_search);
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        var body = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8 };
        body.AddChild(_members);
        body.AddChild(_abilities);
        scroll.AddChild(body);
        root.AddChild(scroll);
        _clear.Text = Loc.GetString("orbitra-ratvar-eminence-menu-clear");
        _clear.OnPressed += _ => ClearSelection?.Invoke();
        root.AddChild(_clear);
        ContentsContainer.AddChild(root);
    }

    /// <summary>Applies a server snapshot while preserving the local search text.</summary>
    public void Update(OrbitraRatvarEminenceUiState state)
    {
        _state = state;
        Populate();
    }

    private void Populate()
    {
        _members.RemoveAllChildren();
        PopulateAbilities();
        _clear.Disabled = _state?.Selected == null;
        if (_state == null) return;
        foreach (var member in _state.Members)
        {
            if (!member.Name.Contains(_search.Text, StringComparison.OrdinalIgnoreCase)) continue;
            var panel = new PanelContainer();
            panel.AddStyleClass("OrbitraRatvarCard");
            var column = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
            var selected = _state.Selected == member.Mind;
            var name = string.IsNullOrWhiteSpace(member.Name) ? Loc.GetString("orbitra-ratvar-eminence-menu-unnamed") : member.Name;
            var button = new Button
            {
                Text = name, ToolTip = name, ClipText = true, HorizontalExpand = true,
                Disabled = selected || member.Availability != OrbitraRatvarEminenceAvailability.Ready,
            };
            button.OnPressed += _ => SelectMember?.Invoke(member.Mind);
            column.AddChild(button);
            var reason = new RichTextLabel();
            reason.SetMessage(Loc.GetString(selected ? "orbitra-ratvar-eminence-menu-selected" : ReasonKey(member.Availability)));
            column.AddChild(reason);
            panel.AddChild(column);
            _members.AddChild(panel);
        }
        if (_members.ChildCount != 0) return;
        var empty = new RichTextLabel();
        empty.SetMessage(Loc.GetString("orbitra-ratvar-eminence-menu-empty"));
        _members.AddChild(empty);
    }

    private void PopulateAbilities()
    {
        if (_state == null) return;
        _abilityStatus.SetMessage(Loc.GetString("orbitra-ratvar-eminence-ability-status",
            ("energy", _state.Energy), ("cost", _state.RecallCost), ("seconds", _state.RecallCooldown)));
        _realityStatus.SetMessage(Loc.GetString("orbitra-ratvar-eminence-reality-status",
            ("cost", _state.RealityCost), ("seconds", _state.RealityCooldown)));
        // Изменение счётчиков не пересоздаёт кнопки подтверждения посреди второго нажатия.
        if (_abilitiesState is { } previous && previous.Recalling == _state.Recalling &&
            previous.RealityReason == _state.RealityReason && previous.Destinations.SequenceEqual(_state.Destinations)) return;
        _abilitiesState = _state;
        _abilities.RemoveAllChildren();
        _abilities.AddChild(_abilityStatus);
        var help = new RichTextLabel();
        help.SetMessage(Loc.GetString("orbitra-ratvar-eminence-ability-help"));
        _abilities.AddChild(help);
        _abilities.AddChild(_realityStatus);
        foreach (var effect in Enum.GetValues<OrbitraRatvarEminenceReality>())
        {
            var button = new ConfirmButton
            {
                Text = Loc.GetString(effect == OrbitraRatvarEminenceReality.Anomaly ?
                    "orbitra-ratvar-eminence-reality-anomaly" : "orbitra-ratvar-eminence-reality-grid"),
                HorizontalExpand = true, Disabled = _state.RealityReason != "orbitra-ratvar-eminence-ability-ready",
                ToolTip = Loc.GetString(_state.RealityReason),
            };
            button.OnPressed += _ => Manipulate?.Invoke(effect);
            _abilities.AddChild(button);
        }
        var realityReason = new RichTextLabel();
        realityReason.SetMessage(Loc.GetString(_state.RealityReason));
        _abilities.AddChild(realityReason);
        foreach (var destination in _state.Destinations)
        {
            var panel = new PanelContainer();
            panel.AddStyleClass("OrbitraRatvarCard");
            var column = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
            column.AddChild(new Label { Text = destination.Name, ClipText = true, ToolTip = destination.Name });
            var recall = new Button
            {
                Text = Loc.GetString("orbitra-ratvar-eminence-recall-button"), HorizontalExpand = true,
                Disabled = _state.Recalling || destination.RecallReason != "orbitra-ratvar-eminence-ability-ready",
                ToolTip = Loc.GetString(destination.RecallReason),
            };
            recall.OnPressed += _ => Recall?.Invoke(destination.Entity);
            column.AddChild(recall);
            var reason = new RichTextLabel();
            reason.SetMessage(Loc.GetString(_state.Recalling ? "orbitra-ratvar-eminence-recall-channeling" : destination.RecallReason));
            column.AddChild(reason);
            var mass = new ConfirmButton
            {
                Text = Loc.GetString("orbitra-ratvar-eminence-mass-button"), HorizontalExpand = true,
                Disabled = destination.MassReason != "orbitra-ratvar-eminence-ability-ready",
                ToolTip = Loc.GetString(destination.MassReason),
            };
            mass.OnPressed += _ => MassRecall?.Invoke(destination.Entity);
            column.AddChild(mass);
            var massReason = new RichTextLabel();
            massReason.SetMessage(Loc.GetString(destination.MassReason));
            column.AddChild(massReason);
            panel.AddChild(column);
            _abilities.AddChild(panel);
        }
        if (_state.Destinations.Length != 0) return;
        var empty = new RichTextLabel();
        empty.SetMessage(Loc.GetString("orbitra-ratvar-eminence-no-destinations"));
        _abilities.AddChild(empty);
    }

    private static string ReasonKey(OrbitraRatvarEminenceAvailability reason) => reason switch
    {
        OrbitraRatvarEminenceAvailability.Ready => "orbitra-ratvar-eminence-menu-ready",
        OrbitraRatvarEminenceAvailability.NoBody => "orbitra-ratvar-eminence-menu-no-body",
        OrbitraRatvarEminenceAvailability.Offline => "orbitra-ratvar-eminence-menu-offline",
        OrbitraRatvarEminenceAvailability.Container => "orbitra-ratvar-eminence-menu-container",
        OrbitraRatvarEminenceAvailability.NotAlive => "orbitra-ratvar-eminence-menu-not-alive",
        OrbitraRatvarEminenceAvailability.Blind => "orbitra-ratvar-eminence-menu-blind",
        OrbitraRatvarEminenceAvailability.OutsideStation => "orbitra-ratvar-eminence-menu-outside",
        _ => "orbitra-ratvar-eminence-menu-unavailable",
    };
}
