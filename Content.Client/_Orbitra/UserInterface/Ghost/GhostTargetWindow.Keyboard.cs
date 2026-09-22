using System.Linq;
using Content.Client._Orbitra.UserInterface;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client.UserInterface.Systems.Ghost.Controls;

public sealed partial class GhostTargetWindow
{
    private readonly RichTextLabel _orbitraSummary = new();
    private readonly OrbitraStatusPanel _orbitraStatus = new();

    private void InitializeOrbitraKeyboard()
    {
        OrbitraKeyboardNavigation.Attach(this).HandleKey = HandleOrbitraTargetKey;
        OnClose += () => _orbitraAwaiting = false;
        _orbitraStatus.ActionButton.OnPressed += _ => OrbitraRetryRequested?.Invoke();
        var container = SearchBar.Parent!;
        container.AddChild(_orbitraSummary);
        _orbitraSummary.SetPositionInParent(GhostScroll.GetPositionInParent());
        container.AddChild(_orbitraStatus);
        _orbitraStatus.SetPositionInParent(GhostScroll.GetPositionInParent());
    }

    private void UpdateOrbitraSummary()
    {
        var people = 0;
        var places = 0;
        var shownPeople = 0;
        var shownPlaces = 0;
        foreach (var row in _orbitraTargets.Values)
        {
            if (row.Group.Id == "places")
            {
                places++;
                if (row.Button.Visible) shownPlaces++;
            }
            else
            {
                people++;
                if (row.Button.Visible) shownPeople++;
            }
        }
        var filtering = _searchText.Trim().Length > 0;
        _orbitraSummary.SetMessage(Loc.GetString("orbitra-ghost-summary",
            ("people", filtering ? $"{shownPeople} / {people}" : people.ToString()),
            ("places", filtering ? $"{shownPlaces} / {places}" : places.ToString())));
        if (_orbitraAwaiting || _orbitraLoadFailed)
            return;
        _orbitraStatus.Visible = shownPeople + shownPlaces == 0;
        _orbitraStatus.SetStatus(Loc.GetString(filtering ? "orbitra-ghost-no-results" : "orbitra-ghost-no-targets"));
    }

    private bool HandleOrbitraTargetKey(KeyEventArgs args)
    {
        if (args.Control && args.Key == Keyboard.Key.F)
        {
            OrbitraKeyboardNavigation.Focus(SearchBar);
            SearchBar.CursorPosition = SearchBar.Text.Length;
            SearchBar.SelectionStart = 0;
            return true;
        }
        if (args.Control || args.Alt || args.System)
            return false;
        var focus = UserInterfaceManager.KeyboardFocused;
        if (focus == SearchBar)
        {
            if (args.Key == Keyboard.Key.Escape && SearchBar.Text.Length > 0)
            {
                SearchBar.SetText("", true);
                return true;
            }
            if (args.Key != Keyboard.Key.Down)
                return false;
            foreach (var root in ButtonContainer.Children)
                foreach (var row in _orbitraOrdered)
                    if (row.Group.Root == root && row.Button.VisibleInTree)
                    {
                        OrbitraKeyboardNavigation.Focus(row.Button);
                        return true;
                    }
            return true;
        }
        var group = _orbitraGroups.Values.FirstOrDefault(g => g.Header == focus);
        if (group != null && args.Key is Keyboard.Key.Left or Keyboard.Key.Right or Keyboard.Key.Return or Keyboard.Key.Space)
        {
            if (!args.IsRepeat && _searchText.Trim().Length == 0)
            {
                group.Collapsed = args.Key == Keyboard.Key.Left || args.Key != Keyboard.Key.Right && !group.Collapsed;
                FilterOrbitraTargets();
            }
            return true;
        }
        var target = _orbitraOrdered.FirstOrDefault(r => r.Button == focus);
        if (target != null && args.Key is Keyboard.Key.Return or Keyboard.Key.Space)
        {
            if (!args.IsRepeat)
                WarpClicked?.Invoke(target.Entity);
            return true;
        }
        if (group == null && target == null)
            return false;
        var stops = GetOrbitraTargetStops();
        var index = stops.IndexOf(focus!);
        switch (args.Key)
        {
            case Keyboard.Key.Up: index--; break;
            case Keyboard.Key.Down: index++; break;
            case Keyboard.Key.Home: index = 0; break;
            case Keyboard.Key.End: index = stops.Count - 1; break;
            default: return false;
        }
        if (stops.Count > 0)
            OrbitraKeyboardNavigation.Focus(stops[Math.Clamp(index, 0, stops.Count - 1)]);
        return true;
    }

    private List<Control> GetOrbitraTargetStops(bool includeHeaders = true)
    {
        var stops = new List<Control>();
        foreach (var root in ButtonContainer.Children)
        {
            var current = _orbitraGroups.Values.FirstOrDefault(g => g.Root == root);
            if (current == null || !current.Root.VisibleInTree)
                continue;
            if (includeHeaders)
                stops.Add(current.Header);
            foreach (var row in current.Rows.Children)
                if (OrbitraKeyboardNavigation.Available(row)) stops.Add(row);
        }
        return stops;
    }
}
