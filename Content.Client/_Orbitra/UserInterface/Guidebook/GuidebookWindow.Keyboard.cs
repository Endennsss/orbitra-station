using System.Numerics;
using Content.Client._Orbitra.UserInterface;
using Robust.Client.Input;
using Robust.Shared.Input;
using Robust.Shared.Utility;
using System.Linq;
using Content.Client.UserInterface.Controls.FancyTree;

namespace Content.Client.Guidebook.Controls;

public sealed partial class GuidebookWindow
{
    private bool HandleOrbitraSearchKey(KeyEventArgs args)
    {
        if (args.Control && args.Key == Keyboard.Key.F)
        {
            if (!OrbitraArticleSearch.VisibleInTree)
                OpenOrbitraSections();
            OrbitraKeyboardNavigation.Focus(OrbitraArticleSearch);
            OrbitraArticleSearch.CursorPosition = OrbitraArticleSearch.Text.Length;
            OrbitraArticleSearch.SelectionStart = 0;
            return true;
        }
        if (args.Control || args.Alt || args.System)
            return false;
        var focus = UserInterfaceManager.KeyboardFocused;
        var inSearch = OrbitraKeyboardNavigation.Contains(OrbitraNavigation, focus);
        if (args.Key == Keyboard.Key.Escape && inSearch && OrbitraArticleSearch.Text.Length > 0)
        {
            OrbitraArticleSearch.SetText("", true);
            OrbitraKeyboardNavigation.Focus(OrbitraArticleSearch);
            return true;
        }
        if (Tree.VisibleInTree && Tree.Items.FirstOrDefault(item => item.Button == focus) is { } treeItem)
        {
            switch (args.Key)
            {
                case Keyboard.Key.Left:
                    treeItem.SetExpanded(false);
                    return true;
                case Keyboard.Key.Right:
                    treeItem.SetExpanded(true);
                    return true;
                case Keyboard.Key.Return:
                case Keyboard.Key.Space:
                    if (!args.IsRepeat)
                        SelectOrbitraGuide(treeItem);
                    return true;
                case Keyboard.Key.Up:
                case Keyboard.Key.Down:
                case Keyboard.Key.Home:
                case Keyboard.Key.End:
                    var visible = Tree.Items.Where(item => item.Button.VisibleInTree).ToList();
                    var current = visible.IndexOf(treeItem);
                    var next = args.Key switch
                    {
                        Keyboard.Key.Home => 0,
                        Keyboard.Key.End => visible.Count - 1,
                        Keyboard.Key.Up => current - 1,
                        _ => current + 1
                    };
                    OrbitraKeyboardNavigation.Focus(visible[Math.Clamp(next, 0, visible.Count - 1)].Button);
                    return true;
            }
        }
        if (inSearch && _orbitraResults.Count > 0)
        {
            var index = _orbitraResults.FindIndex(r => r.Button == focus);
            if (args.Key is Keyboard.Key.Down or Keyboard.Key.Up)
            {
                index = index < 0 ? (args.Key == Keyboard.Key.Down ? 0 : _orbitraResults.Count - 1) :
                    Math.Clamp(index + (args.Key == Keyboard.Key.Down ? 1 : -1), 0, _orbitraResults.Count - 1);
                OrbitraKeyboardNavigation.Focus(_orbitraResults[index].Button);
                return true;
            }
            if (args.Key == Keyboard.Key.Return && index >= 0)
            {
                if (!args.IsRepeat)
                    SelectOrbitraGuide(_orbitraResults[index].Entry.Item);
                return true;
            }
        }
        if (focus == Scroll && args.Key is Keyboard.Key.Up or Keyboard.Key.Down)
        {
            Scroll.SetScrollValue(Scroll.GetScrollValue() + new Vector2(0, args.Key == Keyboard.Key.Up ? -36 : 36));
            return true;
        }
        return false;
    }

    private static FormattedMessage HighlightOrbitraTitle(string title, string[] words)
    {
        var marked = new bool[title.Length];
        foreach (var word in words)
        {
            for (var start = 0; start < title.Length;)
            {
                var match = title.IndexOf(word, start, StringComparison.CurrentCultureIgnoreCase);
                if (match < 0)
                    break;
                Array.Fill(marked, true, match, Math.Min(word.Length, title.Length - match));
                start = match + Math.Max(1, word.Length);
            }
        }
        var message = new FormattedMessage();
        for (var start = 0; start < title.Length;)
        {
            var end = start + 1;
            while (end < title.Length && marked[end] == marked[start])
                end++;
            if (marked[start])
                message.PushTag(new MarkupNode("bold", null, null));
            message.AddText(title[start..end]);
            if (marked[start])
                message.Pop();
            start = end;
        }
        return message;
    }
}
