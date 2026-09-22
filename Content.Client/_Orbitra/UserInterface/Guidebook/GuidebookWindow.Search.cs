using System.Linq;
using Content.Client._Orbitra.UserInterface;
using Content.Client.UserInterface.Controls.FancyTree;
using Content.Shared.Guidebook;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client.Guidebook.Controls;

public sealed partial class GuidebookWindow
{
    private readonly List<GuideSearchEntry> _orbitraGuideIndex = new();
    private string? _orbitraIndexCulture;

    private void InitializeOrbitraSearch()
    {
        OrbitraArticleSearch.OnTextChanged += _ => FilterOrbitraGuides();
        HomeButton.OnPressed += _ =>
        {
            if (Selected is { } id && _entries.TryGetValue(id, out var entry))
                ShowGuide(entry);
        };
        OnOpen += EnsureOrbitraGuideCulture;
    }

    private static bool HasOrbitraAncestor(TreeItem? parent, ProtoId<GuideEntryPrototype> id)
    {
        var visited = new HashSet<Control>();
        for (Control? current = parent; current != null && visited.Add(current); current = current.Parent)
            if (current is TreeItem { Metadata: GuideEntry entry } && entry.Id == id)
                return true;
        return false;
    }

    private void EnsureOrbitraGuideCulture()
    {
        if (_orbitraIndexCulture != IoCManager.Resolve<ILocalizationManager>().DefaultCulture?.Name)
            RebuildOrbitraGuideIndex();
    }

    /// <summary>Indexes only actual permitted tree entries; never parses article bodies for searching.</summary>
    private void RebuildOrbitraGuideIndex()
    {
        _orbitraIndexCulture = IoCManager.Resolve<ILocalizationManager>().DefaultCulture?.Name;
        _orbitraGuideIndex.Clear();
        foreach (var item in Tree.Items)
        {
            if (item.Metadata is not GuideEntry entry)
                continue;
            var path = new List<TreeItem>();
            var visited = new HashSet<Control>();
            for (Control? current = item; current != null && visited.Add(current); current = current.Parent)
            {
                if (current is TreeItem ancestor)
                    path.Add(ancestor);
            }
            path.Reverse();
            var title = Loc.GetString(entry.Name);
            item.Label.Text = title;
            var names = path.Where(p => p.Metadata is GuideEntry).Select(p => Loc.GetString(((GuideEntry)p.Metadata!).Name));
            _orbitraGuideIndex.Add(new GuideSearchEntry(item, title, string.Join(" → ", names), path));
        }
        FilterOrbitraGuides();
        UpdateOrbitraBreadcrumbs();
    }

    private void FilterOrbitraGuides()
    {
        EnsureOrbitraGuideCulture();
        var words = OrbitraArticleSearch.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Tree.Visible = words.Length == 0;
        OrbitraResultsScroll.Visible = words.Length != 0;
        ClearOrbitraControls(OrbitraSearchResults);
        if (words.Length == 0)
            return;
        var matches = _orbitraGuideIndex.Where(e => words.All(w => e.Path.Contains(w, StringComparison.CurrentCultureIgnoreCase)))
            .OrderByDescending(e => words.All(w => e.Title.Contains(w, StringComparison.CurrentCultureIgnoreCase)));
        foreach (var entry in matches)
        {
            var button = new ContainerButton { HorizontalExpand = true, CanKeyboardFocus = true, ToolTip = entry.Path };
            button.AddStyleClass(ContainerButton.StyleClassButton);
            button.AddStyleClass(OrbitraButtonStyles.Ghost);
            OrbitraMotion.AttachButton(button);
            var labels = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 0 };
            var title = new RichTextLabel();
            title.SetMessage(entry.Title);
            var path = new RichTextLabel();
            path.SetMessage(entry.Path);
            path.AddStyleClass("OrbitraLobbyMuted");
            labels.AddChild(title);
            labels.AddChild(path);
            button.AddChild(labels);
            button.OnPressed += _ => SelectOrbitraGuide(entry.Item);
            OrbitraSearchResults.AddChild(button);
        }
        if (OrbitraSearchResults.ChildCount == 0)
        {
            var empty = new RichTextLabel();
            empty.SetMessage(Loc.GetString("orbitra-guide-no-results"));
            OrbitraSearchResults.AddChild(empty);
        }
    }

    private void SelectOrbitraGuide(TreeItem item)
    {
        Tree.ExpandParentEntries(item.Index);
        Tree.SetSelectedIndex(item.Index);
        _orbitraSectionsPopup.Close();
    }

    private void UpdateOrbitraBreadcrumbs()
    {
        ClearOrbitraControls(OrbitraBreadcrumbs);
        if (Selected == null)
            return;
        var selected = _orbitraGuideIndex.FirstOrDefault(e => e.Item == Tree.SelectedItem && e.Item.Metadata is GuideEntry entry && entry.Id == Selected)
                       ?? _orbitraGuideIndex.FirstOrDefault(e => e.Item.Metadata is GuideEntry entry && entry.Id == Selected);
        if (selected == null)
            return;
        foreach (var item in selected.Items)
        {
            if (item.Metadata is not GuideEntry entry)
                continue;
            if (OrbitraBreadcrumbs.ChildCount > 0)
                OrbitraBreadcrumbs.AddChild(new Label { Text = "›", VerticalAlignment = VAlignment.Center });
            var title = Loc.GetString(entry.Name);
            if (entry.Id == Selected)
            {
                var label = new RichTextLabel { MaxWidth = 220, VerticalAlignment = VAlignment.Center };
                label.SetMessage(title);
                OrbitraBreadcrumbs.AddChild(label);
            }
            else
            {
                var button = new Button { Text = title, MaxWidth = 220, ClipText = true, ToolTip = title, CanKeyboardFocus = true };
                button.AddStyleClass(OrbitraButtonStyles.Ghost);
                button.OnPressed += _ => SelectOrbitraGuide(item);
                OrbitraBreadcrumbs.AddChild(button);
            }
        }
    }

    private sealed record GuideSearchEntry(TreeItem Item, string Title, string Path, List<TreeItem> Items);

    private static void ClearOrbitraControls(Control container)
    {
        while (container.ChildCount > 0)
            container.GetChild(0).Dispose();
    }
}
