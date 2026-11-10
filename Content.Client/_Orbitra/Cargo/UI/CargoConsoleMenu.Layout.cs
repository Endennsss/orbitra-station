using Content.Client._Orbitra.UserInterface;
using Robust.Client.Graphics;

namespace Content.Client.Cargo.UI;

public sealed partial class CargoConsoleMenu
{
    private void InitializeOrbitraLayout()
    {
        var tabHost = TabContainer.Parent!;
        TabContainer.Orphan();
        var navigation = new OrbitraMenuTabs(TabContainer)
        {
            SeparationOverride = OrbitraUiMetrics.Small,
        };
        tabHost.AddChild(navigation);
        navigation.SetPositionFirst();
        TabContainer.PanelStyleBoxOverride = new StyleBoxFlat(Color.Transparent);

        SearchBar.MinHeight = OrbitraUiMetrics.ElementHeight;
        Categories.SetWidth = OrbitraUiMetrics.LabelWidth;
    }
}
