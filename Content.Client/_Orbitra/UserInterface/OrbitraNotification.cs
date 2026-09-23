using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>The semantic result of a local UI operation.</summary>
public enum OrbitraNotificationKind { Info, Success, Warning, Error }

/// <summary>A reserved owner-local message slot; replacing a message never queues another one.</summary>
public sealed class OrbitraNotification : Control
{
    private readonly PanelContainer _panel = new() { Visible = false, MaxWidth = 480, HorizontalExpand = true, HorizontalAlignment = HAlignment.Center, RectClipContent = true };
    private readonly RichTextLabel _message = new() { HorizontalExpand = true, VerticalAlignment = VAlignment.Center };
    private readonly OrbitraIcon _icon = new();
    private readonly OrbitraWindowCloseButton _close = new();
    private float _remaining;
    private bool _hiding;
    private int _generation;
    private Control? _returnFocus;
    public OrbitraNotificationKind Kind { get; private set; }
    public bool HasMessage => _panel.Visible;

    public OrbitraNotification()
    {
        SetHeight = MinHeight = MaxHeight = 64;
        HorizontalExpand = true;
        _panel.AddStyleClass("OrbitraNotification");
        var row = new BoxContainer { SeparationOverride = OrbitraUiMetrics.Small };
        row.AddChild(_icon);
        row.AddChild(_message);
        row.AddChild(_close);
        _close.OnPressed += _ => Dismiss();
        _panel.AddChild(row);
        AddChild(_panel);
    }

    /// <summary>Displays an operation result without moving focus or changing the reserved geometry.</summary>
    public void Show(OrbitraNotificationKind kind, string text)
    {
        var reveal = !_panel.Visible || _hiding;
        _generation++;
        _hiding = false;
        Kind = kind;
        if (UserInterfaceManager.KeyboardFocused != _close)
            _returnFocus = UserInterfaceManager.KeyboardFocused;
        _remaining = 4;
        _message.SetMessage(text);
        _panel.ToolTip = text;
        foreach (var name in new[] { "Info", "Success", "Warning", "Error" })
            _panel.RemoveStyleClass("OrbitraNotification" + name);
        _panel.AddStyleClass("OrbitraNotification" + kind);
        _icon.Icon = kind == OrbitraNotificationKind.Success ? "check" : kind == OrbitraNotificationKind.Info ? "info" : "warning";
        _panel.Visible = true;
        _close.Disabled = false;
        if (reveal)
            OrbitraMotion.Reveal(_panel, OrbitraMotionPresets.Section);
    }

    /// <summary>Dismisses the current result, keeping its layout slot reserved.</summary>
    public void Dismiss()
    {
        if (!_panel.Visible || _hiding) return;
        _hiding = true;
        _close.Disabled = true;
        if (_close.HasKeyboardFocus())
        {
            _close.ReleaseKeyboardFocus();
            if (_returnFocus != null && OrbitraKeyboardNavigation.Available(_returnFocus))
                OrbitraKeyboardNavigation.Focus(_returnFocus);
        }
        var generation = ++_generation;
        OrbitraMotion.Hide(_panel, OrbitraMotionPresets.WindowClose, () =>
        {
            if (generation != _generation) return;
            _panel.Visible = false;
            // Очистка не должна менять список переходов во время его обхода.
            UserInterfaceManager.DeferAction(() =>
            {
                if (generation == _generation) Clear();
            });
        });
    }

    /// <summary>Immediately releases a result when its owner leaves the interface.</summary>
    public void Clear()
    {
        _generation++;
        _hiding = false;
        OrbitraMotion.Finish(_panel, UserInterfaceManager);
        _panel.Visible = false;
        _message.SetMessage("");
        _panel.ToolTip = null;
        _remaining = 0;
        _returnFocus = null;
    }

    internal static void ClearOwned(Control owner)
    {
        foreach (var child in owner.Children)
        {
            if (child is OrbitraNotification notification) notification.Clear();
            else ClearOwned(child);
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (!_panel.Visible || _hiding || Kind is OrbitraNotificationKind.Warning or OrbitraNotificationKind.Error)
            return;
        for (var hovered = UserInterfaceManager.CurrentlyHovered; hovered != null; hovered = hovered.Parent)
            if (hovered == _panel) return;
        _remaining -= args.DeltaSeconds;
        if (_remaining <= 0) Dismiss();
    }

    protected override void VisibilityChanged(bool newVisible)
    {
        base.VisibilityChanged(newVisible);
        if (!newVisible) Clear();
    }

    protected override void ExitedTree()
    {
        Clear();
        base.ExitedTree();
    }
}
