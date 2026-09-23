using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

using Content.Client._Orbitra.UserInterface; // Orbitra-Edit

namespace Content.Client.Guidebook.Controls;

public sealed partial class GuidebookWindow
{
    private readonly Popup _orbitraSectionsPopup = new();
    private readonly PanelContainer _orbitraSectionsPanel = new();
    private float _orbitraTreeWidth = 260;

    private void InitializeOrbitraNavigation()
    {
        Tree.DrawLines = true;
        TableOfContents.DrawLines = true;
        _orbitraSectionsPanel.AddStyleClass("OrbitraWindowSurface");
        _orbitraSectionsPopup.AddChild(_orbitraSectionsPanel);
        OrbitraMotion.BindPopup(_orbitraSectionsPopup, this, RestoreOrbitraTree);
        InitializeOrbitraSearch();
        Split.AvailableWidthChanged += UpdateOrbitraNavigation;
        Split.OnSplitCenterChanging += args =>
        {
            if (TreeBox.Visible)
                _orbitraTreeWidth = Math.Clamp(args.SplitCenter - Split.SplitWidth / 2, 220, Math.Max(220, Split.Width - 410));
        };
        OrbitraSections.OnPressed += _ =>
        {
            if (_orbitraSectionsPopup.Visible)
            {
                _orbitraSectionsPopup.Close();
                return;
            }
            OpenOrbitraSections();
        };
        _orbitraSectionsPopup.OnPopupHide += () =>
        {
            _orbitraSectionsPopup.Orphan();
        };
        Tree.OnSelectedItemChanged += _ => _orbitraSectionsPopup.Close();
        OnClose += () => _orbitraSectionsPopup.Close();
    }

    private void OpenOrbitraSections()
    {
        if (_orbitraSectionsPopup.Visible)
            return;
        OrbitraNavigation.Orphan();
        _orbitraSectionsPanel.AddChild(OrbitraNavigation);
        UserInterfaceManager.ModalRoot.AddChild(_orbitraSectionsPopup);
        var available = Root!.Size - new Vector2(32);
        var size = Vector2.Min(new Vector2(320, Math.Max(120, Split.Height)), available);
        _orbitraSectionsPopup.SetSize = _orbitraSectionsPopup.MaxSize = size;
        var origin = Vector2.Clamp(Split.GlobalPosition, new Vector2(16), Vector2.Max(new Vector2(16), Root.Size - size - new Vector2(16)));
        _orbitraSectionsPopup.Open(UIBox2.FromDimensions(origin, size));
        OrbitraMotion.Reveal(_orbitraSectionsPopup, OrbitraMotion.MenuDuration);
    }

    private void RestoreOrbitraTree()
    {
        if (Disposed || Tree.Disposed)
            return;
        OrbitraNavigation.Orphan();
        TreeBox.AddChild(OrbitraNavigation);
        OrbitraNavigation.SetPositionInParent(0);
    }

    private void UpdateOrbitraNavigation()
    {
        UpdateOrbitraNavigation(Split.Width);
    }

    private void UpdateOrbitraNavigation(float width)
    {
        var focused = UserInterfaceManager.KeyboardFocused;
        var navigationFocused = OrbitraKeyboardNavigation.Contains(OrbitraNavigation, focused);
        _orbitraSectionsPopup.Close();
        OrbitraMotion.FinishPopup(_orbitraSectionsPopup);
        var compact = width < 720;
        var hasSections = _entries.Count > 1;
        OrbitraSections.Visible = compact && hasSections;
        TreeBox.Visible = !compact && hasSections;
        var wide = !compact && hasSections;
        TreeBox.MinWidth = wide ? 220 : 0;
        Split.Second!.MinWidth = wide ? 400 : 0;
        Split.SplitWidth = wide ? 10 : 0;
        Split.SplitEdgeSeparation = 0;
        // Ширина дерева не зависит от обрезанных подписей и переживает компактный режим.
        if (width > 0)
        {
            var treeWidth = wide ? Math.Clamp(_orbitraTreeWidth, 220, width - 410) : 0;
            Split.SetSplitFractionOnNextArrange((treeWidth + Split.SplitWidth / 2) / width);
        }
        if (navigationFocused)
            OrbitraKeyboardNavigation.Focus(focused != null && focused.VisibleInTree ? focused :
                OrbitraSections.Visible ? OrbitraSections : Scroll);
    }

    protected override void Dispose(bool disposing)
    {
        _orbitraSectionsPopup.Close();
        _orbitraSectionsPopup.Dispose();
        base.Dispose(disposing);
    }
}
