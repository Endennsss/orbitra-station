using System.Linq;
using System.Numerics;
using Content.Client._Orbitra.UserInterface;
using Content.Client.UserInterface.Controls;
using Content.Shared._Orbitra.Ratvar;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Orbitra.Ratvar;

/// <summary>Displays public scripture definitions and the holder's server-authorized cult state.</summary>
[UsedImplicitly]
public sealed partial class OrbitraRatvarBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    private OrbitraRatvarWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<OrbitraRatvarWindow>();
        _window.Populate(_prototypes.EnumeratePrototypes<OrbitraRatvarScripturePrototype>());
        _window.Recite += id => SendMessage(new OrbitraRatvarScriptureMessage(id));
        _window.Communicate += text => SendMessage(new OrbitraRatvarCommunicateMessage(text));
        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is OrbitraRatvarUiState cult) _window?.UpdateCult(cult);
    }
}

/// <summary>Brass-accented scripture catalogue and private communication entry.</summary>
public sealed class OrbitraRatvarWindow : FancyWindow
{
    public event Action<string>? Recite;
    public event Action<string>? Communicate;
    private readonly RichTextLabel _status = new();
    private readonly BoxContainer _entries = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
    private readonly Dictionary<OrbitraRatvarScripturePrototype, Button> _buttons = [];

    public OrbitraRatvarWindow()
    {
        Title = Loc.GetString("orbitra-ratvar-tablet-title");
        MinSize = new Vector2(440, 360);
        SetSize = new Vector2(580, 640);
        OrbitraEntryWindow.Attach(this);
        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8 };
        root.AddChild(_status);
        var scroll = new ScrollContainer { VerticalExpand = true };
        scroll.AddChild(_entries);
        root.AddChild(scroll);
        var communication = new BoxContainer { SeparationOverride = 6 };
        var input = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("orbitra-ratvar-message-placeholder") };
        var send = new Button { Text = Loc.GetString("orbitra-ratvar-message-send") };
        void Send()
        {
            if (string.IsNullOrWhiteSpace(input.Text)) return;
            Communicate?.Invoke(input.Text);
            input.Text = string.Empty;
        }
        send.OnPressed += _ => Send();
        input.OnTextEntered += _ => Send();
        communication.AddChild(input);
        communication.AddChild(send);
        root.AddChild(communication);
        ContentsContainer.AddChild(root);
    }

    public void Populate(IEnumerable<OrbitraRatvarScripturePrototype> scriptures)
    {
        foreach (var scripture in scriptures.OrderBy(p => p.Tier).ThenBy(p => p.ID))
        {
            var panel = new PanelContainer();
            panel.AddStyleClass("OrbitraRatvarCard");
            var column = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
            var button = new Button { Text = Loc.GetString(scripture.Name), Disabled = true };
            button.OnPressed += _ => Recite?.Invoke(scripture.ID);
            column.AddChild(button);
            var description = new RichTextLabel();
            description.SetMessage(Loc.GetString(scripture.Description));
            column.AddChild(description);
            column.AddChild(new Label { Text = Loc.GetString("orbitra-ratvar-scripture-cost", ("tier", scripture.Tier), ("energy", scripture.Energy)) });
            panel.AddChild(column);
            _entries.AddChild(panel);
            _buttons.Add(scripture, button);
        }
    }

    public void UpdateCult(OrbitraRatvarUiState state)
    {
        _status.SetMessage(Loc.GetString("orbitra-ratvar-status", ("energy", state.Energy), ("tier", state.Tier), ("converts", state.Converts)));
        foreach (var (scripture, button) in _buttons)
        {
            var reason = state.Busy ? "orbitra-ratvar-unavailable-busy" : scripture.Tier > state.Tier ?
                "orbitra-ratvar-unavailable-tier" : scripture.Energy > state.Energy ? "orbitra-ratvar-unavailable-energy" : null;
            if (reason == null) state.Unavailable.TryGetValue(scripture.ID, out reason);
            button.Disabled = reason != null;
            button.ToolTip = reason == null ? null : Loc.GetString(reason);
        }
    }
}
