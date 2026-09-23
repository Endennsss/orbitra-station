using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

using Content.Client._Orbitra.Lobby;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Applies editor-local presentation without altering shared controls elsewhere.</summary>
internal static class OrbitraEditorStyles
{
    public static void OpenWindow(BaseWindow window)
    {
        OrbitraEntryWindow.Attach(window);
        Apply(window);
        FitWindow(window);
        window.OpenCentered();
    }

    public static void FitWindow(BaseWindow window, bool center = false)
    {
        var screen = IoCManager.Resolve<IUserInterfaceManager>().RootControl.Size;
        var available = Vector2.Max(new Vector2(200, 128), screen - new Vector2(OrbitraUiMetrics.ScreenMargin * 2));
        window.MaxSize = available;
        window.MinSize = Vector2.Min(window.MinSize, available);
        if (float.IsFinite(window.SetWidth))
            window.SetWidth = Math.Min(window.SetWidth, available.X);
        if (float.IsFinite(window.SetHeight))
            window.SetHeight = Math.Min(window.SetHeight, available.Y);
        window.Measure(available);
        if (window.IsOpen)
        {
            var size = Vector2.Min(window.DesiredSize, available);
            var position = center ? (screen - size) / 2 : window.Position;
            var margin = new Vector2(OrbitraUiMetrics.ScreenMargin);
            LayoutContainer.SetPosition(window, Vector2.Clamp(position, margin, Vector2.Max(margin, screen - size - margin)));
        }
    }

    private static bool PreservesHorizontalScroll(Control control)
    {
        if (control.HasStyleClass("OrbitraHorizontalScroll"))
            return true;
        for (var parent = control.Parent; parent != null; parent = parent.Parent)
            if (parent.HasStyleClass("OrbitraPreserveHorizontalScroll"))
                return true;
        return false;
    }

