using System.Linq;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Explicit adapters for the two engine-owned spawning windows; no engine code is changed.</summary>
internal sealed class OrbitraEngineMenus : Control
{
    private OrbitraEngineMenus()
    {
        MouseFilter = MouseFilterMode.Ignore;
    }

    public static void Ensure(IUserInterfaceManager ui)
    {
        foreach (var child in ui.RootControl.Children)
            if (child is OrbitraEngineMenus)
                return;
        ui.RootControl.AddChild(new OrbitraEngineMenus());
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

    protected override void EnteredTree()
    {
        base.EnteredTree();
        UserInterfaceManager.WindowRoot.OnChildAdded += OnWindowAdded;
    }

    protected override void ExitedTree()
    {
        UserInterfaceManager.WindowRoot.OnChildAdded -= OnWindowAdded;
        base.ExitedTree();
    }

    private static void OnWindowAdded(Control control)
    {
        if (control is not (EntitySpawnWindow or TileSpawnWindow) ||
            control.HasStyleClass("OrbitraEntryWindow"))
            return;
        var window = (DefaultWindow) control;
        var heading = window.FindControl<PanelContainer>("WindowHeader");
        var title = window.FindControl<Label>("TitleLabel");
        var oldClose = window.FindControl<TextureButton>("CloseButton");
        heading.StyleClasses.Clear();
        heading.AddStyleClass("OrbitraWindowHeader");
        heading.SetHeight = OrbitraUiMetrics.HeaderHeight;
        title.StyleIdentifier = null;
        title.StyleClasses.Clear();
        title.AddStyleClass("OrbitraWindowTitle");
        title.AddStyleClass("FancyWindowTitle");
        title.TooltipSupplier = _ => new OrbitraTooltip(title.Text ?? "");
        title.Margin = new Thickness(OrbitraUiMetrics.WindowPadding, 0, 0, 0);
        oldClose.Visible = false;
        var close = new OrbitraWindowCloseButton();
        close.OnPressed += _ => OrbitraEntryWindow.RequestClose(window);
        oldClose.Parent!.AddChild(close);
        foreach (var child in window.Children)
            if (child is PanelContainer panel)
            {
                panel.StyleClasses.Clear();
                panel.AddStyleClass("OrbitraWindowSurface");
            }
        window.Contents.Margin = new Thickness(OrbitraUiMetrics.WindowPadding);
        if (window is EntitySpawnWindow entityWindow && entityWindow.ReplaceButton.Parent is BoxContainer toolbar)
        {
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
        OrbitraEntryWindow.Attach(window);
    }
}
