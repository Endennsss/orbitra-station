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
    private readonly RichTextLabel _power = new();
    private readonly RichTextLabel _vitality = new() { Visible = false };
    private readonly LineEdit _search = new() { HorizontalExpand = true };
    private readonly OptionButton _tier = new() { MinWidth = 190, HorizontalExpand = true };
    private readonly OptionButton _category = new() { MinWidth = 230, HorizontalExpand = true };
    private readonly RichTextLabel _details = new();
    private readonly RichTextLabel _usage = new();
    private readonly RichTextLabel _availability = new();
    private readonly Button _recite = new() { Disabled = true };
    private readonly Label _empty = new();
    private OrbitraRatvarScripturePrototype? _selected;
    private OrbitraRatvarUiState? _state;
    private readonly Dictionary<OrbitraRatvarScripturePrototype, (PanelContainer Panel, RichTextLabel Reason)> _cards = [];
    private readonly BoxContainer _entries = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
    private readonly Dictionary<OrbitraRatvarScripturePrototype, Button> _buttons = [];

    public OrbitraRatvarWindow()
    {
        Title = Loc.GetString("orbitra-ratvar-tablet-title");
        MinSize = new Vector2(660, 420);
        SetSize = new Vector2(760, 560);
        OrbitraEntryWindow.Attach(this);
        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8 };
        root.AddChild(_status);
        root.AddChild(_power);
        root.AddChild(_vitality);
        _search.PlaceHolder = Loc.GetString("orbitra-ratvar-search");
        _search.OnTextChanged += _ => FilterCards();
        _tier.AddItem(Loc.GetString("orbitra-ratvar-tier-all"), 0);
        for (var tier = 1; tier <= 3; tier++)
            _tier.AddItem(Loc.GetString("orbitra-ratvar-tier-filter", ("tier", tier)), tier);
        _tier.OnItemSelected += args => { _tier.SelectId(args.Id); FilterCards(); };
        _category.AddItem(Loc.GetString("orbitra-ratvar-category-all"), 0);
        foreach (var category in Enum.GetValues<OrbitraRatvarScriptureCategory>())
            _category.AddItem(Loc.GetString(CategoryKey(category)), (int) category + 1);
        _category.OnItemSelected += args => { _category.SelectId(args.Id); FilterCards(); };
        var filters = new BoxContainer { SeparationOverride = 6 };
        root.AddChild(_search);
        filters.AddChild(_tier);
        filters.AddChild(_category);
        root.AddChild(filters);
        var body = new BoxContainer { VerticalExpand = true, SeparationOverride = 10 };
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false, MinWidth = 250, MaxWidth = 280 };
        scroll.AddChild(_entries);
        body.AddChild(scroll);
        var detailPanel = new PanelContainer { HorizontalExpand = true, MinWidth = 270 };
        detailPanel.AddStyleClass("OrbitraRatvarCard");
        var detailColumn = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 10 };
        var detailScroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        var detailText = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 12 };
        detailText.AddChild(_details);
        detailText.AddChild(_usage);
        detailText.AddChild(_availability);
        detailScroll.AddChild(detailText);
        detailColumn.AddChild(detailScroll);
        _recite.OnPressed += _ =>
        {
            if (_selected != null && !_recite.Disabled) Recite?.Invoke(_selected.ID);
        };
        detailColumn.AddChild(_recite);
        detailPanel.AddChild(detailColumn);
        body.AddChild(detailPanel);
        root.AddChild(body);
        _empty.Text = Loc.GetString("orbitra-ratvar-search-empty");
        _empty.Visible = false;
        root.AddChild(_empty);
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
            var button = new Button { Text = Loc.GetString(scripture.Name), ToggleMode = true,
                HorizontalExpand = true, ClipText = true, ToolTip = Loc.GetString(scripture.Name), TextAlign = Label.AlignMode.Left };
            button.OnPressed += _ => SelectScripture(scripture);
            column.AddChild(button);
            column.AddChild(new Label { Text = Loc.GetString("orbitra-ratvar-scripture-cost", ("tier", scripture.Tier), ("energy", scripture.Energy)) });
            var reason = new RichTextLabel { Visible = false };
            column.AddChild(reason);
            panel.AddChild(column);
            _entries.AddChild(panel);
            _buttons.Add(scripture, button);
            _cards.Add(scripture, (panel, reason));
        }
        FilterCards();
    }

    public void UpdateCult(OrbitraRatvarUiState state)
    {
        _state = state;
        _status.SetMessage(Loc.GetString("orbitra-ratvar-status", ("energy", state.Energy), ("tier", state.Tier), ("converts", state.Converts)));
        _power.SetMessage(Loc.GetString("orbitra-ratvar-power-rates",
            ("income", state.IncomeRate.ToString("0.0")), ("expense", state.ExpenseRate.ToString("0.0"))));
        _vitality.Visible = state.Tier >= 2 || state.Vitality > 0;
        _vitality.SetMessage(Loc.GetString("orbitra-ratvar-vitality-stock", ("amount", state.Vitality.ToString("0.0"))));
        foreach (var (scripture, button) in _buttons)
        {
            var reason = state.Busy ? "orbitra-ratvar-unavailable-busy" : scripture.Tier > state.Tier ?
                "orbitra-ratvar-unavailable-tier" : scripture.Energy > state.Energy ? "orbitra-ratvar-unavailable-energy" : null;
            if (reason == null) state.Unavailable.TryGetValue(scripture.ID, out reason);
            button.ToolTip = reason == null ? null : Loc.GetString(reason);
            var label = _cards[scripture].Reason;
            label.Visible = reason != null;
            if (reason != null) label.SetMessage(Loc.GetString(reason));
        }
        UpdateDetails();
    }

    private static string CategoryKey(OrbitraRatvarScriptureCategory category) =>
        $"orbitra-ratvar-category-{category.ToString().ToLowerInvariant()}";

    private void SelectScripture(OrbitraRatvarScripturePrototype scripture)
    {
        _selected = scripture;
        foreach (var (entry, button) in _buttons) button.Pressed = entry == scripture;
        UpdateDetails();
    }

    private void UpdateDetails()
    {
        if (_selected is not { } scripture)
        {
            _details.SetMessage(Loc.GetString("orbitra-ratvar-search-empty"));
            _usage.SetMessage(string.Empty);
            _availability.SetMessage(string.Empty);
            _recite.Disabled = true;
            return;
        }
        _details.SetMessage(Loc.GetString(scripture.Name) + "\n\n" + Loc.GetString(scripture.Description) + "\n\n" +
            Loc.GetString("orbitra-ratvar-scripture-cost", ("tier", scripture.Tier), ("energy", scripture.Energy)) + "\n" +
            Loc.GetString("orbitra-ratvar-scripture-requirements", ("seconds", scripture.Delay.TotalSeconds), ("invokers", scripture.Invokers)));
        var aimed = scripture.Empowerment is not OrbitraRatvarEmpowerment.None and not OrbitraRatvarEmpowerment.Vanguard;
        var hint = aimed ? "target" : scripture.Empowerment == OrbitraRatvarEmpowerment.Vanguard ? "self" :
            scripture.Repair ? "repair" : scripture.Structure ? "structure" : "item";
        _usage.SetMessage(Loc.GetString($"orbitra-ratvar-use-{hint}", ("range", scripture.TargetRange),
            ("seconds", scripture.TargetWindow.TotalSeconds)));
        _recite.Text = Loc.GetString(aimed ? "orbitra-ratvar-prepare" : "orbitra-ratvar-recite");
        var reason = _state == null ? "orbitra-ratvar-waiting" : _state.Busy ? "orbitra-ratvar-unavailable-busy" :
            scripture.Tier > _state.Tier ? "orbitra-ratvar-unavailable-tier" :
            scripture.Energy > _state.Energy ? "orbitra-ratvar-unavailable-energy" : null;
        if (reason == null) _state!.Unavailable.TryGetValue(scripture.ID, out reason);
        _recite.Disabled = reason != null;
        _availability.SetMessage(Loc.GetString(reason ?? "orbitra-ratvar-ready"));
    }

    private void FilterCards()
    {
        var search = _search.Text.Trim();
        foreach (var (scripture, card) in _cards)
            card.Panel.Visible = (_tier.SelectedId == 0 || _tier.SelectedId == scripture.Tier) &&
                (_category.SelectedId == 0 || _category.SelectedId == (int) scripture.Category + 1) &&
                (Loc.GetString(scripture.Name).Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                 Loc.GetString(scripture.Description).Contains(search, StringComparison.CurrentCultureIgnoreCase));
        var first = _cards.FirstOrDefault(pair => pair.Value.Panel.Visible).Key;
        _empty.Visible = first == null;
        if (_selected == null || !_cards[_selected].Panel.Visible)
        {
            _selected = first;
            if (first != null) SelectScripture(first);
            else UpdateDetails();
        }
    }
}
