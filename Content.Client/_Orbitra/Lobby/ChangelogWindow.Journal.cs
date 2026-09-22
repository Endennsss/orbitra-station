using System.Linq;
using Content.Client._Orbitra.Lobby;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

using Content.Client._Orbitra.UserInterface; // Orbitra-Edit

namespace Content.Client.Changelog;

public sealed partial class ChangelogWindow
{
    private readonly Dictionary<string, ChangelogManager.Changelog> _orbitraJournals = new();
    private readonly Dictionary<string, ScrollContainer> _orbitraViews = new();
    private string? _orbitraSelected;
    private int _orbitraLoadGeneration;

    private void InitializeOrbitraJournal() => OrbitraSections.Selected += SelectOrbitraSection;
    private void CancelOrbitraJournalLoad() => _orbitraLoadGeneration++;

    private async void LoadOrbitraJournal()
    {
        if (_orbitraJournals.Count > 0)
        {
            RefreshOrbitraSections();
            return;
        }
        var generation = ++_orbitraLoadGeneration;
        try
        {
            var journals = await _changelog.LoadChangelog();
            if (Disposed || !IsOpen || generation != _orbitraLoadGeneration)
                return;
            OrbitraJournalHost.RemoveAllChildren();
            foreach (var journal in journals)
            {
                _orbitraJournals.Add(journal.Name, journal);
                OrbitraSections.AddSection(journal.Name, Loc.GetString($"changelog-tab-title-{journal.Name}"));
            }
            VersionLabel.Text = _changelog.GetClientVersion();
            RefreshOrbitraSections();
        }
        catch (Exception exception)
        {
            if (Disposed || !IsOpen || generation != _orbitraLoadGeneration)
                return;
            Logger.Error($"Failed to load the Orbitra journal: {exception}");
            OrbitraJournalHost.RemoveAllChildren();
            var retry = new Button { Text = Loc.GetString("orbitra-journal-retry"), VerticalAlignment = VAlignment.Top };
            retry.OnPressed += _ => LoadOrbitraJournal();
            OrbitraJournalHost.AddChild(retry);
        }
    }

    private void RefreshOrbitraSections()
    {
        string? first = null;
        var admin = _adminManager.IsAdmin(true);
        foreach (var (id, journal) in _orbitraJournals)
        {
            var allowed = !journal.AdminOnly || admin;
            OrbitraSections.SetAllowed(id, allowed);
            if (allowed)
                first ??= id;
        }
        if (_orbitraSelected == null || !_orbitraJournals.TryGetValue(_orbitraSelected, out var selected) || selected.AdminOnly && !admin)
            _orbitraSelected = first;
        if (_orbitraSelected != null)
            SelectOrbitraSection(_orbitraSelected);
    }

    private void SelectOrbitraSection(string id)
    {
        if (!_orbitraJournals.TryGetValue(id, out var journal) || journal.AdminOnly && !_adminManager.IsAdmin(true))
            return;
        var changed = _orbitraSelected != id;
        if (changed && _orbitraSelected != null && _orbitraViews.TryGetValue(_orbitraSelected, out var previous))
            OrbitraMotion.Finish(previous);
        _orbitraSelected = id;
        if (!_orbitraViews.TryGetValue(id, out var view))
        {
            view = new ScrollContainer { HScrollEnabled = false, ReserveScrollbarSpace = true };
            view.AddChild(new OrbitraVirtualList(view, CreateOrbitraRows(journal)));
            _orbitraViews.Add(id, view);
            OrbitraJournalHost.AddChild(view);
        }
        foreach (var (key, content) in _orbitraViews)
            content.Visible = key == id;
        OrbitraSections.Select(id);
        if (changed)
            OrbitraMotion.Reveal(view, OrbitraMotion.SectionDuration);
    }

    private List<Func<Control>> CreateOrbitraRows(ChangelogManager.Changelog journal)
    {
        var rows = new List<Func<Control>>();
        var hasRead = journal.Name != ChangelogManager.MainChangelogName || _changelog.MaxId <= _changelog.LastReadId;
        foreach (var day in journal.Entries.GroupBy(entry => entry.Time.ToLocalTime().Date).OrderByDescending(group => group.Key))
        {
            var date = day.Key == DateTime.Today ? Loc.GetString("changelog-today") : day.Key == DateTime.Today.AddDays(-1) ? Loc.GetString("changelog-yesterday") : day.Key.ToShortDateString();
            rows.Add(() => new Label { Text = date, StyleClasses = { "OrbitraJournalDate" }, Margin = new Thickness(0, 12, 0, 4) });
            foreach (var author in day.GroupBy(entry => (entry.Author, Read: entry.Id <= _changelog.LastReadId)).OrderBy(group => group.Key.Read).ThenBy(group => group.Key.Author))
            {
                if (author.Key.Read && !hasRead)
                {
                    hasRead = true;
                    rows.Add(() => new Label { Text = Loc.GetString("changelog-new-changes"), StyleClasses = { "OrbitraLobbyMuted" } });
                }
                var caption = Loc.GetString("changelog-author-changed", ("author", FormattedMessage.EscapeText(author.Key.Author)));
                rows.Add(() => { var label = new RichTextLabel(); label.SetMessage(FormattedMessage.FromMarkupOrThrow(caption)); return label; });
                foreach (var entry in author)
                foreach (var change in entry.Changes)
                {
                    var experimental = entry.Labels.Contains("Intent: Experimental");
                    rows.Add(() =>
                    {
                        var row = new ChangelogEntry();
                        row.SetText(FormattedMessage.FromUnformatted(change.Message));
                        row.SetIcons(change.Type, experimental);
                        return row;
                    });
                }
            }
        }
        return rows;
    }
}
