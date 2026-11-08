using System.Linq;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Feature-specific spawning layout and toggles over the common window theme.</summary>
internal static class OrbitraEngineMenus
{
    public static void Ensure(IUserInterfaceManager ui)
    {
        ui.GetUIController<OrbitraWindowThemeController>().Ensure();
    }

    /// <summary>Routes explicit content-side toggles through the same reversible closing lifecycle.</summary>
    public static void Toggle<T>(IUserInterfaceManager ui, Action nativeToggle) where T : DefaultWindow, new()
    {
        Ensure(ui);
        if (ui.TryGetFirstWindow<T>(out var window) && window is { IsOpen: true })
        {
            if (OrbitraEntryWindow.IsClosing(window))
                window.Open();
            else
                OrbitraEntryWindow.RequestClose(window);
            return;
        }
        nativeToggle();
    }

    internal static void ApplyLayout(DefaultWindow window)
    {
        if (window is EntitySpawnWindow or TileSpawnWindow)
            window.AddStyleClass(OrbitraWindowTheme.EditorLayoutClass);
        if (window is not EntitySpawnWindow entityWindow || window.HasStyleClass("OrbitraSpawnLayout")
            || entityWindow.ReplaceButton.Parent is not BoxContainer toolbar)
            return;
        window.AddStyleClass("OrbitraSpawnLayout");
        var actions = new WrapContainer
        {
            SeparationOverride = OrbitraUiMetrics.Small,
            CrossSeparationOverride = OrbitraUiMetrics.Small
        };
        foreach (var action in toolbar.Children.ToArray())
        {
            action.Orphan();
            action.HorizontalExpand = false;
            actions.AddChild(action);
        }
        toolbar.AddChild(actions);
    }
}
