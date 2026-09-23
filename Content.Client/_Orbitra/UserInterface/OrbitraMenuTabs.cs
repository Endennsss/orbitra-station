using System.Numerics;
using Content.Client._Orbitra.Lobby;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Scrollable navigation for existing tabs; pages and their handlers remain unchanged.</summary>
internal sealed class OrbitraMenuTabs : BoxContainer
{
    private readonly TabContainer _tabs;
    private readonly OrbitraJournalTabs _navigation = new();

    public OrbitraMenuTabs(TabContainer tabs)
    {
        _tabs = tabs;
        Orientation = LayoutOrientation.Vertical;
        VerticalExpand = HorizontalExpand = true;
        _tabs.TabsVisible = false;
        _tabs.VerticalExpand = true;
        for (var i = 0; i < tabs.ChildCount; i++)
            _navigation.AddSection(i.ToString(), tabs.GetActualTabTitle(i));
        _navigation.Selected += SelectTab;
        _tabs.OnTabChanged += SynchronizeSelection;
        AddChild(_navigation);
        AddChild(tabs);
        SynchronizeSelection(_tabs.CurrentTab);
    }

    private void SelectTab(string id) => _tabs.CurrentTab = int.Parse(id);

    private void SynchronizeSelection(int index) => _navigation.Select(index.ToString());

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tabs.OnTabChanged -= SynchronizeSelection;
            _navigation.Selected -= SelectTab;
        }
        base.Dispose(disposing);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        for (var i = 0; i < _tabs.ChildCount; i++)
            _navigation.SetAllowed(i.ToString(), _tabs.GetTabVisible(i));
        return base.MeasureOverride(availableSize);
    }
}
