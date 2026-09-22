using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.Lobby;

/// <summary>Compact navigation for the existing organ and marking-layer tabs.</summary>
internal sealed class OrbitraMarkingNavigation : BoxContainer
{
    private readonly TabContainer _tabs;
    private readonly OptionButton _selector = new();
    private readonly List<string> _titles = new();

    private OrbitraMarkingNavigation(TabContainer tabs, string label)
    {
        Orientation = LayoutOrientation.Vertical;
        VerticalExpand = HorizontalExpand = true;
        SeparationOverride = 8;
        _tabs = tabs;
        _tabs.PanelStyleBoxOverride = new Robust.Client.Graphics.StyleBoxFlat(Color.Transparent);
        _selector.Prefix = label;
        _tabs.VerticalExpand = true;
        AddChild(_selector);
        AddChild(_tabs);
        _selector.OnItemSelected += args =>
        {
            if (args.Id >= 0 && args.Id < _tabs.ChildCount)
                _tabs.CurrentTab = args.Id;
        };
        _tabs.OnTabChanged += _ => SelectCurrent();
    }

    public static void Attach(Control root, string name)
    {
        var tabs = root.FindControl<TabContainer>(name);
        tabs.Orphan();
        var label = Loc.GetString(name == "OrganTabs" ? "orbitra-editor-body-part" : "orbitra-editor-marking-layer");
        root.AddChild(new OrbitraMarkingNavigation(tabs, label));
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var changed = _titles.Count != _tabs.ChildCount;
        for (var i = 0; !changed && i < _titles.Count; i++)
            changed = _titles[i] != _tabs.GetActualTabTitle(i);
        if (changed)
        {
            _titles.Clear();
            _selector.Clear();
            for (var i = 0; i < _tabs.ChildCount; i++)
            {
                var title = _tabs.GetActualTabTitle(i);
                _titles.Add(title);
                _selector.AddItem(title, i);
            }
        }
        _tabs.TabsVisible = false;
        _selector.Visible = _tabs.ChildCount > 1;
        SelectCurrent();
        return base.MeasureOverride(availableSize);
    }

    private void SelectCurrent()
    {
        if (_tabs.CurrentTab >= 0 && _tabs.CurrentTab < _selector.ItemCount)
            _selector.SelectId(_tabs.CurrentTab);
    }
}