    public static void Apply(Control root)
    {
        if (root.HasStyleClass("OrbitraJournalNavigation"))
            return;
        if (root.HasStyleClass("OrbitraEditorControl"))
            return;
        if (root.HasStyleClass("OrbitraOptionRow"))
            return;
        root.AddStyleClass("OrbitraEditorControl");
        if (root is Content.Client.Administration.UI.Tabs.PlayerTab.PlayerTab or
            Content.Client.Administration.UI.Tabs.ObjectsTab.ObjectsTab)
            root.AddChild(new OrbitraTableHeader(root));
        if (root is Content.Client.Administration.UI.CustomControls.PlayerListEntry playerEntry)
            playerEntry.ApplyOrbitraPin();
        OrbitraMenuIcons.Apply(root);
        OrbitraTooltips.Attach(root);
        if (root is LineEdit or Slider)
        {
            // Штатная текстурная подложка поиска не должна перекрывать локальный стиль.
            root.RemoveStyleClass("actionSearchBox");
            root.CanKeyboardFocus = true;
            OrbitraFocusRing.Attach(root);
        }
        if (root is Content.Client.UserInterface.Controls.StripeBack stripe)
            stripe.HasTopEdge = stripe.HasBottomEdge = stripe.HasMargins = false;
        if (root is Content.Client.LateJoin.JobButton job)
        {
            job.AddStyleClass("OrbitraLobbyButton");
            job.JobLabel.ClipText = true;
            job.JobLabel.HorizontalExpand = true;
            OrbitraMotion.AttachButton(job);
        }
        if (root is Content.Client.UserInterface.Controls.FancyTree.TreeItem item)
        {
            item.ApplyOrbitraIcon();
            item.Button.StyleIdentifier = null;
            item.Button.AddStyleClass("OrbitraTreeRow");
            item.Button.AddStyleClass(ContainerButton.StyleClassButton);
            item.Label.ClipText = true;
            item.Label.HorizontalExpand = true;
            item.Button.ToolTip = item.Label.Text;
            OrbitraMotion.AttachButton(item.Button);
        }
        if (root is TabContainer tabs)
            OrbitraMotion.AttachTabs(tabs);
        if (root is Content.Client.UserInterface.Controls.FancyWindow window)
            window.ApplyOrbitraChrome();
        if (root is Content.Client.Voting.UI.VoteCallMenu voteWindow)
            voteWindow.ApplyOrbitraChrome();
        if (root is Content.Client.Administration.UI.Bwoink.BwoinkControl bwoink)
            OrbitraHelpLayout.Attach(bwoink);
        if (root is ScrollContainer scroll && !PreservesHorizontalScroll(root))
            scroll.HScrollEnabled = false;
        if (root is Content.Client.Humanoid.MarkingPicker)
            OrbitraMarkingNavigation.Attach(root, "OrganTabs");
        if (root is Content.Client.Humanoid.OrganMarkingPicker)
            OrbitraMarkingNavigation.Attach(root, "LayerTabs");
        if (root is Content.Client.Lobby.UI.Roles.TraitPreferenceSelector trait)
            trait.Checkbox.HorizontalExpand = true;
        if (root is BoxContainer { Orientation: BoxContainer.LayoutOrientation.Vertical } box)
                box.SeparationOverride ??= OrbitraUiMetrics.Small;
        if (root is Content.Client.Lobby.UI.Roles.RequirementsSelector selector)
            selector.ApplyOrbitraLayout();
        if (root is Button or OptionButton && root is not OrbitraWindowCloseButton && root is not CheckBox)
        {
            if (root.HasStyleClass("negative"))
                root.AddStyleClass("OrbitraDangerButton");
            foreach (var legacy in new[] { "OpenLeft", "OpenRight", "OpenBoth", "negative" })
                root.RemoveStyleClass(legacy);
            root.AddStyleClass(ContainerButton.StyleClassButton);
            if (!root.HasStyleClass("OrbitraLobbyPrimary") && !root.HasStyleClass("OrbitraDangerButton") &&
                !root.HasStyleClass(OrbitraButtonStyles.Primary) && !root.HasStyleClass(OrbitraButtonStyles.Danger) &&
                !root.HasStyleClass(OrbitraButtonStyles.Ghost) && !root.HasStyleClass(OrbitraButtonStyles.Secondary))
                root.AddStyleClass("OrbitraLobbyButton");
        }
        if (root is Button button)
        {
            OrbitraMotion.AttachButton(button);
            root.MinWidth = 0;
            // Обрезаем только растягиваемые строки: у обычной кнопки текст задаёт её ширину.
            button.ClipText = button.HorizontalExpand && button.Parent is not WrapContainer;
            button.ToolTip ??= button.Text;
        }
        if (root is CheckBox check)
        {
            check.RemoveStyleClass("OrbitraLobbyButton");
            check.MinHeight = OrbitraUiMetrics.ElementHeight;
            var wrap = check.Parent is WrapContainer;
            check.HorizontalExpand = !wrap;
            check.Label.HorizontalExpand = !wrap;
            check.ClipText = !wrap;
            check.ToolTip ??= check.Text;
            check.TextureRect.AddStyleClass("OrbitraCheckIcon");
            check.TextureRect.SetSize = new Vector2(20);
            check.TextureRect.Stretch = TextureRect.StretchMode.KeepAspectCentered;
        }
        if (root is OptionButton option)
        {
            OrbitraMotion.AttachButton(option);
            option.MinWidth = Math.Max(option.MinWidth, 100);
            // Выпадающее меню живёт отдельно от дерева редактора и тоже нуждается в оформлении.
            OrbitraOptionPopup.Attach(option);
            foreach (var child in option.Children)
                ClipOptionLabels(child);
        }
        if (root is LineEdit or OptionButton)
            root.MinHeight = OrbitraUiMetrics.ElementHeight;
        if (root is TextureRect texture && root.HasStyleClass(OptionButton.StyleClassOptionTriangle))
        {
            texture.AddStyleClass("OrbitraIcon-chevron_down");
            texture.SetSize = new Vector2(OrbitraUiMetrics.IconSize);
            texture.Stretch = TextureRect.StretchMode.KeepAspectCentered;
        }
        if (root is Content.Client.UserInterface.Controls.ListContainerButton listRow)
        {
            // Белый override штатного списка обходил sheetlet до первого наведения.
            listRow.StyleBoxOverride = null;
            listRow.RemoveStyleClass(Content.Client.UserInterface.Controls.ListContainer.StyleClassListContainerButton);
            listRow.AddStyleClass("OrbitraOptionRow");
            if (listRow.Parent is Content.Client.UserInterface.Controls.SearchListContainer)
                listRow.AddStyleClass("OrbitraTableRow");
            listRow.AddStyleClass(ContainerButton.StyleClassButton);
            OrbitraMotion.AttachButton(listRow);
        }
        foreach (var child in root.Children)
            Apply(child);
        // Динамические маркировки и предметы получают стиль при добавлении, без обхода дерева каждый кадр.
        root.OnChildAdded += Apply;
    }

    private static void ClipOptionLabels(Control control)
    {
        if (control is BoxContainer { Orientation: BoxContainer.LayoutOrientation.Horizontal } box)
            box.SeparationOverride = OrbitraUiMetrics.Small;
        if (control is TextureRect && control.HasStyleClass(OptionButton.StyleClassOptionTriangle))
        {
            control.AddStyleClass("OrbitraIcon-chevron_down");
            control.SetSize = new Vector2(OrbitraUiMetrics.IconSize);
            ((TextureRect) control).Stretch = TextureRect.StretchMode.KeepAspectCentered;
        }
        if (control is Label label)
            label.ClipText = true;
        foreach (var child in control.Children)
            ClipOptionLabels(child);
    }
}
