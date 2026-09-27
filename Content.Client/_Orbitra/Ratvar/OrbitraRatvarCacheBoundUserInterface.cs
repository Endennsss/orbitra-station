using System.Numerics;
using System.Linq;
using Content.Client._Orbitra.UserInterface;
using Content.Client.UserInterface.Controls;
using Content.Shared._Orbitra.Ratvar;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.Ratvar;

[UsedImplicitly]
public sealed class OrbitraRatvarCacheBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private OrbitraRatvarCacheWindow? _window;
    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<OrbitraRatvarCacheWindow>();
        _window.Choose += choice => SendMessage(new OrbitraRatvarCacheMessage(choice));
    }
    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is OrbitraRatvarCacheUiState cache) _window?.Update(cache);
    }
}

public sealed class OrbitraRatvarCacheWindow : FancyWindow
{
    public event Action<int>? Choose;
    private readonly RichTextLabel _status = new();
    private string[] _lastChoices = [];
    private readonly List<Button> _buttons = [];
    private readonly BoxContainer _choices = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8 };
    public OrbitraRatvarCacheWindow()
    {
        Title = Loc.GetString("orbitra-ratvar-cache-title");
        MinSize = new Vector2(440, 300);
        OrbitraEntryWindow.Attach(this);
        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8 };
        root.AddChild(_status);
        root.AddChild(_choices);
        ContentsContainer.AddChild(root);
    }
    public void Update(OrbitraRatvarCacheUiState state)
    {
        _status.SetMessage(Loc.GetString(state.Reason, ("seconds", state.Seconds)));
        var disabled = state.Reason != "orbitra-ratvar-cache-ready";
        if (_lastChoices.SequenceEqual(state.Choices))
        {
            foreach (var button in _buttons) button.Disabled = disabled;
            return;
        }
        _lastChoices = state.Choices;
        _buttons.Clear();
        _choices.RemoveAllChildren();
        for (var i = 0; i < state.Choices.Length; i++)
        {
            var choice = i;
            var panel = new PanelContainer();
            panel.AddStyleClass("OrbitraRatvarCard");
            var column = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
            var button = new Button { Text = Loc.GetString("ent-" + state.Choices[i]), Disabled = disabled };
            _buttons.Add(button);
            button.OnPressed += _ => Choose?.Invoke(choice);
            column.AddChild(button);
            var description = new RichTextLabel();
            var key = state.Choices[i] switch
            {
                "OrbitraRatvarRobes" => "orbitra-ratvar-cache-item-robes",
                "OrbitraRatvarCloak" => "orbitra-ratvar-cache-item-cloak",
                _ => "orbitra-ratvar-cache-item-spectacles",
            };
            description.SetMessage(Loc.GetString(key));
            column.AddChild(description);
            panel.AddChild(column);
            _choices.AddChild(panel);
        }
    }
}
