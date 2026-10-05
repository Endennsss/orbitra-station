using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared.Chat;
using Content.Client._Orbitra.Stylesheets;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using Robust.Shared.Timing;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Вертикальная лента чата, где briefing является обычным элементом списка.</summary>
public sealed class OrbitraChatOutput : ScrollContainer
{
    private readonly BoxContainer _entries;
    private readonly HashSet<string> _shownBriefings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _shownExamines = new(StringComparer.Ordinal);
    private float _examineDedupeTime;
    private bool _scrollToBottomPending;

    public OrbitraChatOutput()
    {
        HScrollEnabled = false;
        _entries = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 4,
        };
        _entries.OnResized += OnEntriesResized;
        AddChild(_entries);
    }

    private void OnEntriesResized()
    {
        if (!_scrollToBottomPending)
            return;

        _scrollToBottomPending = false;
        VScrollTarget = float.MaxValue;
    }

    public void Clear()
    {
        _entries.RemoveAllChildren();
        _shownBriefings.Clear();
        _shownExamines.Clear();
        _examineDedupeTime = 0;
        _scrollToBottomPending = false;
        VScrollTarget = 0;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _examineDedupeTime += args.DeltaSeconds;
        if (_shownExamines.Count == 0)
            return;

        foreach (var key in _shownExamines.Where(pair => _examineDedupeTime - pair.Value > 1.5f).Select(pair => pair.Key).ToArray())
            _shownExamines.Remove(key);
    }

    public void AddMessage(FormattedMessage message, Type[]? tagsAllowed = null, Color? defaultColor = null)
    {
        var followBottom = IsAtBottom;
        var label = new RichTextLabel
        {
            HorizontalExpand = true,
            StyleClasses = { OrbitraChatSheetlet.ChatText },
        };
        label.SetMessage(message, tagsAllowed, defaultColor);
        _entries.AddChild(label);
        _scrollToBottomPending |= followBottom;
    }

    public void AddBriefing(string message)
    {
        // Один и тот же briefing может прийти повторно при обновлении роли или репликации.
        // Разные тексты не подавляются.
        if (!_shownBriefings.Add(message))
            return;

        var followBottom = IsAtBottom;
        _entries.AddChild(new OrbitraBriefingControl(message, ChatChannel.Server.TextColor()));
        _scrollToBottomPending |= followBottom;
    }

    public void AddExamineCard(EntityUid target, string title, FormattedMessage description)
    {
        var key = $"{target}:{title}:{description.ToMarkup()}";
        if (_shownExamines.ContainsKey(key))
            return;
        _shownExamines[key] = _examineDedupeTime;

        var followBottom = IsAtBottom;
        _entries.AddChild(new OrbitraExamineCardControl(target, title, description));
        _scrollToBottomPending |= followBottom;
    }

    private bool IsAtBottom => VScrollTarget + PixelSize.Y >= _entries.DesiredSize.Y - 4;
}
